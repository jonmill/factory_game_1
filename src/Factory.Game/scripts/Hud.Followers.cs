using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>The helper bots list, left: one slim row per bot with what it is doing, its status and a job button.</summary>
public partial class Hud
{
    private const int BuildOption = 1;
    private const int ExploreOption = 2;

    /// <summary>The job picker's entries: Idle, Build, Explore, one Gather per resource on this planet, then Haul and Operate train.</summary>
    private readonly List<(string Label, Job? Job)> _jobOptions = [];
    private int _jobOptionsPlanet = -1;

    private int HaulOption => _jobOptions.Count - 2;

    private int TrainOption => _jobOptions.Count - 1;

    /// <summary>Rebuilds the job list for the viewed planet: gather jobs only for resources it actually has.</summary>
    private void RefreshJobOptions()
    {
        if (_jobOptionsPlanet == P.Id)
        {
            return;
        }

        _jobOptionsPlanet = P.Id;
        _jobOptions.Clear();
        _jobOptions.Add(("Idle", Job.Idle));
        _jobOptions.Add(("Build", new Job(JobKind.Build)));
        _jobOptions.Add(("Explore", new Job(JobKind.Explore)));
        HashSet<string> here = P.Def.Resources.Select(r => r.Resource).ToHashSet();
        if (P.Def.Biomes.Any(b => C.Biomes.TryGetValue(b.Biome, out BiomeDef? def) && (def.Trees > 0)))
        {
            here.UnionWith(C.ResourceList.Where(r => r.IsTree).Select(r => r.Id));
        }

        foreach (ResourceDef r in C.ResourceList.Where(r => here.Contains(r.Id)).OrderBy(r => r.IsTree ? 0 : 1))
        {
            _jobOptions.Add(($"Gather {C.Items[r.Item].Name}", new Job(JobKind.Gather, Resource: r.Id)));
        }

        _jobOptions.Add(("Haul…", null));
        _jobOptions.Add(("Operate train…", null));
        _followerSig = -1;
    }

    private PanelContainer _followerPanel = null!;
    private GridContainer _followerTiles = null!;
    private VBoxContainer _followers = null!;
    private ScrollContainer _followerScroll = null!;
    private Label _followerCount = null!;
    private Button _followerFold = null!;
    private PopupMenu _jobMenu = null!;
    private Unit? _jobMenuUnit;
    private int _followerSig = -1;
    private readonly List<(Unit Unit, Label? Name, Label? Status, Button? Job, Slot Work)> _followerRows = [];
    private bool _followerGrid;

    /// <summary>Past this many bots the list turns into a grid of small tiles, one per bot.</summary>
    private const int FollowerRowsMax = 6;

    /// <summary>Width of a helper bot row's status text, in HUD units.</summary>
    private const float FollowerTextWidth = 150;

    private void BuildFollowers()
    {
        _followerPanel = new PanelContainer();
        _followerPanel.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.Bg, HudStyle.Line, 2, 7, 5));
        _ui.AddChild(_followerPanel);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 3);
        _followerPanel.AddChild(v);
        HBoxContainer head = new HBoxContainer();
        head.AddChild(HudStyle.Title("Helper bots", 11));
        _followerCount = HudStyle.Caption("", HudStyle.TextDim, 10);
        _followerCount.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _followerCount.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        head.AddChild(_followerCount);
        _followerFold = HudStyle.Button("–", () => _followerScroll.Visible = _followerScroll.Visible == false, "Fold or unfold the list");
        _followerFold.AddThemeFontSizeOverride("font_size", 10);
        head.AddChild(_followerFold);
        v.AddChild(head);
        _followerScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _followers = new VBoxContainer();
        _followers.AddThemeConstantOverride("separation", 2);
        _followerScroll.AddChild(_followers);
        _followerTiles = new GridContainer { Columns = 7, Visible = false };
        _followerTiles.AddThemeConstantOverride("h_separation", 3);
        _followerTiles.AddThemeConstantOverride("v_separation", 3);
        v.AddChild(_followerTiles);
        v.AddChild(_followerScroll);

        _jobMenu = new PopupMenu();
        _jobMenu.IdPressed += id =>
        {
            if (_jobMenuUnit is { } u)
            {
                OnJobPicked(u, (int)id);
            }
        };
        _ui.AddChild(_jobMenu);
    }

    /// <summary>Opens the job menu for a bot, under its job button, with jobs it lacks a module for greyed out.</summary>
    private void OpenJobMenu(Unit u, Control from)
    {
        _jobMenuUnit = u;
        _jobMenu.Clear();
        for (int i = 0; i < _jobOptions.Count; i++)
        {
            _jobMenu.AddRadioCheckItem(_jobOptions[i].Label, i);
            _jobMenu.SetItemChecked(i, i == JobIndex(u.Job));
        }

        foreach ((JobKind kind, int index) in new[] { (JobKind.Build, BuildOption), (JobKind.OperateTrain, TrainOption) })
        {
            bool can = P.CanTakeJob(u, kind);
            _jobMenu.SetItemDisabled(index, can == false);
            _jobMenu.SetItemTooltip(index, can ? "" : $"Needs a {P.ModuleForJob(kind)?.Name ?? "module"}: click the bot to install one");
        }

        // Opens under the button, placed in screen space as Godot's own dropdowns are.
        Vector2 at = from.GetScreenPosition() + new Vector2(0, from.Size.Y * from.GetGlobalTransformWithCanvas().Scale.Y);
        _jobMenu.Popup(new Rect2I((Vector2I)at, Vector2I.Zero));
    }

    /// <summary>Opens the first helper bot's job menu (screenshot hook).</summary>
    public void DebugOpenJobMenu()
    {
        if (_followerRows.Count > 0)
        {
            OpenJobMenu(_followerRows[0].Unit, (Control?)_followerRows[0].Job ?? _followerRows[0].Work);
        }
    }

    /// <summary>The short name of a job for a bot's row.</summary>
    private string JobShort(Job j) => j.Kind switch
    {
        JobKind.Gather when (j.Resource is { } r) && C.Resources.TryGetValue(r, out ResourceDef? def) => ItemName(def.Item),
        JobKind.OperateTrain => "Train",
        _ => j.Kind.ToString(),
    };

    private int JobIndex(Job j) => j.Kind switch
    {
        JobKind.Build => BuildOption,
        JobKind.Explore => ExploreOption,
        JobKind.Gather => System.Math.Max(0, _jobOptions.FindIndex(o => o.Job?.Resource == j.Resource)),
        JobKind.Haul => HaulOption,
        JobKind.OperateTrain => TrainOption,
        _ => 0,
    };

    private void UpdateFollowers()
    {
        RefreshJobOptions();
        List<Unit> units = P.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == GameRoot.LocalPlayer)).ToList();
        _followerCount.Text = $"  {units.Count}" + (W.HelpersBuilt > 0 ? $"  (+{W.HelpersBuilt} built)" : "");
        _followerFold.Text = _followerScroll.Visible ? "–" : "+";
        _followerPanel.Visible = units.Count > 0;
        bool grid = units.Count > FollowerRowsMax;
        if ((units.Count != _followerSig) || (grid != _followerGrid))
        {
            _followerSig = units.Count;
            _followerGrid = grid;
            Clear(_followers);
            Clear(_followerTiles);
            _followerRows.Clear();
            if (grid)
            {
                BuildFollowerTiles(units);
            }

            foreach (Unit u in grid ? [] : units)
            {
                Unit unit = u;
                HBoxContainer row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
                row.AddThemeConstantOverride("separation", 5);
                row.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    {
                        Root.SelectedId = unit.Id;
                    }
                };
                // What the bot is gathering or building, with its progress ring.
                Slot work = new Slot(24) { MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
                VBoxContainer text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
                text.AddThemeConstantOverride("separation", -3);
                Label name = HudStyle.Caption(u.Name, HudStyle.Text, 11);
                Label status = HudStyle.Caption("", HudStyle.TextDim, 10);
                status.ClipText = true;
                status.CustomMinimumSize = new Vector2(FollowerTextWidth, 0);
                name.MouseFilter = Control.MouseFilterEnum.Pass;
                status.MouseFilter = Control.MouseFilterEnum.Pass;
                text.AddChild(name);
                text.AddChild(status);
                Button job = HudStyle.Button("", () => { }, "Change job");
                job.AddThemeFontSizeOverride("font_size", 10);
                job.CustomMinimumSize = new Vector2(92, 0);
                job.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                job.ClipText = true;
                job.ClipText = true;
                job.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                job.Pressed += () => OpenJobMenu(unit, job);
                row.AddChild(work);
                row.AddChild(text);
                row.AddChild(job);
                _followers.AddChild(row);
                _followerRows.Add((unit, name, status, job, work));
            }
        }

        _followerTiles.Visible = grid && _followerScroll.Visible;
        foreach ((Unit u, Label? name, Label? status, Button? job, Slot work) in _followerRows)
        {
            if (grid)
            {
                ShowActivity(work, u, keepIdle: true);
                work.TooltipText = $"{u.Name}  ·  {JobShort(u.Job)}\n{u.Status}\nClick to select, right-click to change its job";
                work.Selected = Root.SelectedId == u.Id;
                continue;
            }

            ShowActivity(work, u);
            status!.Text = u.Status;
            name!.TooltipText = $"{u.Name}: {u.Status}";
            name.AddThemeColorOverride("font_color", Root.SelectedId == u.Id ? HudStyle.Orange : HudStyle.Text);
            job!.Text = JobShort(u.Job) + " ▾";
        }
    }

    /// <summary>The compact grid: one small tile per bot, its initials and what it's working on with the ring.</summary>
    private void BuildFollowerTiles(List<Unit> units)
    {
        foreach (Unit u in units)
        {
            Unit unit = u;
            Slot tile = new Slot(30) { KeyHint = u.Name.Length > 2 ? u.Name[..2] : u.Name };
            tile.Clicked += (button, _) =>
            {
                if (button == MouseButton.Right)
                {
                    OpenJobMenu(unit, tile);
                }
                else
                {
                    Root.SelectedId = unit.Id;
                }
            };
            _followerTiles.AddChild(tile);
            _followerRows.Add((unit, null, null, null, tile));
        }
    }

    private void OnJobPicked(Unit u, int index)
    {
        (string _, Job? job) = _jobOptions[index];
        Root.PickUnitId = u.Id;
        if (job is not null)
        {
            Root.Send(new AssignJob(u.Id, job));

            return;
        }

        if (index == HaulOption)
        {
            Root.SetTool(ToolKind.PickHaulFrom);
            Root.Notify($"{u.Name}: click a building to haul FROM", Colors.Khaki);
        }
        else
        {
            Root.SetTool(ToolKind.PickTrain);
            Root.Notify($"{u.Name}: click a train to operate", Colors.Khaki);
        }
    }
}
