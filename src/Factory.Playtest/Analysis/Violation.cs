using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>A broken invariant detected during a run.</summary>
/// <param name="Kind">The invariant category, such as "conservation", "bounds", "stuck-follower" or "stuck-train".</param>
/// <param name="Detail">Human-readable description of what went wrong.</param>
/// <param name="Tick">The world tick at which the violation was detected.</param>
public sealed record Violation(string Kind, string Detail, long Tick);
