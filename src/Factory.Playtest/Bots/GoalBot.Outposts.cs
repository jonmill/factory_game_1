using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// How the bot uses the other planets in the Stellar Age, the way a player is meant to: a trip in the ferry
/// with a kit of parts builds an outpost on each planet worth one. An outpost is an orbital station ringed
/// by drills on the planet's own ores, a fusion reactor and, on the planets being terraformed, a climate
/// engine, both fed from the station by sorter arms. Orbital stations at home take the ores in and ship the
/// climate supplies and fusion cells out, so the outposts run on their own once the ferry has flown home.
/// </summary>
public sealed partial class GoalBot
{
    /// <summary>The ores worth fetching from other planets, most wanted first.</summary>
    private static readonly string[] _remoteOres = ["titanium", "deuterium", "silica", "hafnium", "tantalite", "lanthanide", "brimstone"];

    /// <summary>What home ships out to the outposts: climate engine supplies and reactor fuel.</summary>
    private static readonly string[] _outboundItems = ["fusion-cell", "catalyst-pack", "lanthanide-accelerant", "hydro-cartridge", "seed-pod", "promethium-lattice", "helium-3"];

    /// <summary>Drills per outpost.</summary>
    private const int OutpostDrills = 4;

    /// <summary>Power poles carried per outpost.</summary>
    private const int OutpostPoles = 18;

    /// <summary>Solar panels per outpost: enough to keep the station and the reactor's sorter going if the reactor runs dry.</summary>
    private const int OutpostPanels = 4;

    /// <summary>Of each outpost ore, the most a home station keeps waiting to be unloaded.</summary>
    private const int HomeOreStock = 200;

    /// <summary>Fusion cells an outpost station keeps in stock for its reactor.</summary>
    private const int RemoteCells = 50;

    /// <summary>Crafts' worth of each climate engine input an outpost station keeps in stock.</summary>
    private const int RemoteCrafts = 25;

    /// <summary>Fusion cells carried to start an outpost's reactor.</summary>
    private const int OutpostCells = 10;

    /// <summary>How long a building trip may take before the commander flies home regardless.</summary>
    private const int OutpostTicks = 90 * 3600;

    /// <summary>When set, the planet the planning helpers work on instead of the home planet.</summary>
    private Planet? Focus { get; set; }

    /// <summary>The planet an outpost trip is for, when it started, and whether its ghosts are placed.</summary>
    private (int Planet, long Since, bool Placed)? _outpostTrip;

    private bool Unlocked(string id) => _w.Research.Unlocked.Contains(id);

    /// <summary>True once everything an outpost needs is researched.</summary>
    private bool OutpostsUnlocked() => Params.Automate && (Goal is SettlersGoal) && Unlocked("orbital-station") && Unlocked("fusion-reactor") && Unlocked("fusion-cell")
                                       && Unlocked("electric-drill") && Unlocked("sorter") && Unlocked("climate-engine");

    private Planet Home => _w.Planets[0];

    /// <summary>Up to three ores this planet has that home lacks, most wanted first.</summary>
    private List<ResourceDef> OutpostOres(Planet p) => _remoteOres.Where(r => C.Resources.ContainsKey(r) && p.HasResource(r) && (Home.HasResource(r) == false))
        .Take(3).Select(r => C.Resources[r]).ToList();

    private static Building? StationOn(Planet p) => Stations(p).FirstOrDefault();

    private static IEnumerable<Building> Stations(Planet p) => p.Buildings.Where(b => b.Def.Kind == BuildingKind.OrbitalStation).OrderBy(b => b.Id);

    /// <summary>An outpost's main station: the one whose sorter arms feed the reactor and the engine.</summary>
    private static Building? MainStation(Planet p) =>
        Stations(p).FirstOrDefault(s => p.Buildings.Any(a => (a.Def.Kind == BuildingKind.Sorter) && (p.BuildingAt(a.BackTile())?.Id == s.Id))) ?? StationOn(p);

    /// <summary>The outpost ores the drills facing this station mine.</summary>
    private List<ResourceDef> StationOres(Planet p, Building station) => OutpostOres(p)
        .Where(r => p.Buildings.Any(d => (d.Def.Kind == BuildingKind.Drill) && (p.BuildingAt(d.FrontTile())?.Id == station.Id) && Tiles(p, d.Def, d.Origin, d.Rot).Any(t => p.ResourceAt(t) == r)))
        .ToList();

    /// <summary>Whether a planet gets a climate engine: it is one of the planets being terraformed.</summary>
    private bool GetsEngine(Planet p) => RemoteTargets().Contains(p) && (p.IsReady == false);

    /// <summary>The next planet that should have an outpost and hasn't: the terraform targets first, then any with ores home lacks.</summary>
    private Planet? NextOutpost()
    {
        if (OutpostsUnlocked() == false)
        {
            return null;
        }

        return _w.Planets.Where(p => (p != Home) && (StationOn(p) is null) && (GetsEngine(p) || (OutpostOres(p).Count > 0)))
            .OrderByDescending(GetsEngine).ThenBy(p => p.Id).FirstOrDefault();
    }

    /// <summary>The buildings an outpost on this planet needs.</summary>
    private List<(BuildingDef Def, int Count)> OutpostBuildings(Planet p)
    {
        int stations = System.Math.Max(1, OutpostOres(p).Count);
        List<(BuildingDef, int)> list = [(C.Buildings["orbital-station"], stations), (C.Buildings["fusion-reactor"], 1), (C.Buildings["sorter"], GetsEngine(p) ? 2 : 1), (C.Buildings[PoleId()], OutpostPoles * stations)];
        if (OutpostOres(p).Count > 0)
        {
            list.Add((C.Buildings["electric-drill"], OutpostDrills * OutpostOres(p).Count));
        }

        if (GetsEngine(p))
        {
            list.Add((C.Buildings["climate-engine"], 1));
        }

        if (Unlocked("solar-panel"))
        {
            list.Add((C.Buildings["solar-panel"], OutpostPanels));
        }

        return list;
    }

    /// <summary>Every item the ferry carries to build an outpost here.</summary>
    private Dictionary<string, int> OutpostKit(Planet p)
    {
        Dictionary<string, int> kit = new Dictionary<string, int> { ["fusion-cell"] = OutpostCells };
        foreach ((BuildingDef def, int n) in OutpostBuildings(p))
        {
            foreach (ItemAmount c in def.CostList)
            {
                kit[c.Item] = kit.GetValueOrDefault(c.Item) + (c.Count * n);
            }
        }

        return kit;
    }

    private Building? LaunchPad() => P.Buildings.Where(b => b.Def.Kind == BuildingKind.LaunchPad).OrderBy(b => b.Id).FirstOrDefault();

    /// <summary>What the next outpost needs made: its kit.</summary>
    private IEnumerable<ItemAmount> OutpostDemand()
    {
        if ((_outpostTrip is not null) || (NextOutpost() is not { } next))
        {
            yield break;
        }

        // The whole kit: what is already loaded on the pad counts as stock. Plus the ferry rocket and its fuel.
        foreach ((string item, int n) in OutpostKit(next).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            yield return new ItemAmount(item, n);
        }

        if (LaunchPad() is { } pad)
        {
            foreach (ItemAmount i in pad.Def.LaunchFuelList.Prepend(new ItemAmount(pad.Def.Vessel!, 1)))
            {
                yield return new ItemAmount(i.Item, System.Math.Max(0, i.Count - pad.Inv.Count(i.Item)));
            }
        }
    }

    /// <summary>What every climate engine and outpost reactor will burn soon: ten crafts' worth, twenty cells.</summary>
    private IEnumerable<ItemAmount> ClimateDemand()
    {
        foreach (Building e in _w.Planets.SelectMany(p => p.Buildings).Where(IsClimateEngine).Where(b => b.Recipe is not null).OrderBy(b => b.Id))
        {
            // An engine elsewhere is fed by vessel, which carries a station's worth at a time: keep that much made.
            int crafts = e.Planet == Home ? 10 : RemoteCrafts;
            foreach (ItemAmount i in e.Recipe!.Inputs)
            {
                yield return new ItemAmount(i.Item, (i.Count * crafts) - e.Inv.Count(i.Item));
            }
        }

        int reactors = _w.Planets.Where(p => p != Home).Sum(p => p.Buildings.Count(b => b.Def.Id == "fusion-reactor"));
        if (reactors > 0)
        {
            yield return new ItemAmount("fusion-cell", 20 * reactors);
        }
    }

    /// <summary>Whether a planet's station can get an item: home ships it, or an outpost (this one included) supplies it.</summary>
    private bool Obtainable(string item) => _outboundItems.Contains(item)
        || _w.Planets.SelectMany(Stations).Any(s => s.StationSlots.Any(sl => (sl.Mode == StationMode.Supply) && (sl.Item == item)));

    private static bool IsClimateEngine(Building b) => (b.Def.Kind == BuildingKind.Terraformer) && (b.Def.Recipe is null);

    /// <summary>Resources an outpost already ships home: no need to fly out and dig them by hand.</summary>
    private bool ShippedHome(string res) =>
        _w.Planets.Where(p => p != Home).SelectMany(Stations).Any(s => s.StationSlots.Any(sl => (sl.Mode == StationMode.Supply) && (sl.Item == C.Resources[res].Item)));

    // ------------------------------------------------------------------ home side

    /// <summary>The slots the home stations need, in order: every outpost ore in, every supply out.</summary>
    private List<StationSlot> HomeSlots()
    {
        List<StationSlot> slots = [];
        foreach (string res in _remoteOres.Where(ShippedHome))
        {
            slots.Add(new StationSlot(C.Resources[res].Item, StationMode.Demand, HomeOreStock));
        }

        if (_w.Planets.Any(p => (p != Home) && (StationOn(p) is not null)))
        {
            slots.AddRange(_outboundItems.Where(i => C.Items.ContainsKey(i)).Select(i => new StationSlot(i, StationMode.Supply)));
        }

        return slots;
    }

    /// <summary>Home orbital stations (enough for every slot), climate engines at home, and the station and engine settings everywhere.</summary>
    private void PlanStellar(BotPlan plan, Building lander)
    {
        if (OutpostsUnlocked() == false)
        {
            return;
        }

        BuildingDef station = C.Buildings["orbital-station"];
        List<StationSlot> slots = HomeSlots();
        List<Building> homes = P.Buildings.Where(b => b.Def.Kind == BuildingKind.OrbitalStation).OrderBy(b => b.Id).ToList();
        int needed = (slots.Count + station.StationSlots - 1) / station.StationSlots;
        if ((homes.Count < needed) && (HasSite(station.Id) == false))
        {
            PlaceNear(plan, station.Id, Placement.Center(lander), 6, 40);
        }

        for (int i = 0; i < homes.Count; i++)
        {
            SetSlots(homes[i], slots.Skip(i * station.StationSlots).Take(station.StationSlots).ToList());
        }

        // Climate engines at home too, while the home planet has terraforming left.
        bool homeLeft = C.Terraform.Stats.Any(s => P.Stats.GetValueOrDefault(s.Id) < s.Target);
        if (homeLeft && (CountOf(BuildingKind.Terraformer, "climate-engine") < 2) && (HasSite("climate-engine") == false))
        {
            PlaceNear(plan, "climate-engine", Placement.Center(lander), 6, 40);
        }
    }

    /// <summary>Sets a station's slots to these, changing only the ones that differ.</summary>
    private void SetSlots(Building station, List<StationSlot> want)
    {
        for (int i = 0; i < want.Count; i++)
        {
            if ((i < station.StationSlots.Count) && (station.StationSlots[i] == want[i]))
            {
                continue;
            }

            // Clear a slot holding the item elsewhere first: an item may only be in one slot.
            int clash = station.StationSlots.FindIndex(s => s.Item == want[i].Item);
            if ((clash >= 0) && (clash != i))
            {
                _w.Submit(new SetStationSlot(station.Id, clash, null, StationMode.Supply) { Player = Player, PlanetId = station.Planet.Id });

                return; // slots shift; finish next think
            }

            _w.Submit(new SetStationSlot(station.Id, i, want[i].Item, want[i].Mode, want[i].Max) { Player = Player, PlanetId = station.Planet.Id });
        }

        // Slots no longer wanted go, last first.
        if (station.StationSlots.Count > want.Count)
        {
            _w.Submit(new SetStationSlot(station.Id, station.StationSlots.Count - 1, null, StationMode.Supply) { Player = Player, PlanetId = station.Planet.Id });
        }
    }

    /// <summary>
    /// Every think, wherever the commander is: outpost stations ship their ores and take in what their engine
    /// and reactor burn, and every climate engine works on the stat its planet most lacks.
    /// </summary>
    private void ManageStellar()
    {
        if (OutpostsUnlocked() == false)
        {
            return;
        }

        // Each engine keeps to one stat until it is done, so supplies already shipped aren't stranded.
        foreach (Building e in _w.Planets.SelectMany(p => p.Buildings).Where(IsClimateEngine).OrderBy(b => b.Id))
        {
            Planet q = e.Planet;
            List<(string Stat, string Recipe)> options = C.Terraform.Stats.Where(st => q.Stats.GetValueOrDefault(st.Id) < st.Target)
                .OrderByDescending(st => st.Target - q.Stats.GetValueOrDefault(st.Id)).ThenBy(st => st.Id, StringComparer.Ordinal)
                .Select(st => (st.Id, C.RecipeList.FirstOrDefault(r => e.Def.Categories.Contains(r.Category) && (Planet.TerraformEffect(r.Effect)?.Stat == st.Id) && Unlocked(r.Id))?.Id))
                .Where(x => (x.Item2 is not null) && C.Recipes[x.Item2].Inputs.All(i => Obtainable(i.Item)))
                .Select(x => (x.Item1, x.Item2!)).ToList();
            bool keep = (e.Recipe is { } cur) && options.Any(o => o.Recipe == cur.Id);
            if ((keep == false) && (options.Count > 0) && ((e.Crafting == false) || (e.Status == MachineStatus.Complete)))
            {
                _w.Submit(new SetRecipe(e.Id, options[0].Recipe) { Player = Player, PlanetId = q.Id });
            }
        }

        foreach (Planet q in _w.Planets.Where(p => p != Home))
        {
            Building? main = MainStation(q);
            foreach (Building s in Stations(q))
            {
                List<StationSlot> want = StationOres(q, s).Select(r => new StationSlot(r.Item, StationMode.Supply)).ToList();
                if (s == main)
                {
                    // Demand only a few crafts' worth, so supplies don't crowd the ores out of the station.
                    want.Add(new StationSlot("fusion-cell", StationMode.Demand, RemoteCells));
                    if (q.Buildings.FirstOrDefault(IsClimateEngine) is { Recipe: { } r })
                    {
                        want.AddRange(r.Inputs.Where(i => want.All(w => w.Item != i.Item)).Select(i => new StationSlot(i.Item, StationMode.Demand, i.Count * RemoteCrafts)));
                    }
                }

                SetSlots(s, want.Take(s.Def.StationSlots).ToList());
            }
        }
    }

    /// <summary>Follower jobs at the home stations: unload what came in, keep the outbound supplies stocked.</summary>
    private void StationJobs(List<Job> needs, Func<string[], int> sourceFor, Building lander)
    {
        foreach (Building s in P.Buildings.Where(b => b.Def.Kind == BuildingKind.OrbitalStation).OrderBy(b => b.Id))
        {
            foreach (StationSlot slot in s.StationSlots)
            {
                if ((slot.Mode == StationMode.Demand) && (s.Inv.Count(slot.Item) >= 20) && (WorldProbe.StorageCount(P, slot.Item) < StockpileCap()) && (StoreFor(slot.Item, lander) is { } into))
                {
                    needs.Add(new Job(JobKind.Haul, FromId: s.Id, ToId: into.Id, Item: slot.Item));
                }
                else if ((slot.Mode == StationMode.Supply) && (s.Inv.Count(slot.Item) < 100) && (WorldProbe.StorageCount(P, slot.Item) >= 10))
                {
                    needs.Add(new Job(JobKind.Haul, FromId: sourceFor([slot.Item]), ToId: s.Id, Item: slot.Item));
                }
            }

            // Whatever is left over from a slot since moved elsewhere takes up room the station's own slots need: empty it out.
            foreach (string item in s.Inv.Items.Select(kv => kv.Key).Where(i => s.StationSlots.Any(sl => sl.Item == i) == false).Order(StringComparer.Ordinal).Take(1))
            {
                if (StoreFor(item, lander) is { } into)
                {
                    needs.Add(new Job(JobKind.Haul, FromId: s.Id, ToId: into.Id, Item: item));
                }
            }
        }

        // Gather the next trip's load in the lander, where the commander loads it from: an outpost's kit, or an
        // expedition's rocket, fuel and drills.
        Dictionary<string, int> load = [];
        bool outpost = (_outpostTrip is null) && (NextOutpost() is not null);
        if (outpost)
        {
            load = new Dictionary<string, int>(OutpostKit(NextOutpost()!));
        }

        if (((_offPlanet is not null) || outpost) && (LaunchPad() is { } pad))
        {
            IEnumerable<KeyValuePair<string, int>> kit = _offPlanet is not null ? ExpeditionKit(lander) ?? [] : [];
            foreach (ItemAmount i in pad.Def.LaunchFuelList.Prepend(new ItemAmount(pad.Def.Vessel!, 1)).Concat(kit.Select(kv => new ItemAmount(kv.Key, kv.Value))))
            {
                load[i.Item] = System.Math.Max(load.GetValueOrDefault(i.Item), i.Count);
            }
        }

        foreach ((string item, int n) in load.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            Building? other = WorldProbe.Storages(P).Where(st => (st.Id != lander.Id) && (st.Inv.Count(item) > 0)).OrderByDescending(st => st.Inv.Count(item)).ThenBy(st => st.Id).FirstOrDefault();
            if ((lander.Inv.Count(item) + (LaunchPad()?.Inv.Count(item) ?? 0) < n) && (other is not null))
            {
                needs.Add(new Job(JobKind.Haul, FromId: other.Id, ToId: lander.Id, Item: item));
            }
        }
    }

    /// <summary>Items on the pad that belong to the next outpost's or expedition's kit, up to what it needs: leave them there.</summary>
    private HashSet<string> KitItems(Building pad)
    {
        IEnumerable<KeyValuePair<string, int>> kit = (_outpostTrip is null) && (NextOutpost() is { } next) ? OutpostKit(next) : [];
        if (_offPlanet is not null)
        {
            kit = kit.Concat(_expeditionKit ?? []);
        }

        return kit.Where(kv => pad.Inv.Count(kv.Key) <= kv.Value).Select(kv => kv.Key).ToHashSet();
    }

    // ------------------------------------------------------------------ the trip

    /// <summary>
    /// Gets an outpost trip off the ground once the whole kit is made: the commander loads it onto the pad with
    /// the ferry rocket and its fuel, then flies out with every helper bot. True while there's work.
    /// </summary>
    private bool PrepareOutpost(Unit leader, Building lander)
    {
        if ((_outpostTrip is not null) || (NextOutpost() is not { } next) || (LaunchPad() is null))
        {
            return false;
        }

        (string Resource, int Planet)? saved = _offPlanet;
        _offPlanet = ("", next.Id);
        bool busy = PrepareExpedition(leader, lander, OutpostKit(next));
        _offPlanet = saved;
        if (_expedition is { Resource: "" } launched)
        {
            _expedition = null;
            _outpostTrip = (launched.Planet, _w.Tick, false);
        }

        return busy;
    }

    /// <summary>
    /// On the outpost's planet: lay out the outpost, have the helpers build it from the ferry's hold, run power
    /// to it, start the reactor with the cells brought along, then fly everyone home.
    /// </summary>
    private bool OutpostWork(Unit leader, Planet there, Building ferry, bool nearFerry)
    {
        (int planet, long since, bool placed) = _outpostTrip!.Value;
        Focus = there;
        try
        {
            BotPlan plan = new BotPlan();
            if (placed == false)
            {
                foreach ((BuildingDef def, Int2 origin, Dir rot) in LayOutOutpost(there, ferry))
                {
                    Send(new PlaceBuilding(def.Id, origin, rot));
                }

                _outpostTrip = (planet, since, true);

                return true;
            }

            Building? reactor = there.Buildings.FirstOrDefault(b => b.Def.Id == "fusion-reactor");
            bool cellsLeft = ferry.Inv.Count("fusion-cell") > 0;
            List<Unit> helpers = there.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == Player)).OrderBy(u => u.Id).ToList();
            List<Job> jobs = [];
            if ((reactor is not null) && cellsLeft && (reactor.Inv.Count("fusion-cell") < 10))
            {
                jobs.Add(new Job(JobKind.Haul, FromId: ferry.Id, ToId: reactor.Id, Item: "fusion-cell"));
            }

            if (there.Sites.Count > 0)
            {
                jobs.AddRange(helpers.Select(_ => new Job(JobKind.Build)));
            }

            for (int i = 0; i < helpers.Count; i++)
            {
                Job job = i < jobs.Count ? jobs[i] : new Job(JobKind.Idle);
                if ((helpers[i].Job != job) && ((job.Kind != JobKind.Build) || there.CanTakeJob(helpers[i], JobKind.Build)))
                {
                    Send(new AssignJob(helpers[i].Id, job));
                }
            }

            // Solar panels by the station, one a think (each placement sees the last).
            BuildingDef panel = C.Buildings["solar-panel"];
            int panels = there.Buildings.Count(b => b.Def == panel) + there.Sites.Count(x => x.DefId == panel.Id);
            if (Unlocked(panel.Id) && (panels < OutpostPanels) && (StationOn(there) is { } st) && (ferry.Inv.Count("photonic-wafer") >= 6))
            {
                PlaceNear(plan, panel.Id, Placement.Center(st), 3, 16);
            }

            // Power: a pole chain from the reactor to each machine and panel that isn't on its network yet.
            List<Building> unpowered = there.Buildings.Where(b => ((b.Def.Energy == EnergyKind.Electric) || b.Def.IsRenewable) && (b.Def.Kind != BuildingKind.Pole)
                    && ((b.NetworkId == 0) || (there.Networks.TryGetValue(b.NetworkId, out PowerNetwork? n) && (n.Generators.Count == 0))))
                .OrderBy(b => b.Id).ToList();
            if ((reactor is not null) && (there.Sites.Any(s => C.Buildings.TryGetValue(s.DefId, out BuildingDef? d) && (d.Kind == BuildingKind.Pole)) == false))
            {
                // One chain per think; one that can't be routed doesn't hold up the rest.
                foreach (Building target in unpowered)
                {
                    if (PlacePoleChain(plan, reactor, target))
                    {
                        break;
                    }

                    plan.Problems.Add($"couldn't route power to {target.Def.Name} on {there.Name}");
                }
            }

            bool done = (there.Sites.Count == 0) && (unpowered.Count == 0) && (reactor is not null) && ((reactor.Inv.Count("fusion-cell") >= 10) || (cellsLeft == false)) && ((Unlocked(panel.Id) == false) || (panels >= OutpostPanels));
            if ((done == false) && (_w.Tick - since < OutpostTicks))
            {
                return true;
            }

            _outpostTrip = null;
            Send(nearFerry ? new ReturnFerry(leader.Id, ferry.Id) : new WalkTo(leader.Id, ferry.Origin + new Int2(-1, 1)));

            return true;
        }
        finally
        {
            Focus = null;
        }
    }

    /// <summary>
    /// Where the outpost goes: an orbital station by the planet's ore, drills on its sides facing it, and a
    /// sorter arm on each remaining side feeding the reactor and the climate engine beyond it.
    /// </summary>
    private List<(BuildingDef Def, Int2 Origin, Dir Rot)> LayOutOutpost(Planet there, Building ferry)
    {
        List<ResourceDef> ores = OutpostOres(there);
        Int2 from = Placement.Center(ferry);
        List<BuildingDef> fed = [C.Buildings["fusion-reactor"]];
        if (GetsEngine(there))
        {
            fed.Add(C.Buildings["climate-engine"]);
        }

        // The first ore's station also feeds the reactor and the engine; each other ore gets a station of its own.
        List<(BuildingDef Def, Int2 Origin, Dir Rot)> parts = [];
        for (int i = 0; i < System.Math.Max(1, ores.Count); i++)
        {
            List<ResourceDef> mine = ores.Count > 0 ? [ores[i]] : [];
            Int2 near = mine.Select(r => there.FindNearestResource(from, r)).FirstOrDefault(t => t is not null) ?? from;
            parts.AddRange(Cluster(there, near, mine, i == 0 ? fed : [], parts));
        }

        return parts;
    }

    /// <summary>The best spot near a tile for a station ringed by drills on these ores, with sorter-fed buildings off its free edges.</summary>
    private List<(BuildingDef Def, Int2 Origin, Dir Rot)> Cluster(Planet there, Int2 near, List<ResourceDef> ores, List<BuildingDef> fed, List<(BuildingDef Def, Int2 Origin, Dir Rot)> taken)
    {
        BuildingDef station = C.Buildings["orbital-station"];
        BuildingDef drill = C.Buildings["electric-drill"];
        BuildingDef sorter = C.Buildings["sorter"];
        List<(BuildingDef Def, Int2 Origin, Dir Rot)> best = [];
        int bestDrills = -1;
        for (int r = 0; (r <= 24) && (bestDrills < OutpostDrills); r++)
        {
            foreach (Int2 d in Placement.Ring(r))
            {
                Int2 o = there.Torus.Wrap(near + d);
                if ((there.CanPlace(station, o, Dir.North) is not null) || Overlaps(there, taken, station, o, Dir.North))
                {
                    continue;
                }

                List<(BuildingDef Def, Int2 Origin, Dir Rot)> parts = [(station, o, Dir.North)];
                bool Free(BuildingDef def, Int2 at, Dir rot) => (there.CanPlace(def, at, rot) is null) && (Overlaps(there, parts, def, at, rot) == false) && (Overlaps(there, taken, def, at, rot) == false);

                // Drills on the station's sides, each facing it.
                (Int2 At, Dir Rot)[] slots =
                [
                    (new Int2(4, 0), Dir.West), (new Int2(4, 2), Dir.West), (new Int2(-2, 0), Dir.East), (new Int2(-2, 2), Dir.East),
                    (new Int2(0, -2), Dir.South), (new Int2(2, -2), Dir.South), (new Int2(0, 4), Dir.North), (new Int2(2, 4), Dir.North),
                ];
                foreach ((Int2 at, Dir rot) in slots)
                {
                    Int2 origin = there.Torus.Wrap(o + at);
                    int ore = Enumerable.Range(0, 2).SelectMany(x => Enumerable.Range(0, 2).Select(y => there.Torus.Wrap(origin + new Int2(x, y)))).Count(t => ores.Contains(there.ResourceAt(t)!));
                    if ((parts.Count(p => p.Def == drill) < OutpostDrills) && (ore >= 2) && Free(drill, origin, rot))
                    {
                        parts.Add((drill, origin, rot));
                    }
                }

                int drills = parts.Count(p => p.Def == drill);
                if ((ores.Count > 0) && (drills == 0))
                {
                    continue;
                }

                // A sorter on a free tile of the station's edge, and the fed building just beyond it.
                foreach (BuildingDef def in fed)
                {
                    bool placedOne = false;
                    foreach ((Int2 edge, Dir out_) in StationEdge(o))
                    {
                        Int2 at = there.Torus.Wrap(edge);
                        Int2 front = there.Torus.Wrap(at + Step(out_));
                        Int2 origin = there.Torus.Wrap(OriginTouching(front, out_, def.W, def.H) - (out_ is Dir.East or Dir.West ? new Int2(0, 1) : new Int2(1, 0)));
                        if ((Free(sorter, at, out_) == false) || (Free(def, origin, Dir.North) == false))
                        {
                            continue;
                        }

                        parts.Add((sorter, at, out_));
                        parts.Add((def, origin, Dir.North));
                        placedOne = true;
                        break;
                    }

                    if (placedOne == false)
                    {
                        parts.Clear();
                        break;
                    }
                }

                if ((parts.Count > 0) && (drills > bestDrills))
                {
                    best = parts;
                    bestDrills = drills;
                }
            }
        }

        return best;
    }

    /// <summary>Tiles just outside a 4x4 station's edge, with the direction pointing away from it.</summary>
    private static IEnumerable<(Int2 Tile, Dir Out)> StationEdge(Int2 o)
    {
        for (int i = 0; i < 4; i++)
        {
            yield return (o + new Int2(4, i), Dir.East);
            yield return (o + new Int2(-1, i), Dir.West);
            yield return (o + new Int2(i, 4), Dir.South);
            yield return (o + new Int2(i, -1), Dir.North);
        }
    }

    /// <summary>True if a building would share a tile (or touch, for a one-tile walkway) with any planned part.</summary>
    private static bool Overlaps(Planet there, List<(BuildingDef Def, Int2 Origin, Dir Rot)> parts, BuildingDef def, Int2 origin, Dir rot)
    {
        HashSet<Int2> mine = Tiles(there, def, origin, rot).ToHashSet();

        return parts.Any(p => Tiles(there, p.Def, p.Origin, p.Rot).Any(mine.Contains));
    }

    private static IEnumerable<Int2> Tiles(Planet there, BuildingDef def, Int2 origin, Dir rot)
    {
        (int w, int h) = Planet.SizeOf(def, rot);

        return Enumerable.Range(0, w).SelectMany(x => Enumerable.Range(0, h).Select(y => there.Torus.Wrap(origin + new Int2(x, y))));
    }
}
