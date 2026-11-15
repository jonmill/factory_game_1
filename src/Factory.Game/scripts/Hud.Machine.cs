using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The machine window, opened by clicking a building that holds items: the commander's pockets on the
/// left and the building on the right, with its fuel, input and output slots (or its contents, for
/// storage). Stacks are dragged between the two sides, or clicked across: Shift-click for a stack,
/// Ctrl/Cmd-click for all of that item and right-click for half. Sorter arms keep feeding and emptying machines as before;
/// this is the by-hand route, and it needs the building within the commander's reach.
/// </summary>
public partial class Hud
{
    private const float MachineSlot = 46;
    private const int StorageColumns = 8;

    private Label _machineTitle = null!, _machineInfo = null!, _machineReach = null!;
    private GridContainer _machinePockets = null!;
    private VBoxContainer _machineBody = null!, _machineActions = null!;
    private ProgressBar _machineProgress = null!;
    private string? _machineSig, _machinePocketSig;
    private int _machineId;

    /// <summary>Buildings that hold items get the machine window; the rest keep the plain inspector.</summary>
    private static bool HasMachineWindow(Building b) =>
        b.Def.IsStorage || (b.Def.Energy == EnergyKind.Burner) ||
        (b.Def.Kind is BuildingKind.Furnace or BuildingKind.Assembler or BuildingKind.Lab or BuildingKind.Drill or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink or BuildingKind.PayloadLauncher);

    private PanelContainer BuildMachine()
    {
        PanelContainer win = MakeWindow(HudWindow.Machine, "", null, out VBoxContainer body, out _machineTitle);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        body.AddChild(cols);

        VBoxContainer left = new VBoxContainer();
        left.AddChild(HudStyle.Title("Pockets"));
        _machinePockets = new GridContainer { Columns = PocketColumns };
        _machinePockets.AddThemeConstantOverride("h_separation", 4);
        _machinePockets.AddThemeConstantOverride("v_separation", 4);
        left.AddChild(_machinePockets);
        cols.AddChild(left);

        cols.AddChild(new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 0) });

        VBoxContainer right = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        right.AddThemeConstantOverride("separation", 8);
        _machineInfo = HudStyle.Caption("", HudStyle.TextDim, 12);
        _machineInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _machineInfo.CustomMinimumSize = new Vector2(380, 0);
        right.AddChild(_machineInfo);
        _machineBody = new VBoxContainer();
        _machineBody.AddThemeConstantOverride("separation", 6);
        right.AddChild(_machineBody);
        _machineProgress = HudStyle.Bar(380, 6);
        HudStyle.Tint(_machineProgress, HudStyle.Orange);
        right.AddChild(_machineProgress);
        _machineActions = new VBoxContainer();
        right.AddChild(_machineActions);
        cols.AddChild(right);

        body.AddChild(HudStyle.Divider());
        _machineReach = HudStyle.Caption("", HudStyle.TextDim, 12);
        body.AddChild(_machineReach);

        return win;
    }

    /// <summary>Opens the machine window when a building with items is selected, and closes it when the selection moves on.</summary>
    private void WatchSelection()
    {
        int id = Root.SelectedId;
        bool same = (id != 0) && ((id == _machineId) || (id == _robotId));
        if (same || ((id == 0) && (_machineId == 0) && (_robotId == 0)))
        {
            return;
        }

        if ((W.Get<Building>(id) is { } b) && HasMachineWindow(b))
        {
            ShowOnly(HudWindow.Machine);
            _machineId = id;
            _robotId = 0;
            _machineSig = null;
            _machinePocketSig = null;
        }
        else if (W.Get<Unit>(id) is { IsLeader: false })
        {
            ShowOnly(HudWindow.Robot);
            _robotId = id;
            _machineId = 0;
            _robotSig = null;
        }
        else
        {
            if (_open is HudWindow.Machine or HudWindow.Robot)
            {
                ShowOnly(HudWindow.None);
            }

            _machineId = 0;
            _robotId = 0;
        }
    }

    private void UpdateMachine()
    {
        if (W.Get<Building>(_machineId) is not { } b)
        {
            ShowOnly(HudWindow.None);

            return;
        }

        bool reach = Root.InReach(b.Origin);
        _machineTitle.Text = BuildingTitle(b).ToUpperInvariant();
        _machineInfo.Text = DescribeBuilding(b).TrimEnd();
        _machineReach.Text = reach
            ? TransferHint
            : "Out of reach: walk the commander closer to move items by hand. Sorter arms still feed and empty it.";
        _machineReach.AddThemeColorOverride("font_color", reach ? HudStyle.TextDim : HudStyle.Bad);
        float progress = (b.Crafting && (b.Recipe is { } r)) ? Mathf.Clamp(b.Progress / (float)(r.Ticks * 100), 0, 1) : 0;
        _machineProgress.Value = progress;
        _machineProgress.Visible = b.Def.IsStorage == false;

        // Rebuilding mid-drag would free the slot being dragged, so the slots wait until it's dropped.
        if (Slot.Dragging is not null)
        {
            return;
        }

        Inventory carry = Root.Leader.Carry;
        string pocketSig = string.Join(",", carry.Items.Select(kv => $"{kv.Key}:{kv.Value}")) + $"|{reach}|{b.Id}";
        if (pocketSig != _machinePocketSig)
        {
            _machinePocketSig = pocketSig;
            FillPockets(b, reach);
        }

        // The third part changes whenever the action controls need rebuilding: recipe, launch target or station settings.
        string actionsKey = $"{b.RecipeId};{b.TargetPlanetId};" + string.Join(",", b.StationSlots.Select(s => $"{s.Item}:{s.Mode}"));
        string sig = $"{b.Id}|{reach}|{actionsKey}|" + string.Join(",", b.Inv.Items.Select(kv => $"{kv.Key}:{kv.Value}")) + "|" +
                     string.Join(",", b.Out.Items.Select(kv => $"{kv.Key}:{kv.Value}"));
        if (sig != _machineSig)
        {
            bool recipeChanged = (_machineSig is null) || (_machineSig.Split('|')[2] != actionsKey) || (_machineSig.Split('|')[0] != $"{b.Id}");
            _machineSig = sig;
            FillMachine(b, reach);
            if (recipeChanged)
            {
                Clear(_machineActions);
                BuildBuildingActions(_machineActions, b);
            }
        }
    }

    // ------------------------------------------------------------------ pockets side

    private void FillPockets(Building b, bool reach)
    {
        Clear(_machinePockets);
        Inventory carry = Root.Leader.Carry;
        int used = 0;
        foreach ((string item, int count, int _) in Stacks(carry))
        {
            string it = item;
            int n = count;
            Slot slot = ItemSlot(item, n, MachineSlot);
            slot.TooltipText = $"{ItemName(item)} ×{carry.Count(item)}";
            slot.DragSource = () => reach ? new ItemDrag(it, n, true) : null;
            slot.CanDrop = d => reach && (d.FromPockets == false);
            slot.Dropped += d => TakeIntoPockets(b, d.Item, d.Count, d.Part);
            slot.Clicked += (button, _) => PutIn(b, it, ClickAmount(button, n, carry.Count(it)), reach);
            _machinePockets.AddChild(slot);
            used++;
        }

        for (; used < carry.Slots; used++)
        {
            Slot empty = new Slot(MachineSlot) { CanDrop = d => reach && (d.FromPockets == false) };
            empty.Dropped += d => TakeIntoPockets(b, d.Item, d.Count, d.Part);
            _machinePockets.AddChild(empty);
        }
    }

    private void PutIn(Building b, string item, int count, bool reach)
    {
        if (reach == false)
        {
            Root.Notify("Too far away: walk closer to the machine", Colors.OrangeRed);

            return;
        }

        if (P.AcceptAmount(b, item) <= 0)
        {
            Root.Notify($"{b.Def.Name} won't take {ItemName(item)}", Colors.OrangeRed);

            return;
        }

        Root.Send(new DepositToBuilding(Root.Leader.Id, b.Id, item, count));
    }

    private void TakeIntoPockets(Building b, string item, int count, InventoryPart part)
    {
        if (Root.Leader.Carry.SpaceFor(item) <= 0)
        {
            Root.Notify("Your pockets are full", Colors.OrangeRed);

            return;
        }

        Root.Send(new TakeFromBuilding(Root.Leader.Id, b.Id, item, count, part));
    }

    // ------------------------------------------------------------------ machine side

    private void FillMachine(Building b, bool reach)
    {
        Clear(_machineBody);
        if (b.Def.IsStorage)
        {
            _machineBody.AddChild(HudStyle.Title($"Contents   {b.Inv.UsedSlots} / {b.Inv.Slots}", 11));
            GridContainer grid = new GridContainer { Columns = StorageColumns };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 4);
            int used = 0;
            foreach ((string item, int count, int _) in Stacks(b.Inv))
            {
                grid.AddChild(MachineItemSlot(b, item, count, InventoryPart.Input, reach, acceptsDrops: true));
                used++;
            }

            for (; used < System.Math.Min(b.Inv.Slots, 64); used++)
            {
                grid.AddChild(EmptyMachineSlot(b, reach, null));
            }

            _machineBody.AddChild(grid);

            return;
        }

        bool burner = b.Def.Energy == EnergyKind.Burner;
        HBoxContainer row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        _machineBody.AddChild(row);

        if (burner)
        {
            List<(string Item, int Count, int Index)> fuel = Stacks(b.Inv).Where(s => C.IsFuel(s.Item)).ToList();
            row.AddChild(Section("Fuel", fuel.Select(s => MachineItemSlot(b, s.Item, s.Count, InventoryPart.Input, reach, acceptsDrops: true))
                .DefaultIfEmpty(EmptyMachineSlot(b, reach, null))));
        }

        if (b.Def.Kind is BuildingKind.Furnace or BuildingKind.Assembler or BuildingKind.Lab or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink or BuildingKind.PayloadLauncher)
        {
            List<Control> inputs = Stacks(b.Inv).Where(s => (burner == false) || (C.IsFuel(s.Item) == false))
                .Select(s => (Control)MachineItemSlot(b, s.Item, s.Count, InventoryPart.Input, reach, acceptsDrops: true)).ToList();
            // Show what the recipe still wants as faint icons, so it's clear what goes where.
            foreach (ItemAmount need in RecipeInputs(b).Where(i => b.Inv.Count(i.Item) == 0))
            {
                inputs.Add(EmptyMachineSlot(b, reach, need));
            }

            if (inputs.Count == 0)
            {
                inputs.Add(EmptyMachineSlot(b, reach, null));
            }

            row.AddChild(Section("Input", inputs));
        }

        if (b.Def.Kind is BuildingKind.Furnace or BuildingKind.Assembler or BuildingKind.Drill)
        {
            Label arrow = HudStyle.Caption("→", HudStyle.Header, 22);
            arrow.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            row.AddChild(arrow);
            List<Control> outputs = Stacks(b.Out).Select(s => (Control)MachineItemSlot(b, s.Item, s.Count, InventoryPart.Output, reach, acceptsDrops: false)).ToList();
            for (int i = outputs.Count; i < System.Math.Max(1, b.Def.OutSlots); i++)
            {
                outputs.Add(new Slot(MachineSlot) { TooltipText = "Output" });
            }

            row.AddChild(Section("Output", outputs));
        }
    }

    /// <summary>The recipe's ingredients, or a lab's research costs, for drawing empty input slots.</summary>
    private ItemAmount[] RecipeInputs(Building b)
    {
        if (b.Def.Kind == BuildingKind.PayloadLauncher)
        {
            // A launcher takes any of its payload packs; show each one it doesn't hold.
            return C.RecipeList.Where(r => b.Def.Categories.Contains(r.Category) && W.Research.Unlocked.Contains(r.Id)).SelectMany(r => r.Inputs)
                .DistinctBy(i => i.Item).ToArray();
        }

        if (b.Recipe is { } r)
        {
            return r.Inputs;
        }

        if ((b.Def.Kind == BuildingKind.Lab) && (W.Research.Current is { } tech))
        {
            return C.Techs[tech].UnitCostList;
        }

        return [];
    }

    private static VBoxContainer Section(string title, IEnumerable<Control> slots)
    {
        VBoxContainer v = new VBoxContainer();
        v.AddChild(HudStyle.Title(title, 11));
        HBoxContainer h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 4);
        foreach (Control s in slots)
        {
            h.AddChild(s);
        }

        v.AddChild(h);

        return v;
    }

    private Slot MachineItemSlot(Building b, string item, int count, InventoryPart part, bool reach, bool acceptsDrops)
    {
        Inventory inv = part == InventoryPart.Output ? b.Out : b.Inv;
        Slot slot = ItemSlot(item, count, MachineSlot);
        slot.TooltipText = $"{ItemName(item)} ×{inv.Count(item)}";
        slot.DragSource = () => reach ? new ItemDrag(item, count, false, b.Id, part) : null;
        slot.Clicked += (button, _) =>
        {
            if (reach == false)
            {
                Root.Notify("Too far away: walk closer to the machine", Colors.OrangeRed);

                return;
            }

            TakeIntoPockets(b, item, ClickAmount(button, count, inv.Count(item)), part);
        };
        if (acceptsDrops)
        {
            AcceptPocketDrops(slot, b, reach);
        }

        return slot;
    }

    private Slot EmptyMachineSlot(Building b, bool reach, ItemAmount? ghost)
    {
        Slot slot = new Slot(MachineSlot);
        if (ghost is { } g)
        {
            slot.Icon = ItemIcon(g.Item);
            slot.Ghost = true;
            slot.TooltipText = $"Needs {ItemName(g.Item)} ({g.Count} per craft)";
        }

        AcceptPocketDrops(slot, b, reach);

        return slot;
    }

    private void AcceptPocketDrops(Slot slot, Building b, bool reach)
    {
        slot.CanDrop = d => reach && d.FromPockets && (P.AcceptAmount(b, d.Item) > 0);
        slot.Dropped += d => PutIn(b, d.Item, d.Count, reach);
    }

    /// <summary>Splits an inventory into display stacks of at most one stack size each.</summary>
    private List<(string Item, int Count, int Index)> Stacks(Inventory inv)
    {
        List<(string, int, int)> stacks = [];
        foreach ((string item, int total) in inv.Items.OrderBy(kv => kv.Key, System.StringComparer.Ordinal))
        {
            int size = C.Stack(item);
            int index = 0;
            for (int left = total; left > 0; left -= size)
            {
                stacks.Add((item, System.Math.Min(size, left), index++));
            }
        }

        return stacks;
    }
}
