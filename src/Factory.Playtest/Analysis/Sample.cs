using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>A periodic telemetry snapshot of the first planet.</summary>
/// <param name="Tick">The world tick the sample was taken on.</param>
/// <param name="Storage">Item totals held in storage buildings (lander and crates).</param>
/// <param name="Produced">Cumulative amount produced per item, excluding starting stock.</param>
/// <param name="Consumed">Cumulative amount consumed per item.</param>
/// <param name="Followers">Number of non-leader units on the planet.</param>
/// <param name="PowerSatisfaction">Average satisfaction across power networks, or -1 when there are none.</param>
/// <param name="Buildings">Number of completed buildings.</param>
/// <param name="Sites">Number of pending construction sites.</param>
public sealed record Sample(
    long Tick,
    Dictionary<string, long> Storage,
    Dictionary<string, long> Produced,
    Dictionary<string, long> Consumed,
    int Followers,
    int PowerSatisfaction,
    int Buildings,
    int Sites);
