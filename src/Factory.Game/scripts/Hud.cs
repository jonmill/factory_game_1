using System.Text;
using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Code-built UI in the style of a sci-fi factory game: status cards at the top, the commander's panel
/// bottom-left, the build bar bottom-centre, and centred windows for the inventory, replicator, tech
/// tree and help. It only reads simulation state; every action goes out as a Command via
/// <see cref="GameRoot.Send"/>.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Control _ui = null!;
    private Label _messages = null!;
    private HudWindow _open = HudWindow.Help;
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
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = HudStyle.BuildTheme() };
        _ui.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_ui);

        BuildStatus();
        BuildFollowers();
        BuildInspector();
        BuildBuildBar();
        BuildCommander();

        _messages = new Label { VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = Control.MouseFilterEnum.Ignore };
        _messages.AddThemeColorOverride("font_outline_color", Colors.Black);
        _messages.AddThemeConstantOverride("outline_size", 4);
        _ui.AddChild(_messages);

        _windows[HudWindow.Inventory] = BuildInventory();
        _windows[HudWindow.Replicator] = BuildReplicator();
        _windows[HudWindow.Tech] = BuildTechTree();
        _windows[HudWindow.Help] = BuildHelp();
        ShowOnly(_open);
    }

    /// <summary>
    /// A centred window: a header strip with the title, its hotkey and a close button, over a body the
    /// caller fills in.
    /// </summary>
    private PanelContainer MakeWindow(HudWindow which, string title, string key, out VBoxContainer body)
    {
        PanelContainer win = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        win.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.WindowBg, HudStyle.LineBright, 3, 14, 12));
        _ui.AddChild(win);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        win.AddChild(v);
        HBoxContainer header = new HBoxContainer();
        header.AddChild(HudStyle.Title(title, 15));
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        header.AddChild(HudStyle.Key(key));
        header.AddChild(HudStyle.Button("✕", () => Toggle(which), "Close (Esc)"));
        v.AddChild(header);
        v.AddChild(HudStyle.Divider());
        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        v.AddChild(body);

        return win;
    }

    private void ShowOnly(HudWindow which)
    {
        _open = which;
        foreach ((HudWindow w, PanelContainer panel) in _windows)
        {
            panel.Visible = w == which;
        }
    }

    // ------------------------------------------------------------------ public API (keys from GameRoot)

    /// <summary>Opens a window, or closes it if it is already open; only one window is open at a time.</summary>
    /// <param name="which">The window to toggle.</param>
    public void Toggle(HudWindow which) => ShowOnly(_open == which ? HudWindow.None : which);

    /// <summary>Shows or hides the tech tree.</summary>
    public void ToggleResearch() => Toggle(HudWindow.Tech);

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
            ShowOnly(HudWindow.None);

            return true;
        }

        return CloseCategory();
    }

    // ------------------------------------------------------------------ per-frame

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        UpdateStatus();
        UpdateBuildBar();
        UpdateFollowers();
        UpdateInspector();
        UpdateCommander();
        UpdateMessages();
        switch (_open)
        {
            case HudWindow.Inventory:
                UpdateInventory();
                break;
            case HudWindow.Replicator:
                UpdateReplicator();
                break;
            case HudWindow.Tech:
                UpdateTechTree();
                break;
        }

        Layout();
    }

    /// <summary>Explicit layout every frame: panels shrink-wrap their content and are pinned to screen edges.</summary>
    private void Layout()
    {
        Vector2 vs = _ui.GetViewportRect().Size;
        static Vector2 Min(Control c) => c.GetCombinedMinimumSize();
        static void Put(Control c, Vector2 pos, float? width = null)
        {
            Vector2 m = Min(c);
            c.Size = new Vector2(width ?? m.X, m.Y);
            c.Position = pos;
        }

        const float Gap = 10;
        Put(_planetCard, new Vector2(Gap, Gap));
        Put(_followerPanel, new Vector2(Gap, _planetCard.Position.Y + _planetCard.Size.Y + 8));
        Put(_researchCard, new Vector2(vs.X - Gap - Min(_researchCard).X, Gap));
        Put(_powerCard, new Vector2(vs.X - Gap - Min(_powerCard).X, _researchCard.Position.Y + _researchCard.Size.Y + 8));
        Put(_inspector, new Vector2(vs.X - Gap - Min(_inspector).X, _powerCard.Position.Y + _powerCard.Size.Y + 8));

        _buildPanel.Size = Vector2.Zero;
        Put(_buildPanel, new Vector2(Mathf.Max(Gap, (vs.X - Min(_buildPanel).X) / 2), vs.Y - Gap - Min(_buildPanel).Y));
        Put(_commanderPanel, new Vector2(Gap, vs.Y - Gap - Min(_commanderPanel).Y));
        // Keep the commander panel clear of the build bar on narrow windows.
        if (_commanderPanel.Position.X + _commanderPanel.Size.X + Gap > _buildPanel.Position.X)
        {
            _commanderPanel.Position = new Vector2(Gap, _buildPanel.Position.Y - Gap - _commanderPanel.Size.Y);
        }

        Vector2 mm = Min(_messages);
        _messages.Size = mm;
        _messages.Position = new Vector2(Gap + 4, _commanderPanel.Position.Y - Gap - mm.Y);

        foreach (PanelContainer win in _windows.Values)
        {
            if (win.Visible)
            {
                win.Size = Vector2.Zero;
                Vector2 size = Min(win);
                win.Size = size;
                win.Position = ((vs - size) / 2).Floor();
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

    private void UpdateMessages()
    {
        StringBuilder sb = new StringBuilder();
        foreach (Message m in Root.Messages)
        {
            if (Root.Clock - m.Time < 12)
            {
                sb.AppendLine(m.Text);
            }
        }

        _messages.Text = sb.ToString();
    }
}
