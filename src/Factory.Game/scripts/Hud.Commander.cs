using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The commander's end of the dock: what they are doing and how full their pockets are, plus the
/// shortcut buttons that open the HUD's windows at the dock's other end.
/// </summary>
public partial class Hud
{
    private PanelContainer _commanderPanel = null!;
    private Label _commanderStatus = null!, _pocketText = null!;
    private ProgressBar _pocketBar = null!;
    private Slot _commanderWork = null!;
    private string? _peekSig;
    private readonly List<(HudWindow Window, Button Button)> _menuButtons = [];

    private void BuildCommander()
    {
        // The commander's chip, left end of the dock: what they're doing (with its ring) and how full their pockets are.
        _commanderPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        _commanderPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        HBoxContainer chip = new HBoxContainer();
        chip.AddThemeConstantOverride("separation", 6);
        _commanderPanel.AddChild(chip);
        _commanderWork = new Slot(40) { MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        chip.AddChild(_commanderWork);
        VBoxContainer v = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        v.AddThemeConstantOverride("separation", 1);
        chip.AddChild(v);
        _commanderStatus = HudStyle.Caption("", HudStyle.TextDim, 10);
        _commanderStatus.ClipText = true;
        _commanderStatus.CustomMinimumSize = new Vector2(130, 0);
        v.AddChild(_commanderStatus);
        HBoxContainer pockets = new HBoxContainer();
        pockets.AddThemeConstantOverride("separation", 4);
        _pocketBar = HudStyle.Bar(70, 4);
        _pocketBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _pocketBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pockets.AddChild(_pocketBar);
        _pocketText = HudStyle.Caption("", null, 10);
        pockets.AddChild(_pocketText);
        v.AddChild(pockets);
        _dock.AddChild(_commanderPanel);
        _dock.MoveChild(_commanderPanel, 0);
        _dock.AddChild(VLine());
        _dock.MoveChild(_dock.GetChild(_dock.GetChildCount() - 1), 1);

        // Window shortcuts, right end of the dock, as in a shortcut bar.
        _dock.AddChild(VLine());
        GridContainer menu = new GridContainer { Columns = 3, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        menu.AddThemeConstantOverride("h_separation", 2);
        menu.AddThemeConstantOverride("v_separation", 2);
        foreach ((HudWindow w, string label, string key) in new[]
        {
            (HudWindow.Inventory, "Inventory", "E"), (HudWindow.Replicator, "Replicator", "C"), (HudWindow.Tech, "Tech tree", "T"),
            (HudWindow.System, "System map", "M"), (HudWindow.Help, "Help", "H"), (HudWindow.Menu, "Menu", "Esc"),
        })
        {
            HudWindow which = w;
            Button b = HudStyle.Button(key, () => Toggle(which), $"{label} ({key})");
            b.ToggleMode = true;
            b.AddThemeFontSizeOverride("font_size", 10);
            b.CustomMinimumSize = new Vector2(30, 0);
            menu.AddChild(b);
            _menuButtons.Add((which, b));
        }

        _dock.AddChild(menu);
    }

    private static ColorRect VLine() =>
        new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 34), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };

    private void UpdateCommander()
    {
        Unit leader = Root.Leader;
        Inventory carry = leader.Carry;
        _commanderStatus.Text = leader.Status;
        _commanderStatus.AddThemeColorOverride("font_color", leader.MiningTile is null ? HudStyle.TextDim : HudStyle.Orange);
        _pocketText.Text = $"{carry.UsedSlots}/{carry.Slots}";
        ShowActivity(_commanderWork, leader);
        double fill = carry.UsedSlots / (double)System.Math.Max(1, carry.Slots);
        _pocketBar.Value = fill;
        HudStyle.Tint(_pocketBar, fill >= 1 ? HudStyle.Bad : fill > 0.75 ? HudStyle.Orange : HudStyle.Header);

        foreach ((HudWindow w, Button b) in _menuButtons)
        {
            b.SetPressedNoSignal(_open == w);
        }

        string sig = string.Join(",", carry.Items.Select(kv => $"{kv.Key}:{kv.Value}"));
        if (sig != _peekSig)
        {
            _peekSig = sig;
            _commanderPanel.TooltipText = "Commander  ·  " + leader.Status + "\nPockets: " + (carry.IsEmpty ? "empty  ·  right-click ore or trees to mine" :
                string.Join(", ", carry.Items.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => $"{ItemName(kv.Key)} ×{kv.Value}"))) + "\nE opens your inventory";
        }
    }
}
