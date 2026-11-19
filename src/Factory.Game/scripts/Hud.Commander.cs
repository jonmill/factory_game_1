using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The commander's end of the dock: what they are doing (pockets live in the inventory screen), plus the
/// shortcut buttons that open the HUD's windows at the dock's other end.
/// </summary>
public partial class Hud
{
    private PanelContainer _commanderPanel = null!;
    private Label _commanderStatus = null!;
    private Slot _commanderWork = null!;
    private readonly List<(HudWindow Window, Button Button)> _menuButtons = [];

    private void BuildCommander()
    {
        // The commander's chip, left end of the dock: what they're doing, with its ring.
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
        v.AddChild(HudStyle.Caption("Commander", HudStyle.Header, 10));
        _commanderStatus = HudStyle.Caption("", HudStyle.TextDim, 10);
        _commanderStatus.ClipText = true;
        _commanderStatus.CustomMinimumSize = new Vector2(130, 0);
        v.AddChild(_commanderStatus);
        _dock.AddChild(_commanderPanel);
        _dock.MoveChild(_commanderPanel, 0);
        _dock.AddChild(VLine());
        _dock.MoveChild(_dock.GetChild(_dock.GetChildCount() - 1), 1);

        // Window shortcuts, right end of the dock, as in a shortcut bar.
        _dock.AddChild(VLine());
        GridContainer menu = new GridContainer { Columns = 4, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        menu.AddThemeConstantOverride("h_separation", 2);
        menu.AddThemeConstantOverride("v_separation", 2);
        foreach ((HudWindow w, string label, string key) in new[]
        {
            (HudWindow.Inventory, "Inventory", "E"), (HudWindow.Replicator, "Replicator", "C"), (HudWindow.Tech, "Tech tree", "T"),
            (HudWindow.Map, "Map", "M"),
            (HudWindow.System, "Star system", "P"), (HudWindow.Help, "Help", "H"), (HudWindow.Menu, "Menu", "Esc"),
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
        _commanderStatus.Text = leader.Status;
        _commanderStatus.AddThemeColorOverride("font_color", leader.MiningTile is null ? HudStyle.TextDim : HudStyle.Orange);
        _commanderPanel.TooltipText = $"Commander  ·  {leader.Status}\nE opens your inventory";
        ShowActivity(_commanderWork, leader);

        foreach ((HudWindow w, Button b) in _menuButtons)
        {
            b.SetPressedNoSignal(_open == w);
        }
    }
}
