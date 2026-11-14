using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>Inputs shared by all experiments. Unused options are ignored by experiments that don't need them.</summary>
public sealed class ExperimentContext
{
    /// <summary>Game content the experiments run against.</summary>
    public ContentDb Content { get; init; } = ContentDb.LoadDefault();
    /// <summary>Directory where reports and replays are written.</summary>
    public string OutputDir { get; init; } = "playtest-results";
    /// <summary>World seeds to run; each seed is an independent run.</summary>
    public IReadOnlyList<ulong> Seeds { get; init; } = [1, 2, 3, 4];
    /// <summary>Game-time budget per run, in minutes.</summary>
    public double Minutes { get; init; } = 60;
    /// <summary>The goal bots pursue.</summary>
    public Goal Goal { get; init; } = new TechGoal("automation");
    /// <summary>Search iterations (speedrun) or fuzz runs (softlock).</summary>
    public int Iterations { get; init; } = 12;
    /// <summary>Maximum number of runs executed concurrently.</summary>
    public int Parallelism { get; init; } = Environment.ProcessorCount;
    /// <summary>Sink for progress messages.</summary>
    public Action<string> Log { get; init; } = Console.WriteLine;
    /// <summary>Strategy knobs for the reference bot.</summary>
    public BotParams BotParams { get; init; } = new();

    /// <summary>The per-run time budget converted to simulation ticks.</summary>
    public long MaxTicks => (long)(Minutes * 60 * World.TicksPerSecond);
    /// <summary>Directory where replays for findings are saved.</summary>
    public string ReplayDir => Path.Combine(OutputDir, "replays");

    /// <summary>Formats a tick count as game minutes with one decimal place.</summary>
    public static string FormatMinutes(long ticks) => $"{ticks / 3600.0:F1}";
}
