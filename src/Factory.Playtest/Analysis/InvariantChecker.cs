using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>
/// Rules that must always hold. Violations are bugs (or design holes), never "balance".
///  - Conservation: items in the world == produced − consumed, per item (catches dupes / losses).
///  - Bounds: units are on the planet.
///  - Stuck followers: same waiting status and position for too long while they have a job.
///  - Stuck trains: blocked/no route for too long while they have a schedule.
/// </summary>
public sealed class InvariantChecker
{
    /// <summary>How many ticks a follower or train may sit in the same waiting state before it is reported as stuck (default three game minutes).</summary>
    public int StuckTicks { get; init; } = 60 * 60 * 3;
    /// <summary>Every distinct violation found so far, in the order it was first detected.</summary>
    public List<Violation> Violations { get; } = [];

    private readonly Dictionary<int, (string Status, Int2 Pos, long Since)> _unitState = new();
    private readonly Dictionary<int, (TrainState State, Int2 Head, long Since)> _trainState = new();
    private readonly HashSet<string> _reported = [];

    private void Report(string kind, string key, string detail, long tick)
    {
        if (_reported.Add(kind + "|" + key))
        {
            Violations.Add(new Violation(kind, detail, tick));
        }
    }

    /// <summary>Checks every invariant against the current world state and records any new violations.</summary>
    /// <param name="w">The world to inspect.</param>
    public void Check(World w)
    {
        // Conservation
        SortedDictionary<string, long> all = WorldProbe.AllItems(w);
        foreach (string? item in all.Keys.Union(w.Stats.AllItems()))
        {
            long expected = w.Stats.Produced(item) - w.Stats.Consumed(item);
            long actual = all.GetValueOrDefault(item);
            if (expected != actual)
            {
                Report("conservation", item, $"{item}: {actual} exist but stats say {expected} (diff {actual - expected:+#;-#;0})", w.Tick);
            }
        }

        foreach (Planet p in w.Planets)
        {
            foreach (Unit u in p.Units)
            {
                if ((u.Pos.X < 0) || (u.Pos.Y < 0) || (u.Pos.X >= p.Width * Fixed.One) || (u.Pos.Y >= p.Height * Fixed.One))
                {
                    Report("bounds", $"unit{u.Id}", $"{u.Name} is off the planet at {u.Pos}", w.Tick);
                }

                if (u.IsLeader)
                {
                    continue;
                }

                bool waiting = (u.Job.Kind != JobKind.Idle) && WorldProbe.IsWaitingStatus(u.Status);
                if (waiting == false)
                {
                    _unitState.Remove(u.Id);
                    continue;
                }

                if (_unitState.TryGetValue(u.Id, out (string Status, Int2 Pos, long Since) s) && (s.Status == u.Status) && (s.Pos == u.Pos))
                {
                    if (w.Tick - s.Since >= StuckTicks)
                    {
                        Report("stuck-follower", $"{u.Job.Kind}|{Normalize(u.Status)}",
                            $"Follower stuck for {(w.Tick - s.Since) / 3600} min on job {u.Job.Kind}: \"{u.Status}\" (carrying: {Carry(u)})", w.Tick);
                    }
                }
                else
                {
                    _unitState[u.Id] = (u.Status, u.Pos, w.Tick);
                }
            }

            foreach (Train t in p.Trains)
            {
                bool bad = (t.Schedule.Count > 0) && (t.State is TrainState.Blocked or TrainState.NoPath);
                if (bad == false)
                {
                    _trainState.Remove(t.Id);
                    continue;
                }

                if (_trainState.TryGetValue(t.Id, out (TrainState State, Int2 Head, long Since) s) && (s.State == t.State) && (s.Head == t.Tiles[0]))
                {
                    if (w.Tick - s.Since >= StuckTicks)
                    {
                        Report("stuck-train", t.State.ToString(), $"{t.Name} stuck in state {t.State} for {(w.Tick - s.Since) / 3600} min", w.Tick);
                    }
                }
                else
                {
                    _trainState[t.Id] = (t.State, t.Tiles[0], w.Tick);
                }
            }
        }
    }

    private static string Carry(Unit u) => u.Carry.IsEmpty ? "nothing" : string.Join(", ", u.Carry.Items.Select(kv => $"{kv.Key}×{kv.Value}"));

    /// <summary>Strip entity-specific names so equivalent problems dedupe.</summary>
    private static string Normalize(string status) =>
        System.Text.RegularExpressions.Regex.Replace(status, @"(Stop|Train|Supply Crate|Burner Smelter|Arc Smelter|Colony Lander|Lander|Research Station|Steam Engine|Fabricator)\s*\d*", "<building>");
}
