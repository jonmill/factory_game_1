using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// Adversarial player: issues random, frequently silly commands (build junk, dismantle things, send
/// followers on pointless hauls, waste materials on crafts, wreck schedules). Used to push the game
/// into unusual states, then a GoalBot tries to recover. Also exercises command validation.
/// </summary>
public sealed class ChaosBot(ulong seed, int actionsPerMinute = 30, int player = 0) : IBot
{
    private readonly Rng _rng = new(seed);
    /// <inheritdoc />
    public string Name => $"ChaosBot({seed})";
    /// <summary>Number of commands this bot has submitted so far.</summary>
    public int ActionsSubmitted { get; private set; }

    private T? Pick<T>(IReadOnlyList<T> list) => list.Count == 0 ? default : list[_rng.Range(0, list.Count)];

    /// <inheritdoc />
    public void Tick(World world)
    {
        int interval = System.Math.Max(1, 3600 / System.Math.Max(1, actionsPerMinute));
        if (world.Tick % interval != 0)
        {
            return;
        }

        Planet p = world.Planets[0];
        ContentDb c = world.Content;
        Building? lander = world.Get<Building>(p.LanderId);
        if (lander is null)
        {
            return;
        }

        Int2 near = lander.Origin;
        Int2 RandTile(int r) => p.Torus.Wrap(near + new Int2(_rng.Range(-r, r + 1), _rng.Range(-r, r + 1)));
        List<Unit> followers = p.Units.Where(u => u.IsLeader == false).ToList();
        Unit? leader = p.Units.FirstOrDefault(u => u.IsLeader && (u.OwnerPlayer == player));
        Unit? anyLeader = world.Planets.SelectMany(q => q.Units).FirstOrDefault(u => u.IsLeader && (u.OwnerPlayer == player));
        List<Building> buildings = p.Buildings;
        Dir RandDir() => (Dir)_rng.Range(0, 4);

        Command? cmd = _rng.Range(0, 100) switch
        {
            < 22 => Pick(c.BuildingList.Where(b => b.Buildable && world.Research.Unlocked.Contains(b.Id)).ToList()) is { } def
                ? new PlaceBuilding(def.Id, RandTile(18), RandDir()) : null,
            < 30 => RailLine(p, RandTile(15), RandDir()),
            < 40 => Pick(buildings.Where(b => b.Def.Kind != BuildingKind.Lander).ToList()) is { } b ? new DeconstructEntity(b.Id) : null,
            < 44 => Pick(p.Sites) is { } s ? new CancelSite(s.Id) : null,
            < 62 => Pick(followers) is { } u ? new AssignJob(u.Id, RandomJob(world, p)) : null,
            < 72 => Pick(c.RecipeList.Where(r => (r.Category == "crafting") && world.Research.Unlocked.Contains(r.Id)).ToList()) is { } r
                ? new QueueCraft(lander.Id, r.Id, _rng.Range(1, 21)) : null,
            < 75 => new CancelQueuedCrafts(lander.Id),
            < 79 => Pick(c.TechList) is { } t ? new SetResearch(t.Id) : null,
            < 84 => Pick(new[] { new[] { "hand-cart", "mine-cart" }, new[] { "steam-locomotive", "mine-cart", "mine-cart" }, new[] { "mine-cart" } })
                    is { } preset ? new PlaceTrain(RandTile(15), RandDir(), preset) : null,
            < 88 => Pick(p.Trains) is { } tr
                ? new SetSchedule(tr.Id, buildings.Where(b => (b.Def.Kind == BuildingKind.RailStop) && (_rng.Range(0, 2) == 0))
                    .Select(b => new ScheduleStop(b.Id, (StopAction)_rng.Range(0, 3))).ToArray())
                : null,
            < 92 => leader is null ? null : new SetMoveIntent(leader.Id, _rng.Range(-1, 2), _rng.Range(-1, 2)),
            < 94 => leader is null ? null : new MineTile(leader.Id, _rng.Range(0, 3) == 0 ? null : RandTile(3)),
            < 95 => Travel(world, anyLeader, followers),
            < 98 => (leader is null) || (buildings.Count == 0) ? null : new DepositToBuilding(leader.Id, Pick(buildings)!.Id),
            _ => (leader is null) || (buildings.Count == 0) ? null : new TakeFromBuilding(leader.Id, Pick(buildings)!.Id),
        };
        if (cmd is null)
        {
            return;
        }

        world.Submit(cmd with
        {
            Player = player
        });
        ActionsSubmitted++;
    }

    /// <summary>Launch from a random pad to a random planet with some helpers, or fly a landed ferry home.</summary>
    private Command? Travel(World w, Unit? leader, List<Unit> followers)
    {
        if (leader is null)
        {
            return null;
        }

        if (Pick(leader.Planet.Buildings.Where(b => b.Def.Kind == BuildingKind.Ferry).ToList()) is { } ferry)
        {
            return new ReturnFerry(leader.Id, ferry.Id);
        }

        return Pick(leader.Planet.Buildings.Where(b => b.Def.Kind == BuildingKind.LaunchPad).ToList()) is { } pad
            ? new LaunchFerry(leader.Id, pad.Id, _rng.Range(0, w.Planets.Count), followers.Where(_ => _rng.Range(0, 2) == 0).Select(u => u.Id).ToArray())
            : null;
    }

    private Command? RailLine(Planet p, Int2 start, Dir d)
    {
        // Rails are one command per tile; submit a short run now and return the last.
        int len = _rng.Range(3, 12);
        Command? last = null;
        for (int i = 0; i < len; i++)
        {
            last = new PlaceBuilding("rail", p.Torus.Wrap(start + (d.Vec() * i)), Dir.North);
        }

        for (int i = 0; i < len - 1; i++)
        {
            p.World.Submit(new PlaceBuilding("rail", p.Torus.Wrap(start + (d.Vec() * i)), Dir.North));
        }

        return last;
    }

    private Job RandomJob(World w, Planet p)
    {
        ContentDb c = w.Content;
        switch (_rng.Range(0, 6))
        {
            case 0:
                return Job.Idle;
            case 1:
                return new Job(JobKind.Build);
            case 2:
                return new Job(JobKind.Gather, Resource: Pick(c.ResourceList.Where(r => p.HasResource(r.Id)).ToList())!.Id);
            case 3 when p.Buildings.Count >= 2:
                {
                    Building a = Pick(p.Buildings)!;
                    Building b = Pick(p.Buildings)!;
                    string? item = _rng.Range(0, 2) == 0 ? null : Pick(c.ItemList)!.Id;

                    return new Job(JobKind.Haul, FromId: a.Id, ToId: b.Id, Item: item);
                }
            case 4 when p.Trains.Count > 0:
                return new Job(JobKind.OperateTrain, TrainId: Pick(p.Trains)!.Id);
            default:
                return new Job(JobKind.Gather, Resource: "xylo");
        }
    }
}
