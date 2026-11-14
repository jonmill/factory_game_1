using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>Registry of the available experiments and shared helpers for running them.</summary>
public static class Experiments
{
    /// <summary>Every registered experiment.</summary>
    public static readonly IReadOnlyList<IExperiment> All =
    [
        new StaticBalanceExperiment(),
        new BalanceExperiment(),
        new SpeedrunExperiment(),
        new SoftlockExperiment(),
    ];

    /// <summary>Looks up an experiment by name, ignoring case.</summary>
    /// <exception cref="ArgumentException">No experiment has that name.</exception>
    public static IExperiment Get(string name) =>
        All.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown experiment '{name}'. Known: {string.Join(", ", All.Select(e => e.Name))}");

    /// <summary>Runs work items in parallel; each item owns its own World so this is safe and deterministic per item.</summary>
    public static List<T> ParallelMap<TIn, T>(IReadOnlyList<TIn> inputs, int parallelism, Func<TIn, T> work)
    {
        T[] results = new T[inputs.Count];
        Parallel.For(0, inputs.Count, new ParallelOptions { MaxDegreeOfParallelism = System.Math.Max(1, parallelism) }, i => results[i] = work(inputs[i]));

        return [.. results];
    }
}
