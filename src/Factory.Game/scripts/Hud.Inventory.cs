using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The character screen (E, I or Tab), laid out like a factory game's: the commander's pockets on the
/// left and tabs on the right for the replicator (crafting) and the colony's storage. Stacks move by
/// the same rules as in machine windows: drag and drop, Shift-click for a stack, Ctrl/Cmd-click for all
/// of an item and right-click for half. Moving items to or from storage needs a lander or crate in reach.
/// </summary>
public partial class Hud
{
    private const int PocketColumns = 5;
    private const float InventorySlot = 50;

    private GridContainer _pocketGrid = null!, _storageGrid = null!;
    private Label _pocketTitle = null!, _storageTitle = null!, _reachNote = null!;
    private Control _replicatorTab = null!, _storageTab = null!;
    private Button _replicatorTabButton = null!, _storageTabButton = null!;
    private string? _inventorySig;

    /// <summary>
    /// How many items a click moves, by the usual conventions: right-click half a stack,
    /// Ctrl or Cmd all of that item, otherwise (with or without Shift) one stack.
    /// </summary>
    private static int ClickAmount(MouseButton button, int stack, int all)
    {
        if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Meta))
        {
            return all;
        }

        return button == MouseButton.Right ? System.Math.Max(1, stack / 2) : stack;
    }

    private const string TransferHint = "Drag to move   ·   Shift-click: a stack   ·   Ctrl/Cmd-click: all of it   ·   Right-click: half";

    private PanelContainer BuildInventory()
    {
        PanelContainer win = MakeWindow(HudWindow.Inventory, "Commander", "E", out VBoxContainer body);
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
        left.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        left.AddChild(HudStyle.Button("Unload all  [F]", () => Root.UnloadPockets(), "Put everything into the nearest lander or crate"));
        cols.AddChild(left);

        cols.AddChild(new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 0) });

        // Fixed width so switching tabs doesn't resize the window.
        VBoxContainer right = new VBoxContainer { CustomMinimumSize = new Vector2(680, 0) };
        right.AddThemeConstantOverride("separation", 8);
        HBoxContainer tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 2);
        _replicatorTabButton = TabButton("Replicator  [C]", () => ShowOnly(HudWindow.Replicator));
        _storageTabButton = TabButton("Colony storage", () => ShowOnly(HudWindow.Inventory));
        tabs.AddChild(_replicatorTabButton);
        tabs.AddChild(_storageTabButton);
        right.AddChild(tabs);

        _replicatorTab = BuildReplicatorTab();
        right.AddChild(_replicatorTab);

        VBoxContainer storage = new VBoxContainer();
        _storageTitle = HudStyle.Title("Colony storage");
        _storageGrid = new GridContainer { Columns = 8 };
        _storageGrid.AddThemeConstantOverride("h_separation", 4);
        _storageGrid.AddThemeConstantOverride("v_separation", 4);
        storage.AddChild(_storageTitle);
        storage.AddChild(HudStyle.Caption("Everything in the lander and supply crates. Drop pocket stacks here to store them.", HudStyle.TextDim, 11));
        storage.AddChild(_storageGrid);
        _storageTab = storage;
        right.AddChild(_storageTab);
        cols.AddChild(right);

        body.AddChild(HudStyle.Divider());
        _reachNote = HudStyle.Caption("", HudStyle.TextDim, 12);
        body.AddChild(_reachNote);

        return win;
    }

    private static Button TabButton(string text, System.Action onPress)
    {
        Button b = HudStyle.Button(text, onPress);
        b.ToggleMode = true;
        b.CustomMinimumSize = new Vector2(150, 0);

        return b;
    }

    /// <summary>Shows the tab that matches which of the two windows is open.</summary>
    private void SyncInventoryTabs()
    {
        bool replicator = _open == HudWindow.Replicator;
        _replicatorTab.Visible = replicator;
        _storageTab.Visible = replicator == false;
        _replicatorTabButton.SetPressedNoSignal(replicator);
        _storageTabButton.SetPressedNoSignal(replicator == false);
    }

    private void UpdateInventory()
    {
        SyncInventoryTabs();
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
        _reachNote.Text = near is null
            ? "Walk to the lander or a crate to move items between your pockets and storage."
            : $"In reach of {near.Name}.   {TransferHint}";

        // Rebuilding mid-drag would free the slot being dragged, so the grids wait until it's dropped.
        string sig = string.Join(",", carry.Items.Select(kv => $"{kv.Key}:{kv.Value}")) + "|" +
                     string.Join(",", totals.Select(kv => $"{kv.Key}:{kv.Value}")) + "|" + (near?.Id ?? 0);
        if ((sig == _inventorySig) || (Slot.Dragging is not null))
        {
            return;
        }

        _inventorySig = sig;
        bool reach = near is not null;
        _pocketTitle.Text = $"POCKETS   {carry.UsedSlots} / {carry.Slots}";
        _storageTitle.Text = $"COLONY STORAGE   {stores.Count} building{(stores.Count == 1 ? "" : "s")}";

        Clear(_pocketGrid);
        int used = 0;
        foreach ((string item, int count, int _) in Stacks(carry))
        {
            string it = item;
            int n = count;
            Slot slot = ItemSlot(item, n, InventorySlot);
            slot.TooltipText = $"{ItemName(item)} ×{carry.Count(item)}";
            slot.DragSource = () => new ItemDrag(it, n, true);
            slot.CanDrop = d => reach && (d.FromPockets == false);
            slot.Dropped += d => TakeFromStorage(d.Item, d.Count);
            slot.Clicked += (button, _) => StoreFromPockets(it, ClickAmount(button, n, carry.Count(it)));
            _pocketGrid.AddChild(slot);
            used++;
        }

        for (; used < carry.Slots; used++)
        {
            Slot empty = new Slot(InventorySlot) { CanDrop = d => reach && (d.FromPockets == false) };
            empty.Dropped += d => TakeFromStorage(d.Item, d.Count);
            _pocketGrid.AddChild(empty);
        }

        Clear(_storageGrid);
        foreach ((string item, int count) in totals)
        {
            string it = item;
            int stack = System.Math.Min(count, C.Stack(item));
            Slot slot = ItemSlot(item, count, InventorySlot);
            slot.Dim = reach == false;
            slot.TooltipText = $"{ItemName(item)} ×{count}\n" + string.Join("\n", stores.Where(b => b.Inv.Count(item) > 0).Select(b => $"  {b.Name}: {b.Inv.Count(item)}"));
            slot.DragSource = () => reach ? new ItemDrag(it, stack, false) : null;
            slot.CanDrop = d => reach && d.FromPockets;
            slot.Dropped += d => StoreFromPockets(d.Item, d.Count);
            slot.Clicked += (button, _) => TakeFromStorage(it, ClickAmount(button, stack, count));
            _storageGrid.AddChild(slot);
        }

        // A few empty wells so there is always somewhere to drop a stack.
        for (int i = 0; i < 8 - (totals.Count % 8); i++)
        {
            Slot empty = new Slot(InventorySlot) { CanDrop = d => reach && d.FromPockets };
            empty.Dropped += d => StoreFromPockets(d.Item, d.Count);
            _storageGrid.AddChild(empty);
        }
    }

    private void StoreFromPockets(string item, int count)
    {
        if (Root.NearestStorage(b => b.Inv.SpaceFor(item) > 0) is not { } store)
        {
            Root.Notify("No lander or crate with room within reach", Colors.OrangeRed);

            return;
        }

        Root.Send(new DepositToBuilding(Root.Leader.Id, store.Id, item, count));
    }

    private void TakeFromStorage(string item, int count)
    {
        if (Root.NearestStorage(b => b.Inv.Count(item) > 0) is not { } store)
        {
            Root.Notify($"No {ItemName(item)} in a lander or crate within reach", Colors.OrangeRed);

            return;
        }

        Root.Send(new TakeFromBuilding(Root.Leader.Id, store.Id, item, count));
    }
}
