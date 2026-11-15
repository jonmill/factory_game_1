using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The inventory window: the commander's pockets as a slot grid beside the colony's storage. Clicking a
/// pocket stack stores it; clicking a stored item takes a stack (Shift: all of it). Both need a lander
/// or crate within reach.
/// </summary>
public partial class Hud
{
    private const int PocketColumns = 5;
    private const float InventorySlot = 50;

    private GridContainer _pocketGrid = null!, _storageGrid = null!;
    private Label _pocketTitle = null!, _storageTitle = null!, _reachNote = null!;
    private string? _inventorySig;

    private PanelContainer BuildInventory()
    {
        PanelContainer win = MakeWindow(HudWindow.Inventory, "Inventory", "I", out VBoxContainer body);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        body.AddChild(cols);

        VBoxContainer left = new VBoxContainer();
        _pocketTitle = HudStyle.Title("Pockets");
        _pocketGrid = new GridContainer { Columns = PocketColumns };
        _pocketGrid.AddThemeConstantOverride("h_separation", 4);
        _pocketGrid.AddThemeConstantOverride("v_separation", 4);
        left.AddChild(_pocketTitle);
        left.AddChild(_pocketGrid);
        cols.AddChild(left);

        cols.AddChild(new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 0) });

        VBoxContainer right = new VBoxContainer();
        _storageTitle = HudStyle.Title("Colony storage");
        _storageGrid = new GridContainer { Columns = 6 };
        _storageGrid.AddThemeConstantOverride("h_separation", 4);
        _storageGrid.AddThemeConstantOverride("v_separation", 4);
        right.AddChild(_storageTitle);
        right.AddChild(HudStyle.Caption("Everything in the lander and supply crates.", HudStyle.TextDim, 11));
        right.AddChild(_storageGrid);
        cols.AddChild(right);

        body.AddChild(HudStyle.Divider());
        HBoxContainer foot = new HBoxContainer();
        _reachNote = HudStyle.Caption("", HudStyle.TextDim, 12);
        _reachNote.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foot.AddChild(_reachNote);
        foot.AddChild(HudStyle.Button("Unload all  [F]", () => Root.UnloadPockets()));
        body.AddChild(foot);

        return win;
    }

    private void UpdateInventory()
    {
        Unit leader = Root.Leader;
        Inventory carry = leader.Carry;
        List<Building> stores = P.Buildings.Where(b => b.Def.Kind is BuildingKind.Lander or BuildingKind.Chest).ToList();
        SortedDictionary<string, int> totals = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        foreach (Building b in stores)
        {
            foreach ((string item, int n) in b.Inv.Items)
            {
                totals[item] = totals.GetValueOrDefault(item) + n;
            }
        }

        Building? near = Root.NearestStorage();
        string sig = string.Join(",", carry.Items.Select(kv => $"{kv.Key}:{kv.Value}")) + "|" +
                     string.Join(",", totals.Select(kv => $"{kv.Key}:{kv.Value}")) + "|" + (near?.Id ?? 0);
        if (sig == _inventorySig)
        {
            return;
        }

        _inventorySig = sig;
        _pocketTitle.Text = $"POCKETS   {carry.UsedSlots} / {carry.Slots}";
        _storageTitle.Text = $"COLONY STORAGE   {stores.Count} building{(stores.Count == 1 ? "" : "s")}";
        _reachNote.Text = near is null
            ? "Walk to the lander or a crate to move items by hand."
            : $"In reach of {near.Name}: click a pocket stack to store it, a stored item to take a stack (Shift: all).";

        Clear(_pocketGrid);
        int used = 0;
        foreach ((string item, int count) in carry.Items)
        {
            int stack = C.Stack(item);
            for (int left = count; left > 0; left -= stack)
            {
                string it = item;
                Slot slot = ItemSlot(item, System.Math.Min(stack, left), InventorySlot);
                slot.TooltipText = $"{ItemName(item)} ×{count}\nClick to store in the nearest lander or crate";
                slot.Clicked += (_, _) => StoreFromPockets(it);
                _pocketGrid.AddChild(slot);
                used++;
            }
        }

        for (; used < carry.Slots; used++)
        {
            _pocketGrid.AddChild(new Slot(InventorySlot));
        }

        Clear(_storageGrid);
        foreach ((string item, int count) in totals)
        {
            string it = item;
            Slot slot = ItemSlot(item, count, InventorySlot);
            slot.Dim = near is null;
            slot.TooltipText = $"{ItemName(item)} ×{count}\n" + string.Join("\n", stores.Where(b => b.Inv.Count(item) > 0).Select(b => $"  {b.Name}: {b.Inv.Count(item)}")) +
                               "\nClick to take a stack, Shift-click to take all";
            slot.Clicked += (_, shift) => TakeIntoPockets(it, shift);
            _storageGrid.AddChild(slot);
        }

        if (totals.Count == 0)
        {
            _storageGrid.AddChild(HudStyle.Caption("(empty)", HudStyle.TextDim));
        }
    }

    private void StoreFromPockets(string item)
    {
        if (Root.NearestStorage(b => b.Inv.SpaceFor(item) > 0) is not { } store)
        {
            Root.Notify("No lander or crate with room within reach", Colors.OrangeRed);

            return;
        }

        Root.Send(new DepositToBuilding(Root.Leader.Id, store.Id, item));
    }

    private void TakeIntoPockets(string item, bool all)
    {
        if (Root.NearestStorage(b => b.Inv.Count(item) > 0) is not { } store)
        {
            Root.Notify($"No {ItemName(item)} in a lander or crate within reach", Colors.OrangeRed);

            return;
        }

        Root.Send(new TakeFromBuilding(Root.Leader.Id, store.Id, item, all ? int.MaxValue : C.Stack(item)));
    }
}
