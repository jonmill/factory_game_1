using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// Expeditions the way a player runs them once hand-gathering is slow: the ferry carries the parts for a few
/// burner drills and fuel for them, the helper bots put the drills down on the ore, keep them fuelled and
/// carry what they dig into the ferry's hold, and everyone left over digs by hand.
/// </summary>
public sealed partial class GoalBot
{
    /// <summary>Burner drills an expedition carries.</summary>
    private const int ExpeditionDrills = 6;

    /// <summary>Fuel an expedition carries for its drills.</summary>
    private const int ExpeditionFuel = 60;

    /// <summary>The kit for the expedition being prepared, fixed when it is first worked out so loading it doesn't shrink it.</summary>
    private Dictionary<string, int>? _expeditionKit;

    /// <summary>The parts for as many burner drills as storage can spare (up to <see cref="ExpeditionDrills"/>) and fuel for them.</summary>
    private Dictionary<string, int>? ExpeditionKit(Building lander)
    {
        if ((Params.Automate == false) || (_offPlanet is null))
        {
            _expeditionKit = null;

            return null;
        }

        if (_expeditionKit is not null)
        {
            return _expeditionKit;
        }

        BuildingDef drill = C.Buildings["burner-drill"];
        int sets = drill.CostList.Min(c => lander.Inv.Count(c.Item) / c.Count);
        int n = System.Math.Min(ExpeditionDrills, sets);
        Dictionary<string, int> kit = drill.CostList.ToDictionary(c => c.Item, c => c.Count * n);
        string? fuel = new[] { "kerogen-fuel", "compacted-carbon", "biomass" }.FirstOrDefault(f => C.Items.ContainsKey(f) && AcceptsFuel(drill, f) && (lander.Inv.Count(f) >= 20));
        if ((n > 0) && (fuel is not null))
        {
            kit[fuel] = kit.GetValueOrDefault(fuel) + System.Math.Min(ExpeditionFuel, lander.Inv.Count(fuel));
        }

        _expeditionKit = n > 0 ? kit : [];

        return _expeditionKit;
    }

    /// <summary>When the crew started taking the expedition's drills down, if it has.</summary>
    private long? _packUpSince;

    /// <summary>How long the crew may spend taking drills down before the ferry leaves without them.</summary>
    private const long PackUpTicks = 10 * 3600;

    /// <summary>
    /// The haul is in: cancel drills not yet built and take the built ones down, their parts going back into the
    /// ferry's hold. True while there's still some to do.
    /// </summary>
    private bool ExpeditionPackUp(Planet there)
    {
        Focus = there;
        try
        {
            _packUpSince ??= _w.Tick;
            if (_w.Tick - _packUpSince.Value > PackUpTicks)
            {
                return false;
            }

            BuildingDef drill = C.Buildings["burner-drill"];
            foreach (ConstructionSite site in there.Sites.Where(x => (x.Kind == SiteKind.Build) && (x.DefId == drill.Id)).OrderBy(x => x.Id))
            {
                Send(new CancelSite(site.Id));
            }

            HashSet<int> marked = there.Sites.Where(x => x.Kind == SiteKind.Deconstruct).Select(x => x.TargetId).ToHashSet();
            List<Building> drills = there.Buildings.Where(b => b.Def == drill).OrderBy(b => b.Id).ToList();
            foreach (Building d in drills.Where(d => marked.Contains(d.Id) == false))
            {
                Send(new DeconstructEntity(d.Id));
            }

            foreach (Unit h in there.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == Player) && (u.Job.Kind != JobKind.Build) && there.CanTakeJob(u, JobKind.Build)).OrderBy(u => u.Id))
            {
                Send(new AssignJob(h.Id, new Job(JobKind.Build)));
            }

            return (drills.Count > 0) || there.Sites.Any(x => x.DefId == drill.Id);
        }
        finally
        {
            Focus = null;
        }
    }

    private bool AcceptsFuel(BuildingDef def, string item) => C.IsFuel(item) && ((def.Fuels.Length == 0) || def.Fuels.Contains(item));

    /// <summary>
    /// On an expedition's planet: put the carried drills down on the ore, then keep them fuelled and emptied
    /// into the ferry; the other helper bots dig by hand.
    /// </summary>
    private void ExpeditionCrew(Planet there, Building ferry, List<ResourceDef> fetching)
    {
        Focus = there;
        try
        {
            BuildingDef drill = C.Buildings["burner-drill"];
            if (_expeditionDrillsPlaced == false)
            {
                _packUpSince = null;
                // The drills shared out over the resources being fetched.
                int sets = System.Math.Min(ExpeditionDrills, drill.CostList.Min(c => ferry.Inv.Count(c.Item) / c.Count));
                List<(BuildingDef Def, Int2 Origin, Dir Rot)> placed = [];
                for (int i = 0; i < fetching.Count; i++)
                {
                    int share = (sets / fetching.Count) + (i < sets % fetching.Count ? 1 : 0);
                    foreach (Int2 at in DrillSpots(there, ferry, fetching[i], drill, share, placed))
                    {
                        Send(new PlaceBuilding(drill.Id, at, Dir.North));
                        placed.Add((drill, at, Dir.North));
                    }
                }

                _expeditionDrillsPlaced = true;
            }

            List<Unit> helpers = there.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == Player)).OrderBy(u => u.Id).ToList();
            List<Building> drills = there.Buildings.Where(b => b.Def == drill).OrderBy(b => b.Id).ToList();
            string? fuel = ferry.Inv.Items.Select(kv => kv.Key).Where(i => AcceptsFuel(drill, i)).OrderBy(i => i, StringComparer.Ordinal).FirstOrDefault();
            List<Job> jobs = [];
            foreach (Building d in drills)
            {
                if ((fuel is not null) && (d.Inv.Items.Where(kv => C.IsFuel(kv.Key)).Sum(kv => kv.Value) < 4))
                {
                    jobs.Add(new Job(JobKind.Haul, FromId: ferry.Id, ToId: d.Id, Item: fuel));
                }

                if (d.Out.Total >= 5)
                {
                    jobs.Add(new Job(JobKind.Haul, FromId: d.Id, ToId: ferry.Id));
                }
            }

            int builders = System.Math.Min(there.Sites.Count, System.Math.Max(1, helpers.Count / 2));
            jobs.AddRange(Enumerable.Range(0, builders).Select(_ => new Job(JobKind.Build)));
            for (int i = 0; i < helpers.Count; i++)
            {
                Job job = i < jobs.Count ? jobs[i] : new Job(JobKind.Gather, Resource: fetching[i % fetching.Count].Id);
                if (((job.Kind == JobKind.Build) && (there.CanTakeJob(helpers[i], JobKind.Build) == false)) || ((job.Kind == JobKind.Gather) && (helpers[i].Job.Kind == JobKind.Gather) && (helpers[i].Job.Resource == job.Resource)))
                {
                    continue;
                }

                if (helpers[i].Job != job)
                {
                    Send(new AssignJob(helpers[i].Id, job));
                }
            }
        }
        finally
        {
            Focus = null;
        }
    }

    /// <summary>Up to this many free spots for a drill on the ore nearest the ferry, each covering at least two ore tiles.</summary>
    private static List<Int2> DrillSpots(Planet there, Building ferry, ResourceDef res, BuildingDef drill, int count, List<(BuildingDef Def, Int2 Origin, Dir Rot)> taken)
    {
        List<(BuildingDef Def, Int2 Origin, Dir Rot)> chosen = [];
        if ((count <= 0) || (there.FindNearestResource(Placement.Center(ferry), res) is not { } near))
        {
            return [];
        }

        for (int r = 0; (r <= 12) && (chosen.Count < count); r++)
        {
            foreach (Int2 d in Placement.Ring(r))
            {
                Int2 o = there.Torus.Wrap(near + d);
                if ((chosen.Count >= count) || (there.CanPlace(drill, o, Dir.North) is not null) || Overlaps(there, chosen, drill, o, Dir.North) || Overlaps(there, taken, drill, o, Dir.North))
                {
                    continue;
                }

                if (Tiles(there, drill, o, Dir.North).Count(t => there.ResourceAt(t) == res) >= 2)
                {
                    chosen.Add((drill, o, Dir.North));
                }
            }
        }

        return chosen.Select(c => c.Origin).ToList();
    }
}
