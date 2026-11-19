using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The guide for new players: a card on the right with the next objective and how to do it, ticked off by
/// watching the game itself (so it skips ahead on a loaded game and never asks for something already done),
/// and one-off tips that pop up the first time the player hits a common snag. G or the card's ✕ hides it;
/// the choice is remembered in <see cref="GameSettings.ShowHints"/>.
/// </summary>
public partial class Hud
{
    /// <summary>One objective: what to do, how, and how the guide tells it's been done.</summary>
    /// <remarks>
    /// <see cref="Past"/> names a later milestone that makes the step moot, so a player who got there another
    /// way (or loads a game that did) isn't sent back to, say, mine by hand.
    /// </remarks>
    private sealed record GuideStep(string Title, string How, System.Func<bool> Done, System.Func<string?>? Progress = null, System.Func<bool>? Past = null);

    /// <summary>A one-off tip, shown the first time its trigger holds and never again once seen.</summary>
    private sealed record GuideTip(string Id, string Text, System.Func<bool> Trigger);

    private const float GuideWidth = MinimapSize + 50;
    private const double TipSeconds = 30;

    private readonly List<GuideStep> _steps = [];
    private readonly List<GuideTip> _tips = [];
    private readonly HashSet<int> _stepsDone = [];
    private PanelContainer _guideCard = null!;
    private Label _guideCount = null!, _guideTitle = null!, _guideHow = null!, _guideProgress = null!, _tipText = null!;
    private VBoxContainer _tipBox = null!;
    private World? _guideWorld;
    private int _homePlanet;
    private GuideTip? _tip;
    private double _tipShownAt;
    private double _guideCheckAt;

    private void BuildGuide()
    {
        _guideCard = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        _guideCard.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.Bg, HudStyle.Orange with { A = 0.6f }, 2, 8, 6));
        _ui.AddChild(_guideCard);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        _guideCard.AddChild(v);

        HBoxContainer header = new HBoxContainer();
        header.AddChild(HudStyle.Title("Guide"));
        _guideCount = HudStyle.Caption("", HudStyle.TextDim, 11);
        _guideCount.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(new Control { CustomMinimumSize = new Vector2(6, 0) });
        header.AddChild(_guideCount);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        header.AddChild(HudStyle.Key("G"));
        Button close = HudStyle.Button("✕", () => SetHints(false), "Hide the guide (G). Turn it back on with G or from the menu (Esc).");
        close.AddThemeFontSizeOverride("font_size", 11);
        header.AddChild(close);
        v.AddChild(header);

        _guideTitle = HudStyle.Caption("", HudStyle.Orange, 12);
        _guideTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _guideTitle.CustomMinimumSize = new Vector2(GuideWidth, 0);
        v.AddChild(_guideTitle);
        _guideHow = HudStyle.Caption("", HudStyle.Text, 11);
        _guideHow.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _guideHow.CustomMinimumSize = new Vector2(GuideWidth, 0);
        v.AddChild(_guideHow);
        _guideProgress = HudStyle.Caption("", HudStyle.Header, 12);
        v.AddChild(_guideProgress);

        _tipBox = new VBoxContainer();
        _tipBox.AddThemeConstantOverride("separation", 4);
        _tipBox.AddChild(HudStyle.Divider());
        HBoxContainer tipHeader = new HBoxContainer();
        tipHeader.AddChild(HudStyle.Title("Tip"));
        tipHeader.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Button gotIt = HudStyle.Button("Got it", DismissTip);
        gotIt.AddThemeFontSizeOverride("font_size", 11);
        tipHeader.AddChild(gotIt);
        _tipBox.AddChild(tipHeader);
        _tipText = HudStyle.Caption("", HudStyle.Good, 11);
        _tipText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tipText.CustomMinimumSize = new Vector2(GuideWidth, 0);
        _tipBox.AddChild(_tipText);
        v.AddChild(_tipBox);

        DefineSteps();
        DefineTips();
    }

    /// <summary>Turns the guide on or off, remembering the choice for later games.</summary>
    /// <param name="on">Whether to show it.</param>
    public void SetHints(bool on)
    {
        GameSettings.ShowHints = on;
        Root.Notify(on ? "Guide on (G hides it)" : "Guide hidden. G or the menu (Esc) brings it back.", Colors.LightSkyBlue);
    }

    /// <summary>Shows or hides the guide (the G key).</summary>
    public void ToggleHints() => SetHints(GameSettings.ShowHints == false);

    // ------------------------------------------------------------------ the objectives

    private IEnumerable<Building> AllBuildings => W.Planets.SelectMany(p => p.Buildings);

    private IEnumerable<Unit> MyHelpers => W.Planets.SelectMany(p => p.Units).Where(u => (u.IsLeader == false) && (u.OwnerPlayer == GameRoot.LocalPlayer));

    private int BuiltOfKind(BuildingKind kind) => C.Buildings.Values.Where(d => d.Kind == kind).Sum(d => W.Stats.Built(d.Id));

    private bool Researched(string tech) => W.Research.Completed.Contains(tech);

    /// <summary>True once any research has finished: the player has the basics, wherever they learnt them.</summary>
    private bool AnyResearch() => W.Research.Completed.Count > 0;

    /// <summary>"Researching X: 12 / 30" for the current tech, or a nudge to pick one.</summary>
    private string? ResearchProgress(string goal)
    {
        if (Researched(goal))
        {
            return null;
        }

        if ((W.Research.Current is { } cur) && C.Techs.TryGetValue(cur, out TechDef? t))
        {
            return $"Researching {t.Name}: {W.Research.UnitsDone} / {W.Research.UnitsFor(t)}";
        }

        return "Nothing being researched: press T";
    }

    private void DefineSteps()
    {
        _steps.Add(new GuideStep(
            "Welcome, Commander",
            "You're a robot sent ahead of human settlers. Walk with WASD and zoom with the mouse wheel. " +
            "Your Colony Lander (the ship you landed in) holds supplies to get started. H shows every control.",
            () => (Root.Leader.MoveIntent != Int2.Zero) || (W.Stats.Count(flow: ItemFlow.HandMined, by: "commander") > 0) || (W.Tick > 2 * 60 * World.TicksPerSecond),
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Gather by hand",
            "Right-click a tree for biomass (fuel), or an ore patch. You walk over and keep mining the deposit; " +
            "right-drag to sweep across tiles. What you mine goes into your pockets (E).",
            () => W.Stats.Count(flow: ItemFlow.HandMined, by: "commander") > 0,
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Put your helper bots to work",
            "In the Helper Bots list (top left), click a bot's job button and pick a job: \"Gather Fibrous Biomass\" for fuel, " +
            "or an ore. They carry what they gather to the lander or the nearest crate.",
            () => MyHelpers.Any(u => u.Job.Kind == JobKind.Gather),
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Build a Burner Smelter",
            "Press 2 (Production) and click the Burner Smelter, then left-click to place it near the lander " +
            "(R rotates, Esc backs out). You build ghosts within reach yourself, from your pockets or the lander.",
            () => W.Stats.Built("burner-smelter") > 0,
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Smelt your first plates",
            "Click the smelter to open it beside your pockets. Drag ore in, and biomass or compacted carbon into its " +
            "fuel slot (Ctrl/Cmd-click moves everything of one item). Take the plates out the same way.",
            () => W.Stats.ByFlow(ItemFlow.Smelted).Values.Sum() > 0,
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Get power running",
            "Research Stations need electricity. Press 3 (Power): place a Steam Engine touching water and fuel it " +
            "with biomass. Power Poles link to poles up to 7 tiles away and power machines within 2 tiles.",
            () => (BuiltOfKind(BuildingKind.SteamEngine) > 0) && (BuiltOfKind(BuildingKind.Pole) > 0),
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Build a Research Station",
            "Press 4 (Science) and place a Research Station within 2 tiles of a Power Pole on the engine's network. " +
            "Hover it to check it's powered.",
            () => BuiltOfKind(BuildingKind.Lab) > 0,
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Make Survey Cores",
            "Press C for the Replicator and queue Survey Cores (a cupral bar and a drive cog each). The lander " +
            "crafts them from its stock and from your pockets while you're nearby.",
            () => W.Stats.Get(ItemFlow.Crafted, "survey-core") > 0,
            Past: AnyResearch));

        _steps.Add(new GuideStep(
            "Start researching",
            "Press T and pick Automated Fabrication. Then load the Research Station with Survey Cores: click it and " +
            "drag them in, or set a helper bot to Haul… from the lander to the station.",
            () => (W.Research.Completed.Count > 0) || W.Research.Progress.Values.Any(v => v > 0),
            () => ResearchProgress("automation")));

        _steps.Add(new GuideStep(
            "Mine automatically",
            "A Burner Drill (Production) mines the deposit under it and pushes ore out of its front (the arrow). " +
            "Point it into a smelter or a crate and keep it fuelled; R rotates before placing.",
            () => W.Stats.Built("burner-drill") > 0,
            Past: () => Researched("analysis-cores")));

        _steps.Add(new GuideStep(
            "Automate crafting",
            "Finish Automated Fabrication, then build a Fabricator (Production) by a power pole, click it and pick a " +
            "recipe. It crafts by itself while it has ingredients and power: no more waiting on the lander.",
            () => W.Stats.Built("assembler") > 0,
            () => ResearchProgress("automation"),
            Past: () => Researched("analysis-cores")));

        _steps.Add(new GuideStep(
            "Reach the Combustion Age",
            "Research Analytical Engines (it needs Automated Fabrication). It unlocks Analysis Cores " +
            "(logic board, ballast plate, charge cell) and the Combustion Age: grid power, rail and robotics.",
            () => Researched("analysis-cores"),
            () => ResearchProgress("analysis-cores")));

        _steps.Add(new GuideStep(
            "Teach a helper bot to build",
            "Research Robot Modules and craft a Construction Module. Click a helper bot, drag the module into its " +
            "slot, then set its job to Build: it builds ghosts anywhere on the planet, so you don't have to walk.",
            () => MyHelpers.Any(u => u.Modules.Contains("construction-module")),
            () => ResearchProgress("robot-modules"),
            Past: () => Researched("synthesis-cores")));

        _steps.Add(new GuideStep(
            "Fly to another planet",
            "Refine crude kerogen (from fen shale) into Kerogen Fuel, research Orbital Ferry and build a Launch Pad. " +
            "Load it with a Ferry Rocket and 20 Kerogen Fuel, pick a planet and launch. M shows the system map.",
            () => W.Planets.Any(p => (p.Id != _homePlanet) && p.Units.Any(u => u.OwnerPlayer == GameRoot.LocalPlayer)),
            () => ResearchProgress("orbital-ferry"),
            Past: () => Researched("synthesis-cores")));

        _steps.Add(new GuideStep(
            "Reach the Clean Age",
            "Research Synthesis Cores. They need photonic wafers (smelted from Dune's silica sand) and polymer resin " +
            "(refined from crude kerogen). Terraforming, clean power, belts and logistics stations follow.",
            () => Researched("synthesis-cores"),
            () => ResearchProgress("synthesis-cores"),
            Past: () => Researched("stellar-cores")));

        _steps.Add(new GuideStep(
            "Start terraforming",
            "Research Atmospheric Processing (a Clean Age tech) and build an Atmosphere Processor (6, Terraform). Feed it Atmospheric " +
            "Catalysts. The Settlers bar on the planet strip (top left) shows how close the system is; click the strip for this planet's bars.",
            () => BuiltOfKind(BuildingKind.Terraformer) > 0,
            () => ResearchProgress("atmospheric-processing"),
            Past: () => Researched("stellar-cores")));

        _steps.Add(new GuideStep(
            "Reach the Stellar Age",
            "Research Stellar Cores (via Orbital Logistics and Orbital Collection). An Orbital Collector gathers " +
            "helium-3 or osmiridium; with Cinder's titanium and hafnium they make Stellar Cores.",
            () => Researched("stellar-cores"),
            () => ResearchProgress("stellar-cores")));

        _steps.Add(new GuideStep(
            "Call the settlers",
            "Make three of the four terraformable planets ready: Climate Engines on each planet finish the job fast. " +
            "Then build a Settler Uplink and call the settlers. M shows every planet's readiness.",
            () => W.SettlersCalled,
            () => $"{W.ReadyPlanetCount} / {W.PlanetsNeeded} planets ready · {W.SystemReadiness}% overall"));
    }

    private void DefineTips()
    {
        IEnumerable<Building> Mine() => Root.Leader.Planet.Buildings;
        bool Any(MachineStatus s, System.Func<Building, bool>? filter = null) => Mine().Any(b => (b.Status == s) && (filter?.Invoke(b) ?? true));

        _tips.Add(new GuideTip("explore",
            "Most of the planet is still unexplored, and robots only work ground that has been seen. Set a helper " +
            "bot's job to Explore (left panel) and it walks an outward spiral from the lander, uncovering deposits.",
            () => (W.Tick > 3 * 60 * World.TicksPerSecond) || P.Units.Any(u => u.Status.Contains("found yet", System.StringComparison.Ordinal))));
        _tips.Add(new GuideTip("no-fuel",
            "A burner machine has stopped for fuel. Click it and drag biomass or compacted carbon into its fuel slot; " +
            "a helper bot set to Haul… can keep it topped up.",
            () => Any(MachineStatus.NoFuel)));
        _tips.Add(new GuideTip("no-water",
            "A Steam Engine has to touch water. Dismantle it (X) and place it right on the shoreline.",
            () => Any(MachineStatus.NoWater)));
        _tips.Add(new GuideTip("no-power",
            "A machine has no power. Put a Power Pole within 2 tiles of it, linked by poles (7 tiles apart at most) " +
            "to a fuelled Steam Engine. Hover a pole to see its network.",
            () => Any(MachineStatus.NoPower)));
        _tips.Add(new GuideTip("no-research",
            "The Research Station is waiting for a technology: press T and pick one.",
            () => Any(MachineStatus.NoResearch)));
        _tips.Add(new GuideTip("pockets-full",
            "Your pockets are full. Press F near the lander or a crate to unload them, or build a Supply Crate (1, Logistics).",
            () => Root.Leader.Carry.IsFull));
        _tips.Add(new GuideTip("out-of-reach",
            "You only build ghosts within reach. Walk closer, or give a helper bot a Construction Module and the Build job.",
            () => (Root.Leader.Planet == P) && (P.Sites.Count > 0) && P.Sites.All(s => Root.InReach(s.Origin) == false)));
        _tips.Add(new GuideTip("first-tech",
            "Research done! What it unlocks appears in the build bar and the Replicator (C). Pick the next technology with T.",
            () => W.Research.Completed.Count > 0));
        _tips.Add(new GuideTip("combustion",
            "Welcome to the Combustion Age. Research now costs Analysis Cores as well: a Fabricator making them saves " +
            "a lot of waiting.",
            () => Researched("analysis-cores")));
    }

    // ------------------------------------------------------------------ per frame

    private void UpdateGuide()
    {
        // A new or loaded game starts the guide over; objectives already met tick off straight away, silently.
        bool fresh = _guideWorld != W;
        if (fresh)
        {
            _guideWorld = W;
            _homePlanet = Root.Leader.Planet.Id;
            _stepsDone.Clear();
            _tip = null;
        }

        bool show = GameSettings.ShowHints && (_open is not HudWindow.Menu and not HudWindow.Victory);
        _guideCard.Visible = show;
        if ((show == false) || ((fresh == false) && (Root.Clock < _guideCheckAt)))
        {
            return;
        }

        _guideCheckAt = Root.Clock + 0.25;
        int was = Enumerable.Range(0, _steps.Count).FirstOrDefault(i => _stepsDone.Contains(i) == false, -1);
        for (int i = 0; i < _steps.Count; i++)
        {
            if ((_stepsDone.Contains(i) == false) && (_steps[i].Done() || (_steps[i].Past?.Invoke() ?? false)))
            {
                _stepsDone.Add(i);
                // Only the objective the player was working on is announced; ones met along the way tick off quietly.
                if ((fresh == false) && (i == was))
                {
                    Root.Notify($"Guide: {_steps[i].Title} ✓", HudStyle.Good);
                }
            }
        }

        int current = Enumerable.Range(0, _steps.Count).FirstOrDefault(i => _stepsDone.Contains(i) == false, -1);
        if (current < 0)
        {
            _guideCount.Text = "all done";
            _guideTitle.Text = "Settlers are on their way";
            _guideHow.Text = "You've done everything the guide knows. Keep improving the colony, or press G to hide this.";
            _guideProgress.Visible = false;
        }
        else
        {
            GuideStep step = _steps[current];
            _guideCount.Text = $"{current + 1} / {_steps.Count}";
            _guideTitle.Text = step.Title;
            _guideHow.Text = step.How;
            string? progress = step.Progress?.Invoke();
            _guideProgress.Text = progress ?? "";
            _guideProgress.Visible = progress is not null;
        }

        UpdateTip();
    }

    private void UpdateTip()
    {
        if ((_tip is not null) && (Root.Clock - _tipShownAt > TipSeconds))
        {
            DismissTip();
        }

        if (_tip is null)
        {
            _tip = _tips.FirstOrDefault(t => (GameSettings.TipSeen(t.Id) == false) && t.Trigger());
            if (_tip is not null)
            {
                _tipShownAt = Root.Clock;
                // Seen as soon as it shows, so a tip never comes back in a later game.
                GameSettings.MarkTipSeen(_tip.Id);
            }
        }

        _tipBox.Visible = _tip is not null;
        _tipText.Text = _tip?.Text ?? "";
    }

    private void DismissTip()
    {
        _tip = null;
        _tipBox.Visible = false;
        _guideCheckAt = 0;
    }

    /// <summary>Pins the guide under the status cards on the right; returns the y below it for the next panel.</summary>
    private float LayoutGuide(Vector2 vs, float gap, float y)
    {
        if (_guideCard.Visible == false)
        {
            return y;
        }

        // While the inspector shows something, the guide shrinks to its objective so both fit the column.
        bool compact = _inspector.Visible;
        _guideHow.Visible = compact == false;
        _guideProgress.Visible = compact == false;

        _guideCard.Size = Vector2.Zero;
        Vector2 m = _guideCard.GetCombinedMinimumSize();
        _guideCard.Size = m;
        _guideCard.Position = new Vector2(vs.X - gap - m.X, y);

        return y + m.Y + 8;
    }
}
