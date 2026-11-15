using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>Time series + accumulated activity counters for one run.</summary>
public sealed class Telemetry
{
    /// <summary>How often, in ticks, a full <see cref="Sample"/> is recorded.</summary>
    public int SampleEvery { get; init; } = 600;
    /// <summary>How often, in ticks, follower activity and machine status are tallied.</summary>
    public int ActivityEvery { get; init; } = 30;

    /// <summary>Periodic snapshots taken every <see cref="SampleEvery"/> ticks.</summary>
    public List<Sample> Samples { get; } = [];
    /// <summary>Follower-ticks spent per activity key (e.g. "Gather:tree", "Haul", "Build").</summary>
    public Dictionary<string, long> ActivityTicks { get; } = new();
    /// <summary>Follower-ticks spent in waiting/idle statuses, by status text.</summary>
    public Dictionary<string, long> WaitingTicks { get; } = new();
    /// <summary>Total follower-ticks observed; the denominator for activity and waiting shares.</summary>
    public long FollowerTicks { get; private set; }
    /// <summary>Building-ticks per (def id, status).</summary>
    public Dictionary<(string Def, MachineStatus Status), long> MachineTicks { get; } = new();
    /// <summary>The tick at which each technology was first seen as completed.</summary>
    public Dictionary<string, long> TechCompletedTick { get; } = new();
    /// <summary>The tick at which each additional follower was summoned, in order.</summary>
    public List<long> FollowerSummonTicks { get; } = [];

    private int _summoned;

    /// <summary>Updates counters and samples from the world after a step.</summary>
    /// <param name="w">The world that just stepped.</param>
    public void Observe(World w)
    {
        Planet p = w.Planets[0];
        foreach (string t in w.Research.Completed)
        {
            TechCompletedTick.TryAdd(t, w.Tick);
        }

        while (_summoned < w.HelpersBuilt)
        {
            _summoned++;
            FollowerSummonTicks.Add(w.Tick);
        }

        if (w.Tick % ActivityEvery == 0)
        {
            foreach (Unit u in p.Units)
            {
                if (u.IsLeader)
                {
                    continue;
                }

                FollowerTicks += ActivityEvery;
                string key = WorldProbe.ActivityKey(u);
                ActivityTicks[key] = ActivityTicks.GetValueOrDefault(key) + ActivityEvery;
                if (WorldProbe.IsWaitingStatus(u.Status))
                {
                    WaitingTicks[u.Status] = WaitingTicks.GetValueOrDefault(u.Status) + ActivityEvery;
                }
            }

            foreach (Building b in p.Buildings)
            {
                if (b.Def.Kind is BuildingKind.Pole or BuildingKind.Rail or BuildingKind.Chest or BuildingKind.RailStop or BuildingKind.WaterTower or BuildingKind.Lander)
                {
                    continue;
                }

                (string Id, MachineStatus Status) k = (b.Def.Id, b.Status);
                MachineTicks[k] = MachineTicks.GetValueOrDefault(k) + ActivityEvery;
            }
        }

        if (w.Tick % SampleEvery == 0)
        {
            Dictionary<string, long> produced = new Dictionary<string, long>();
            Dictionary<string, long> consumed = new Dictionary<string, long>();
            foreach (string item in w.Stats.AllItems())
            {
                produced[item] = w.Stats.Produced(item) - w.Stats.Get(ItemFlow.Start, item);
                consumed[item] = w.Stats.Consumed(item);
            }

            int sat = p.Networks.Count == 0 ? -1 : (int)p.Networks.Values.Average(n => n.Satisfaction);
            Samples.Add(new Sample(w.Tick, WorldProbe.StorageTotals(p), produced, consumed,
                p.Units.Count(u => u.IsLeader == false), sat, p.Buildings.Count, p.Sites.Count));
        }
    }
}
