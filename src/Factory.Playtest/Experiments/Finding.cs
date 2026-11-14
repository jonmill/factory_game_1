using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Playtest.Experiments;

/// <summary>One thing worth a designer's or programmer's attention.</summary>
public sealed record Finding(
    Severity Severity,
    string Category,
    string Title,
    string Detail,
    ulong? Seed = null,
    long? Tick = null,
    string? Replay = null)
{
    /// <summary>How many times this finding was reported; duplicates are merged into one entry.</summary>
    public int Occurrences { get; init; } = 1;
}
