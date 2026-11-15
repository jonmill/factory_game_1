using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The build bar along the bottom: a row of categories, and above it the buildings of the open
/// category. A number key opens a category, the next number key picks a building from it, and Esc steps
/// back out.
/// </summary>
public partial class Hud
{
    private static readonly (string Name, BuildingKind[] Kinds)[] _categories =
    [
        ("Logistics", [BuildingKind.Chest, BuildingKind.Sorter]),
        ("Production", [BuildingKind.Drill, BuildingKind.Furnace, BuildingKind.Assembler]),
        ("Power", [BuildingKind.SteamEngine, BuildingKind.Pole, BuildingKind.Battery]),
        ("Science", [BuildingKind.Lab, BuildingKind.Beacon]),
        ("Transport", [BuildingKind.Rail, BuildingKind.RailStop, BuildingKind.WaterTower]),
        ("Tools", []),
    ];

    private PanelContainer _buildPanel = null!;
    private Label _hint = null!, _categoryTitle = null!;
    private HBoxContainer _categoryRow = null!, _itemRow = null!;
    private VBoxContainer _itemBox = null!;
    private readonly List<List<BuildEntry>> _entries = [];
    private readonly List<Slot> _categorySlots = [];
    private readonly List<(Slot Slot, BuildEntry Entry)> _itemSlots = [];
    private int _openCategory = -1;
    private int _unlockSig = -1;

    private void BuildBuildBar()
    {
        _buildPanel = new PanelContainer();
        _ui.AddChild(_buildPanel);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        _buildPanel.AddChild(v);
        _hint = HudStyle.Caption("", HudStyle.TextDim, 12);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(_hint);

        _itemBox = new VBoxContainer();
        _categoryTitle = HudStyle.Title("");
        _categoryTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _itemRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _itemRow.AddThemeConstantOverride("separation", 4);
        _itemBox.AddChild(_categoryTitle);
        _itemBox.AddChild(_itemRow);
        _itemBox.AddChild(HudStyle.Divider());
        v.AddChild(_itemBox);

        _categoryRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _categoryRow.AddThemeConstantOverride("separation", 4);
        v.AddChild(_categoryRow);
    }

    /// <summary>Handles a number key: opens a category, or picks a building in the open one.</summary>
    /// <param name="i">Zero-based slot (key 1 is 0, key 0 is 9).</param>
    public void BuildKey(int i)
    {
        if (_openCategory < 0)
        {
            OpenCategory(i);
        }
        else if (i < _entries[_openCategory].Count)
        {
            _entries[_openCategory][i].Activate();
        }
    }

    /// <summary>Closes the open build category; returns false if none was open.</summary>
    private bool CloseCategory()
    {
        if (_openCategory < 0)
        {
            return false;
        }

        _openCategory = -1;
        RebuildItems();

        return true;
    }

    private void OpenCategory(int i)
    {
        if ((i < 0) || (i >= _entries.Count) || (_entries[i].Count == 0))
        {
            return;
        }

        _openCategory = _openCategory == i ? -1 : i;
        RebuildItems();
    }

    /// <summary>Everything the player can place right now, grouped by category.</summary>
    private void RebuildEntries()
    {
        _entries.Clear();
        foreach ((string _, BuildingKind[] kinds) in _categories)
        {
            List<BuildEntry> list = [];
            foreach (BuildingDef def in C.BuildingList.Where(d => d.Buildable && kinds.Contains(d.Kind) && W.Research.Unlocked.Contains(d.Id)))
            {
                BuildingDef d = def;
                list.Add(new BuildEntry(
                    d.Name,
                    $"{d.Name}\n{d.Description}\nCost: {Costs(d.CostList)}",
                    (ci, r) => Icons.Building(ci, r, d),
                    () => Root.SetTool(ToolKind.Place, d.Id),
                    () => (Root.Tool == ToolKind.Place) && (Root.PlaceDefId == d.Id)));
            }

            _entries.Add(list);
        }

        AddTrain("Hand Cart + 1 Cart", ["hand-cart", "mine-cart"]);
        AddTrain("Steam Loco + 3 Carts", ["steam-locomotive", "mine-cart", "mine-cart", "mine-cart"]);
        _entries[^1].Add(new BuildEntry(
            "Dismantle",
            "Dismantle (X)\nClick buildings, track, trains or ghosts to remove them.",
            Icons.Dismantle,
            () => Root.SetTool(Root.Tool == ToolKind.Deconstruct ? ToolKind.Select : ToolKind.Deconstruct),
            () => Root.Tool == ToolKind.Deconstruct));
    }

    private void AddTrain(string label, string[] preset)
    {
        if (preset.All(W.Research.Unlocked.Contains) == false)
        {
            return;
        }

        IEnumerable<ItemAmount> cost = preset.SelectMany(v => C.Vehicles[v].CostList).GroupBy(c => c.Item).Select(g => new ItemAmount(g.Key, g.Sum(x => x.Count)));
        VehicleDef head = C.Vehicles[preset[0]];
        int transport = System.Array.FindIndex(_categories, c => c.Name == "Transport");
        _entries[transport].Add(new BuildEntry(
            label,
            $"{label}\nPlace on track: the front goes at the cursor, R faces it another way.\nCost: {Costs(cost)}",
            (ci, r) => Icons.Vehicle(ci, r, head),
            () =>
            {
                Root.TrainPreset = preset;
                Root.SetTool(ToolKind.PlaceTrain);
            },
            () => (Root.Tool == ToolKind.PlaceTrain) && Root.TrainPreset.SequenceEqual(preset)));
    }

    private void RebuildCategories()
    {
        Clear(_categoryRow);
        _categorySlots.Clear();
        for (int i = 0; i < _categories.Length; i++)
        {
            int index = i;
            List<BuildEntry> list = _entries[i];
            VBoxContainer cell = new VBoxContainer();
            cell.AddThemeConstantOverride("separation", 1);
            Slot slot = new Slot(44)
            {
                Icon = list.Count > 0 ? list[0].Icon : null,
                KeyHint = $"{i + 1}",
                Dim = list.Count == 0,
                TooltipText = list.Count == 0 ? $"{_categories[i].Name} (nothing unlocked yet)" : $"{_categories[i].Name}  [{i + 1}]",
            };
            slot.Clicked += (_, _) => OpenCategory(index);
            Label name = HudStyle.Caption(_categories[i].Name, HudStyle.TextDim, 11);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.CustomMinimumSize = new Vector2(70, 0);
            slot.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            cell.AddChild(slot);
            cell.AddChild(name);
            _categoryRow.AddChild(cell);
            _categorySlots.Add(slot);
        }
    }

    private void RebuildItems()
    {
        Clear(_itemRow);
        _itemSlots.Clear();
        _itemBox.Visible = _openCategory >= 0;
        if (_openCategory < 0)
        {
            return;
        }

        _categoryTitle.Text = $"{_categories[_openCategory].Name.ToUpperInvariant()}   ·   number keys pick, Esc goes back";
        List<BuildEntry> list = _entries[_openCategory];
        for (int i = 0; i < list.Count; i++)
        {
            BuildEntry e = list[i];
            Slot slot = new Slot(52) { Icon = e.Icon, KeyHint = i < 9 ? $"{i + 1}" : i == 9 ? "0" : null, TooltipText = e.Tooltip };
            slot.Clicked += (_, _) => e.Activate();
            _itemRow.AddChild(slot);
            _itemSlots.Add((slot, e));
        }
    }

    private void UpdateBuildBar()
    {
        int sig = W.Research.Unlocked.Count;
        if (sig != _unlockSig)
        {
            _unlockSig = sig;
            RebuildEntries();
            RebuildCategories();
            RebuildItems();
        }

        for (int i = 0; i < _categorySlots.Count; i++)
        {
            bool active = (i == _openCategory) || _entries[i].Any(e => e.IsActive());
            _categorySlots[i].Set(null, active, _entries[i].Count == 0);
        }

        foreach ((Slot slot, BuildEntry entry) in _itemSlots)
        {
            slot.Set(null, entry.IsActive());
        }

        _hint.Text = ToolHint();
    }

    private string ToolHint() => Root.Tool switch
    {
        ToolKind.Place when Root.PlaceDefId is { } id => $"Placing {C.Buildings[id].Name}" + (id == "rail" ? "  ·  drag to lay track" : $"  ·  facing {Root.PlaceRot} (R)") + "  ·  Q cancels",
        ToolKind.PlaceTrain => $"Placing a train facing {Root.PlaceRot} (R)  ·  click a rail tile  ·  Q cancels",
        ToolKind.Deconstruct => "Dismantle: click a building, track, train or ghost  ·  Q cancels",
        ToolKind.PickHaulFrom => "Haul: click the building to take items FROM",
        ToolKind.PickHaulTo => "Haul: click the building to deliver items TO",
        ToolKind.PickTrain => "Click the train this follower should operate",
        ToolKind.PickStop => "Click rail stops to add them to the schedule  ·  Q when done",
        _ => "Left-click select  ·  Right-click ore or trees to mine  ·  F unload pockets",
    };
}
