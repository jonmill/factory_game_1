using System.Text;
using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The robot window, opened by clicking a helper bot: the commander's pockets on the left and the bot on
/// the right, with its module slots and cargo. Modules go in by dragging them from the pockets onto a
/// free slot or clicking them, and come out by dragging them back or clicking the slot. The commander's
/// own module slots sit under its pockets on the character screen and work the same way.
/// </summary>
public partial class Hud
{
    private const float ModuleSlotSize = 50;

    private Label _robotTitle = null!, _robotInfo = null!, _robotReach = null!;
    private Label _robotPocketsTitle = null!, _robotPocketsEmpty = null!;
    private GridContainer _robotPockets = null!, _robotCargo = null!;
    private HBoxContainer _robotModules = null!;
    private Slot _robotWork = null!;
    private string? _robotSig;
    private int _robotId;

    private PanelContainer BuildRobot()
    {
        PanelContainer win = MakeWindow(HudWindow.Robot, "", null, out VBoxContainer body, out _robotTitle);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        body.AddChild(cols);

        VBoxContainer left = new VBoxContainer();
        // The commander's own pockets, so items can be dragged across; says whose, since it isn't the building's.
        _robotPocketsTitle = HudStyle.Title("Your pockets");
        left.AddChild(_robotPocketsTitle);
        _robotPockets = new GridContainer { Columns = PocketColumns };
        _robotPockets.AddThemeConstantOverride("h_separation", 4);
        _robotPockets.AddThemeConstantOverride("v_separation", 4);
        left.AddChild(_robotPockets);
        _robotPocketsEmpty = HudStyle.Caption("Empty: right-click ore or trees to mine, or drag items here.", HudStyle.TextDim, 11);
        _robotPocketsEmpty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _robotPocketsEmpty.CustomMinimumSize = new Vector2(PocketColumns * 60, 0);
        left.AddChild(_robotPocketsEmpty);
        cols.AddChild(left);

        cols.AddChild(new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 0) });

        VBoxContainer right = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        right.AddThemeConstantOverride("separation", 8);
        _robotInfo = HudStyle.Caption("", HudStyle.TextDim, 12);
        _robotInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _robotInfo.CustomMinimumSize = new Vector2(380, 0);
        right.AddChild(_robotInfo);
        right.AddChild(HudStyle.Title("Module slots", 11));
        _robotModules = new HBoxContainer();
        _robotModules.AddThemeConstantOverride("separation", 4);
        right.AddChild(_robotModules);
        // Cargo, with what the bot is gathering or building (and its progress ring) at the end of the row.
        HBoxContainer cargoHead = new HBoxContainer();
        Label cargoTitle = HudStyle.Title("Cargo", 11);
        cargoTitle.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        cargoHead.AddChild(cargoTitle);
        cargoHead.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _robotWork = new Slot(34) { MouseFilter = Control.MouseFilterEnum.Pass };
        cargoHead.AddChild(_robotWork);
        right.AddChild(cargoHead);
        _robotCargo = new GridContainer { Columns = 6 };
        _robotCargo.AddThemeConstantOverride("h_separation", 4);
        right.AddChild(_robotCargo);
        cols.AddChild(right);

        body.AddChild(HudStyle.Divider());
        _robotReach = HudStyle.Caption("", HudStyle.TextDim, 12);
        body.AddChild(_robotReach);

        return win;
    }

    private void UpdateRobot()
    {
        if (W.Get<Unit>(_robotId) is not { } u)
        {
            ShowOnly(HudWindow.None);

            return;
        }

        Unit leader = Root.Leader;
        bool reach = (u.Planet == leader.Planet) && P.InLeaderReach(leader, u.Tile);
        _robotTitle.Text = $"{u.Name}   ·   {u.Robot.Name}".ToUpperInvariant();
        _robotInfo.Text = DescribeRobot(u);
        _robotReach.Text = reach
            ? "Drag a module onto a free slot, or click it in your pockets.   Click a module slot to take it out."
            : "Out of reach: walk the commander closer to change its modules.";
        _robotReach.AddThemeColorOverride("font_color", reach ? HudStyle.TextDim : HudStyle.Bad);
        ShowActivity(_robotWork, u);

        if (Slot.Dragging is not null)
        {
            return;
        }

        string sig = $"{u.Id}|{reach}|{string.Join(",", u.Modules)}|{P.ModuleSlots(u)}|" + string.Join(",", leader.Carry.Items.Select(kv => $"{kv.Key}:{kv.Value}")) +
                     "|" + string.Join(",", u.Carry.Items.Select(kv => $"{kv.Key}:{kv.Value}"));
        if (sig == _robotSig)
        {
            return;
        }

        _robotSig = sig;
        FillModuleSlots(_robotModules, u, reach);
        FillRobotPockets(u, reach);
        Clear(_robotCargo);
        foreach ((string item, int count, int _) in Stacks(u.Carry))
        {
            _robotCargo.AddChild(ItemSlot(item, count, MachineSlot));
        }

        for (int i = Stacks(u.Carry).Count; i < u.Carry.Slots; i++)
        {
            _robotCargo.AddChild(new Slot(MachineSlot) { TooltipText = "Empty cargo slot" });
        }
    }

    /// <summary>What a robot is, is doing, and gets from its modules.</summary>
    private string DescribeRobot(Unit u)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(u.Robot.Description);
        sb.AppendLine();
        sb.AppendLine(u.Status);
        if (u.IsLeader == false)
        {
            sb.AppendLine($"Job: {JobLabel(u.Job)}");
            string[] jobs = [.. new[] { JobKind.Gather, JobKind.Haul, JobKind.Explore, JobKind.Build, JobKind.OperateTrain }.Where(k => P.CanTakeJob(u, k)).Select(k => JobLabel(new Job(k)))];
            sb.AppendLine($"Can do: {string.Join(", ", jobs)}");
        }

        sb.Append($"Speed {u.Speed * 60f / Fixed.One:0.0} tiles/s   ·   Cargo {u.Carry.Slots} stacks");

        return sb.ToString();
    }

    private string JobLabel(Job j) => j.Kind switch
    {
        JobKind.OperateTrain => "Operate train",
        JobKind.Gather when (j.Resource is { } res) && C.Resources.TryGetValue(res, out ResourceDef? def) => $"Gather {def.Name}",
        _ => j.Kind.ToString(),
    };

    /// <summary>A robot's module slots: installed modules first, then free slots that take dropped modules.</summary>
    private void FillModuleSlots(Container into, Unit u, bool reach)
    {
        Clear(into);
        int slots = P.ModuleSlots(u);
        for (int i = 0; i < slots; i++)
        {
            int index = i;
            if (i < u.Modules.Count)
            {
                string item = u.Modules[i];
                Slot slot = ItemSlot(item, null, ModuleSlotSize);
                slot.TooltipText = $"{ItemName(item)}\n{C.Item(item).Module!.Description}\nClick or drag to your pockets to take it out.";
                slot.DragSource = () => reach ? new ItemDrag(item, 1, false, UnitId: u.Id, ModuleSlot: index) : null;
                slot.Clicked += (_, _) => TakeModuleOut(u, index, reach);
                into.AddChild(slot);
            }
            else
            {
                Slot empty = new Slot(ModuleSlotSize)
                {
                    TooltipText = "Free module slot: drag a module here from your pockets",
                    CanDrop = d => reach && d.FromPockets && (C.Item(d.Item).Module?.FitsRobot(u.IsLeader) == true),
                };
                empty.Dropped += d => PutModuleIn(u, d.Item, reach);
                into.AddChild(empty);
            }
        }

        if (slots == 0)
        {
            into.AddChild(HudStyle.Caption("No module slots", HudStyle.TextDim, 11));
        }
    }

    private void FillRobotPockets(Unit u, bool reach)
    {
        Clear(_robotPockets);
        Inventory mine = Root.Leader.Carry;
        _robotPocketsTitle.Text = $"YOUR POCKETS   {mine.UsedSlots} / {mine.Slots}";
        _robotPocketsEmpty.Visible = mine.IsEmpty;
        Inventory carry = Root.Leader.Carry;
        int used = 0;
        foreach ((string item, int count, int _) in Stacks(carry))
        {
            string it = item;
            bool module = C.Item(item).Module?.FitsRobot(u.IsLeader) == true;
            Slot slot = ItemSlot(item, count, MachineSlot);
            slot.Dim = module == false;
            slot.TooltipText = module ? $"{ItemName(item)} ×{carry.Count(item)}\n{C.Item(item).Module!.Description}\nClick to install" : $"{ItemName(item)} ×{carry.Count(item)}";
            slot.DragSource = () => module && reach ? new ItemDrag(it, 1, true) : null;
            slot.CanDrop = d => d.IsInstalledModule && (d.UnitId == u.Id);
            slot.Dropped += d => TakeModuleOut(u, d.ModuleSlot, reach);
            if (module)
            {
                slot.Clicked += (_, _) => PutModuleIn(u, it, reach);
            }

            _robotPockets.AddChild(slot);
            used++;
        }

        for (; used < carry.Slots; used++)
        {
            Slot empty = new Slot(MachineSlot) { CanDrop = d => d.IsInstalledModule && (d.UnitId == u.Id) };
            empty.Dropped += d => TakeModuleOut(u, d.ModuleSlot, reach);
            _robotPockets.AddChild(empty);
        }
    }

    private void PutModuleIn(Unit u, string item, bool reach)
    {
        if (reach == false)
        {
            Root.Notify($"Too far away: walk closer to {u.Name}", Colors.OrangeRed);

            return;
        }

        Root.Send(new InstallModule(Root.Leader.Id, u.Id, item));
    }

    private void TakeModuleOut(Unit u, int slot, bool reach)
    {
        if (reach == false)
        {
            Root.Notify($"Too far away: walk closer to {u.Name}", Colors.OrangeRed);

            return;
        }

        Root.Send(new RemoveModule(Root.Leader.Id, u.Id, slot));
    }
}
