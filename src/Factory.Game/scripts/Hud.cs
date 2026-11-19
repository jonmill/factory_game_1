using System.Text;
using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Code-built UI laid out like Factorio's and DSP's: a compact planet strip and helper bot list on the
/// left, the minimap with research and power top-right (hover info under it), one dock along the bottom
/// (commander, build categories, window shortcuts), a few fading message lines, and centred windows for
/// the inventory, replicator, tech tree and help. The play area stays clear. It only reads simulation state; every action goes out as a Command via
/// <see cref="GameRoot.Send"/>.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Control _ui = null!;
    private Label _messages = null!;
    private HudWindow _open = HudWindow.None;
    private Theme _theme = null!;
    private float _scale; // canvas units of the game viewport per HUD unit
    private readonly Dictionary<HudWindow, PanelContainer> _windows = [];

    /// <summary>The game root this HUD reads state from and sends commands through.</summary>
    public GameRoot Root { get; set; } = null!;

    private ContentDb C => Root.Content;

    private World W => Root.World;

    private Planet P => Root.Planet;

    // ------------------------------------------------------------------ setup

    /// <inheritdoc />
    public override void _Ready()
    {
        // Not anchored to the viewport: the HUD is scaled (see UpdateScale), so its root is sized by hand.
        _theme = HudStyle.BuildTheme();
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = _theme };
        AddChild(_ui);
        UpdateScale();

        BuildStatus();
        BuildFollowers();
        BuildInspector();
        BuildBuildBar();
        BuildCommander();
        BuildGuide();

        _messages = new Label { VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _messages.AddThemeColorOverride("font_outline_color", Colors.Black);
        _messages.AddThemeConstantOverride("outline_size", 4);
        _messages.AddThemeFontSizeOverride("font_size", 11);
        _ui.AddChild(_messages);

        _windows[HudWindow.Inventory] = BuildInventory();
        _windows[HudWindow.Tech] = BuildTechTree();
        _windows[HudWindow.Help] = BuildHelp();
        _windows[HudWindow.Machine] = BuildMachine();
        _windows[HudWindow.Robot] = BuildRobot();
        _windows[HudWindow.Menu] = BuildMenu();
        _windows[HudWindow.Victory] = BuildVictory();
        _windows[HudWindow.System] = BuildSystem();
        _windows[HudWindow.Map] = BuildMap();
        BuildSaveWindows();
        ShowOnly(_open);
    }

    /// <summary>
    /// A centred window: a header strip with the title, its hotkey and a close button, over a body the
    /// caller fills in.
    /// </summary>
    private PanelContainer MakeWindow(HudWindow which, string title, string key, out VBoxContainer body) =>
        MakeWindow(which, title, key, out body, out _);

    /// <summary>A centred window whose title can be changed later through <paramref name="titleLabel"/>.</summary>
    private PanelContainer MakeWindow(HudWindow which, string title, string? key, out VBoxContainer body, out Label titleLabel)
    {
        PanelContainer win = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        win.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.WindowBg, HudStyle.LineBright, 3, 14, 12));
        _ui.AddChild(win);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        win.AddChild(v);
        HBoxContainer header = new HBoxContainer();
        titleLabel = HudStyle.Title(title, 15);
        header.AddChild(titleLabel);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        if (key is not null)
        {
            header.AddChild(HudStyle.Key(key));
        }

        header.AddChild(HudStyle.Button("✕", () => Toggle(which), "Close (Esc)"));
        v.AddChild(header);
        v.AddChild(HudStyle.Divider());
        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        v.AddChild(body);

        return win;
    }

    /// <summary>The replicator is a tab of the character screen, so both open the same panel.</summary>
    private static HudWindow Panel(HudWindow which) => which == HudWindow.Replicator ? HudWindow.Inventory : which;

    private void ShowOnly(HudWindow which)
    {
        if ((which == HudWindow.Map) && (_open != HudWindow.Map))
        {
            CentreMap();
        }

        // Closing a machine window lets go of the building it showed, as closing a machine's panel does.
        // (Unless the selection has already moved on to something else.)
        if ((_open == HudWindow.Machine) && (which != HudWindow.Machine))
        {
            if (Root.SelectedId == _machineId)
            {
                Root.SelectedId = 0;
            }

            _machineId = 0;
        }

        if ((_open == HudWindow.Robot) && (which != HudWindow.Robot))
        {
            if (Root.SelectedId == _robotId)
            {
                Root.SelectedId = 0;
            }

            _robotId = 0;
        }

        bool wasMenu = PausesGame(_open);
        bool isMenu = PausesGame(which);
        if (wasMenu != isMenu)
        {
            if (isMenu)
            {
                _pausedBeforeMenu = Root.Paused;
                Root.Paused = true;
            }
            else
            {
                Root.Paused = _pausedBeforeMenu;
            }
        }

        _open = which;
        foreach ((HudWindow w, PanelContainer panel) in _windows)
        {
            panel.Visible = w == Panel(which);
        }

        if (Panel(which) == HudWindow.Inventory)
        {
            _inventorySig = null;
            SyncInventoryTabs();
        }
    }

    // ------------------------------------------------------------------ public API (keys from GameRoot)

    /// <summary>Opens a window, or closes it if it is already open; only one window is open at a time.</summary>
    /// <param name="which">The window to toggle.</param>
    public void Toggle(HudWindow which) => ShowOnly(_open == which ? HudWindow.None : which);

    /// <summary>Shows or hides the tech tree.</summary>
    public void ToggleResearch() => Toggle(HudWindow.Tech);

    /// <summary>Closes whichever window is open.</summary>
    public void CloseWindows() => ShowOnly(HudWindow.None);

    /// <summary>Shows or hides the help window.</summary>
    public void ToggleHelp() => Toggle(HudWindow.Help);

    /// <summary>
    /// Steps back one level for Esc: closes the open window, then the open build category. Returns
    /// false when there was nothing to close, so the caller can clear the selection instead.
    /// </summary>
    /// <returns>True if something was closed.</returns>
    public bool BackOut()
    {
        if (_open != HudWindow.None)
        {
            // The save and load windows step back to the game menu they were opened from.
            ShowOnly(_open is HudWindow.Save or HudWindow.Load ? HudWindow.Menu : HudWindow.None);

            return true;
        }

        return CloseCategory();
    }

    // ------------------------------------------------------------------ per-frame

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        UpdateScale();
        WatchSelection();
        UpdateStatus();
        UpdateBuildBar();
        UpdateFollowers();
        UpdateDrillWarnings();
        UpdateInspector();
        UpdateCommander();
        UpdateMessages();
        UpdateGuide();
        UpdateSaves();
        switch (_open)
        {
            case HudWindow.Inventory:
                UpdateInventory();
                break;
            case HudWindow.Replicator:
                UpdateInventory();
                UpdateReplicator();
                break;
            case HudWindow.Tech:
                UpdateTechTree();
                break;
            case HudWindow.Machine:
                UpdateMachine();
                break;
            case HudWindow.Robot:
                UpdateRobot();
                break;
            case HudWindow.System:
                UpdateSystem();
                break;
        }

        Layout();
    }

    /// <summary>
    /// Sizes the HUD from the actual game window, not the screen or the 1600×900 design size: text and
    /// panels get bigger in a bigger window and on high-DPI displays, never drop below a readable size in a
    /// small one (unless they would no longer fit), and the player's interface-size setting multiplies the
    /// result. The whole HUD layer is scaled, so every panel, slot and font grows together.
    /// </summary>
    private void UpdateScale()
    {
        Vector2 vs = GetViewport().GetVisibleRect().Size;
        Vector2 win = GetWindow().Size;
        if ((vs.Y <= 0) || (win.Y <= 0))
        {
            return;
        }

        // The canvas_items stretch already maps the viewport onto the window at this many pixels per unit.
        float stretch = win.Y / vs.Y;
        float s = Mathf.Snapped(HudStyle.PixelsPerUnit(win) * GameSettings.UiScale / stretch, 0.01f);
        if (Mathf.IsEqualApprox(s, _scale) == false)
        {
            _scale = s;
            Scale = new Vector2(s, s);
            HudStyle.ScaleTheme(_theme, s);
        }

        _ui.Position = Vector2.Zero;
        _ui.Size = vs / s;
    }

    /// <summary>Explicit layout every frame: panels shrink-wrap their content and are pinned to screen edges.</summary>
    private void Layout()
    {
        Vector2 vs = _ui.Size;
        static Vector2 Min(Control c) => c.GetCombinedMinimumSize();
        static void Put(Control c, Vector2 pos, float? width = null)
        {
            Vector2 m = Min(c);
            c.Size = new Vector2(width ?? m.X, m.Y);
            c.Position = pos;
        }

        const float Gap = 8;
        // Top-left: the planet strip, and the helper bots list under it.
        Put(_planetCard, new Vector2(Gap, Gap));
        // Top-right: the minimap with research and power, then the guide and the inspector below it.
        Put(_researchCard, new Vector2(vs.X - Gap - Min(_researchCard).X, Gap));
        float rightY = LayoutGuide(vs, Gap, _researchCard.Position.Y + _researchCard.Size.Y + 6);
        Put(_inspector, new Vector2(vs.X - Gap - Min(_inspector).X, rightY));

        // Bottom-centre: the dock (commander, build categories, shortcuts), with an open category above it.
        _buildPanel.Size = Vector2.Zero;
        Put(_buildPanel, new Vector2(Mathf.Max(Gap, (vs.X - Min(_buildPanel).X) / 2), vs.Y - Gap - Min(_buildPanel).Y));

        // The bots list gets the left edge down to the messages; past that it scrolls.
        float botsTop = _planetCard.Position.Y + _planetCard.Size.Y + 6;
        float messagesBottom = vs.Y - Gap;
        if (_buildPanel.Position.X < 440)
        {
            messagesBottom = _buildPanel.Position.Y - 6; // narrow window: messages go above the dock
        }

        const int MessageLines = 5;
        float lineH = System.Math.Max(1, _messages.GetLineHeight());
        float botsRoom = Mathf.Max(60, messagesBottom - (MessageLines * lineH) - Gap - botsTop - 40);
        _followerScroll.CustomMinimumSize = new Vector2(0, Mathf.Min(_followers.GetCombinedMinimumSize().Y, botsRoom));
        Put(_followerPanel, new Vector2(Gap, botsTop));

        float messagesTop = _followerPanel.Visible ? _followerPanel.Position.Y + _followerPanel.Size.Y + Gap : botsTop;
        _messageRoom = System.Math.Clamp((int)((messagesBottom - messagesTop) / lineH), 0, MessageLines);
        // Long messages wrap within the room left of the dock (at most 420 wide); only the newest lines that fit are shown.
        float messagesWidth = Mathf.Clamp(_buildPanel.Position.X - Gap - 4, 200, 420);
        _messages.CustomMinimumSize = new Vector2(messagesWidth, 0);
        _messages.Size = new Vector2(messagesWidth, 0);
        _messages.MaxLinesVisible = System.Math.Max(1, _messageRoom);
        _messages.LinesSkipped = System.Math.Max(0, _messages.GetLineCount() - _messageRoom);
        float messagesH = System.Math.Min(_messages.GetLineCount(), _messageRoom) * lineH;
        _messages.Size = new Vector2(messagesWidth, messagesH);
        _messages.Position = new Vector2(Gap + 2, messagesBottom - messagesH);

        // Centred windows always fit: long ones scroll (help), and anything still too big for a small
        // window is shrunk to fit rather than running off the edge.
        Vector2 room = vs - new Vector2(2 * Gap, 2 * Gap);
        FitHelp(room);
        FitMap(room);
        foreach (PanelContainer win in _windows.Values)
        {
            if (win.Visible)
            {
                win.Size = Vector2.Zero;
                Vector2 size = Min(win);
                win.Size = size;
                float fit = Mathf.Min(1f, Mathf.Min(room.X / size.X, room.Y / size.Y));
                win.Scale = new Vector2(fit, fit);
                win.Position = ((vs - (size * fit)) / 2).Floor().Max(Vector2.Zero);
            }
        }
    }

    // ------------------------------------------------------------------ shared helpers

    private string ItemName(string item) => C.Items.TryGetValue(item, out ItemDef? d) ? d.Name : item;

    private string Costs(IEnumerable<ItemAmount> list) => string.Join(", ", list.Select(c => $"{c.Count} {ItemName(c.Item)}"));

    private System.Action<CanvasItem, Rect2> ItemIcon(string item)
    {
        ItemDef def = C.Item(item);

        return (ci, r) => Icons.Item(ci, r, def);
    }

    /// <summary>A square icon tile for an item with a count.</summary>
    private Slot ItemSlot(string item, int? count, float size)
    {
        return new Slot(size) { Icon = ItemIcon(item), Count = count, TooltipText = ItemName(item) };
    }

    private static void Clear(Node parent)
    {
        foreach (Node c in parent.GetChildren())
        {
            parent.RemoveChild(c);
            c.QueueFree();
        }
    }

    private int _messageRoom = 8;

    private void UpdateMessages()
    {
        // A few recent lines that fade out, as a console does: they report, they don't stay in the way.
        const double Show = 8, Fade = 2;
        StringBuilder sb = new StringBuilder();
        List<Message> recent = Root.Messages.Where(m => Root.Clock - m.Time < Show).ToList();
        foreach (Message m in recent.Skip(System.Math.Max(0, recent.Count - _messageRoom)))
        {
            sb.AppendLine(m.Text);
        }

        _messages.Text = sb.ToString().TrimEnd();
        double newest = recent.Count > 0 ? Root.Clock - recent[^1].Time : Show;
        _messages.Modulate = new Color(1, 1, 1, (float)Mathf.Clamp((Show - newest) / Fade, 0.35, 1));
    }
}
