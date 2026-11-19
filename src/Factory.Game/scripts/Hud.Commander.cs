using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The commander's panel, bottom-left: what they are doing, how full their pockets are with the
/// biggest stacks at a glance, and the buttons that open the HUD's windows.
/// </summary>
public partial class Hud
{
    private PanelContainer _commanderPanel = null!;
    private Label _commanderStatus = null!, _pocketText = null!;
    private ProgressBar _pocketBar = null!;
    private HBoxContainer _pocketPeek = null!;
    private Slot _commanderWork = null!;
    private string? _peekSig;
    private readonly List<(HudWindow Window, Button Button)> _menuButtons = [];

    private void BuildCommander()
    {
        _commanderPanel = new PanelContainer();
        _ui.AddChild(_commanderPanel);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 5);
        _commanderPanel.AddChild(v);

        HBoxContainer head = new HBoxContainer();
        head.AddChild(HudStyle.Title("Commander"));
        _commanderStatus = HudStyle.Caption("", HudStyle.TextDim, 12);
        _commanderStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _commanderStatus.HorizontalAlignment = HorizontalAlignment.Right;
        head.AddChild(_commanderStatus);
        v.AddChild(head);

        HBoxContainer pockets = new HBoxContainer();
        pockets.AddChild(HudStyle.Caption("Pockets", HudStyle.TextDim, 12));
        _pocketBar = HudStyle.Bar(150);
        _pocketBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _pocketBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pockets.AddChild(_pocketBar);
        _pocketText = HudStyle.Caption("", null, 12);
        pockets.AddChild(_pocketText);
        v.AddChild(pockets);

        // What the commander is mining or building, with its progress ring, then the fullest stacks.
        HBoxContainer peekRow = new HBoxContainer();
        peekRow.AddThemeConstantOverride("separation", 8);
        _commanderWork = new Slot(34) { MouseFilter = Control.MouseFilterEnum.Pass };
        peekRow.AddChild(_commanderWork);
        _pocketPeek = new HBoxContainer();
        _pocketPeek.AddThemeConstantOverride("separation", 3);
        _pocketPeek.CustomMinimumSize = new Vector2(0, 34);
        peekRow.AddChild(_pocketPeek);
        v.AddChild(peekRow);

        HBoxContainer menu = new HBoxContainer();
        menu.AddThemeConstantOverride("separation", 4);
        foreach ((HudWindow w, string label, string key) in new[]
        {
            (HudWindow.Inventory, "Inventory", "E"), (HudWindow.Replicator, "Replicator", "C"),
            (HudWindow.Tech, "Tech", "T"), (HudWindow.System, "Map", "M"), (HudWindow.Help, "Help", "H"),
        })
        {
            HudWindow which = w;
            Button b = HudStyle.Button($"{label} [{key}]", () => Toggle(which));
            b.ToggleMode = true;
            b.AddThemeFontSizeOverride("font_size", 12);
            menu.AddChild(b);
            _menuButtons.Add((which, b));
        }

        v.AddChild(menu);
    }

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
        if (sig == _peekSig)
        {
            return;
        }

        _peekSig = sig;
        Clear(_pocketPeek);
        if (carry.IsEmpty)
        {
            _pocketPeek.AddChild(HudStyle.Caption("Empty  ·  right-click ore or trees to mine", HudStyle.TextDim, 12));

            return;
        }

        foreach ((string item, int count) in carry.Items.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, System.StringComparer.Ordinal).Take(8))
        {
            _pocketPeek.AddChild(ItemSlot(item, count, 34));
        }
    }
}
