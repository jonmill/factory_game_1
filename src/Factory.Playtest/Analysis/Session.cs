using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>Runs a world with bots attached, recording telemetry and checking invariants.</summary>
public sealed class Session
{
    /// <summary>The simulated world this session drives.</summary>
    public World World { get; }
    /// <summary>Bots that act on the world each tick, in order, before it steps.</summary>
    public List<IBot> Bots { get; } = [];
    /// <summary>Telemetry collected after every step.</summary>
    public Telemetry Telemetry { get; } = new();
    /// <summary>Invariant checker run periodically and at the end of each run.</summary>
    public InvariantChecker Invariants { get; } = new();
    /// <summary>How often, in ticks, invariants are checked during a run.</summary>
    public int InvariantEvery { get; init; } = 600;
    /// <summary>The world generation seed this session was created with.</summary>
    public ulong Seed { get; }

    /// <summary>Creates a fresh world from the given content and seed.</summary>
    /// <param name="content">Game content definitions.</param>
    /// <param name="seed">World generation seed.</param>
    public Session(ContentDb content, ulong seed)
    {
        Seed = seed;
        World = new World(content, new WorldSettings { Seed = seed });
    }

    /// <summary>Runs until <paramref name="done"/> or maxTicks more ticks have elapsed.</summary>
    public RunResult Run(Func<World, bool> done, long maxTicks)
    {
        long end = World.Tick + maxTicks;
        try
        {
            while (World.Tick < end)
            {
                if (done(World))
                {
                    Invariants.Check(World);

                    return new RunResult(true, World.Tick, null, "goal met");
                }

                foreach (IBot b in Bots)
                {
                    b.Tick(World);
                }

                World.Step();
                World.Events.Clear();
                Telemetry.Observe(World);
                if (World.Tick % InvariantEvery == 0)
                {
                    Invariants.Check(World);
                }
            }

            Invariants.Check(World);

            return new RunResult(done(World), World.Tick, null, "time limit");
        }
        catch (Exception e)
        {
            return new RunResult(false, World.Tick, e, $"crash: {e.GetType().Name}");
        }
    }

    /// <summary>Writes a replay of the world so far to <c>&lt;dir&gt;/&lt;name&gt;.replay.json</c>.</summary>
    /// <param name="dir">Directory to write into; created if missing.</param>
    /// <param name="name">Base file name for the replay.</param>
    /// <returns>The path of the written replay file.</returns>
    public string SaveReplay(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{name}.replay.json");
        File.WriteAllText(path, Replay.Capture(World).ToJson());

        return path;
    }

    /// <summary>One-line state summary for reports.</summary>
    public string Describe()
    {
        Planet p = World.Planets[0];
        string units = string.Join("; ", p.Units.Where(u => u.IsLeader == false).Select(u => $"{u.Name}: {u.Job.Kind} – {u.Status}"));
        string store = string.Join(", ", WorldProbe.StorageTotals(p).OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}={kv.Value}"));

        return $"tick {World.Tick} ({World.Tick / 3600} min); research: {World.Research.Current ?? "none"}; " +
               $"buildings {p.Buildings.Count}, sites {p.Sites.Count}; followers [{units}]; storage [{store}]";
    }
}
