using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Assemble this many new helper bots in a Robot Assembly Bay.</summary>
public sealed record HelpersGoal(int Count) : Goal
{
    /// <summary>The robotics recipe the bot uses to assemble helpers.</summary>
    public const string Recipe = "helper-mk1";

    /// <inheritdoc />
    public override bool IsMet(World w) => w.HelpersBuilt >= Count;
    /// <inheritdoc />
    public override string Describe(ContentDb c) => $"build {Count} helper bot(s)";
    /// <inheritdoc />
    public override string ToString() => $"helpers:{Count}";
}
