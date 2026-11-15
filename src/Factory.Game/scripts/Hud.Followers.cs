using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>The helper bots panel, top-left: one row per bot with its status and a job picker.</summary>
public partial class Hud
{
    private const int BuildOption = 1;

    /// <summary>The job picker's entries: Idle, Build, one Gather per resource on this planet, then Haul and Operate train.</summary>
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
    private VBoxContainer _followers = null!;
    private int _followerSig = -1;
    private readonly List<(Unit Unit, Label Name, Label Status, OptionButton Job)> _followerRows = [];

    private void BuildFollowers()
    {
        _followerPanel = new PanelContainer();
        _ui.AddChild(_followerPanel);
        VBoxContainer v = new VBoxContainer();
        _followerPanel.AddChild(v);
        v.AddChild(HudStyle.Title("Helper bots"));
        _followers = new VBoxContainer();
        _followers.AddThemeConstantOverride("separation", 2);
        v.AddChild(_followers);
    }

    private int JobIndex(Job j) => j.Kind switch
    {
        JobKind.Build => BuildOption,
        JobKind.Gather => System.Math.Max(0, _jobOptions.FindIndex(o => o.Job?.Resource == j.Resource)),
        JobKind.Haul => HaulOption,
        JobKind.OperateTrain => TrainOption,
        _ => 0,
    };

    private void UpdateFollowers()
    {
        RefreshJobOptions();
        List<Unit> units = P.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == GameRoot.LocalPlayer)).ToList();
        if (units.Count != _followerSig)
        {
            _followerSig = units.Count;
            Clear(_followers);
            _followerRows.Clear();
            foreach (Unit u in units)
            {
                Unit unit = u;
                HBoxContainer row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                VBoxContainer text = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0), MouseFilter = Control.MouseFilterEnum.Stop };
                text.AddThemeConstantOverride("separation", -2);
                text.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true })
                    {
                        Root.SelectedId = unit.Id;
                    }
                };
                Label name = HudStyle.Caption(u.Name, HudStyle.Text, 13);
                Label status = HudStyle.Caption("", HudStyle.TextDim, 11);
                status.ClipText = true;
                status.CustomMinimumSize = new Vector2(220, 0);
                name.MouseFilter = Control.MouseFilterEnum.Pass;
                status.MouseFilter = Control.MouseFilterEnum.Pass;
                text.AddChild(name);
                text.AddChild(status);
                OptionButton opt = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(140, 0) };
                opt.AddThemeFontSizeOverride("font_size", 12);
                foreach ((string l, Job? _) in _jobOptions)
                {
                    opt.AddItem(l);
                }

                opt.ItemSelected += i => OnJobPicked(unit, (int)i);
                opt.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(text);
                row.AddChild(opt);
                _followers.AddChild(row);
                _followerRows.Add((unit, name, status, opt));
            }
        }

        foreach ((Unit u, Label name, Label status, OptionButton opt) in _followerRows)
        {
            status.Text = u.Status;
            status.TooltipText = u.Status;
            name.AddThemeColorOverride("font_color", Root.SelectedId == u.Id ? HudStyle.Orange : HudStyle.Text);
            // Jobs a bot lacks the module for are greyed out, naming the module it needs.
            foreach ((JobKind kind, int index) in new[] { (JobKind.Build, BuildOption), (JobKind.OperateTrain, TrainOption) })
            {
                bool can = P.CanTakeJob(u, kind);
                if (opt.IsItemDisabled(index) == can)
                {
                    opt.SetItemDisabled(index, can == false);
                    opt.SetItemTooltip(index, can ? "" : $"Needs a {P.ModuleForJob(kind)?.Name ?? "module"}: click the bot to install one");
                }
            }

            if ((opt.GetPopup().Visible == false) && (opt.Selected != JobIndex(u.Job)))
            {
                opt.Select(JobIndex(u.Job));
            }
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
