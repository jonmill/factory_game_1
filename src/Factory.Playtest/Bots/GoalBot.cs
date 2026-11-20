using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// A competent-but-simple player that pursues a <see cref="Goal"/>. It is fully state-driven: every think
/// it re-derives what to build, craft, research and what each follower should do from the current world.
/// That means it can be dropped into ANY state (e.g. after a chaos bot wrecked the base) and will try to
/// recover — which is how the softlock experiment tells "bad state" from "impossible state".
/// </summary>
public sealed partial class GoalBot : IBot
{
    /// <inheritdoc />
    public string Name => $"GoalBot({Goal})";
    /// <summary>The goal this bot is pursuing.</summary>
    public Goal Goal { get; }
    /// <summary>Strategy knobs controlling how the bot plays.</summary>
    public BotParams Params { get; }
    /// <summary>The player index the bot issues commands as.</summary>
    public int Player { get; }

    /// <summary>Last plan, for diagnostics in reports.</summary>
    public BotPlan? LastPlan { get; private set; }

    /// <summary>Creates a bot pursuing a goal.</summary>
    /// <param name="goal">The goal to pursue.</param>
    /// <param name="prm">Strategy knobs; defaults are used when <c>null</c>.</param>
    /// <param name="player">The player index to act as.</param>
    public GoalBot(Goal goal, BotParams? prm = null, int player = 0)
    {
        Goal = goal;
        Params = prm ?? new BotParams();
        Player = player;
    }

    private World _w = null!;
    private Int2 _lastLeaderPos;
    private int _stuckThinks;
    private Int2? _cutting;
    /// <summary>Recipe categories that only a dedicated machine runs (refining), wanted on the last think.</summary>
    private HashSet<string> _machineCategories = [];
    /// <summary>A resource the plan needs that only another planet has, from the last think (it sends an expedition).</summary>
    private (string Resource, int Planet)? _offPlanet;
    /// <summary>Other resources the plan lacks that the same planet has: one trip fetches them all.</summary>
    private List<string> _offPlanetAlso = [];
    /// <summary>The extra resources the expedition under way is fetching besides its main one.</summary>
    private List<string> _expeditionResources = [];
    /// <summary>The expedition under way: what is being fetched from which planet, how much, and since when.</summary>
    private (string Resource, int Planet, int Amount, long Since)? _expedition;
    private const int ExpeditionAmount = 600;
    /// <summary>Whether this expedition's burner drills have been placed on the ore.</summary>
    private bool _expeditionDrillsPlaced;
    private const int ExpeditionTicks = 25 * 3600;
    private Planet P => Focus ?? _w.Planets[0];
    private ContentDb C => _w.Content;
    private void Send(Command c) => _w.Submit(c with { Player = Player, PlanetId = P.Id });

    /// <inheritdoc />
    public void Tick(World world)
    {
        if (world.Tick % Params.ThinkInterval != 0)
        {
            return;
        }

        _w = world;
        if (Goal.IsMet(world))
        {
            return;
        }

        ManageStellar();
        Building? lander = world.Get<Building>(P.LanderId);
        if ((lander is null) || Away())
        {
            return;
        }

        BotPlan plan = new BotPlan();
        LastPlan = plan;

        ChooseResearch(plan);
        CleanUp(plan);
        PlanStructures(plan, lander);
        ComputeDemand(plan);
        PlanFactory(plan, lander);
        QueueCrafting(plan, lander);
        AssignFollowers(plan, lander);
        ManageCommander(plan, lander);
    }

    // ------------------------------------------------------------------ commander

    /// <summary>
    /// The bot plays from the start planet. If the commander is on another planet it is either on an
    /// expedition (mine the resource into the ferry's hold, with the helper bots, then fly home) or was
    /// left there by someone else (walk to the landed ferry and fly home, which brings the helper bots
    /// too). True while the commander is away.
    /// </summary>
    private bool Away()
    {
        Unit? away = _w.Planets.Where(p => p != P).SelectMany(p => p.Units).FirstOrDefault(u => u.IsLeader && (u.OwnerPlayer == Player));
        if (away is null)
        {
            _expedition = null;

            return false;
        }

        Planet there = away.Planet;
        Building? ferry = there.Buildings.Where(b => b.Def.Kind == BuildingKind.Ferry)
            .OrderBy(b => there.SubTorus.DistSq(away.Pos, there.SubTorus.Wrap(b.CenterSub))).ThenBy(b => b.Id).FirstOrDefault();
        if (ferry is null)
        {
            return true; // nothing to fly home in; nothing to do
        }

        bool nearFerry = there.InLeaderReach(away, ferry.Origin);
        if ((_outpostTrip is { } trip) && (trip.Planet == there.Id))
        {
            return OutpostWork(away, there, ferry, nearFerry);
        }

        if ((_expedition is { } ex) && (ex.Planet == there.Id) && there.HasResource(ex.Resource))
        {
            ResourceDef res = C.Resources[ex.Resource];
            List<ResourceDef> fetching = _expeditionResources.Where(there.HasResource).Select(r => C.Resources[r]).Prepend(res).Distinct().ToList();
            bool AllIn() => fetching.All(r => ferry.Inv.Count(r.Item) + away.Carry.Count(r.Item) >= ex.Amount);
            bool done = AllIn() || (ferry.Inv.SpaceFor(res.Item) == 0) || (_w.Tick - ex.Since > ExpeditionTicks);
            if (done == false)
            {
                ExpeditionCrew(there, ferry, fetching);

                if (away.Carry.SpaceFor(res.Item) > 0)
                {
                    if ((away.MiningTile is null) && (there.FindNearestResource(away.Tile, res) is { } tile))
                    {
                        Send(new MineTile(away.Id, tile));
                    }
                    else if ((away.MiningTile is null) && (there.FindNearestResource(away.Tile, res) is null))
                    {
                        // Not found yet: go and look for it.
                        Scout(away, there, Placement.Center(ferry));
                    }

                    return true;
                }
            }

            // Pockets full, or the hold has enough: unload into the ferry (if it has room; else fly home with full pockets).
            if ((away.Carry.Count(res.Item) > 0) && (ferry.Inv.SpaceFor(res.Item) > 0))
            {
                if (away.MiningTile is not null)
                {
                    Send(new MineTile(away.Id, null));
                }

                if (nearFerry)
                {
                    Send(new DepositToBuilding(away.Id, ferry.Id, res.Item));
                }
                else
                {
                    Send(new WalkTo(away.Id, ferry.Origin + new Int2(-1, 1)));
                }

                return true;
            }

            if (done == false)
            {
                return true;
            }
        }

        // Take the drills back aboard first, so they don't sit on ore an outpost will want.
        if ((_expedition is { } fetched) && (fetched.Planet == there.Id) && ExpeditionPackUp(there))
        {
            return true;
        }

        Send(nearFerry ? new ReturnFerry(away.Id, ferry.Id) : new WalkTo(away.Id, ferry.Origin + new Int2(-1, 1)));

        return true;
    }

    /// <summary>
    /// Gets an expedition off the ground: carry the ferry rocket and its fuel from the lander to the launch
    /// pad, empty the commander's pockets, then launch with every helper bot. True while there's work.
    /// </summary>
    private bool PrepareExpedition(Unit leader, Building lander, Dictionary<string, int>? kit = null)
    {
        if ((_offPlanet is not { } want) || (P.Buildings.Where(b => b.Def.Kind == BuildingKind.LaunchPad).OrderBy(b => b.Id).FirstOrDefault() is not { } pad))
        {
            return false;
        }

        List<ItemAmount> load = pad.Def.LaunchFuelList.Prepend(new ItemAmount(pad.Def.Vessel!, 1)).Concat((kit ?? []).Select(kv => new ItemAmount(kv.Key, kv.Value)))
            .GroupBy(i => i.Item).Select(g => new ItemAmount(g.Key, g.Sum(i => i.Count) - pad.Inv.Count(g.Key))).Where(i => i.Count > 0).ToList();
        bool nearPad = P.InLeaderReach(leader, pad.Origin);
        bool nearLander = P.InLeaderReach(leader, lander.Origin);
        Int2 besidePad = P.Torus.Wrap(pad.Origin + new Int2(-1, 1));
        Int2 home = P.Torus.Wrap(lander.Origin + new Int2(1, lander.H + 1));
        if (load.Count > 0)
        {
            if (load.Any(i => leader.Carry.Count(i.Item) > 0))
            {
                if (nearPad)
                {
                    foreach (ItemAmount i in load.Where(i => leader.Carry.Count(i.Item) > 0))
                    {
                        Send(new DepositToBuilding(leader.Id, pad.Id, i.Item, i.Count));
                    }
                }
                else
                {
                    WalkTowards(leader, besidePad);
                }

                return true;
            }

            if (load.All(i => lander.Inv.Count(i.Item) >= i.Count) == false)
            {
                return false; // still being made
            }

            if (nearLander)
            {
                foreach (ItemAmount i in load)
                {
                    Send(new TakeFromBuilding(leader.Id, lander.Id, i.Item, i.Count));
                }
            }
            else
            {
                WalkTowards(leader, home);
            }

            return true;
        }

        // Loaded. Travel light: leave everything in the lander, so the pockets have room for the haul.
        if (leader.Carry.IsEmpty == false)
        {
            if (nearLander)
            {
                Send(new DepositToBuilding(leader.Id, lander.Id));
            }
            else
            {
                WalkTowards(leader, home);
            }

            return true;
        }

        if (nearPad == false)
        {
            WalkTowards(leader, besidePad);

            return true;
        }

        int[] crew = P.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == Player)).OrderBy(u => u.Id).Select(u => u.Id).ToArray();
        Send(new LaunchFerry(leader.Id, pad.Id, want.Planet, crew));
        _expedition = (want.Resource, want.Planet, ExpeditionAmount, _w.Tick);
        _expeditionDrillsPlaced = false;
        _expeditionKit = null;
        _expeditionDrillCount = null;
        _expeditionResources = _offPlanetAlso;

        return true;
    }

    private Unit? Commander() => P.Units.FirstOrDefault(u => u.IsLeader && (u.OwnerPlayer == Player));

    /// <summary>Helper bots that can't take a job yet but have a free slot for its module.</summary>
    private List<Unit> HelpersLacking(JobKind kind) => P.Units
        .Where(u => (u.IsLeader == false) && (u.OwnerPlayer == Player) && (P.CanTakeJob(u, kind) == false) && (u.Modules.Count < P.ModuleSlots(u)))
        .OrderBy(u => u.Id).ToList();

    /// <summary>
    /// What a player does with the commander: fit helper bots with construction modules, set new robot bays
    /// to assemble helpers, and, while no helper can build, carry materials out to ghosts and build them
    /// itself; otherwise wait by the lander.
    /// </summary>
    private void ManageCommander(BotPlan plan, Building lander)
    {
        if (Commander() is not { } leader)
        {
            return;
        }

        foreach (Building bay in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.RobotBay) && (b.RecipeId is null)))
        {
            if (_w.Research.Unlocked.Contains(HelpersGoal.Recipe))
            {
                Send(new SetRecipe(bay.Id, HelpersGoal.Recipe));
            }
        }

        bool nearLander = P.InLeaderReach(leader, lander.Origin);
        Int2 home = P.Torus.Wrap(lander.Origin + new Int2(1, lander.H + 1));
        if (P.ModuleForJob(JobKind.Build) is { } module)
        {
            List<Unit> lacking = HelpersLacking(JobKind.Build);
            if ((leader.Carry.Count(module.Id) > 0) && (lacking.FirstOrDefault(h => P.InLeaderReach(leader, h.Tile)) is { } helper))
            {
                Send(new InstallModule(leader.Id, helper.Id, module.Id));

                return;
            }

            int want = lacking.Count - leader.Carry.Count(module.Id);
            if ((want > 0) && nearLander && (lander.Inv.Count(module.Id) > 0))
            {
                Send(new TakeFromBuilding(leader.Id, lander.Id, module.Id, want));

                return;
            }
        }

        // A smelter stuck on a leftover ore that isn't wanted (one regolith when bricks need two) blocks
        // every other ore: take the leftover out by hand.
        HashSet<string> wantedOres = plan.SmeltDeficit.Keys.Select(it => WorldProbe.RecipeFor(C, it)!.Inputs[0].Item).ToHashSet();
        Building? clogged = P.Buildings
            .Where(b => (b.Def.Kind == BuildingKind.Furnace) && (b.Crafting == false) && (DrillFedFurnaces().Contains(b.Id) == false))
            .Where(b => b.Inv.Items.Any(kv => (C.IsFuel(kv.Key) == false) && (wantedOres.Contains(kv.Key) == false)))
            .OrderBy(b => P.SubTorus.DistSq(leader.Pos, b.CenterSub)).ThenBy(b => b.Id).FirstOrDefault();
        if ((clogged is not null) && (wantedOres.Count > 0))
        {
            if (P.InLeaderReach(leader, clogged.Origin))
            {
                string ore = clogged.Inv.Items.First(kv => (C.IsFuel(kv.Key) == false) && (wantedOres.Contains(kv.Key) == false)).Key;
                Send(new TakeFromBuilding(leader.Id, clogged.Id, ore, int.MaxValue, InventoryPart.Input));
            }
            else
            {
                WalkTowards(leader, P.Torus.Wrap(clogged.Origin + new Int2(0, clogged.H)));
            }

            return;
        }

        bool helpersBuild = P.Units.Any(u => (u.IsLeader == false) && (u.OwnerPlayer == Player) && P.CanTakeJob(u, JobKind.Build));
        IEnumerable<ConstructionSite> sites = helpersBuild ? [] : P.Sites.OrderBy(s => P.SubTorus.DistSq(lander.CenterSub, s.CenterSub)).ThenBy(s => s.Id);
        foreach (ConstructionSite site in sites)
        {
            List<ItemAmount> missing = site.Missing().Select(m => new ItemAmount(m.Item, m.Count - leader.Carry.Count(m.Item))).Where(m => m.Count > 0).ToList();
            if (missing.Count > 0)
            {
                // Fetch from the lander and crates: enough must be in storage altogether, possibly split.
                List<Building> stores = P.Buildings.Where(b => b.Def.IsDepot)
                    .OrderBy(b => b.Id == lander.Id ? 0 : 1).ThenBy(b => P.SubTorus.DistSq(leader.Pos, b.CenterSub)).ThenBy(b => b.Id).ToList();
                if (missing.Any(m => stores.Sum(st => st.Inv.Count(m.Item)) < m.Count))
                {
                    continue; // not all in storage yet; try another ghost
                }

                // Pockets stuffed with things this ghost doesn't need: unload them into storage with room first.
                if (missing.Any(m => leader.Carry.SpaceFor(m.Item) == 0))
                {
                    HashSet<string> needed = missing.Select(m => m.Item).ToHashSet();
                    Building? dump = stores.FirstOrDefault(st => leader.Carry.Items.Any(kv => (needed.Contains(kv.Key) == false) && (st.Inv.SpaceFor(kv.Key) > 0)));
                    if (dump is null)
                    {
                        plan.Problems.Add("pockets and storage are full");
                    }
                    else if (P.InLeaderReach(leader, dump.Origin))
                    {
                        foreach ((string item, int _) in leader.Carry.Items.Where(kv => needed.Contains(kv.Key) == false).ToList())
                        {
                            Send(new DepositToBuilding(leader.Id, dump.Id, item));
                        }
                    }
                    else
                    {
                        WalkTowards(leader, dump.Id == lander.Id ? home : P.Torus.Wrap(dump.Origin + new Int2(0, dump.H)));
                    }

                    return;
                }

                Building source = stores.First(st => missing.Any(m => st.Inv.Count(m.Item) > 0));
                if (P.InLeaderReach(leader, source.Origin))
                {
                    foreach (ItemAmount m in missing.Where(m => source.Inv.Count(m.Item) > 0))
                    {
                        Send(new TakeFromBuilding(leader.Id, source.Id, m.Item, System.Math.Min(m.Count, source.Inv.Count(m.Item))));
                    }
                }
                else
                {
                    WalkTowards(leader, source.Id == lander.Id ? home : P.Torus.Wrap(source.Origin + new Int2(0, source.H)));
                }

                return;
            }

            if (P.SubTorus.DistSq(leader.Pos, site.CenterSub) > 5L * 5 * Fixed.One * Fixed.One)
            {
                WalkTowards(leader, P.Torus.Wrap(Fixed.ToTile(site.CenterSub) + new Int2(0, (site.H / 2) + 1)));
            }

            return;
        }

        if (((Params.Automate == false) && TendMachines(plan, leader, lander)) || ClearJammedColumn(leader) || PrepareOutpost(leader, lander) || PrepareExpedition(leader, lander, ExpeditionKit(lander)))
        {
            return;
        }

        if (BootstrapMining(plan, leader))
        {
            return;
        }

        if (nearLander == false)
        {
            WalkTowards(leader, home);
        }
        else if (leader.Carry.IsEmpty == false)
        {
            // Unload at the lander, or the nearest crate with room once the lander is full.
            Building? store = P.Buildings.Where(b => b.Def.IsDepot && leader.Carry.Items.Any(kv => b.Inv.SpaceFor(kv.Key) > 0))
                .OrderBy(b => b.Id == lander.Id ? 0 : 1).ThenBy(b => P.SubTorus.DistSq(leader.Pos, b.CenterSub)).ThenBy(b => b.Id).FirstOrDefault();
            if (store is null)
            {
                plan.Problems.Add("nowhere to unload the commander's pockets");
            }
            else if (P.InLeaderReach(leader, store.Origin))
            {
                Send(new DepositToBuilding(leader.Id, store.Id));
            }
            else
            {
                WalkTowards(leader, P.Torus.Wrap(store.Origin + new Int2(0, store.H)));
            }
        }
    }

    /// <summary>
    /// The lander starts empty: until the first research is done the commander mines by hand alongside the
    /// helper bots, as a player does, fuel first while storage has little. Returns false (and stops mining)
    /// once the pockets are full, so the caller unloads them.
    /// </summary>
    private const int BootstrapLoad = 40;

    private bool BootstrapMining(BotPlan plan, Unit leader)
    {
        if (_w.Research.Completed.Count > 0)
        {
            return false;
        }

        // A load at a time: carry it home (the caller unloads it) rather than filling every pocket slot.
        if (leader.Carry.IsFull || (leader.Carry.Total >= BootstrapLoad))
        {
            if (leader.MiningTile is not null)
            {
                Send(new MineTile(leader.Id, null));
            }

            return false;
        }

        if (leader.MiningTile is not null)
        {
            return true; // still at it
        }

        ResourceDef fuel = C.Resources[FuelResource()];
        long fuelStored = C.ItemList.Where(i => i.FuelKj > 0).Sum(i => WorldProbe.StorageCount(P, i.Id));
        ResourceDef? res = fuelStored < Params.MinFuelStock ? fuel
            : plan.RawDeficit.Where(kv => P.HasResource(kv.Key)).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => C.Resources[kv.Key]).FirstOrDefault();
        if ((res is null) || (P.FindNearestResource(leader.Tile, res) is not { } tile))
        {
            return false;
        }

        Send(new MineTile(leader.Id, tile));

        return true;
    }

    private void WalkTowards(Unit leader, Int2 tile)
    {
        // Hemmed in by trees (chaos can leave the commander deep in a forest): cut a way out, as a player would.
        _stuckThinks = leader.Pos == _lastLeaderPos ? _stuckThinks + 1 : 0;
        _lastLeaderPos = leader.Pos;
        if (leader.MiningTile is { } cutting)
        {
            if ((cutting == _cutting) && (leader.Status.StartsWith("Pockets full") == false))
            {
                return; // still cutting
            }

            // That tree is down (mining moved on to the next one), or there's no room for the wood: stop.
            _cutting = null;
            Send(new MineTile(leader.Id, null));

            return;
        }

        if ((_stuckThinks >= 3) && (TreeTowards(leader, tile) is { } tree))
        {
            _stuckThinks = 0;
            _cutting = tree;
            Send(new MineTile(leader.Id, tree));

            return;
        }

        if (leader.WalkTarget != tile)
        {
            Send(new WalkTo(leader.Id, tile));
        }
    }

    /// <summary>The tree within the commander's mining reach that lies most in the direction of <paramref name="goal"/>.</summary>
    private Int2? TreeTowards(Unit leader, Int2 goal)
    {
        Int2 at = Fixed.ToTile(leader.Pos);
        Int2? best = null;
        long bestDist = long.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Int2 t = P.Torus.Wrap(at + new Int2(dx, dy));
                long d = P.SubTorus.DistSq(Fixed.TileCenter(t), Fixed.TileCenter(goal));
                bool inReach = P.SubTorus.DistSq(leader.Pos, Fixed.TileCenter(t)) <= (long)Planet.LeaderMineReachTiles * Fixed.One * Planet.LeaderMineReachTiles * Fixed.One;
                if (inReach && (P.KnownResourceAt(t) is { IsTree: true }) && (d < bestDist))
                {
                    best = t;
                    bestDist = d;
                }
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ research

    private List<string> TechPath(string target)
    {
        List<string> order = new List<string>();
        void Visit(string id)
        {
            if (order.Contains(id) || _w.Research.Completed.Contains(id))
            {
                return;
            }

            foreach (string p in C.Techs[id].Prereqs)
            {
                Visit(p);
            }

            order.Add(id);
        }

        Visit(target);

        return order;
    }

    private string? TechUnlocking(string id) =>
        _w.Research.Unlocked.Contains(id) ? null : C.TechList.FirstOrDefault(t => t.Unlocks.Contains(id))?.Id;

    private List<string> RequiredTechs()
    {
        switch (Goal)
        {
            case TechGoal t:
                return TechPath(t.TechId);
            case HelpersGoal:
                {
                    BuildingDef bay = C.BuildingList.First(b => b.Kind == BuildingKind.RobotBay);
                    List<string> path = new List<string>();
                    foreach (string? id in new[] { bay.Id, HelpersGoal.Recipe })
                    {
                        if (TechUnlocking(id) is { } tech)
                        {
                            path.AddRange(TechPath(tech).Where(x => path.Contains(x) == false));
                        }
                    }

                    return path;
                }
            case SettlersGoal:
                {
                    List<string> path = new List<string>();
                    if (Params.Automate)
                    {
                        foreach (string tech in _factoryPath.Where(C.Techs.ContainsKey))
                        {
                            path.AddRange(TechPath(tech).Where(x => path.Contains(x) == false));
                        }
                    }

                    foreach (BuildingDef def in TerraformBuildings())
                    {
                        if (TechUnlocking(def.Id) is { } tech)
                        {
                            path.AddRange(TechPath(tech).Where(x => path.Contains(x) == false));
                        }
                    }

                    return path;
                }
            case ItemGoal ig:
                {
                    RecipeDef? r = WorldProbe.RecipeFor(C, ig.Item);

                    return (r is not null) && (TechUnlocking(r.Id) is { } tech) ? TechPath(tech) : [];
                }
            default:
                return [];
        }
    }

    private void ChooseResearch(BotPlan plan)
    {
        plan.TechPath = RequiredTechs();
        string? next = plan.TechPath.FirstOrDefault(t => C.Techs[t].Prereqs.All(_w.Research.Completed.Contains) && (C.Techs[t].ComingSoon == false));
        // As in DSP: while the next tech waits on a core the base can't supply yet (or the path is done),
        // the stations run endless research with the cores in stock instead of standing idle.
        if (((next is null) || (CoresInStock(C.Techs[next]) == false)) && (EndlessResearch() is { } endless))
        {
            next = endless;
        }

        if ((next is not null) && (_w.Research.Current != next))
        {
            Send(new SetResearch(next));
        }
    }

    /// <summary>True when storage holds at least one of each core a tech costs per station, enough to keep them all going.</summary>
    private bool CoresInStock(TechDef t)
    {
        int stations = System.Math.Max(1, CountOf(BuildingKind.Lab));

        return t.UnitCostList.All(c => WorldProbe.StorageCount(P, c.Item) + P.Buildings.Where(b => b.Def.Kind == BuildingKind.Lab).Sum(b => b.Inv.Count(c.Item)) >= c.Count * stations);
    }

    /// <summary>
    /// The endless tech worth researching now, with the cores in stock: Terraforming Efficiency once
    /// terraforming has started, then whichever of Research Speed and Robot Efficiency is lower.
    /// </summary>
    private string? EndlessResearch()
    {
        bool terraforming = P.Buildings.Any(b => b.Def.Kind is BuildingKind.Terraformer or BuildingKind.PayloadLauncher);
        return new[] { Planet.TerraformingEfficiency, Planet.ResearchSpeed, Planet.RobotEfficiency }
            .Where(id => C.Techs.TryGetValue(id, out TechDef? t) && t.Prereqs.All(_w.Research.Completed.Contains) && CoresInStock(t))
            .OrderBy(id => (id == Planet.TerraformingEfficiency) && terraforming ? 0 : 1)
            .ThenBy(id => _w.Research.Level(id)).ThenBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    // ------------------------------------------------------------------ recovery / cleanup

    private static readonly HashSet<BuildingKind> _wanted =
        [BuildingKind.Lab, BuildingKind.Furnace, BuildingKind.SteamEngine, BuildingKind.Pole, BuildingKind.Chest, BuildingKind.RobotBay, BuildingKind.Terraformer, BuildingKind.Uplink,
            BuildingKind.LaunchPad, BuildingKind.PayloadLauncher, BuildingKind.Drill, BuildingKind.Sorter, BuildingKind.Assembler, BuildingKind.OrbitalStation, BuildingKind.SolarPanel];

    /// <summary>Kinds the factory builds several of at once (columns and cells): duplicate orders are fine.</summary>
    private static readonly HashSet<BuildingKind> _many = [BuildingKind.Pole, BuildingKind.Drill, BuildingKind.Furnace, BuildingKind.Sorter, BuildingKind.Chest, BuildingKind.Assembler, BuildingKind.SteamEngine];

    /// <summary>
    /// The terraformers and the uplink, the buildings the settlers goal needs. Only the first terraformers,
    /// each with its own fixed recipe: the late climate engine is a speed-up, not part of the route.
    /// </summary>
    private IEnumerable<BuildingDef> TerraformBuildings() => C.BuildingList.Where(b =>
        ((b.Kind == BuildingKind.Terraformer) && (b.Recipe is not null)) || (b.Kind == BuildingKind.Uplink) || ((b.Kind == BuildingKind.PayloadLauncher) && RemoteTargets().Any()));

    /// <summary>
    /// The other planets the bot terraforms from here with a payload launcher: as many as the settlers need
    /// besides the home planet, picking those already closest to ready.
    /// </summary>
    private IEnumerable<Planet> RemoteTargets() => _w.TerraformablePlanets.Where(p => p != Home)
        .OrderByDescending(p => p.Readiness).ThenBy(p => p.Id).Take(System.Math.Max(0, _w.PlanetsNeeded - 1));

    /// <summary>Terraforming points a stat still needs across the remote targets.</summary>
    private int RemotePointsLeft(string stat)
    {
        int target = C.Terraform.Stats.First(s => s.Id == stat).Target;

        return RemoteTargets().Sum(p => System.Math.Max(0, target - p.Stats.GetValueOrDefault(stat)));
    }

    /// <summary>The researched payload recipes, each with the stat it raises.</summary>
    private IEnumerable<(RecipeDef Recipe, string Stat, int Points)> Payloads(BuildingDef launcher) =>
        C.RecipeList.Where(r => launcher.Categories.Contains(r.Category) && _w.Research.Unlocked.Contains(r.Id))
            .Select(r => (r, Planet.PayloadEffect(r.Effect)))
            .Where(x => x.Item2 is not null).Select(x => (x.r, x.Item2!.Value.Stat, x.Item2!.Value.Points));

    /// <summary>
    /// How many of a terraforming building a player puts up: the settler uplink once; otherwise several,
    /// since terraforming goes as fast as the consumables can be made and fed in.
    /// </summary>
    private static int TerraformerTarget(BuildingDef def) => def.Kind switch
    {
        BuildingKind.Uplink => 1,
        BuildingKind.PayloadLauncher => System.Math.Min(2, def.MaxPerPlanet > 0 ? def.MaxPerPlanet : 2),
        _ => def.MaxPerPlanet > 0 ? System.Math.Min(3, def.MaxPerPlanet) : 3,
    };

    /// <summary>Points a terraformer still has to add before its stat is complete; 0 for the uplink.</summary>
    private int PointsLeft(BuildingDef def)
    {
        if (def.Kind == BuildingKind.PayloadLauncher)
        {
            return C.Terraform.Stats.Sum(s => RemotePointsLeft(s.Id));
        }

        if ((def.Recipe is null) || (Planet.TerraformEffect(C.Recipes[def.Recipe].Effect) is not { } t))
        {
            return 0;
        }

        int target = C.Terraform.Stats.First(s => s.Id == t.Stat).Target;

        return System.Math.Max(0, target - P.Stats.GetValueOrDefault(t.Stat));
    }

    /// <summary>
    /// What a sensible player does when inheriting a messy base: cancel ghosts it doesn't want (a few per
    /// think) and rescind dismantle orders on buildings it relies on. This is what lets the bot recover
    /// from chaos instead of drowning its builders in junk orders.
    /// </summary>
    /// <summary>Buildings the bot keeps: its usual kinds, plus machines for the recipe categories it needs.</summary>
    private bool Wanted(BuildingDef d) => _wanted.Contains(d.Kind) || d.Categories.Any(_machineCategories.Contains);

    private void CleanUp(BotPlan plan)
    {
        int cancelled = 0;
        foreach (ConstructionSite? site in P.Sites.ToList())
        {
            if (cancelled >= 5)
            {
                break;
            }

            bool junk = site.Kind switch
            {
                SiteKind.Build => (C.Buildings.TryGetValue(site.DefId, out BuildingDef? d) == false) || (Wanted(d) == false)
                                  || ((_many.Contains(d.Kind) == false) && (P.Sites.Count(x => x.DefId == site.DefId) > 1) && (site.Id != P.Sites.First(x => x.DefId == site.DefId).Id)),
                SiteKind.BuildTrain => true,
                SiteKind.Deconstruct => (_w.Get<Building>(site.TargetId) is { } b) && Wanted(b.Def) && (_retiring.Contains(b.Id) == false),
                _ => false,
            };
            if (junk == false)
            {
                continue;
            }

            Send(new CancelSite(site.Id));
            cancelled++;
        }

        if (cancelled > 0)
        {
            plan.Problems.Add($"cleaning up {cancelled} unwanted orders");
        }
    }

    // ------------------------------------------------------------------ structures

    private int CountOf(BuildingKind kind, string? id = null) =>
        P.Buildings.Count(b => (b.Def.Kind == kind) && ((id is null) || (b.Def.Id == id))) +
        P.Sites.Count(s => (s.Kind == SiteKind.Build) && C.Buildings.TryGetValue(s.DefId, out BuildingDef? d) && (d.Kind == kind) && ((id is null) || (d.Id == id)));

    private bool HasSite(string defId) => P.Sites.Any(s => (s.Kind == SiteKind.Build) && (s.DefId == defId));

    private bool PlaceNear(BotPlan plan, string defId, Int2 near, int minR, int maxR, Dir rot = Dir.North)
    {
        BuildingDef def = C.Buildings[defId];
        if (_w.Research.Unlocked.Contains(defId) == false)
        {
            return false;
        }

        Int2? spot = Placement.FindSpot(P, def, rot, near, minR, maxR);
        if (spot is null)
        {
            plan.Problems.Add($"no room for {def.Name}");

            return false;
        }

        Send(new PlaceBuilding(defId, spot.Value, rot));
        plan.Placed.Add(defId);
        plan.PlannedCosts.AddRange(def.CostList);

        return true;
    }

    private void PlanStructures(BotPlan plan, Building lander)
    {
        Int2 center = Placement.Center(lander);
        bool needLab = plan.TechPath.Count > 0;
        BuildingDef bayDef = C.BuildingList.First(b => b.Kind == BuildingKind.RobotBay);
        bool needBay = (Goal is HelpersGoal) && _w.Research.Unlocked.Contains(bayDef.Id);

        if (needLab && (CountOf(BuildingKind.Lab) < LabTarget()))
        {
            PlaceNear(plan, "lab", center, 4, 30);
        }

        if (needBay && (CountOf(BuildingKind.RobotBay) == 0))
        {
            PlaceNear(plan, bayDef.Id, center, 5, 30);
        }

        if (Goal is SettlersGoal)
        {
            foreach (BuildingDef def in TerraformBuildings())
            {
                bool needed = (def.Kind == BuildingKind.Uplink) || (PointsLeft(def) > 0);
                if (needed && _w.Research.Unlocked.Contains(def.Id) && (CountOf(def.Kind, def.Id) < TerraformerTarget(def)))
                {
                    PlaceNear(plan, def.Id, center, 5, 40);
                }
            }
        }

        if (((_offPlanet is not null) || (NextOutpost() is not null)) && (CountOf(BuildingKind.LaunchPad) == 0))
        {
            PlaceNear(plan, C.BuildingList.First(b => b.Kind == BuildingKind.LaunchPad).Id, center, 5, 18);
        }

        foreach (Building launcher in P.Buildings.Where(b => b.Def.Kind == BuildingKind.PayloadLauncher))
        {
            Planet? aim = RemoteTargets().Where(t => t.IsReady == false).OrderBy(t => t.Id).FirstOrDefault();
            if ((aim is not null) && (launcher.TargetPlanetId != aim.Id))
            {
                Send(new SetLaunchTarget(launcher.Id, aim.Id));
            }
        }

        foreach (string category in _machineCategories.Order(StringComparer.Ordinal))
        {
            if ((MachineFor(category) is { } def) && (CountOf(def.Kind, def.Id) == 0))
            {
                PlaceNear(plan, def.Id, center, 4, 30);
            }
        }

        if (CountOf(BuildingKind.Furnace) < Params.Furnaces)
        {
            PlaceNear(plan, "burner-smelter", center, 3, 10);
        }

        // Storage pressure → add a crate (also when the lander is stuffed and no crate has room).
        List<Building> storages = WorldProbe.Storages(P).ToList();
        int used = storages.Sum(s => s.Inv.UsedSlots), slots = storages.Sum(s => s.Inv.Slots);
        bool noCrateRoom = LanderCongested(lander) && storages.All(st => (st.Id == lander.Id) || (st.Inv.UsedSlots + 4 > st.Inv.Slots));
        if ((slots > 0) && ((used * 100 / slots > 85) || noCrateRoom) && (HasSite("chest") == false))
        {
            PlaceNear(plan, "chest", center, 3, 28);
        }

        List<Building> consumers = P.Buildings.Where(b => b.Def.Energy == EnergyKind.Electric).ToList();
        if ((consumers.Count > 0) || needLab)
        {
            EnsurePower(plan, center, consumers);
        }
    }

    /// <summary>True when the lander is nearly out of slots.</summary>
    private static bool LanderCongested(Building lander) => lander.Inv.Slots - lander.Inv.UsedSlots < 6;

    private void EnsurePower(BotPlan plan, Int2 center, List<Building> consumers)
    {
        if (CountOf(BuildingKind.SteamEngine) == 0)
        {
            if (PlaceNear(plan, "steam-engine", center, 2, 45) == false)
            {
                plan.Problems.Add("no water reachable for a steam engine");
            }

            return;
        }

        if (P.Sites.Any(st => C.Buildings.TryGetValue(st.DefId, out BuildingDef? d) && (d.Kind == BuildingKind.Pole)) || HasSite("steam-engine"))
        {
            return; // wait for builders
        }

        Building? engine = P.Buildings.FirstOrDefault(b => b.Def.Kind == BuildingKind.SteamEngine);
        if (engine is null)
        {
            return;
        }

        foreach (Building c in consumers)
        {
            bool live = (c.NetworkId != 0) && P.Networks.TryGetValue(c.NetworkId, out PowerNetwork? n) && (n.Generators.Count > 0);
            if (live)
            {
                continue;
            }

            if (PlacePoleChain(plan, engine, c))
            {
                return; // one chain per think
            }

            // This one can't be reached: try the next rather than leave them all unpowered.
            plan.Problems.Add($"couldn't route power to {c.Def.Name}");
        }
    }

    private bool PlacePoleChain(BotPlan plan, Building engine, Building target)
    {
        BuildingDef pole = C.Buildings[PoleId()];
        int reach = pole.WireReach - 1, supply = pole.SupplyRadius;
        Int2 targetCenter = Placement.Center(target);

        // Start from an existing pole on the engine's network, else a new pole next to the engine.
        Int2? start = P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Pole) && (engine.NetworkId != 0) && (b.NetworkId == engine.NetworkId))
            .OrderBy(b => P.Torus.DistSq(b.Origin, targetCenter)).FirstOrDefault()?.Origin;
        List<Int2> chain = new List<Int2>();
        if (start is null)
        {
            Int2? first = BestPoleSpot(engine.Origin, supply + 1, t => Placement.DistToRect(P, t, engine.Origin, engine.W, engine.H) <= supply, targetCenter, chain);
            if (first is null)
            {
                return false;
            }

            chain.Add(first.Value);
            start = first;
        }

        Int2 last = start.Value;
        for (int i = 0; (i < 25) && (Placement.DistToRect(P, last, target.Origin, target.W, target.H) > supply); i++)
        {
            Int2 from = last;
            Int2? next = BestPoleSpot(from, reach, t => P.Torus.Chebyshev(t, from) <= reach, targetCenter, chain);
            if ((next is null) || (P.Torus.DistSq(next.Value, targetCenter) >= P.Torus.DistSq(last, targetCenter)))
            {
                return false;
            }

            chain.Add(next.Value);
            last = next.Value;
        }

        foreach (Int2 t in chain)
        {
            Send(new PlaceBuilding(pole.Id, t, Dir.North));
        }

        plan.Placed.AddRange(chain.Select(_ => pole.Id));

        return (chain.Count > 0) || (Placement.DistToRect(P, last, target.Origin, target.W, target.H) <= supply);
    }

    private Int2? BestPoleSpot(Int2 around, int radius, Func<Int2, bool> ok, Int2 toward, List<Int2> taken)
    {
        Int2? best = null;
        long bestD = long.MaxValue;
        BuildingDef def = C.Buildings[PoleId()];
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Int2 t = P.Torus.Wrap(around + new Int2(x, y));
                if ((ok(t) == false) || taken.Contains(t) || (P.CanPlace(def, t, Dir.North) is not null))
                {
                    continue;
                }

                long d = P.Torus.DistSq(t, toward);
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ demand

    private void ComputeDemand(BotPlan plan)
    {
        Dictionary<string, long> need = new Dictionary<string, long>();
        void Add(string item, long n)
        {
            if (n > 0)
            {
                need[item] = need.GetValueOrDefault(item) + n;
            }
        }

        foreach (string id in plan.TechPath)
        {
            TechDef t = C.Techs[id];
            // The research under way: enough cores to keep every station busy for a while (about 15 units each),
            // so the science lines never stop as "surplus". Later techs: a small lookahead.
            int ahead = id == _w.Research.Current ? System.Math.Max(Params.ResearchLookahead, 15 * System.Math.Max(1, CountOf(BuildingKind.Lab))) : Params.ResearchLookahead;
            int remaining = System.Math.Min(ahead, _w.Research.UnitsFor(t) - _w.Research.Progress.GetValueOrDefault(id));
            foreach (ItemAmount c in t.UnitCostList)
            {
                Add(c.Item, (long)c.Count * remaining);
            }
        }

        foreach (ConstructionSite s in P.Sites)
        {
            foreach (ItemAmount m in s.Missing())
            {
                Add(m.Item, m.Count);
            }
        }

        foreach (ItemAmount c in plan.PlannedCosts)
        {
            Add(c.Item, c.Count);
        }

        // A construction module for every helper bot that can't build yet.
        ItemDef? buildModule = P.ModuleForJob(JobKind.Build);
        if ((buildModule is not null) && _w.Research.Unlocked.Contains(buildModule.Id))
        {
            Add(buildModule.Id, System.Math.Max(0, HelpersLacking(JobKind.Build).Count - (Commander()?.Carry.Count(buildModule.Id) ?? 0)));
        }

        if (Goal is HelpersGoal fg)
        {
            RecipeDef recipe = C.Recipes[HelpersGoal.Recipe];
            int remaining = fg.Count - _w.HelpersBuilt;
            foreach (ItemAmount c in recipe.Inputs)
            {
                Add(c.Item, (long)c.Count * remaining);
            }

            BuildingDef beacon = C.BuildingList.First(b => b.Kind == BuildingKind.RobotBay);
            if (CountOf(BuildingKind.RobotBay) == 0)
            {
                foreach (ItemAmount c in beacon.CostList)
                {
                    Add(c.Item, c.Count);
                }
            }
        }

        if (Goal is ItemGoal ig)
        {
            Add(ig.Item, ig.Count);
        }

        if (WantsHelpers() && (P.Buildings.FirstOrDefault(b => b.Def.Kind == BuildingKind.RobotBay) is { Recipe: { } helperRecipe } bay))
        {
            foreach (ItemAmount c in helperRecipe.Inputs)
            {
                Add(c.Item, System.Math.Max(0, c.Count - bay.Inv.Count(c.Item)));
            }
        }

        if (Params.Automate)
        {
            // A standing order of plates and parts, so drills and fabricators keep running ahead of need.
            foreach (string item in _standingStock.Where(i => (WorldProbe.RecipeFor(C, i) is { } r) && _w.Research.Unlocked.Contains(r.Id)))
            {
                Add(item, 50L * (1 + AgeReached()));
            }
        }

        if (Goal is SettlersGoal)
        {
            foreach (BuildingDef def in TerraformBuildings().Where(d => _w.Research.Unlocked.Contains(d.Id) && (d.Kind == BuildingKind.PayloadLauncher)))
            {
                Building? built = P.Buildings.FirstOrDefault(b => b.Def.Id == def.Id);
                foreach ((RecipeDef recipe, string stat, int points) in Payloads(def))
                {
                    int crafts = (RemotePointsLeft(stat) + points - 1) / points;
                    foreach (ItemAmount c in recipe.Inputs)
                    {
                        long want = (long)c.Count * System.Math.Min(crafts, 10);
                        Add(c.Item, System.Math.Max(0, want - (built?.Inv.Count(c.Item) ?? 0)));
                    }
                }
            }

            foreach (BuildingDef def in TerraformBuildings().Where(d => _w.Research.Unlocked.Contains(d.Id) && (d.Kind != BuildingKind.PayloadLauncher)))
            {
                RecipeDef recipe = C.Recipes[def.Recipe!];
                int perCraft = Planet.TerraformEffect(recipe.Effect)?.Points ?? 1;
                int crafts = def.Kind == BuildingKind.Uplink ? 1 : (PointsLeft(def) + perCraft - 1) / perCraft;
                Building? built = P.Buildings.FirstOrDefault(b => b.Def.Id == def.Id);
                foreach (ItemAmount c in recipe.Inputs)
                {
                    // Only a few crafts' worth at a time, so the rest of the base isn't starved.
                    long want = (long)c.Count * System.Math.Min(crafts, 10);
                    Add(c.Item, System.Math.Max(0, want - (built?.Inv.Count(c.Item) ?? 0)));
                }
            }
        }

        string fuelItem = C.Resources[FuelResource()].Item;
        Add(fuelItem, Params.MinFuelStock + (5 * P.Buildings.Count(b => b.Def.Energy == EnergyKind.Burner)));

        // An expedition needs the ferry rocket and its fuel (minus what's already on the pad).
        if ((_offPlanet is not null) && (P.Buildings.FirstOrDefault(b => b.Def.Kind == BuildingKind.LaunchPad) is { } padForTrip))
        {
            foreach (ItemAmount i in padForTrip.Def.LaunchFuelList.Prepend(new ItemAmount(padForTrip.Def.Vessel!, 1)))
            {
                Add(i.Item, System.Math.Max(0, i.Count - padForTrip.Inv.Count(i.Item)));
            }

            // And the drills and fuel it carries, once worked out.
            foreach ((string item, int n) in (_expeditionKit ?? []).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                Add(item, System.Math.Max(0, n - padForTrip.Inv.Count(item)));
            }
        }

        foreach (ItemAmount i in OutpostDemand().Concat(ClimateDemand()))
        {
            Add(i.Item, i.Count);
        }

        Dictionary<string, long> stock = WorldProbe.StorageTotals(P);
        // Cargo back from an expedition waits on the launch pad until it's hauled in: it counts too.
        // (Not the rocket and its fuel: those are already set aside for the trip.)
        foreach (Building pad in P.Buildings.Where(b => b.Def.Kind == BuildingKind.LaunchPad))
        {
            foreach ((string item, int n) in pad.Inv.Items.Where(kv => (kv.Key != pad.Def.Vessel) && pad.Def.LaunchFuel.ContainsKey(kv.Key) == false))
            {
                stock[item] = stock.GetValueOrDefault(item) + n;
            }
        }

        // What the commander is carrying counts too (it's on its way to storage or a machine).
        foreach ((string item, int n) in Commander()?.Carry.Items ?? [])
        {
            stock[item] = stock.GetValueOrDefault(item) + n;
        }

        // Count crafts already queued/in progress as incoming, so the bot doesn't over-queue.
        if (_w.Get<Building>(P.LanderId) is { } lander)
        {
            // Parts queued for another craft are used up by it, so only the crafts proper count. (Their
            // ingredients, set aside in the lander's reserve, aren't stock any more.)
            IEnumerable<string> queued = lander.CraftQueue.Where(e => e.Intermediate == false).Select(e => e.RecipeId)
                .Concat(lander.Crafting && (lander.CraftingIntermediate == false) && (lander.RecipeId is { } cur) ? [cur] : []);
            foreach (string id in queued)
            {
                foreach (ItemAmount o in C.Recipes[id].Outputs)
                {
                    stock[o.Item] = stock.GetValueOrDefault(o.Item) + o.Count;
                }
            }
        }

        foreach ((string? item, long n) in need.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            Expand(plan, stock, item, n, 0);
        }

        _machineCategories = plan.CraftDeficit.Keys.Select(it => WorldProbe.RecipeFor(C, it)!.Category).Where(NeedsMachine).ToHashSet();

        // Raw materials this planet doesn't have: fetch them on an expedition to a planet that does.
        _offPlanet = null;
        _offPlanetAlso = [];
        foreach (string res in plan.RawDeficit.Keys.Where(r => (P.HasResource(r) == false) && (ShippedHome(r) == false)).Order(StringComparer.Ordinal))
        {
            if (_w.Planets.FirstOrDefault(p => (p != P) && p.HasResource(res)) is { } there)
            {
                _offPlanet = (res, there.Id);
                _offPlanetAlso = plan.RawDeficit.Keys.Where(r => (r != res) && there.HasResource(r) && (P.HasResource(r) == false) && (ShippedHome(r) == false)).Order(StringComparer.Ordinal).ToList();
                break;
            }

            plan.Problems.Add($"no planet has {res}");
        }
    }

    /// <summary>True for recipe categories neither the lander nor a smelter runs, so they need their own machine.</summary>
    private bool NeedsMachine(string category) =>
        (category != "smelting") && (C.Buildings.Values.Any(b => (b.Kind == BuildingKind.Lander) && b.Categories.Contains(category)) == false);

    /// <summary>The researched building that runs a recipe category.</summary>
    private BuildingDef? MachineFor(string category) =>
        C.BuildingList.FirstOrDefault(b => b.Buildable && b.Categories.Contains(category) && _w.Research.Unlocked.Contains(b.Id));

    /// <summary>
    /// Hand-feeds a machine that makes something the plan wants (a refinery): set its recipe, carry the
    /// ingredients over from the lander and bring the products back. True if the commander has work there.
    /// </summary>
    private bool TendMachines(BotPlan plan, Unit leader, Building lander)
    {
        foreach ((string item, long _) in plan.CraftDeficit.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            RecipeDef r = WorldProbe.RecipeFor(C, item)!;
            if (NeedsMachine(r.Category) == false)
            {
                continue;
            }

            Building? m = P.Buildings.Where(b => b.Def.Categories.Contains(r.Category)).OrderBy(b => b.Id).FirstOrDefault();
            if (m is null)
            {
                continue;
            }

            bool powered = (m.Def.Energy != EnergyKind.Electric) || ((m.NetworkId != 0) && P.Networks.TryGetValue(m.NetworkId, out PowerNetwork? n) && (n.Generators.Count > 0));
            if (powered == false)
            {
                plan.Problems.Add($"{m.Def.Name} has no power yet");

                continue;
            }

            Int2 beside = P.Torus.Wrap(m.Origin + new Int2(0, m.H));
            bool atMachine = P.InLeaderReach(leader, m.Origin);
            // A collector never stops (its recipes take nothing), so switch it once its product isn't wanted.
            bool idleOrSurplus = (m.Crafting == false) || ((m.Recipe is { Inputs.Length: 0 } cur) && (plan.CraftDeficit.ContainsKey(cur.Outputs[0].Item) == false));
            if ((m.RecipeId != r.Id) && idleOrSurplus)
            {
                if (atMachine && m.Out.IsEmpty)
                {
                    Send(new SetRecipe(m.Id, r.Id));
                }
                else if (atMachine)
                {
                    Send(new TakeFromBuilding(leader.Id, m.Id, null, int.MaxValue, InventoryPart.Output));
                }
                else
                {
                    WalkTowards(leader, beside);
                }

                return true;
            }

            if (m.Out.IsEmpty == false)
            {
                if (atMachine)
                {
                    Send(new TakeFromBuilding(leader.Id, m.Id, null, int.MaxValue, InventoryPart.Output));
                }
                else
                {
                    WalkTowards(leader, beside);
                }

                return true;
            }

            ItemAmount[] wanted = r.Inputs.Select(i => new ItemAmount(i.Item, System.Math.Min((i.Count * Params.CraftBatch) - m.Inv.Count(i.Item), P.AcceptAmount(m, i.Item))))
                .Where(i => i.Count >= r.In[i.Item]).ToArray();
            if (wanted.Length == 0)
            {
                continue; // fed and working
            }

            if (wanted.Any(i => leader.Carry.Count(i.Item) > 0))
            {
                if (atMachine)
                {
                    foreach (ItemAmount i in wanted.Where(i => leader.Carry.Count(i.Item) > 0))
                    {
                        Send(new DepositToBuilding(leader.Id, m.Id, i.Item, i.Count));
                    }
                }
                else
                {
                    WalkTowards(leader, beside);
                }

                return true;
            }

            // Fetch from the lander, or whichever crate holds the most of the first missing ingredient.
            Building? source = wanted.All(i => lander.Inv.Count(i.Item) >= r.In[i.Item]) ? lander
                : WorldProbe.Storages(P).Where(st => st.Inv.Count(wanted[0].Item) >= r.In[wanted[0].Item])
                    .OrderByDescending(st => st.Inv.Count(wanted[0].Item)).ThenBy(st => st.Id).FirstOrDefault();
            if (source is not null)
            {
                if (P.InLeaderReach(leader, source.Origin))
                {
                    foreach (ItemAmount i in wanted.Where(i => source.Inv.Count(i.Item) > 0))
                    {
                        Send(new TakeFromBuilding(leader.Id, source.Id, i.Item, System.Math.Min(i.Count, source.Inv.Count(i.Item))));
                    }
                }
                else
                {
                    WalkTowards(leader, source.Id == lander.Id ? P.Torus.Wrap(lander.Origin + new Int2(1, lander.H + 1)) : P.Torus.Wrap(source.Origin + new Int2(0, source.H)));
                }

                return true;
            }
        }

        return false;
    }

    private void Expand(BotPlan plan, Dictionary<string, long> stock, string item, long qty, int depth)
    {
        if (depth > 8)
        {
            return;
        }

        long have = System.Math.Min(qty, stock.GetValueOrDefault(item));
        stock[item] = stock.GetValueOrDefault(item) - have;
        long d = qty - have;
        if (d <= 0)
        {
            return;
        }

        // Gas-giant and asteroid resources lie on no planet's surface: an orbital collector makes them.
        bool orbitalOnly = (WorldProbe.ResourceFor(C, item) is { } raw) && (_w.Planets.Any(p => p.HasResource(raw.Id)) == false) && (WorldProbe.RecipeFor(C, item) is not null);
        if ((WorldProbe.ResourceFor(C, item) is { } res) && (orbitalOnly == false))
        {
            plan.RawDeficit[res.Id] = plan.RawDeficit.GetValueOrDefault(res.Id) + d;

            return;
        }

        RecipeDef? r = WorldProbe.RecipeFor(C, item);
        if ((r is null) || (_w.Research.Unlocked.Contains(r.Id) == false))
        {
            plan.Unobtainable[item] = plan.Unobtainable.GetValueOrDefault(item) + d;

            return;
        }

        long batches = (d + r.Out[item] - 1) / r.Out[item];
        Dictionary<string, long> target = r.Category == "smelting" ? plan.SmeltDeficit : plan.CraftDeficit;
        target[item] = target.GetValueOrDefault(item) + d;
        plan.CraftDepth[item] = System.Math.Max(plan.CraftDepth.GetValueOrDefault(item), depth);
        foreach (ItemAmount i in r.Inputs)
        {
            Expand(plan, stock, i.Item, i.Count * batches, depth + 1);
        }
    }

    // ------------------------------------------------------------------ crafting

    private void QueueCrafting(BotPlan plan, Building lander)
    {
        // A queue nothing in it can be crafted from (e.g. inherited from a messy player) blocks every
        // other craft and hides real demand: clear it, the way a player would.
        if ((lander.Crafting == false) && (lander.CraftQueue.Count > 0) && (lander.CraftQueue.Any(e => Planet.LanderHas(lander, C.Recipes[e.RecipeId].Inputs)) == false))
        {
            Send(new CancelQueuedCrafts(lander.Id));
            plan.Problems.Add($"cleared a stalled craft queue ({lander.CraftQueue.Count} entries)");

            return;
        }

        if (lander.CraftQueue.Count >= 2)
        {
            return;
        }
        // Deepest intermediates first (wire before circuits).
        foreach ((string? item, long deficit) in plan.CraftDeficit.OrderByDescending(kv => plan.CraftDepth[kv.Key]).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            RecipeDef r = WorldProbe.RecipeFor(C, item)!;
            if ((lander.Def.Categories.Contains(r.Category) == false) || Fabricators().Any(f => (f.RecipeId == r.Id) && (f.Status != MachineStatus.NoPower)))
            {
                continue;
            }

            long batches = System.Math.Min(Params.CraftBatch, (deficit + r.Out[item] - 1) / r.Out[item]);
            int canMake = (int)System.Math.Min(batches, r.Inputs.Min(i => lander.Inv.Count(i.Item) / i.Count));
            if (canMake <= 0)
            {
                continue;
            }

            Send(new QueueCraft(lander.Id, r.Id, canMake));

            return;
        }
    }

    // ------------------------------------------------------------------ followers

    private void AssignFollowers(BotPlan plan, Building lander)
    {
        string fuelItem = C.Resources[FuelResource()].Item;
        List<string> storageFuel = C.ItemList.Where(i => i.FuelKj > 0).Select(i => i.Id).Where(i => WorldProbe.StorageCount(P, i) > 0).ToList();
        List<Job> needs = new List<Job>();
        int FuelIn(Building b) => b.Inv.Items.Where(kv => C.IsFuel(kv.Key)).Sum(kv => kv.Value);
        // Haul from whichever storage actually holds the item (the lander when tied), not always the lander.
        int SourceFor(params string[] items) => WorldProbe.Storages(P)
            .OrderByDescending(st => items.Sum(it => (long)st.Inv.Count(it))).ThenBy(st => st.Id == lander.Id ? 0 : 1).ThenBy(st => st.Id)
            .FirstOrDefault()?.Id ?? lander.Id;

        // Construction
        int buildable = P.Sites.Count(s => (s.Kind == SiteKind.Deconstruct) || s.MaterialsComplete || s.Missing().Any(m => WorldProbe.StorageCount(P, m.Item) > 0));
        int maxBuilders = Params.Automate ? System.Math.Max(Params.MaxBuilders, P.Units.Count(u => (u.IsLeader == false) && (u.OwnerPlayer == Player)) / 3) : Params.MaxBuilders;
        for (int i = 0; i < System.Math.Min(buildable, maxBuilders); i++)
        {
            needs.Add(new Job(JobKind.Build));
        }

        // Burners with nothing to burn stop everything (the lander starts empty): fuel comes first.
        long fuelStored = C.ItemList.Where(i => i.FuelKj > 0).Sum(i => WorldProbe.StorageCount(P, i.Id));
        bool burners = P.Buildings.Any(b => b.Def.Energy == EnergyKind.Burner) || P.Sites.Any(st => C.Buildings.TryGetValue(st.DefId, out BuildingDef? d) && (d.Energy == EnergyKind.Burner));
        if (burners && (fuelStored < Params.MinFuelStock / 2))
        {
            needs.Add(new Job(JobKind.Gather, Resource: FuelResource()));
        }

        // A lander stuffed with stockpiled ore can't take anything crafting needs: move the bulkiest
        // thing that isn't a crafting ingredient out to a crate.
        if (LanderCongested(lander))
        {
            HashSet<string> craftIn = plan.CraftDeficit.Keys.Select(it => WorldProbe.RecipeFor(C, it)!).SelectMany(r => r.Inputs).Select(i => i.Item).ToHashSet();
            string? bulk = lander.Inv.Items.Where(kv => craftIn.Contains(kv.Key) == false).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key).FirstOrDefault();
            Building? crate = bulk is null ? null : WorldProbe.Storages(P).Where(st => (st.Id != lander.Id) && (st.Inv.SpaceFor(bulk) >= 50))
                .OrderBy(st => P.SubTorus.DistSq(lander.CenterSub, st.CenterSub)).ThenBy(st => st.Id).FirstOrDefault();
            if (crate is not null)
            {
                needs.Add(new Job(JobKind.Haul, FromId: lander.Id, ToId: crate.Id, Item: bulk));
            }
        }

        // Keep generators fuelled
        foreach (Building? e in P.Buildings.Where(b => b.Def.Kind == BuildingKind.SteamEngine))
        {
            if ((FuelIn(e) < 3) && (storageFuel.Count > 0))
            {
                string fi = BestFuel(e, storageFuel, fuelItem);
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(fi), ToId: e.Id, Item: fi));
            }
        }

        // Feed labs
        if (_w.Research.Current is { } techId)
        {
            TechDef tech = C.Techs[techId];
            // A hauler for each lab that is running dry.
            foreach (Building hungry in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Lab) && (b.NetworkId != 0))
                .Where(lab => (lab.Inv.Has(tech.UnitCostList) == false) && tech.UnitCostList.Any(c => (lab.Inv.Count(c.Item) < c.Count) && (WorldProbe.StorageCount(P, c.Item) > 0)))
                .OrderBy(lab => lab.Id))
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(tech.UnitCostList.Where(c => hungry.Inv.Count(c.Item) < c.Count).Select(c => c.Item).ToArray()), ToId: hungry.Id));
            }
        }

        // Furnaces: collect output, fuel, feed ore (each furnace gets one ore type).
        HashSet<int> drillFed = DrillFedFurnaces();
        List<Building> furnaces = P.Buildings.Where(b => b.Def.Kind == BuildingKind.Furnace).ToList();
        List<string> oreQueue = plan.SmeltDeficit.OrderByDescending(kv => kv.Value)
            .Select(kv => WorldProbe.RecipeFor(C, kv.Key)!.Inputs[0].Item)
            .Where(ore => WorldProbe.StorageCount(P, ore) > 0).Distinct().ToList();
        for (int i = 0; i < furnaces.Count; i++)
        {
            Building f = furnaces[i];
            string? product = f.Out.Items.Select(kv => kv.Key).FirstOrDefault();
            bool collect = (f.Out.Total >= Params.FurnaceCollectAt) || ((f.Status != MachineStatus.Working) && (f.Out.Total > 0));
            // A drill column with plenty in store is left to back up: its drill stops until the plates are wanted.
            bool plenty = Params.Automate && drillFed.Contains(f.Id) && (product is not null) && (WorldProbe.StorageCount(P, product) >= StockpileCap() * 3);
            if (collect && (plenty == false) && (product is not null) && (StoreFor(product, lander) is { } into))
            {
                needs.Add(new Job(JobKind.Haul, FromId: f.Id, ToId: into.Id));
            }

            string? held = f.Inv.Items.Select(kv => kv.Key).FirstOrDefault(k => C.IsFuel(k) == false);
            string? ore = held ?? (oreQueue.Count > 0 ? oreQueue[i % oreQueue.Count] : null);
            bool wantsOre = (ore is not null) && oreQueue.Contains(ore) && (f.Inv.Count(ore) < 5) && (drillFed.Contains(f.Id) == false);
            if (drillFed.Contains(f.Id) && (f.Def.Energy == EnergyKind.Burner) && (FuelIn(f) < 2) && (f.FuelJ < 200_000) && (storageFuel.Count > 0))
            {
                string fi = BestFuel(f, storageFuel, fuelItem);
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(fi), ToId: f.Id, Item: fi));
            }

            if (drillFed.Contains(f.Id) && P.Buildings.Any(s => (s.Def.Kind == BuildingKind.Sorter) && (P.BuildingAt(s.BackTile())?.Id == f.Id)))
            {
                continue; // emptied by its sorter arm
            }

            // Fuel any furnace that has (or is about to get) ore to smelt — not just ones still wanting ore.
            if ((wantsOre || (held is not null)) && (f.Def.Energy == EnergyKind.Burner) && (FuelIn(f) < 2) && (f.FuelJ < 200_000) && (storageFuel.Count > 0))
            {
                string fi = BestFuel(f, storageFuel, fuelItem);
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(fi), ToId: f.Id, Item: fi));
            }

            if (wantsOre)
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(ore!), ToId: f.Id, Item: ore));
            }
        }

        // Burner drills
        foreach (Building d in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Drill) && (b.Def.Energy == EnergyKind.Burner)).OrderBy(b => b.Id))
        {
            if ((FuelIn(d) < 2) && (d.FuelJ < 200_000) && (storageFuel.Count > 0))
            {
                string fi = BestFuel(d, storageFuel, fuelItem);
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(fi), ToId: d.Id, Item: fi));
            }
        }

        // Drills with nothing in front to take their ore (early ones put down before columns): carry it to storage.
        // (A column backed up behind a full furnace is left alone: it stops until the plates are wanted.)
        // Only for ore that's short, one at a time: other hauling comes first.
        foreach (Building d in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Drill) && (b.Out.Total >= 5) && (_retiring.Contains(b.Id) == false) && (P.BuildingAt(b.FrontTile()) is null)).OrderBy(b => b.Id))
        {
            string ore = d.Out.Items.Select(kv => kv.Key).First();
            if ((WorldProbe.StorageCount(P, ore) < StockpileCap()) && (StoreFor(ore, lander) is { } into))
            {
                needs.Add(new Job(JobKind.Haul, FromId: d.Id, ToId: into.Id));

                break;
            }
        }

        // Robot bays
        bool moreHelpers = (Params.Automate == false) || (Goal is HelpersGoal) || WantsHelpers();
        foreach (Building? bc in P.Buildings.Where(b => (b.Def.Kind is BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink) && (b.Status != MachineStatus.Complete))
                     .Where(b => (b.Def.Kind != BuildingKind.RobotBay) || moreHelpers))
        {
            // Climate engines surge every few seconds: keep several surges' worth in them, not just the next one.
            int stock = IsClimateEngine(bc) ? 5 : 1;
            if ((bc.Recipe is { } r) && ((bc.Crafting == false) || (stock > 1))
                && r.Inputs.Any(c => (bc.Inv.Count(c.Item) < c.Count * stock) && (P.AcceptAmount(bc, c.Item) > 0) && (WorldProbe.StorageCount(P, c.Item) > 0)))
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(r.Inputs.Where(c => bc.Inv.Count(c.Item) < c.Count * stock).Select(c => c.Item).ToArray()), ToId: bc.Id));
            }
        }

        // Payload launchers: keep a payload loaded for a stat the remote targets still need.
        foreach (Building pl in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.PayloadLauncher) && (b.Crafting == false)))
        {
            string? pack = Payloads(pl.Def).Where(x => (RemotePointsLeft(x.Stat) > 0) && x.Recipe.Inputs.Any(c => (pl.Inv.Count(c.Item) < c.Count) && (WorldProbe.StorageCount(P, c.Item) > 0)))
                .Select(x => x.Recipe.Inputs[0].Item).FirstOrDefault();
            if (pack is not null)
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(pack), ToId: pl.Id, Item: pack));
            }
        }

        // Raw materials a ghost is waiting on come before keeping the factory fed: one gatherer each.
        foreach (string res in P.Sites.SelectMany(st => st.Missing()).Where(m => WorldProbe.StorageCount(P, m.Item) < m.Count)
                     .Select(m => WorldProbe.ResourceFor(C, m.Item)).Where(r => (r is not null) && P.HasResource(r.Id)).Select(r => r!.Id).Distinct().Order(StringComparer.Ordinal))
        {
            needs.Add(new Job(JobKind.Gather, Resource: res));
        }

        StationJobs(needs, SourceFor, lander);
        FeedFabricators(plan, needs, SourceFor, lander);

        // Expedition cargo waits on the launch pad: bring it in (but leave the rocket and its fuel).
        foreach (Building pad in P.Buildings.Where(b => b.Def.Kind == BuildingKind.LaunchPad))
        {
            HashSet<string> keep = pad.Def.LaunchFuelList.Select(i => i.Item).Append(pad.Def.Vessel!).Concat(KitItems(pad)).ToHashSet();
            if (pad.Inv.Items.Select(kv => kv.Key).Where(k => keep.Contains(k) == false).Order(StringComparer.Ordinal).FirstOrDefault() is { } cargo)
            {
                needs.Add(new Job(JobKind.Haul, FromId: pad.Id, ToId: lander.Id, Item: cargo));
            }
        }

        // Consolidate: crafting draws only from the lander, so fetch craft inputs sitting in other storage.
        // Enough for one craft of whichever wants the most of it (a rocket takes 40 plates), and at least 10.
        Dictionary<string, int> craftInputs = plan.CraftDeficit.Keys.Select(it => WorldProbe.RecipeFor(C, it)!)
            .Where(r => lander.Def.Categories.Contains(r.Category)).SelectMany(r => r.Inputs)
            .GroupBy(i => i.Item).ToDictionary(g => g.Key, g => System.Math.Max(10, g.Max(i => i.Count)));
        foreach ((string item, int enough) in craftInputs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            Building? other = WorldProbe.Storages(P).Where(st => (st.Id != lander.Id) && (st.Inv.Count(item) > 0)).OrderByDescending(st => st.Inv.Count(item)).FirstOrDefault();
            if ((other is not null) && (lander.Inv.Count(item) < enough))
            {
                needs.Add(new Job(JobKind.Haul, FromId: other.Id, ToId: lander.Id, Item: item));
            }
        }

        // Something this planet has hasn't turned up yet: send a helper bot to explore.
        if (WantExplorer())
        {
            needs.Add(new Job(JobKind.Explore));
        }

        // Raw materials, biggest gathering backlog first.
        HashSet<string> drilled = DrilledResources();
        foreach ((string? res, long _) in plan.RawDeficit.Where(kv => P.HasResource(kv.Key) && (drilled.Contains(kv.Key) == false)).OrderByDescending(kv => kv.Value * C.Resources[kv.Key].HandTicks).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            needs.Add(new Job(JobKind.Gather, Resource: res));
        }

        plan.Needs.AddRange(needs);

        // Assign: keep followers already doing a needed job, then hand out the rest in priority order.
        // Let a follower finish a delivery already under way: the items it carries no longer show up in
        // storage, so the need that sent it can vanish, and reassigning it would just put them back.
        List<Unit> free = P.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == Player))
            .Where(u => ((u.Job.Kind == JobKind.Haul) && (u.Phase == UnitPhase.Deliver) && (u.Carry.IsEmpty == false)) == false)
            .OrderBy(u => u.Id).ToList();
        List<Job> open = new List<Job>();
        // At the start (two robots, an empty lander) only as many needs as there are robots count, most urgent
        // first: a robot gathering something far down the list moves to feeding the first smelters instead of
        // being kept on its job just because it's listed. Later, keeping robots on any listed job spreads them
        // over everything the base needs, which the long late-game supply chains depend on.
        int considered = _w.Research.Completed.Count == 0 ? free.Count : needs.Count;
        foreach (Job job in needs.Take(considered))
        {
            Unit? keeper = free.FirstOrDefault(u => u.Job == job);
            if (keeper is not null)
            {
                free.Remove(keeper);
            }
            else
            {
                open.Add(job);
            }
        }

        foreach (Job job in open)
        {
            // Only robots with the right module can take some jobs (Build, Operate train).
            if (free.FirstOrDefault(x => P.CanTakeJob(x, job.Kind)) is not { } u)
            {
                continue;
            }

            free.Remove(u);
            Send(new AssignJob(u.Id, job));
        }
        // Spare hands: top up fuel if low, otherwise stockpile whichever resource storage has least of
        // (what a sensible player does; also gives balance analysis data on every resource).
        ResourceDef fuelRes = C.Resources[FuelResource()];
        List<ResourceDef> stockpile = C.ResourceList.Where(r => (r.IsTree == false) && P.HasResource(r.Id) && (WorldProbe.ResourceFor(C, r.Item) == r) && (drilled.Contains(r.Id) == false))
            .Where(r => (Params.Automate == false) || (WorldProbe.StorageCount(P, r.Item) < StockpileCap()))
            .OrderBy(r => WorldProbe.StorageCount(P, r.Item)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        int k = 0;
        foreach (Unit? u in free)
        {
            bool lowFuel = WorldProbe.StorageCount(P, fuelRes.Item) < Params.MinFuelStock * 2;
            if (Params.Automate && (lowFuel == false) && (stockpile.Count == 0))
            {
                if (u.Job.Kind != JobKind.Idle)
                {
                    Send(new AssignJob(u.Id, new Job(JobKind.Idle)));
                }

                continue; // nothing worth stockpiling: stand by instead of filling the crates
            }

            ResourceDef res = lowFuel || (stockpile.Count == 0) ? fuelRes : stockpile[k++ % stockpile.Count];
            Job job = new Job(JobKind.Gather, Resource: res.Id);
            if ((u.Job.Kind == JobKind.Gather) && (u.Job.Resource is { } cur) && ((cur == fuelRes.Id) || stockpile.Any(s => s.Id == cur)))
            {
                continue; // don't thrash
            }

            if (u.Job != job)
            {
                Send(new AssignJob(u.Id, job));
            }
        }
    }
}
