using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// A competent-but-simple player that pursues a <see cref="Goal"/>. It is fully state-driven: every think
/// it re-derives what to build, craft, research and what each follower should do from the current world.
/// That means it can be dropped into ANY state (e.g. after a chaos bot wrecked the base) and will try to
/// recover — which is how the softlock experiment tells "bad state" from "impossible state".
/// </summary>
public sealed class GoalBot : IBot
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
    private Planet P => _w.Planets[0];
    private ContentDb C => _w.Content;
    private void Send(Command c) => _w.Submit(c with { Player = Player });

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

        Building? lander = world.Get<Building>(P.LanderId);
        if (lander is null)
        {
            return;
        }

        BotPlan plan = new BotPlan();
        LastPlan = plan;

        ChooseResearch(plan);
        CleanUp(plan);
        PlanStructures(plan, lander);
        ComputeDemand(plan);
        QueueCrafting(plan, lander);
        AssignFollowers(plan, lander);
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
            case FollowersGoal:
                {
                    BuildingDef beacon = C.BuildingList.First(b => b.Kind == BuildingKind.Beacon);
                    List<string> path = new List<string>();
                    foreach (string? id in new[] { beacon.Id, beacon.Recipe! })
                    {
                        if (TechUnlocking(id) is { } tech)
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
        if ((next is not null) && (_w.Research.Current != next))
        {
            Send(new SetResearch(next));
        }
    }

    // ------------------------------------------------------------------ recovery / cleanup

    private static readonly HashSet<BuildingKind> _wanted =
        [BuildingKind.Lab, BuildingKind.Furnace, BuildingKind.SteamEngine, BuildingKind.Pole, BuildingKind.Chest, BuildingKind.Beacon];

    /// <summary>
    /// What a sensible player does when inheriting a messy base: cancel ghosts it doesn't want (a few per
    /// think) and rescind dismantle orders on buildings it relies on. This is what lets the bot recover
    /// from chaos instead of drowning its builders in junk orders.
    /// </summary>
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
                SiteKind.Build => (C.Buildings.TryGetValue(site.DefId, out BuildingDef? d) == false) || (_wanted.Contains(d.Kind) == false)
                                  || ((d.Kind != BuildingKind.Pole) && (P.Sites.Count(x => x.DefId == site.DefId) > 1) && (site.Id != P.Sites.First(x => x.DefId == site.DefId).Id)),
                SiteKind.BuildTrain => true,
                SiteKind.Deconstruct => (_w.Get<Building>(site.TargetId) is { } b) && _wanted.Contains(b.Def.Kind),
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
        BuildingDef beaconDef = C.BuildingList.First(b => b.Kind == BuildingKind.Beacon);
        bool needBeacon = (Goal is FollowersGoal) && _w.Research.Unlocked.Contains(beaconDef.Id);

        if (needLab && (CountOf(BuildingKind.Lab) < Params.Labs))
        {
            PlaceNear(plan, "lab", center, 4, 14);
        }

        if (needBeacon && (CountOf(BuildingKind.Beacon) == 0))
        {
            PlaceNear(plan, beaconDef.Id, center, 5, 16);
        }

        if (CountOf(BuildingKind.Furnace) < Params.Furnaces)
        {
            PlaceNear(plan, "stone-furnace", center, 3, 10);
        }

        // Storage pressure → add a crate.
        List<Building> storages = WorldProbe.Storages(P).ToList();
        int used = storages.Sum(s => s.Inv.UsedSlots), slots = storages.Sum(s => s.Inv.Slots);
        if ((slots > 0) && (used * 100 / slots > 85) && (HasSite("chest") == false))
        {
            PlaceNear(plan, "chest", center, 3, 12);
        }

        List<Building> consumers = P.Buildings.Where(b => b.Def.Energy == EnergyKind.Electric).ToList();
        if ((consumers.Count > 0) || needLab)
        {
            EnsurePower(plan, center, consumers);
        }
    }

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

        if (HasSite("power-pole") || HasSite("steam-engine"))
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

            if (PlacePoleChain(plan, engine, c) == false)
            {
                plan.Problems.Add($"couldn't route power to {c.Def.Name}");
            }

            return; // one chain per think
        }
    }

    private bool PlacePoleChain(BotPlan plan, Building engine, Building target)
    {
        BuildingDef pole = C.Buildings["power-pole"];
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
            Send(new PlaceBuilding("power-pole", t, Dir.North));
        }

        plan.Placed.AddRange(chain.Select(_ => "power-pole"));

        return (chain.Count > 0) || (Placement.DistToRect(P, last, target.Origin, target.W, target.H) <= supply);
    }

    private Int2? BestPoleSpot(Int2 around, int radius, Func<Int2, bool> ok, Int2 toward, List<Int2> taken)
    {
        Int2? best = null;
        long bestD = long.MaxValue;
        BuildingDef def = C.Buildings["power-pole"];
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
            int remaining = System.Math.Min(Params.ResearchLookahead, t.Units - _w.Research.Progress.GetValueOrDefault(id));
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

        if (Goal is FollowersGoal fg)
        {
            RecipeDef recipe = C.Recipes[C.BuildingList.First(b => b.Kind == BuildingKind.Beacon).Recipe!];
            int remaining = fg.Count - _w.FollowersSummoned;
            foreach (ItemAmount c in recipe.Inputs)
            {
                Add(c.Item, (long)c.Count * remaining);
            }

            BuildingDef beacon = C.BuildingList.First(b => b.Kind == BuildingKind.Beacon);
            if (CountOf(BuildingKind.Beacon) == 0)
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

        string fuelItem = C.Resources[Params.FuelResource].Item;
        Add(fuelItem, Params.MinFuelStock);

        Dictionary<string, long> stock = WorldProbe.StorageTotals(P);
        // Count crafts already queued/in progress as incoming, so the bot doesn't over-queue.
        if (_w.Get<Building>(P.LanderId) is { } lander)
        {
            IEnumerable<string> queued = lander.CraftQueue.Concat(lander.Crafting && (lander.RecipeId is { } cur) ? [cur] : []);
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

        if (WorldProbe.ResourceFor(C, item) is { } res)
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
        if ((lander.Crafting == false) && (lander.CraftQueue.Count > 0) && (lander.CraftQueue.Any(id => lander.Inv.Has(C.Recipes[id].Inputs)) == false))
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
            if (lander.Def.Categories.Contains(r.Category) == false)
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
        string fuelItem = C.Resources[Params.FuelResource].Item;
        List<string> storageFuel = C.ItemList.Where(i => i.FuelKj > 0).Select(i => i.Id).Where(i => WorldProbe.StorageCount(P, i) > 0).ToList();
        List<Job> needs = new List<Job>();
        int FuelIn(Building b) => b.Inv.Items.Where(kv => C.IsFuel(kv.Key)).Sum(kv => kv.Value);
        // Haul from whichever storage actually holds the item (the lander when tied), not always the lander.
        int SourceFor(params string[] items) => WorldProbe.Storages(P)
            .OrderByDescending(st => items.Sum(it => (long)st.Inv.Count(it))).ThenBy(st => st.Id == lander.Id ? 0 : 1).ThenBy(st => st.Id)
            .FirstOrDefault()?.Id ?? lander.Id;

        // Construction
        int buildable = P.Sites.Count(s => (s.Kind == SiteKind.Deconstruct) || s.MaterialsComplete || s.Missing().Any(m => WorldProbe.StorageCount(P, m.Item) > 0));
        for (int i = 0; i < System.Math.Min(buildable, Params.MaxBuilders); i++)
        {
            needs.Add(new Job(JobKind.Build));
        }

        // Keep generators fuelled
        foreach (Building? e in P.Buildings.Where(b => b.Def.Kind == BuildingKind.SteamEngine))
        {
            if ((FuelIn(e) < 3) && (storageFuel.Count > 0))
            {
                string fi = storageFuel.Contains(fuelItem) ? fuelItem : storageFuel[0];
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(fi), ToId: e.Id, Item: fi));
            }
        }

        // Feed labs
        if (_w.Research.Current is { } techId)
        {
            TechDef tech = C.Techs[techId];
            // One lab hauler at a time: feed the lab that is closest to running dry.
            Building? hungry = P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Lab) && (b.NetworkId != 0))
                .Where(lab => (lab.Inv.Has(tech.UnitCostList) == false) && tech.UnitCostList.Any(c => (lab.Inv.Count(c.Item) < c.Count) && (WorldProbe.StorageCount(P, c.Item) > 0)))
                .OrderBy(lab => lab.Id).FirstOrDefault();
            if (hungry is not null)
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(tech.UnitCostList.Where(c => hungry.Inv.Count(c.Item) < c.Count).Select(c => c.Item).ToArray()), ToId: hungry.Id));
            }
        }

        // Furnaces: collect output, fuel, feed ore (each furnace gets one ore type).
        List<Building> furnaces = P.Buildings.Where(b => b.Def.Kind == BuildingKind.Furnace).ToList();
        List<string> oreQueue = plan.SmeltDeficit.OrderByDescending(kv => kv.Value)
            .Select(kv => WorldProbe.RecipeFor(C, kv.Key)!.Inputs[0].Item)
            .Where(ore => WorldProbe.StorageCount(P, ore) > 0).Distinct().ToList();
        for (int i = 0; i < furnaces.Count; i++)
        {
            Building f = furnaces[i];
            if ((f.Out.Total >= Params.FurnaceCollectAt) || ((f.Status != MachineStatus.Working) && (f.Out.Total > 0)))
            {
                needs.Add(new Job(JobKind.Haul, FromId: f.Id, ToId: lander.Id));
            }

            string? held = f.Inv.Items.Select(kv => kv.Key).FirstOrDefault(k => C.IsFuel(k) == false);
            string? ore = held ?? (oreQueue.Count > 0 ? oreQueue[i % oreQueue.Count] : null);
            bool wantsOre = (ore is not null) && oreQueue.Contains(ore) && (f.Inv.Count(ore) < 5);
            // Fuel any furnace that has (or is about to get) ore to smelt — not just ones still wanting ore.
            if ((wantsOre || (held is not null)) && (FuelIn(f) < 2) && (f.FuelJ < 200_000) && (storageFuel.Count > 0))
            {
                string fi = storageFuel.Contains(fuelItem) ? fuelItem : storageFuel[0];
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(fi), ToId: f.Id, Item: fi));
            }

            if (wantsOre)
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(ore!), ToId: f.Id, Item: ore));
            }
        }

        // Beacon
        foreach (Building? bc in P.Buildings.Where(b => b.Def.Kind == BuildingKind.Beacon))
        {
            if ((bc.Recipe is { } r) && (bc.Crafting == false) && r.Inputs.Any(c => (bc.Inv.Count(c.Item) < c.Count) && (WorldProbe.StorageCount(P, c.Item) > 0)))
            {
                needs.Add(new Job(JobKind.Haul, FromId: SourceFor(r.Inputs.Where(c => bc.Inv.Count(c.Item) < c.Count).Select(c => c.Item).ToArray()), ToId: bc.Id));
            }
        }

        // Consolidate: crafting draws only from the lander, so fetch craft inputs sitting in other storage.
        IOrderedEnumerable<string> craftInputs = plan.CraftDeficit.Keys.Select(it => WorldProbe.RecipeFor(C, it)!)
            .Where(r => lander.Def.Categories.Contains(r.Category)).SelectMany(r => r.Inputs).Select(i => i.Item).Distinct().OrderBy(i => i, StringComparer.Ordinal);
        foreach (string item in craftInputs)
        {
            Building? other = WorldProbe.Storages(P).Where(st => (st.Id != lander.Id) && (st.Inv.Count(item) > 0)).OrderByDescending(st => st.Inv.Count(item)).FirstOrDefault();
            if ((other is not null) && (lander.Inv.Count(item) < 10))
            {
                needs.Add(new Job(JobKind.Haul, FromId: other.Id, ToId: lander.Id, Item: item));
            }
        }

        // Raw materials, biggest gathering backlog first.
        foreach ((string? res, long _) in plan.RawDeficit.OrderByDescending(kv => kv.Value * C.Resources[kv.Key].HandTicks).ThenBy(kv => kv.Key, StringComparer.Ordinal))
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
        foreach (Job job in needs)
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
            if (free.Count == 0)
            {
                break;
            }

            Unit u = free[0];
            free.RemoveAt(0);
            Send(new AssignJob(u.Id, job));
        }
        // Spare hands: top up fuel if low, otherwise stockpile whichever resource storage has least of
        // (what a sensible player does; also gives balance analysis data on every resource).
        ResourceDef fuelRes = C.Resources[Params.FuelResource];
        List<ResourceDef> stockpile = C.ResourceList.Where(r => (r.IsTree == false) && (WorldProbe.ResourceFor(C, r.Item) == r))
            .OrderBy(r => WorldProbe.StorageCount(P, r.Item)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        int k = 0;
        foreach (Unit? u in free)
        {
            ResourceDef res = (WorldProbe.StorageCount(P, fuelRes.Item) < Params.MinFuelStock * 2) || (stockpile.Count == 0) ? fuelRes : stockpile[k++ % stockpile.Count];
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
