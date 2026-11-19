using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// How the bot automates its home planet, the way a player does once the first parts are in hand: drill
/// columns on every ore patch it needs (a drill pushing straight into a smelter, or into a crate for ore
/// that isn't smelted), fabricator cells for the parts it keeps crafting (a fabricator emptied by a sorter
/// arm into a crate), more research stations and power as the base grows, and more helper bots to keep it
/// all supplied. Everything is re-derived from the world each think.
/// </summary>
public sealed partial class GoalBot
{
    /// <summary>Resources the bot always mines with drills once it can: the staples of every recipe.</summary>
    private static readonly string[] _staples = ["siderite", "cuprite", "carbon", "regolith"];

    /// <summary>The automation research a player takes on the way to the settlers, in order.</summary>
    private static readonly string[] _factoryPath =
    [
        "automation", "mechanical-arms", "robot-modules", "analysis-cores", "electric-mining", "robotics", "arc-smelting", "kerogen-refining",
        "advanced-helpers", "atmospheric-processing", "hydro-seeding", "bio-engineering", "precision-fabrication", "solar-power",
        "fusion-power", "climate-engineering", "plasma-smelting", "plasma-mining", "plasma-fabrication", "lanthanide-catalysis",
    ];

    /// <summary>Plates and parts kept in stock ahead of need once the factory runs.</summary>
    private static readonly string[] _standingStock = ["siderite-plate", "cupral-filament", "sintered-brick", "drive-cog", "logic-board"];

    /// <summary>Buildings the bot is taking down on purpose (worked-out columns): cleanup leaves those orders alone.</summary>
    private readonly HashSet<int> _retiring = [];

    /// <summary>A drill and the building it feeds.</summary>
    private readonly record struct Column(Building Drill, Building? Into, string Resource);

    /// <summary>The age the bot has reached: 0 Salvage, 1 Combustion, 2 Clean, 3 Stellar.</summary>
    private int AgeReached() => C.AgeList.Select((a, i) => (a, i)).Where(x => C.TechList.Any(t => (t.Age == x.a.Id) && _w.Research.Completed.Contains(t.Id))).Select(x => x.i).DefaultIfEmpty(0).Max();

    private List<Column> Columns() => P.Buildings.Where(b => b.Def.Kind == BuildingKind.Drill)
        .Select(b => new Column(b, P.BuildingAt(b.FrontTile()), b.Footprint().Select(t => P.ResourceAt(t)).FirstOrDefault(r => r is { IsTree: false })?.Id ?? ""))
        .ToList();

    /// <summary>Furnaces fed by a drill: nobody needs to carry ore to them.</summary>
    private HashSet<int> DrillFedFurnaces() => Columns().Where(c => c.Into is { Def.Kind: BuildingKind.Furnace }).Select(c => c.Into!.Id).ToHashSet();

    /// <summary>
    /// A drill column whose furnace holds some other ore (a stray handful too small to smelt) can't take its
    /// drill's ore and stands still: the commander takes the stray ore out. True while it's on the way.
    /// </summary>
    private bool ClearJammedColumn(Unit leader)
    {
        string? Stray(Column c) => C.Resources.TryGetValue(c.Resource, out ResourceDef? rd)
            ? c.Into!.Inv.Items.Select(kv => kv.Key).FirstOrDefault(i => (C.IsFuel(i) == false) && (i != rd.Item))
            : null;
        if (Columns().Where(c => (c.Into is { Def.Kind: BuildingKind.Furnace } f) && (f.Crafting == false) && (c.Drill.Status == MachineStatus.OutputFull) && (Stray(c) is not null))
                .OrderBy(c => c.Drill.Id).Cast<Column?>().FirstOrDefault() is not { } jammed)
        {
            return false;
        }

        Building furnace = jammed.Into!;
        if (P.InLeaderReach(leader, furnace.Origin))
        {
            Send(new TakeFromBuilding(leader.Id, furnace.Id, Stray(jammed), int.MaxValue, InventoryPart.Input));
        }
        else
        {
            WalkTowards(leader, P.Torus.Wrap(furnace.Origin + new Int2(0, furnace.H)));
        }

        return true;
    }

    /// <summary>Resources a drill is mining right now: nobody needs to dig them by hand.</summary>
    private HashSet<string> DrilledResources() => Columns().Where(c => c.Drill.Status == MachineStatus.Working).Select(c => c.Resource).ToHashSet();

    /// <summary>What the bot burns: compacted carbon once a drill mines it, else what the parameters say.</summary>
    private string FuelResource() => Params.Automate && DrilledResources().Contains("carbon") ? "carbon" : Params.FuelResource;

    /// <summary>The automation the bot runs each think, after demand is known.</summary>
    private void PlanFactory(BotPlan plan, Building lander)
    {
        if (Params.Automate == false)
        {
            return;
        }

        PlanMining(plan, lander);
        PlanFreeSmelters(plan, lander);
        PlanFabricators(plan, lander);
        PlanWorkforce(plan, lander);
        PlanPowerGrowth(plan, lander);
        PlanStellar(plan, lander);
        AssignFabricatorRecipes(plan);
        AssignMachineRecipes(plan);
    }

    // ------------------------------------------------------------------ mining

    /// <summary>The best drill researched, and what it feeds smelted ore into.</summary>
    private (BuildingDef Drill, BuildingDef? Smelter, bool Electric) BestMiningKit()
    {
        BuildingDef? drill = C.BuildingList.Where(b => (b.Kind == BuildingKind.Drill) && _w.Research.Unlocked.Contains(b.Id) && (b.W == 2))
            .OrderByDescending(b => b.Speed * 1000 / b.MiningTicks).FirstOrDefault();
        BuildingDef? smelter = C.BuildingList.Where(b => (b.Kind == BuildingKind.Furnace) && _w.Research.Unlocked.Contains(b.Id) && (b.W == 2))
            .OrderByDescending(b => b.Speed).FirstOrDefault();
        bool electric = (drill?.Energy == EnergyKind.Electric) && (smelter?.Energy == EnergyKind.Electric) && _w.Research.Unlocked.Contains("sorter")
                        && P.Buildings.Any(b => b.Def.IsGenerator);
        if (electric == false)
        {
            drill = C.BuildingList.FirstOrDefault(b => (b.Kind == BuildingKind.Drill) && (b.Energy == EnergyKind.Burner) && _w.Research.Unlocked.Contains(b.Id));
            smelter = C.BuildingList.FirstOrDefault(b => (b.Kind == BuildingKind.Furnace) && (b.Energy == EnergyKind.Burner) && _w.Research.Unlocked.Contains(b.Id));
        }

        return (drill!, smelter, electric);
    }

    /// <summary>The most drill columns the bot keeps on one resource at the age it has reached.</summary>
    private int MiningCap(string res) => (_staples.Contains(res) ? 3 : 2) + (2 * AgeReached());

    private void PlanMining(BotPlan plan, Building lander)
    {
        (BuildingDef? drill, BuildingDef? smelter, bool electric) = BestMiningKit();
        if ((drill is null) || HasSite(drill.Id))
        {
            return; // one column at a time
        }

        // A worked-out column comes down (its parts are refunded) so a new one can go up on fresh ore.
        if (Columns().Where(c => (c.Drill.Status == MachineStatus.NoResource) && (P.Sites.Any(x => x.TargetId == c.Drill.Id) == false)).Cast<Column?>().FirstOrDefault() is { } dead)
        {
            _retiring.Add(dead.Drill.Id);
            Send(new DeconstructEntity(dead.Drill.Id));
            if (dead.Into is { Def.Kind: BuildingKind.Furnace } smelterIn)
            {
                _retiring.Add(smelterIn.Id);
                Send(new DeconstructEntity(smelterIn.Id));
                if (P.Buildings.FirstOrDefault(s => (s.Def.Kind == BuildingKind.Sorter) && (P.BuildingAt(s.BackTile())?.Id == smelterIn.Id)) is { } arm)
                {
                    _retiring.Add(arm.Id);
                    Send(new DeconstructEntity(arm.Id));
                }
            }
        }

        List<Column> columns = Columns().Where(c => c.Drill.Status != MachineStatus.NoResource).ToList();
        HashSet<string> wanted = plan.RawDeficit.Keys.Where(P.HasResource).Concat(_staples.Where(P.HasResource)).ToHashSet();
        if (_w.Research.Unlocked.Contains("kerogen-refinery") && P.HasResource("kerogen"))
        {
            wanted.Add("kerogen");
        }

        foreach (string res in wanted.Where(r => C.Resources[r].IsTree == false).OrderByDescending(r => plan.RawDeficit.GetValueOrDefault(r)).ThenBy(r => r, StringComparer.Ordinal))
        {
            List<Column> mine = columns.Where(c => c.Resource == res).ToList();
            bool busy = mine.All(c => c.Drill.Status == MachineStatus.Working);
            bool behind = plan.RawDeficit.GetValueOrDefault(res) > 0;
            if ((mine.Count == 0) || ((mine.Count < MiningCap(res)) && busy && behind))
            {
                if (PlaceColumn(plan, lander, res, drill, smelter, electric))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Smelters no drill feeds, for ore that is carried in: from another planet, from the orbital collector,
    /// or dug by hand. More of them as the game goes on.
    /// </summary>
    private void PlanFreeSmelters(BotPlan plan, Building lander)
    {
        HashSet<int> fed = DrillFedFurnaces();
        int free = P.Buildings.Count(b => (b.Def.Kind == BuildingKind.Furnace) && (fed.Contains(b.Id) == false) && (_retiring.Contains(b.Id) == false));
        bool pending = P.Sites.Any(s => (s.Kind == SiteKind.Build) && C.Buildings.TryGetValue(s.DefId, out BuildingDef? d) && (d.Kind == BuildingKind.Furnace));
        if (pending || (free >= 2 + AgeReached()))
        {
            return;
        }

        (BuildingDef _, BuildingDef? smelter, bool _) = BestMiningKit();
        if (smelter is not null)
        {
            PlaceNear(plan, smelter.Id, Placement.Center(lander), 4, 30);
        }
    }

    /// <summary>The smelting recipe that takes this resource's ore, if any.</summary>
    private RecipeDef? SmeltingOf(string res) =>
        C.RecipeList.FirstOrDefault(r => (r.Category == "smelting") && r.In.ContainsKey(C.Resources[res].Item) && _w.Research.Unlocked.Contains(r.Id));

    /// <summary>Where a building must stand so that a tile is part of it, given the side it is approached from.</summary>
    private static Int2 OriginTouching(Int2 front, Dir from, int w, int h) => from switch
    {
        Dir.West => front - new Int2(w - 1, 0),
        Dir.North => front - new Int2(0, h - 1),
        _ => front,
    };

    private static Int2 Step(Dir d) => d switch
    {
        Dir.East => new Int2(1, 0),
        Dir.West => new Int2(-1, 0),
        Dir.North => new Int2(0, -1),
        _ => new Int2(0, 1),
    };

    /// <summary>
    /// Places one drill column on the nearest free ore of a resource: drill → smelter (→ sorter → crate once
    /// electric) for ores that are smelted, drill → crate for the rest. Keeps a walkway around the whole column.
    /// </summary>
    private bool PlaceColumn(BotPlan plan, Building lander, string res, BuildingDef drill, BuildingDef? smelter, bool electric)
    {
        ResourceDef rd = C.Resources[res];
        RecipeDef? smelt = SmeltingOf(res);
        bool smelts = (smelt is not null) && (smelter is not null) && (res != "carbon");
        BuildingDef into = smelts ? smelter! : C.Buildings["chest"];
        BuildingDef sorter = C.Buildings["sorter"];
        BuildingDef chest = C.Buildings["chest"];
        Int2 home = Placement.Center(lander);

        // Ore tiles of this resource, nearest the lander first.
        List<Int2> tiles = OreTiles(rd, home, 60);
        foreach (Int2 t in tiles)
        {
            foreach (Dir rot in new[] { Dir.East, Dir.South, Dir.West, Dir.North })
            {
                (int dw, int dh) = Planet.SizeOf(drill, rot);
                Int2 d = t;
                if (Enumerable.Range(0, dw).SelectMany(x => Enumerable.Range(0, dh).Select(y => P.Torus.Wrap(d + new Int2(x, y)))).Count(x => P.ResourceAt(x) == rd) < 3)
                {
                    continue;
                }

                if (P.CanPlace(drill, d, rot) is not null)
                {
                    continue;
                }

                Int2 front = FrontOf(d, dw, dh, rot);
                Int2 intoOrigin = OriginTouching(front, rot, into.W, into.H);
                if (P.CanPlace(into, intoOrigin, rot) is not null)
                {
                    continue;
                }

                List<(BuildingDef Def, Int2 Origin)> parts = [(drill, d), (into, intoOrigin)];
                if (smelts && electric)
                {
                    Int2 sorterAt = FrontOf(intoOrigin, into.W, into.H, rot);
                    Int2 crateAt = P.Torus.Wrap(sorterAt + Step(rot));
                    if ((P.CanPlace(sorter, sorterAt, rot) is not null) || (P.CanPlace(chest, crateAt, rot) is not null))
                    {
                        continue;
                    }

                    parts.Add((sorter, sorterAt));
                    parts.Add((chest, crateAt));
                }

                if (parts.Any(p => Crowded(p.Def, p.Origin, rot, parts)))
                {
                    continue;
                }

                foreach ((BuildingDef def, Int2 origin) in parts)
                {
                    Send(new PlaceBuilding(def.Id, origin, rot));
                    plan.Placed.Add(def.Id);
                    plan.PlannedCosts.AddRange(def.CostList);
                }

                return true;
            }
        }

        plan.Problems.Add($"no room for a {rd.Name} drill column");

        return false;
    }

    /// <summary>The tile just outside a footprint's front face, as <see cref="Building.FrontTile"/> computes it.</summary>
    private Int2 FrontOf(Int2 origin, int w, int h, Dir rot) => P.Torus.Wrap(rot switch
    {
        Dir.East => new Int2(origin.X + w, origin.Y + ((h - 1) / 2)),
        Dir.West => new Int2(origin.X - 1, origin.Y + ((h - 1) / 2)),
        Dir.North => new Int2(origin.X + ((w - 1) / 2), origin.Y - 1),
        _ => new Int2(origin.X + ((w - 1) / 2), origin.Y + h),
    });

    /// <summary>True if a building of the column would touch anything that isn't part of the same column (keeps walkways open).</summary>
    private bool Crowded(BuildingDef def, Int2 origin, Dir rot, List<(BuildingDef Def, Int2 Origin)> parts)
    {
        (int w, int h) = Planet.SizeOf(def, rot);
        HashSet<Int2> own = parts.SelectMany(p =>
        {
            (int pw, int ph) = Planet.SizeOf(p.Def, rot);

            return Enumerable.Range(0, pw).SelectMany(x => Enumerable.Range(0, ph).Select(y => P.Torus.Wrap(p.Origin + new Int2(x, y))));
        }).ToHashSet();
        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                Int2 t = P.Torus.Wrap(origin + new Int2(x, y));
                if (own.Contains(t))
                {
                    continue;
                }

                if ((P.OccupantAt(t) != 0) || P.IsRail(t) || (P.TerrainAt(t) != Terrain.Ground))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tiles of a resource within a radius of a point, nearest first (deterministic).</summary>
    private List<Int2> OreTiles(ResourceDef res, Int2 near, int radius)
    {
        List<Int2> found = [];
        for (int r = 0; r <= radius; r++)
        {
            foreach (Int2 d in Placement.Ring(r))
            {
                Int2 t = P.Torus.Wrap(near + d);
                if ((P.ResourceAt(t) == res) && (P.OccupantAt(t) == 0))
                {
                    found.Add(t);
                }
            }

            if (found.Count > 400)
            {
                break;
            }
        }

        return found;
    }

    // ------------------------------------------------------------------ fabricators

    /// <summary>The fastest fabricator researched.</summary>
    private BuildingDef? BestFabricator() => C.BuildingList.Where(b => (b.Kind == BuildingKind.Assembler) && b.Categories.Contains("crafting") && _w.Research.Unlocked.Contains(b.Id))
        .OrderByDescending(b => b.Speed).FirstOrDefault();

    /// <summary>Fabricators on the home planet that craft (not refineries or collectors).</summary>
    private List<Building> Fabricators() => P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Assembler) && b.Def.Categories.Contains("crafting")).ToList();

    private int FabricatorCap() => 4 + (5 * AgeReached());

    /// <summary>Recipes worth a fabricator: crafted in volume, not one-off machines or vessels.</summary>
    private bool WorthAFabricator(RecipeDef r) => (r.Category == "crafting") && (r.Effect is null) && (C.Items[r.Outputs[0].Item].Module is null)
                                                  && (C.Items[r.Outputs[0].Item].Stack >= 50);

    private void PlanFabricators(BotPlan plan, Building lander)
    {
        BuildingDef? fab = BestFabricator();
        if ((fab is null) || (P.Buildings.Any(b => b.Def.IsGenerator) == false) || P.Sites.Any(s => C.Buildings.TryGetValue(s.DefId, out BuildingDef? d) && (d.Kind == BuildingKind.Assembler)))
        {
            return;
        }

        List<Building> fabs = Fabricators();
        if (fabs.Count + 1 > FabricatorCap())
        {
            return;
        }

        // The craft with the biggest backlog that has no fabricator, or whose fabricators are all busy and far behind.
        foreach ((string item, long deficit) in plan.CraftDeficit.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            RecipeDef r = WorldProbe.RecipeFor(C, item)!;
            if (WorthAFabricator(r) == false)
            {
                continue;
            }

            List<Building> making = fabs.Where(f => f.RecipeId == r.Id).ToList();
            bool more = (making.Count == 0) ? deficit >= 5 : (making.Count < 4) && making.All(f => f.Status == MachineStatus.Working) && (deficit > 40 * making.Count);
            if (more == false)
            {
                continue;
            }

            if (PlaceFabricatorCell(plan, lander, fab))
            {
                return;
            }

            return;
        }
    }

    /// <summary>A fabricator with a sorter arm emptying it into a crate on its east side, in a ring around the lander.</summary>
    private bool PlaceFabricatorCell(BotPlan plan, Building lander, BuildingDef fab)
    {
        BuildingDef sorter = C.Buildings["sorter"];
        BuildingDef chest = C.Buildings["chest"];
        Int2 home = Placement.Center(lander);
        for (int r = 6; r <= 40; r++)
        {
            foreach (Int2 d in Placement.Ring(r))
            {
                Int2 origin = P.Torus.Wrap(home + d);
                if ((P.CanPlace(fab, origin, Dir.North) is not null) || HasOreUnder(origin, fab.W, fab.H))
                {
                    continue;
                }

                Int2 sorterAt = P.Torus.Wrap(origin + new Int2(fab.W, 1));
                Int2 crateAt = P.Torus.Wrap(origin + new Int2(fab.W + 1, 1));
                if ((P.CanPlace(sorter, sorterAt, Dir.East) is not null) || (P.CanPlace(chest, crateAt, Dir.North) is not null))
                {
                    continue;
                }

                List<(BuildingDef Def, Int2 Origin)> parts = [(fab, origin), (sorter, sorterAt), (chest, crateAt)];
                if (parts.Any(p => Crowded(p.Def, p.Origin, Dir.North, parts)))
                {
                    continue;
                }

                Send(new PlaceBuilding(fab.Id, origin, Dir.North));
                Send(new PlaceBuilding(sorter.Id, sorterAt, Dir.East));
                Send(new PlaceBuilding(chest.Id, crateAt, Dir.North));
                foreach ((BuildingDef def, Int2 _) in parts)
                {
                    plan.Placed.Add(def.Id);
                    plan.PlannedCosts.AddRange(def.CostList);
                }

                return true;
            }
        }

        plan.Problems.Add("no room for a fabricator cell");

        return false;
    }

    private bool HasOreUnder(Int2 origin, int w, int h) =>
        Enumerable.Range(0, w).SelectMany(x => Enumerable.Range(0, h).Select(y => P.Torus.Wrap(origin + new Int2(x, y)))).Any(t => P.ResourceAt(t) is { IsTree: false });

    /// <summary>
    /// Refineries, orbital collectors and the like run whatever recipe of their category is most wanted; a
    /// collector (whose recipes take nothing) is switched as soon as its product isn't wanted any more.
    /// </summary>
    private void AssignMachineRecipes(BotPlan plan)
    {
        HashSet<string> taken = [];
        foreach (Building m in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Assembler) && (b.Def.Categories.Contains("crafting") == false)).OrderBy(b => b.Id))
        {
            // Fuel for a waiting expedition first, then the biggest backlog; a second machine of the kind takes the next one.
            HashSet<string> launch = P.Buildings.Where(b => b.Def.Kind == BuildingKind.LaunchPad).SelectMany(b => b.Def.LaunchFuelList).Select(i => i.Item).ToHashSet();
            List<RecipeDef> wanted = plan.CraftDeficit.OrderByDescending(kv => ((_offPlanet is not null) || (NextOutpost() is not null)) && launch.Contains(kv.Key)).ThenByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => WorldProbe.RecipeFor(C, kv.Key)!).Where(r => m.Def.Categories.Contains(r.Category)).ToList();
            RecipeDef? pick = wanted.FirstOrDefault(r => taken.Contains(r.Id) == false) ?? wanted.FirstOrDefault();
            if (pick is not null)
            {
                taken.Add(pick.Id);
            }

            bool idleOrSurplus = (m.Crafting == false) || (m.Recipe is { } cur && (plan.CraftDeficit.ContainsKey(cur.Outputs[0].Item) == false))
                                 || ((pick is not null) && ((_offPlanet is not null) || (NextOutpost() is not null)) && launch.Contains(pick.Outputs[0].Item));
            if ((pick is not null) && (pick.Id != m.RecipeId) && idleOrSurplus)
            {
                Send(new SetRecipe(m.Id, pick.Id));
            }
            else if ((pick is null) && (m.Recipe is { Inputs.Length: 0 } idle) && Surplus(plan, idle))
            {
                Send(new SetRecipe(m.Id, null));
            }
        }
    }

    /// <summary>True when a machine's product isn't wanted and there is plenty of it in store: let it stop.</summary>
    private bool Surplus(BotPlan plan, RecipeDef r) =>
        (r.Outputs.Length > 0) && (plan.CraftDeficit.ContainsKey(r.Outputs[0].Item) == false) && (WorldProbe.StorageCount(P, r.Outputs[0].Item) >= StockpileCap());

    /// <summary>Sets every idle fabricator to the craft that needs one most; retargets fabricators whose product is no longer wanted.</summary>
    private void AssignFabricatorRecipes(BotPlan plan)
    {
        List<Building> fabs = Fabricators();
        List<RecipeDef> wanted = plan.CraftDeficit.OrderByDescending(kv => plan.CraftDepth[kv.Key]).ThenByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => WorldProbe.RecipeFor(C, kv.Key)!).Where(WorthAFabricator).ToList();
        foreach (Building f in fabs.OrderBy(f => f.Id))
        {
            bool unwanted = (f.Recipe is not null) && Surplus(plan, f.Recipe) && (wanted.Count > 0);
            if ((f.RecipeId is not null) && (unwanted == false))
            {
                continue;
            }

            RecipeDef? pick = wanted.FirstOrDefault(r => fabs.Any(x => x.RecipeId == r.Id) == false) ?? wanted.FirstOrDefault();
            if ((pick is not null) && (pick.Id != f.RecipeId))
            {
                Send(new SetRecipe(f.Id, pick.Id));
            }
        }
        // A fabricator starved of an ingredient that is itself short moves upstream to make it, one per pass,
        // while more fabricators make the product than its ingredient: the chain finds its own ratio.
        foreach (Building f in fabs.Where(f => (f.Crafting == false) && (f.Status == MachineStatus.NoInput) && (f.Recipe is not null)).OrderBy(f => f.Id))
        {
            RecipeDef r = f.Recipe!;
            RecipeDef? upstream = r.Inputs.Where(i => (f.Inv.Count(i.Item) < i.Count) && (WorldProbe.StorageCount(P, i.Item) < i.Count))
                .Select(i => wanted.FirstOrDefault(w => w.Outputs.Any(o => o.Item == i.Item))).FirstOrDefault(w => w is not null);
            if ((upstream is not null) && (fabs.Count(x => x.RecipeId == r.Id) > 1) && (fabs.Count(x => x.RecipeId == upstream.Id) < fabs.Count(x => x.RecipeId == r.Id)))
            {
                Send(new SetRecipe(f.Id, upstream.Id));

                return;
            }
        }
    }

    /// <summary>
    /// Machine jobs: carry each missing ingredient from storage to every fabricator, refinery and the like,
    /// and empty those that have no sorter arm of their own into storage.
    /// </summary>
    private void FeedFabricators(BotPlan plan, List<Job> needs, Func<string[], int> sourceFor, Building lander)
    {
        if (Params.Automate == false)
        {
            return;
        }

        int budget = System.Math.Max(1, P.Units.Count(u => (u.IsLeader == false) && (u.OwnerPlayer == Player)) / 2);
        int start = needs.Count;
        foreach (Building f in P.Buildings.Where(b => (b.Def.Kind == BuildingKind.Assembler) && (b.Recipe is not null)).OrderBy(f => f.Id))
        {
            if (needs.Count - start >= budget)
            {
                return; // the rest of the helpers have other work
            }

            RecipeDef r = f.Recipe!;
            bool surplus = (r.Outputs.Length > 0) && Surplus(plan, r);
            // Emptied by its arm into a crate, unless that crate is full (then carry the output to storage too).
            Building? arm = P.Buildings.FirstOrDefault(s => (s.Def.Kind == BuildingKind.Sorter) && (P.BuildingAt(s.BackTile())?.Id == f.Id));
            bool armWorks = arm is not null && P.BuildingAt(arm.FrontTile()) is { } crate && f.Out.Items.All(kv => crate.Inv.SpaceFor(kv.Key) > 0);
            bool leftovers = f.Out.Items.Any(kv => r.Outputs.All(o => o.Item != kv.Key));
            if (((armWorks == false) || leftovers) && ((f.Out.Total >= 10) || leftovers) && (f.Out.Items.Select(kv => kv.Key).FirstOrDefault() is { } made) && (StoreFor(made, lander) is { } into))
            {
                needs.Add(new Job(JobKind.Haul, FromId: f.Id, ToId: into.Id));
            }

            foreach (ItemAmount i in r.Inputs.Where(_ => surplus == false))
            {
                if ((f.Inv.Count(i.Item) < i.Count * 2) && (P.AcceptAmount(f, i.Item) >= i.Count) && (WorldProbe.StorageCount(P, i.Item) >= i.Count))
                {
                    needs.Add(new Job(JobKind.Haul, FromId: sourceFor([i.Item]), ToId: f.Id, Item: i.Item));
                }
            }
        }
    }

    // ------------------------------------------------------------------ workforce

    /// <summary>How many helper bots the bot wants at the age it has reached.</summary>
    private int HelperTarget() => 2 + (4 * AgeReached());

    private void PlanWorkforce(BotPlan plan, Building lander)
    {
        BuildingDef bay = C.BuildingList.First(b => b.Kind == BuildingKind.RobotBay);
        int helpers = P.Units.Count(u => (u.IsLeader == false) && (u.OwnerPlayer == Player));
        if ((helpers < HelperTarget()) && _w.Research.Unlocked.Contains(bay.Id) && (CountOf(BuildingKind.RobotBay) == 0))
        {
            PlaceNear(plan, bay.Id, Placement.Center(lander), 5, 20);
        }

        foreach (Building b in P.Buildings.Where(b => b.Def.Kind == BuildingKind.RobotBay))
        {
            string? best = C.RecipeList.Where(r => b.Def.Categories.Contains(r.Category) && _w.Research.Unlocked.Contains(r.Id)).Select(r => r.Id).LastOrDefault();
            if ((best is not null) && (b.RecipeId != best) && (b.Crafting == false))
            {
                Send(new SetRecipe(b.Id, best));
            }
        }
    }

    /// <summary>Labs wanted: more each age, as research grows dearer (at least the bot's own setting).</summary>
    private int LabTarget() => Params.Automate ? System.Math.Max(Params.Labs, 1 + (2 * AgeReached())) : Params.Labs;

    /// <summary>How much of each raw resource spare helpers stockpile before they stand by.</summary>
    private int StockpileCap() => 200 * (1 + AgeReached());

    /// <summary>Where to put an item: the lander while it has room for a stack, else the nearest crate that does.</summary>
    private Building? StoreFor(string item, Building lander) =>
        lander.Inv.SpaceFor(item) >= 10 ? lander
            : WorldProbe.Storages(P).Where(st => (st.Def.Kind != BuildingKind.Ferry) && (st.Inv.SpaceFor(item) >= 10))
                .OrderBy(st => P.SubTorus.DistSq(lander.CenterSub, st.CenterSub)).ThenBy(st => st.Id).FirstOrDefault();

    /// <summary>The pole the bot runs power lines with: lattice pylons once researched (no biomass, which runs out with the trees).</summary>
    private string PoleId() => _w.Research.Unlocked.Contains("lattice-pylon") ? "lattice-pylon" : "power-pole";

    /// <summary>
    /// The fuel to carry to a burner: the richest one in storage that it burns, keeping biomass (the trees run
    /// out) for when there's nothing else, and rocket fuel for rockets while another fuel is plentiful.
    /// </summary>
    private string BestFuel(Building b, List<string> inStorage, string preferred)
    {
        List<string> burns = inStorage.Where(i => (b.Def.Fuels.Length == 0) ? (C.Items[i].FuelKj < 100_000) : b.Def.Fuels.Contains(i)).ToList();
        if (Params.Automate == false)
        {
            return burns.Contains(preferred) ? preferred : burns.FirstOrDefault() ?? inStorage[0];
        }

        // Rocket fuel goes to rockets while there's plenty of another fuel to burn instead.
        HashSet<string> launch = C.BuildingList.Where(d => d.Kind == BuildingKind.LaunchPad).SelectMany(d => d.LaunchFuelList).Select(i => i.Item).ToHashSet();
        bool spare = burns.Any(i => (i != "biomass") && (launch.Contains(i) == false) && (WorldProbe.StorageCount(P, i) >= 50));

        return burns.OrderBy(i => i == "biomass" ? 2 : spare && launch.Contains(i) ? 1 : 0).ThenByDescending(i => C.Items[i].FuelKj).ThenBy(i => i, StringComparer.Ordinal).FirstOrDefault() ?? inStorage[0];
    }

    /// <summary>Whether the bot should still be assembling helper bots.</summary>
    private bool WantsHelpers() => Params.Automate && (P.Units.Count(u => (u.IsLeader == false) && (u.OwnerPlayer == Player)) < HelperTarget());

    // ------------------------------------------------------------------ power

    /// <summary>When the grid falls short, adds generation: solar once researched (no fuel to haul), else another steam engine.</summary>
    private void PlanPowerGrowth(BotPlan plan, Building lander)
    {
        if (P.Sites.Any(s => C.Buildings.TryGetValue(s.DefId, out BuildingDef? d) && (d.IsGenerator || (d.Kind == BuildingKind.Pole))))
        {
            return;
        }

        Building? engine = P.Buildings.FirstOrDefault(b => b.Def.IsGenerator && (b.NetworkId != 0));
        if (engine is null)
        {
            return;
        }

        // A generator that isn't wired in yet: run poles to it first.
        if (P.Buildings.FirstOrDefault(b => b.Def.IsGenerator && (b.NetworkId == 0)) is { } loose)
        {
            PlacePoleChain(plan, engine, loose);

            return;
        }

        bool powerShort = P.Networks.Values.Any(n => (n.Generators.Count > 0) && (n.Satisfaction < 950));
        if (powerShort == false)
        {
            return;
        }

        string gen = _w.Research.Unlocked.Contains("solar-panel") ? "solar-panel" : "steam-engine";
        PlaceNear(plan, gen, Placement.Center(gen == "steam-engine" ? engine : lander), 2, 40);
    }
}
