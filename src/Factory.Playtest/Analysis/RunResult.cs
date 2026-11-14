using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>Outcome of a single <see cref="Session.Run"/> call.</summary>
/// <param name="GoalMet">Whether the stop condition was satisfied when the run ended.</param>
/// <param name="Ticks">The world tick at which the run ended.</param>
/// <param name="Crash">The exception that aborted the run, or <c>null</c> if it finished normally.</param>
/// <param name="StopReason">Short human-readable reason the run stopped (goal met, time limit or crash).</param>
public sealed record RunResult(bool GoalMet, long Ticks, Exception? Crash, string StopReason);
