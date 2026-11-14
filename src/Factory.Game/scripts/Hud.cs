using System.Text;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>Code-built UI. Reads simulation state; every action goes out as a Command via GameRoot.Send.</summary>
public partial class Hud : CanvasLayer
{
    /// <summary>The game root this HUD reads state from and sends commands through.</summary>
    public GameRoot Root { get; set; } = null!;

    private Control _ui = null!;
    private Label _top = null!, _hint = null!, _messages = null!;
    private HFlowContainer _hotbar = null!;
    private VBoxContainer _followers = null!;
    private PanelContainer _inspector = null!;
    private Label _inspTitle = null!, _inspBody = null!;
    private VBoxContainer _inspActions = null!;
    private PanelContainer _research = null!, _help = null!, _topPanel = null!, _followerPanel = null!, _hotbarPanel = null!;
    private VBoxContainer _researchList = null!;

    private readonly List<(string Label, Action Activate)> _hotbarItems = [];
    private int _unlockSig = -1, _followerSig = -1, _researchSig = -1;
    private string _inspSig = "";
    private readonly List<(Unit Unit, Label Label, OptionButton Job)> _followerRows = [];

    private static readonly Color _accent = new(0.3f, 0.85f, 1f);

    private ContentDb C => Root.Content;
    private World W => Root.World;
    private Planet P => Root.Planet;

    // ------------------------------------------------------------------ setup

    /// <inheritdoc />
    public override void _Ready()
    {
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = BuildTheme() };
        _ui.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_ui);

        PanelContainer top = _topPanel = Panel(_ui);
        _top = new Label();
        top.AddChild(_top);

        // Followers (left)
        PanelContainer fp = _followerPanel = Panel(_ui);
        fp.CustomMinimumSize = new Vector2(420, 0);
        VBoxContainer fv = new VBoxContainer();
        fp.AddChild(fv);
        fv.AddChild(Title("FOLLOWERS"));
        _followers = new VBoxContainer();
        fv.AddChild(_followers);

        // Inspector (right)
        _inspector = Panel(_ui);
        VBoxContainer iv = new VBoxContainer();
        _inspector.AddChild(iv);
        _inspTitle = Title("");
        _inspBody = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(320, 0) };
        _inspActions = new VBoxContainer();
        iv.AddChild(_inspTitle);
        iv.AddChild(_inspBody);
        iv.AddChild(_inspActions);

        // Hotbar (bottom)
        PanelContainer hb = _hotbarPanel = Panel(_ui);
        VBoxContainer hv = new VBoxContainer();
        hb.AddChild(hv);
        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _hotbar = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        hv.AddChild(_hint);
        hv.AddChild(_hotbar);

        BuildPockets();

        _messages = new Label { VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = Control.MouseFilterEnum.Ignore };
        _messages.AddThemeColorOverride("font_outline_color", Colors.Black);
        _messages.AddThemeConstantOverride("outline_size", 4);
        _ui.AddChild(_messages);

        // Research (center)
        _research = Panel(_ui);
        _research.CustomMinimumSize = new Vector2(560, 0);
        VBoxContainer rv = new VBoxContainer();
        _research.AddChild(rv);
        rv.AddChild(Title("RESEARCH  (T to close)"));
        _researchList = new VBoxContainer();
        rv.AddChild(_researchList);
        _research.Visible = false;

        _help = Panel(_ui);
        _help.AddChild(new Label { Text = HelpText });
        _help.Visible = true;
    }

    private const string HelpText =
        """
        COLONY PLAYTEST  —  H to toggle this help

        You are the Commander (orange suit). WASD to walk. The planet wraps in every direction.
        Goal: research the Colony Beacon, build it, and call your first new follower.

        FOLLOWERS (left panel): give each a job — Build, Gather a resource, Haul between two
        buildings, or Operate a hand cart. Builders construct every ghost you place,
        fetching materials from the lander/crates.

        BUILD: pick from the hotbar (1-0) and click. R rotates, Q/Esc/right-click cancels.
        Drag to lay rail. X = dismantle tool. Arrows on drills/stops/sorters show their front.
        MINING: right-click ore or trees and you walk over and mine them, moving on through the
        deposit until your pockets fill. Hold and sweep to pick tiles; WASD or right-clicking
        bare ground stops. While you're nearby, your pockets supply construction sites and the
        lander's crafts. F unloads your pockets into the nearest lander or crate.

        POWER: Steam engines must touch water and burn wood/coal. Copper poles carry power.
        TRAINS: place track, a Rail Stop pointing at it, then a train. Select the train to set
        its schedule. Hand carts need a follower to pump them; steam engines need water
        (a Water Tower near a stop) and fuel (put coal in the stop).

        Space pause   +/- game speed   T research   F5 save   F9 load   Wheel zoom
        """;

    private static Theme BuildTheme()
    {
        Theme theme = new Theme { DefaultFontSize = 14 };
        StyleBoxFlat panel = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.07f, 0.12f, 0.86f),
            BorderColor = new Color(0.3f, 0.85f, 1f, 0.55f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        panel.SetBorderWidthAll(1);
        theme.SetStylebox("panel", "PanelContainer", panel);
        static StyleBoxFlat Btn(Color bg, Color border)
        {
            StyleBoxFlat s = new StyleBoxFlat { BgColor = bg, BorderColor = border, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
            s.SetBorderWidthAll(1);
            s.SetCornerRadiusAll(4);

            return s;
        }

        foreach (string? type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Btn(new Color(0.08f, 0.13f, 0.2f), new Color(0.3f, 0.6f, 0.8f, 0.6f)));
            theme.SetStylebox("hover", type, Btn(new Color(0.1f, 0.2f, 0.3f), new Color(0.4f, 0.9f, 1f)));
            theme.SetStylebox("pressed", type, Btn(new Color(0.15f, 0.35f, 0.45f), new Color(0.6f, 1f, 1f)));
            theme.SetStylebox("disabled", type, Btn(new Color(0.06f, 0.08f, 0.1f), new Color(0.2f, 0.25f, 0.3f)));
            theme.SetColor("font_color", type, new Color(0.85f, 0.95f, 1f));
        }

        theme.SetColor("font_color", "Label", new Color(0.82f, 0.9f, 1f));

        return theme;
    }

    private static PanelContainer Panel(Control parent)
    {
        PanelContainer p = new PanelContainer();
        parent.AddChild(p);

        return p;
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

        Put(_topPanel, new Vector2(10, 8), vs.X - 20);
        Put(_followerPanel, new Vector2(10, 56));
        Put(_inspector, new Vector2(vs.X - 10 - Min(_inspector).X, 56));
        // The hotbar grows as research unlocks buildings; wrap it onto more rows rather than run off screen.
        float natural = 0;
        foreach (Node child in _hotbar.GetChildren())
        {
            if (child is Control c)
            {
                natural += c.GetCombinedMinimumSize().X + 4;
            }
        }

        _hotbar.CustomMinimumSize = new Vector2(Mathf.Min(natural, vs.X - 60), 0);
        _hotbarPanel.Size = Vector2.Zero;
        Put(_hotbarPanel, new Vector2(Mathf.Max(10, (vs.X - Min(_hotbarPanel).X) / 2), vs.Y - 10 - Min(_hotbarPanel).Y));
        Put(_pocketPanel, new Vector2(vs.X - 10 - Min(_pocketPanel).X, _hotbarPanel.Position.Y - 8 - Min(_pocketPanel).Y));
        Put(_research, (vs - Min(_research)) / 2);
        Put(_help, (vs - Min(_help)) / 2);
        Vector2 mm = Min(_messages);
        _messages.Size = mm;
        _messages.Position = new Vector2(14, _hotbarPanel.Position.Y - 10 - mm.Y);
    }

    private static Label Title(string text)
    {
        Label l = new Label { Text = text };
        l.AddThemeColorOverride("font_color", _accent);
        l.AddThemeFontSizeOverride("font_size", 15);

        return l;
    }

    private static Button Btn(string text, Action onPress, string? tooltip = null)
    {
        Button b = new Button { Text = text, TooltipText = tooltip ?? "" };
        b.Pressed += onPress;
        b.FocusMode = Control.FocusModeEnum.None;

        return b;
    }

    /// <summary>Shows or hides the research panel, closing the help panel.</summary>
    public void ToggleResearch()
    {
        _research.Visible = _research.Visible == false;
        _help.Visible = false;
    }
    /// <summary>Shows or hides the help panel, closing the research panel.</summary>
    public void ToggleHelp()
    {
        _help.Visible = _help.Visible == false;
        _research.Visible = false;
    }

    /// <summary>Activates the hotbar entry at the given index, if there is one.</summary>
    /// <param name="i">Zero-based hotbar slot.</param>
    public void HotbarSelect(int i)
    {
        if (i < _hotbarItems.Count)
        {
            _hotbarItems[i].Activate();
        }
    }

    // ------------------------------------------------------------------ per-frame

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        UpdateTop();
        UpdateHotbar();
        UpdateFollowers();
        UpdateInspector();
        UpdateResearch();
        UpdateMessages();
        UpdatePockets();
        _hint.Text = ToolHint();
        Layout();
    }

    private string ItemName(string item) => C.Items.TryGetValue(item, out ItemDef? d) ? d.Name : item;

    private string Costs(IEnumerable<ItemAmount> list) => string.Join(", ", list.Select(c => $"{c.Count} {ItemName(c.Item)}"));

    private void UpdateTop()
    {
        long secs = W.Tick / World.TicksPerSecond;
        long demand = 0, supply = 0, cap = 0, stored = 0, storeCap = 0;
        foreach (PowerNetwork n in P.Networks.Values)
        {
            demand += n.DemandJ;
            supply += n.SuppliedJ;
            cap += n.GenCapacityJ;
            stored += n.StoredJ;
            storeCap += n.StorageCapacityJ;
        }

        static long Kw(long jPerTick) => jPerTick * World.TicksPerSecond / 1000;
        string power = P.Networks.Count == 0 ? "no grid" :
            $"{Kw(supply)}/{Kw(demand)} kW (cap {Kw(cap)} kW)" + (storeCap > 0 ? $"  battery {stored * 100 / storeCap}%" : "");
        string research = W.Research.Current is { } id
            ? $"{C.Techs[id].Name} {W.Research.UnitsDone}/{C.Techs[id].Units}"
            : "none (T)";
        int followers = P.Units.Count(u => u.IsLeader == false);
        _top.Text = $"{P.Name}   {secs / 60:00}:{secs % 60:00}   {(Root.Paused ? "PAUSED" : $"x{Root.Speed}")}" +
                    $"   |   Power: {power}   |   Research: {research}   |   Followers: {followers}" +
                    (W.FollowersSummoned > 0 ? $"  (+{W.FollowersSummoned} called)" : "");
    }

    private void UpdateHotbar()
    {
        int sig = W.Research.Unlocked.Count;
        if (sig == _unlockSig)
        {
            return;
        }

        _unlockSig = sig;
        foreach (Node? c in _hotbar.GetChildren())
        {
            c.QueueFree();
        }

        _hotbarItems.Clear();

        void Add(string label, string tooltip, Action act)
        {
            int index = _hotbarItems.Count;
            string key = index < 9 ? $"{index + 1}" : index == 9 ? "0" : "";
            _hotbarItems.Add((label, act));
            _hotbar.AddChild(Btn(key.Length > 0 ? $"{key} {label}" : label, act, tooltip));
        }

        foreach (BuildingDef? d in C.BuildingList.Where(d => d.Buildable && W.Research.Unlocked.Contains(d.Id)))
        {
            BuildingDef def = d;
            Add(def.Name, $"{def.Name}\n{def.Description}\nCost: {Costs(def.CostList)}", () => { Root.SetTool(ToolKind.Place, def.Id); });
        }

        void Train(string label, string[] preset)
        {
            if (preset.All(W.Research.Unlocked.Contains) == false)
            {
                return;
            }

            IEnumerable<ItemAmount> cost = preset.SelectMany(v => C.Vehicles[v].CostList).GroupBy(c => c.Item).Select(g => new ItemAmount(g.Key, g.Sum(x => x.Count)));
            Add(label, $"Place on track (front at cursor, R to face another way).\nCost: {Costs(cost)}", () =>
            {
                Root.TrainPreset = preset;
                Root.SetTool(ToolKind.PlaceTrain);
            });
        }

        Train("Hand Cart+1", ["hand-cart", "mine-cart"]);
        Train("Steam+3", ["steam-locomotive", "mine-cart", "mine-cart", "mine-cart"]);
        Add("Dismantle", "Click buildings, track, trains or ghosts to remove them (X)", () => Root.SetTool(ToolKind.Deconstruct));
    }

    private string ToolHint() => Root.Tool switch
    {
        ToolKind.Place when Root.PlaceDefId is { } id => $"Placing {C.Buildings[id].Name}" + (id == "rail" ? " — drag to lay track" : $" — facing {Root.PlaceRot} (R)") + "   [Q cancel]",
        ToolKind.PlaceTrain => $"Placing train facing {Root.PlaceRot} (R) — click a rail tile   [Q cancel]",
        ToolKind.Deconstruct => "Dismantle: click a building, track, train or ghost   [Q cancel]",
        ToolKind.PickHaulFrom => "Haul: click the building to take items FROM",
        ToolKind.PickHaulTo => "Haul: click the building to deliver items TO",
        ToolKind.PickTrain => "Click the train this follower should operate",
        ToolKind.PickStop => "Click rail stops to add them to the schedule   [Q when done]",
        _ => "Left-click select  •  Right-click ore/trees to mine  •  F unload pockets  •  H help",
    };

    // ------------------------------------------------------------------ followers

    private static readonly (string Label, Job? Job)[] _jobOptions =
    [
        ("Idle", Job.Idle), ("Build", new Job(JobKind.Build)),
        ("Gather Wood", new Job(JobKind.Gather, Resource: "tree")), ("Gather Iron", new Job(JobKind.Gather, Resource: "iron")),
        ("Gather Copper", new Job(JobKind.Gather, Resource: "copper")), ("Gather Coal", new Job(JobKind.Gather, Resource: "coal")),
        ("Gather Stone", new Job(JobKind.Gather, Resource: "stone")), ("Gather Lead", new Job(JobKind.Gather, Resource: "lead")),
        ("Haul…", null), ("Operate train…", null),
    ];

    private static int JobIndex(Job j) => j.Kind switch
    {
        JobKind.Build => 1,
        JobKind.Gather => j.Resource switch { "tree" => 2, "iron" => 3, "copper" => 4, "coal" => 5, "stone" => 6, "lead" => 7, _ => 0 },
        JobKind.Haul => 8,
        JobKind.OperateTrain => 9,
        _ => 0,
    };

    private void UpdateFollowers()
    {
        List<Unit> units = P.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == GameRoot.LocalPlayer)).ToList();
        if (units.Count != _followerSig)
        {
            _followerSig = units.Count;
            foreach (Node? c in _followers.GetChildren())
            {
                c.QueueFree();
            }

            _followerRows.Clear();
            foreach (Unit? u in units)
            {
                Unit unit = u;
                HBoxContainer row = new HBoxContainer();
                Label label = new Label { CustomMinimumSize = new Vector2(260, 0), ClipText = true, MouseFilter = Control.MouseFilterEnum.Stop };
                label.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true })
                    {
                        Root.SelectedId = unit.Id;
                    }
                };
                OptionButton opt = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(140, 0) };
                foreach ((string l, Job? _) in _jobOptions)
                {
                    opt.AddItem(l);
                }

                opt.ItemSelected += i => OnJobPicked(unit, (int)i);
                row.AddChild(label);
                row.AddChild(opt);
                _followers.AddChild(row);
                _followerRows.Add((unit, label, opt));
            }
        }

        foreach ((Unit? u, Label? label, OptionButton? opt) in _followerRows)
        {
            label.Text = $"{u.Name}: {u.Status}";
            label.TooltipText = label.Text;
            if ((opt.GetPopup().Visible == false) && (opt.Selected != JobIndex(u.Job)))
            {
                opt.Select(JobIndex(u.Job));
            }
        }
    }

    private void OnJobPicked(Unit u, int index)
    {
        (string? label, Job? job) = _jobOptions[index];
        Root.PickUnitId = u.Id;
        if (job is not null)
        {
            Root.Send(new AssignJob(u.Id, job));

            return;
        }

        if (index == 8)
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

    // ------------------------------------------------------------------ inspector

    private static string Inv(Inventory inv, ContentDb c) =>
        inv.IsEmpty ? "  (empty)" : string.Join("\n", inv.Items.Select(kv => $"  {c.Item(kv.Key).Name} ×{kv.Value}"));

    private void UpdateInspector()
    {
        int id = Root.SelectedId;
        StringBuilder sb = new StringBuilder();
        string title = "";
        string sig = $"{id}";
        Entity? e = W.Get<Building>(id) ?? W.Get<Unit>(id) ?? W.Get<Train>(id) ?? (Entity?)W.Get<ConstructionSite>(id);
        if (e is null)
        {
            _inspector.Visible = false;

            return;
        }

        _inspector.Visible = true;

        switch (e)
        {
            case Building b:
                {
                    title = b.Name == b.Def.Name ? b.Def.Name : $"{b.Name} ({b.Def.Name})";
                    sb.AppendLine(b.Def.Description);
                    sb.AppendLine($"Status: {b.Status}");
                    if (b.Def.Energy == EnergyKind.Electric)
                    {
                        sb.AppendLine(b.NetworkId == 0 ? "Not connected to power" : $"Power: {b.Satisfaction / 10}%");
                    }

                    if (b.Def.Kind == BuildingKind.SteamEngine)
                    {
                        sb.AppendLine($"Output: {b.LastOutputJ * World.TicksPerSecond / 1000} kW");
                    }

                    if (b.Def.Kind == BuildingKind.Battery)
                    {
                        sb.AppendLine($"Charge: {b.StoredJ / 1000} / {b.Def.CapacityKj} kJ");
                    }

                    if ((b.Recipe is { } r) && (b.Def.Kind != BuildingKind.Lab))
                    {
                        sb.AppendLine($"Recipe: {r.Name}  ({Costs(r.Inputs)} → {(r.Outputs.Length > 0 ? Costs(r.Outputs) : r.Effect)})");
                    }

                    if ((b.Def.Kind == BuildingKind.Lander) && (b.CraftQueue.Count > 0))
                    {
                        sb.AppendLine("Queue: " + string.Join(", ", b.CraftQueue.GroupBy(x => x).Select(g => $"{C.Recipes[g.Key].Name} ×{g.Count()}")));
                    }

                    if ((b.Def.Kind == BuildingKind.Drill) && (P.FindMinable(b) is { } t))
                    {
                        sb.AppendLine($"Mining {P.ResourceAt(t)!.Name} ({P.AmountAt(t)} left on tile)");
                    }

                    if (b.Def.Slots > 0)
                    {
                        sb.AppendLine(b.Def.IsStorage ? $"Contents ({b.Inv.UsedSlots}/{b.Inv.Slots} slots):" : "Input/fuel:");
                        sb.AppendLine(Inv(b.Inv, C));
                    }

                    if ((b.Def.IsStorage == false) && (b.Def.OutSlots > 0) && (b.Out.IsEmpty == false))
                    {
                        sb.AppendLine("Output:");
                        sb.AppendLine(Inv(b.Out, C));
                    }

                    bool reach = P.InLeaderReach(Root.Leader, b.Origin);
                    sig += $"|{b.RecipeId}|{reach}";
                    if (sig != _inspSig)
                    {
                        BuildBuildingActions(b, reach);
                    }

                    break;
                }
            case Unit u:
                {
                    title = u.IsLeader ? $"{u.Name} (you)" : u.Name;
                    sb.AppendLine(u.Status);
                    sb.AppendLine($"Job: {u.Job.Kind}" + (u.Job.Resource is { } res ? $" ({C.Resources[res].Name})" : ""));
                    sb.AppendLine("Carrying:");
                    sb.AppendLine(Inv(u.Carry, C));
                    if (sig != _inspSig)
                    {
                        ClearActions();
                    }

                    break;
                }
            case Train t:
                {
                    title = t.Name;
                    Vehicle? loco = t.Locomotive;
                    sb.AppendLine($"State: {WorldView.StateText(t.State)}   Speed: {t.Speed * 60 / Fixed.One} tiles/s");
                    if (loco is not null)
                    {
                        sb.AppendLine($"Load: {Planet.PulledMass(t)} / {loco.Def.MaxPull} kg pull limit");
                        if (loco.Def.Kind == VehicleKind.Steam)
                        {
                            sb.AppendLine($"Water: {t.Water * 100 / System.Math.Max(1, loco.Def.WaterCapacity)}%   Fuel: {t.FuelJ / 1000} kJ + {Inv(loco.Cargo, C).Trim()}");
                        }

                        if (loco.Def.Kind == VehicleKind.HandCart)
                        {
                            sb.AppendLine(P.HasOperator(t) ? "Operator aboard" : "Needs a follower to operate (left panel → Operate train)");
                        }
                    }

                    int ci = 0;
                    foreach (Vehicle? v in t.Vehicles.Where(v => v.Def.Kind == VehicleKind.Cart))
                    {
                        sb.AppendLine($"Cart {++ci}: " + Inv(v.Cargo, C).Trim());
                    }

                    sb.AppendLine("Schedule:");
                    if (t.Schedule.Count == 0)
                    {
                        sb.AppendLine("  (none — add stops below)");
                    }

                    sig += "|" + string.Join(",", t.Schedule.Select(s => $"{s.StopId}{s.Action}"));
                    if (sig != _inspSig)
                    {
                        BuildTrainActions(t);
                    }

                    break;
                }
            case ConstructionSite s:
                {
                    title = s.Kind == SiteKind.Deconstruct ? $"Dismantle {P.SiteLabel(s)}" : $"Construction: {P.SiteLabel(s)}";
                    List<ItemAmount> missing = s.Missing().ToList();
                    sb.AppendLine(missing.Count == 0 ? "All materials delivered" : "Still needs: " + Costs(missing));
                    sb.AppendLine($"Work: {s.WorkDone * 100 / System.Math.Max(1, s.WorkNeeded)}%");
                    Unit? builder = W.Get<Unit>(s.ClaimedBy);
                    sb.AppendLine(builder is null ? "No builder assigned (set a follower to Build)" : $"Builder: {builder.Name}");
                    if (sig != _inspSig)
                    {
                        ClearActions();
                        _inspActions.AddChild(Btn("Cancel", () => Root.Send(new CancelSite(s.Id))));
                    }

                    break;
                }
        }

        _inspSig = sig;
        _inspTitle.Text = title.ToUpperInvariant();
        _inspBody.Text = sb.ToString().TrimEnd();
    }

    private void ClearActions()
    {
        foreach (Node? c in _inspActions.GetChildren())
        {
            c.QueueFree();
        }
    }

    private void BuildBuildingActions(Building b, bool reach)
    {
        ClearActions();
        Unit leader = Root.Leader;
        if (b.Def.Kind == BuildingKind.Lander)
        {
            _inspActions.AddChild(Title("FABRICATE (slow)"));
            foreach (RecipeDef? r in C.RecipeList.Where(r => b.Def.Categories.Contains(r.Category) && W.Research.Unlocked.Contains(r.Id)))
            {
                RecipeDef recipe = r;
                HBoxContainer row = new HBoxContainer();
                row.AddChild(new Label { Text = r.Name, CustomMinimumSize = new Vector2(130, 0), TooltipText = $"{Costs(r.Inputs)} → {Costs(r.Outputs)}", MouseFilter = Control.MouseFilterEnum.Stop });
                row.AddChild(Btn("+1", () => Root.Send(new QueueCraft(b.Id, recipe.Id, 1))));
                row.AddChild(Btn("+5", () => Root.Send(new QueueCraft(b.Id, recipe.Id, 5))));
                row.AddChild(Btn("+20", () => Root.Send(new QueueCraft(b.Id, recipe.Id, 20))));
                _inspActions.AddChild(row);
            }

            _inspActions.AddChild(Btn("Clear queue", () => Root.Send(new CancelQueuedCrafts(b.Id))));
        }

        if (b.Def.Kind == BuildingKind.Assembler)
        {
            OptionButton opt = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            List<RecipeDef> recipes = C.RecipeList.Where(r => b.Def.Categories.Contains(r.Category) && W.Research.Unlocked.Contains(r.Id)).ToList();
            opt.AddItem("(choose recipe)");
            foreach (RecipeDef? r in recipes)
            {
                opt.AddItem(r.Name);
            }

            opt.Select(b.RecipeId is null ? 0 : recipes.FindIndex(r => r.Id == b.RecipeId) + 1);
            opt.ItemSelected += i => Root.Send(new SetRecipe(b.Id, i == 0 ? null : recipes[(int)i - 1].Id));
            _inspActions.AddChild(opt);
        }

        if (b.Def.Kind == BuildingKind.RailStop)
        {
            LineEdit edit = new LineEdit { Text = b.Name, PlaceholderText = "Stop name" };
            edit.TextSubmitted += txt =>
            {
                Root.Send(new RenameBuilding(b.Id, txt));
                edit.ReleaseFocus();
            };
            _inspActions.AddChild(edit);
        }

        if (reach && (b.Def.Kind is not (BuildingKind.Pole or BuildingKind.Rail)))
        {
            HBoxContainer row = new HBoxContainer();
            row.AddChild(Btn("Deposit all", () => Root.Send(new DepositToBuilding(leader.Id, b.Id)), "Put what you're carrying into this building (only what it accepts)"));
            row.AddChild(Btn("Take all", () => Root.Send(new TakeFromBuilding(leader.Id, b.Id))));
            _inspActions.AddChild(row);
        }
        else if (b.Def.Slots > 0)
        {
            _inspActions.AddChild(new Label { Text = "(walk closer to move items by hand)" });
        }

        if (b.Def.Kind != BuildingKind.Lander)
        {
            _inspActions.AddChild(Btn("Dismantle", () => Root.Send(new DeconstructEntity(b.Id))));
        }
    }

    private void BuildTrainActions(Train t)
    {
        ClearActions();
        for (int i = 0; i < t.Schedule.Count; i++)
        {
            int index = i;
            ScheduleStop stop = t.Schedule[i];
            HBoxContainer row = new HBoxContainer();
            row.AddChild(new Label { Text = $"{i + 1}. {W.Get<Building>(stop.StopId)?.Name ?? "(removed)"}", CustomMinimumSize = new Vector2(120, 0) });
            OptionButton opt = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            opt.AddItem("Load");
            opt.AddItem("Unload");
            opt.AddItem("Pass");
            opt.Select((int)stop.Action);
            opt.ItemSelected += a =>
            {
                ScheduleStop[] stops = t.Schedule.ToArray();
                stops[index] = stops[index] with
                {
                    Action = (StopAction)(int)a
                };
                Root.Send(new SetSchedule(t.Id, stops));
            };
            row.AddChild(opt);
            row.AddChild(Btn("✕", () => Root.Send(new SetSchedule(t.Id, t.Schedule.Where((_, k) => k != index).ToArray()))));
            _inspActions.AddChild(row);
        }

        _inspActions.AddChild(Btn("Add stops (click rail stops on map)", () => Root.SetTool(ToolKind.PickStop)));
        _inspActions.AddChild(Btn("Clear schedule", () => Root.Send(new SetSchedule(t.Id, []))));
        _inspActions.AddChild(Btn("Dismantle train", () => Root.Send(new DeconstructEntity(t.Id))));
    }

    // ------------------------------------------------------------------ research

    private void UpdateResearch()
    {
        if (_research.Visible == false)
        {
            return;
        }

        int sig = (W.Research.Completed.Count * 1000) + ((W.Research.Current?.GetHashCode() ?? 0) % 1000) + (W.Research.UnitsDone * 7);
        if (sig == _researchSig)
        {
            return;
        }

        _researchSig = sig;
        foreach (Node? c in _researchList.GetChildren())
        {
            c.QueueFree();
        }

        foreach (TechDef t in C.TechList)
        {
            TechDef tech = t;
            bool done = W.Research.Completed.Contains(t.Id);
            bool available = (done == false) && (t.ComingSoon == false) && t.Prereqs.All(W.Research.Completed.Contains);
            bool current = W.Research.Current == t.Id;
            HBoxContainer row = new HBoxContainer();
            int units = W.Research.Progress.GetValueOrDefault(t.Id);
            string state = done ? "✓ done" : t.ComingSoon ? "coming soon" : current ? $"researching {units}/{t.Units}" :
                available ? (units > 0 ? $"{units}/{t.Units}" : "available") : "locked: needs " + string.Join(", ", t.Prereqs.Select(p => C.Techs[p].Name));
            Label label = new Label
            {
                Text = $"{t.Name}  —  {state}\n   {t.Description}\n   {t.Units} × ({Costs(t.UnitCostList)}), {t.UnitTicks / 60}s each",
                CustomMinimumSize = new Vector2(420, 0),
            };
            if (done)
            {
                label.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.6f));
            }
            else if (available == false)
            {
                label.AddThemeColorOverride("font_color", new Color(0.55f, 0.6f, 0.7f));
            }

            row.AddChild(label);
            if (available && (current == false))
            {
                row.AddChild(Btn("Research", () => Root.Send(new SetResearch(tech.Id))));
            }

            _researchList.AddChild(row);
        }

        _researchList.AddChild(new Label { Text = "Labs need power and the unit materials (haul them in or deposit by hand)." });
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
