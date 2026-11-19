using System.Globalization;
using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The save and load windows, opened from the game menu as in Factorio: the saves so far (name, when,
/// time played, planet, build), and under them a name and Save, or a Load button per save. Overwriting
/// and deleting ask first, inline. Saves this build can't load are greyed out with the reason. Also the
/// loading overlay, which covers the screen with a progress bar while a save replays.
/// </summary>
public partial class Hud
{
    private const float NameWidth = 210, SavedWidth = 128, PlayedWidth = 66, PlanetWidth = 104, BuildWidth = 178;
    private const float ListMaxHeight = 300;

    private List<SaveSlot> _slots = [];
    private HudWindow _slotsShownFor = HudWindow.None;
    private string? _confirmDelete;

    private VBoxContainer _saveRows = null!, _loadRows = null!;
    private ScrollContainer _saveScroll = null!, _loadScroll = null!;
    private LineEdit _saveName = null!;
    private Button _saveButton = null!;
    private HBoxContainer _overwriteRow = null!;
    private Label _overwriteLabel = null!;
    private Label _saveStatus = null!, _loadStatus = null!;

    private Control _loadingOverlay = null!;
    private PanelContainer _loadingPanel = null!;
    private Label _loadingTitle = null!, _loadingText = null!;
    private ProgressBar _loadingBar = null!;

    /// <summary>Windows that pause the game while open: the game menu and the save and load windows under it.</summary>
    private static bool PausesGame(HudWindow which) => which is HudWindow.Menu or HudWindow.Save or HudWindow.Load;

    private void BuildSaveWindows()
    {
        _windows[HudWindow.Save] = BuildSaveWindow();
        _windows[HudWindow.Load] = BuildLoadWindow();
        BuildLoadingOverlay();
    }

    private PanelContainer BuildSaveWindow()
    {
        PanelContainer win = MakeWindow(HudWindow.Save, "Save game", null, out VBoxContainer body, out _);
        _saveScroll = SaveList(body, out _saveRows);

        HBoxContainer row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(HudStyle.Caption("Name", HudStyle.TextDim, 12));
        _saveName = new LineEdit { PlaceholderText = "Save name", MaxLength = 60, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(300, 30) };
        _saveName.TextChanged += _ => HideOverwrite();
        _saveName.TextSubmitted += _ => TrySave(false);
        row.AddChild(_saveName);
        _saveButton = HudStyle.Button("Save", () => TrySave(false), "Save the game under this name");
        _saveButton.CustomMinimumSize = new Vector2(96, 30);
        row.AddChild(_saveButton);
        body.AddChild(row);

        _overwriteRow = new HBoxContainer { Visible = false };
        _overwriteRow.AddThemeConstantOverride("separation", 8);
        _overwriteLabel = HudStyle.Caption("", HudStyle.Orange, 12);
        _overwriteLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _overwriteRow.AddChild(_overwriteLabel);
        _overwriteRow.AddChild(HudStyle.Button("Overwrite", () => TrySave(true), "Replace the old save with this game"));
        _overwriteRow.AddChild(HudStyle.Button("Cancel", HideOverwrite));
        body.AddChild(_overwriteRow);

        _saveStatus = HudStyle.Caption("", HudStyle.Bad, 11);
        body.AddChild(_saveStatus);
        body.AddChild(HudStyle.Caption($"Saved in {GameRoot.SaveDir}", HudStyle.TextDim, 10));

        return win;
    }

    private PanelContainer BuildLoadWindow()
    {
        PanelContainer win = MakeWindow(HudWindow.Load, "Load game", null, out VBoxContainer body, out _);
        _loadScroll = SaveList(body, out _loadRows);
        _loadStatus = HudStyle.Caption("", HudStyle.Bad, 11);
        _loadStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _loadStatus.CustomMinimumSize = new Vector2(400, 0);
        body.AddChild(_loadStatus);
        body.AddChild(HudStyle.Caption($"Quicksave F5 · quickload F9 · an autosave every 10 minutes.  Saves are in {GameRoot.SaveDir}", HudStyle.TextDim, 10));

        return win;
    }

    /// <summary>The column headers and a scrolling list under them.</summary>
    private static ScrollContainer SaveList(VBoxContainer body, out VBoxContainer rows)
    {
        HBoxContainer header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        foreach ((string text, float width) in new[] { ("Name", NameWidth), ("Saved", SavedWidth), ("Played", PlayedWidth), ("Planet", PlanetWidth), ("Build", BuildWidth) })
        {
            Label l = HudStyle.Caption(text.ToUpperInvariant(), HudStyle.TextDim, 10);
            l.CustomMinimumSize = new Vector2(width, 0);
            header.AddChild(l);
        }

        // Pads the header to the rows' width (their panel margin).
        MarginContainer pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left", 9);
        pad.AddChild(header);
        body.AddChild(pad);

        ScrollContainer scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(rows);
        body.AddChild(scroll);

        return scroll;
    }

    // ------------------------------------------------------------------ per frame

    private void UpdateSaves()
    {
        // Read the folder each time a window opens, so the list is never stale.
        HudWindow shown = _open is HudWindow.Save or HudWindow.Load ? _open : HudWindow.None;
        if (shown != _slotsShownFor)
        {
            if (_slotsShownFor == HudWindow.Save)
            {
                _saveName.ReleaseFocus();
            }

            _slotsShownFor = shown;
            if (shown != HudWindow.None)
            {
                OpenSaves(shown);
            }
        }

        _saveStatus.Visible = _saveStatus.Text.Length > 0;
        _loadStatus.Visible = _loadStatus.Text.Length > 0;
        FitList(_saveScroll, _saveRows);
        FitList(_loadScroll, _loadRows);
        UpdateLoadingOverlay();
    }

    private static void FitList(ScrollContainer scroll, VBoxContainer rows)
    {
        Vector2 content = rows.GetCombinedMinimumSize();
        float height = Mathf.Min(content.Y, ListMaxHeight);
        float bar = height < content.Y ? 14 : 0;
        scroll.CustomMinimumSize = new Vector2(content.X + bar, Mathf.Max(height, 40));
    }

    private void OpenSaves(HudWindow which)
    {
        _confirmDelete = null;
        _saveStatus.Text = "";
        _loadStatus.Text = "";
        HideOverwrite();
        if (which == HudWindow.Save)
        {
            _saveName.Text = $"{P.Def.Name} {System.DateTime.Now.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture)}";
            _saveName.GrabFocus();
            _saveName.SelectAll();
        }

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        _slots = Root.ListSaves();
        FillRows(_saveRows, saving: true);
        FillRows(_loadRows, saving: false);
    }

    private void FillRows(VBoxContainer rows, bool saving)
    {
        Clear(rows);
        if (_slots.Count == 0)
        {
            rows.AddChild(HudStyle.Caption(saving ? "No saves yet." : "No saves yet. Save from the game menu (Esc), or press F5 to quicksave.", HudStyle.TextDim, 12));

            return;
        }

        foreach (SaveSlot slot in _slots)
        {
            rows.AddChild(SlotRow(slot, saving));
        }
    }

    private PanelContainer SlotRow(SaveSlot slot, bool saving)
    {
        bool ok = slot.Check.CanLoad;
        Color text = ok ? HudStyle.Text : HudStyle.TextDim;
        PanelContainer card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", HudStyle.Box(ok ? HudStyle.Well : new Color(0.03f, 0.035f, 0.045f, 0.9f), HudStyle.Line, 2, 8, 4));
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        card.AddChild(v);
        HBoxContainer row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        v.AddChild(row);

        Label Cell(string value, float width, Color color, int size = 12)
        {
            Label l = HudStyle.Caption(value, color, size);
            l.CustomMinimumSize = new Vector2(width, 0);
            l.ClipText = true;
            l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            l.TooltipText = value;
            l.MouseFilter = Control.MouseFilterEnum.Pass;
            row.AddChild(l);

            return l;
        }

        if (saving)
        {
            // Clicking a save's name picks it, to save over it.
            Button pick = HudStyle.Button(slot.Name, () =>
            {
                _saveName.Text = slot.Name;
                HideOverwrite();
            }, "Save over this one");
            pick.Flat = true;
            pick.Alignment = HorizontalAlignment.Left;
            pick.ClipText = true;
            pick.CustomMinimumSize = new Vector2(NameWidth, 0);
            pick.AddThemeColorOverride("font_color", ok ? HudStyle.Header : HudStyle.TextDim);
            pick.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(pick);
        }
        else
        {
            Cell(slot.Name, NameWidth, ok ? HudStyle.Header : HudStyle.TextDim, 13);
        }

        Cell(slot.SavedAt.ToLocalTime().ToString("d MMM yyyy  HH:mm", CultureInfo.InvariantCulture), SavedWidth, text);
        Cell(slot.Ticks is { } t ? PlayTime(t) : "–", PlayedWidth, text);
        Cell(slot.Planet ?? "–", PlanetWidth, text);
        Cell(slot.Build ?? (slot.Version == 1 ? "older build" : "–"), BuildWidth, HudStyle.TextDim, 11);

        HBoxContainer actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.End, CustomMinimumSize = new Vector2(132, 0) };
        actions.AddThemeConstantOverride("separation", 4);
        row.AddChild(actions);
        if (_confirmDelete == slot.Stem)
        {
            actions.AddChild(HudStyle.Caption("Delete?", HudStyle.Bad, 12));
            Button yes = HudStyle.Button("Yes", () => Delete(slot));
            yes.AddThemeColorOverride("font_color", HudStyle.Bad);
            actions.AddChild(yes);
            actions.AddChild(HudStyle.Button("No", () =>
            {
                _confirmDelete = null;
                RefreshSlots();
            }));
        }
        else
        {
            if (saving == false)
            {
                Button load = HudStyle.Button("Load", () => Load(slot), ok ? "Load this save" : slot.Check.Problem);
                load.Disabled = ok == false;
                load.CustomMinimumSize = new Vector2(64, 0);
                actions.AddChild(load);
            }

            actions.AddChild(HudStyle.Button("Delete", () =>
            {
                _confirmDelete = slot.Stem;
                RefreshSlots();
            }, "Delete this save (asks first)"));
        }

        string? note = slot.Check.Problem ?? slot.Check.Warning;
        if (note is not null)
        {
            Label reason = HudStyle.Caption(note, ok ? HudStyle.Orange : HudStyle.Bad, 11);
            reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            reason.CustomMinimumSize = new Vector2(NameWidth + SavedWidth + PlayedWidth + PlanetWidth + BuildWidth, 0);
            v.AddChild(reason);
        }

        return card;
    }

    /// <summary>In-game time played, as h:mm:ss.</summary>
    private static string PlayTime(long ticks)
    {
        long s = ticks / World.TicksPerSecond;

        return $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
    }

    // ------------------------------------------------------------------ actions

    private void HideOverwrite()
    {
        _overwriteRow.Visible = false;
        _saveButton.Disabled = false;
    }

    private void TrySave(bool overwrite)
    {
        string name = _saveName.Text.Trim();
        if (GameRoot.FileStem(name).Length == 0)
        {
            _saveStatus.Text = "Give the save a name.";

            return;
        }

        if ((overwrite == false) && (GameRoot.FindSlot(_slots, name) is { } existing))
        {
            _overwriteLabel.Text = $"\"{existing.Name}\" already exists. Overwrite it?";
            _overwriteRow.Visible = true;
            _saveButton.Disabled = true;

            return;
        }

        if (Root.SaveGame(name) is { } error)
        {
            _saveStatus.Text = error;
            HideOverwrite();

            return;
        }

        Root.Notify($"Saved \"{name}\"", Colors.LightGreen);
        ShowOnly(HudWindow.None);
    }

    private void Load(SaveSlot slot)
    {
        if (Root.BeginLoad(slot.Stem) is { } error)
        {
            _loadStatus.Text = error;
            RefreshSlots();
        }
    }

    private void Delete(SaveSlot slot)
    {
        _confirmDelete = null;
        string? error = GameRoot.DeleteSave(slot.Stem);
        RefreshSlots();
        _saveStatus.Text = error ?? "";
        _loadStatus.Text = error ?? "";
    }

    // ------------------------------------------------------------------ loading overlay

    private void BuildLoadingOverlay()
    {
        // Covers the whole HUD and swallows the mouse, so nothing can be clicked while a save replays.
        _loadingOverlay = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _loadingOverlay.AddChild(new ColorRect { Color = new Color(0.01f, 0.02f, 0.035f, 0.82f), MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        _loadingPanel = new PanelContainer();
        _loadingPanel.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.WindowBg, HudStyle.LineBright, 3, 18, 14));
        _loadingOverlay.AddChild(_loadingPanel);
        VBoxContainer v = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        v.AddThemeConstantOverride("separation", 8);
        _loadingPanel.AddChild(v);
        _loadingTitle = HudStyle.Title("Loading", 15);
        v.AddChild(_loadingTitle);
        v.AddChild(HudStyle.Divider());
        _loadingBar = HudStyle.Bar(420, 12);
        v.AddChild(_loadingBar);
        _loadingText = HudStyle.Caption("", HudStyle.Text, 12);
        v.AddChild(_loadingText);
        v.AddChild(HudStyle.Caption("Replaying the colony's history from the save. The game resumes when it's done.", HudStyle.TextDim, 11));
        Button cancel = HudStyle.Button("Cancel", () => Root.CancelLoad(), "Stop loading and go back to the game you were playing");
        cancel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        v.AddChild(cancel);
        _ui.AddChild(_loadingOverlay);
    }

    private void UpdateLoadingOverlay()
    {
        _loadingOverlay.Visible = Root.IsLoading;
        if (Root.IsLoading == false)
        {
            return;
        }

        _loadingOverlay.Position = Vector2.Zero;
        _loadingOverlay.Size = _ui.Size;
        _loadingPanel.Size = Vector2.Zero;
        Vector2 size = _loadingPanel.GetCombinedMinimumSize();
        _loadingPanel.Size = size;
        _loadingPanel.Position = ((_ui.Size - size) / 2).Floor();

        _loadingTitle.Text = $"LOADING \"{Root.LoadingName.ToUpperInvariant()}\"";
        double progress = Root.LoadingProgress;
        _loadingBar.Value = progress;
        _loadingText.Text = Root.LoadingTick == 0 ? "Landing on the planets…"
            : $"Tick {Root.LoadingTick.ToString("N0", CultureInfo.InvariantCulture)} / {Root.LoadingEndTick.ToString("N0", CultureInfo.InvariantCulture)}   ·   {(int)(progress * 100)}%   ·   {PlayTime(Root.LoadingTick)} of {PlayTime(Root.LoadingEndTick)} played";
    }

    /// <summary>
    /// Called when the game switches to another world (a load or a new game): drops every panel's hold on
    /// the old world's units and buildings so they rebuild from the new one.
    /// </summary>
    public void OnWorldReplaced()
    {
        _followerSig = -1;
        _jobOptionsPlanet = -1;
        _inventorySig = null;
        _inspGridSig = null;
        _machineSig = null;
        _machinePocketSig = null;
        _robotSig = null;
        _replicatorSig = null;
        _techSig = null;

        // The star system window holds the old world's planets: build it again.
        _systemRows.Clear();
        PanelContainer old = _windows[HudWindow.System];
        _windows[HudWindow.System] = BuildSystem();
        old.QueueFree();
        _ui.MoveChild(_loadingOverlay, -1);
        ShowOnly(HudWindow.None);
    }
}
