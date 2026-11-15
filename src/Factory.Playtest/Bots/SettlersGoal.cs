using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>The whole game: terraform enough planets until they are ready, then call the settlers.</summary>
public sealed record SettlersGoal : Goal
{
    /// <inheritdoc />
    public override bool IsMet(World w) => w.SettlersCalled;
    /// <inheritdoc />
    public override string Describe(ContentDb c) => "terraform the system and call the settlers";
    /// <inheritdoc />
    public override string ToString() => "settlers";
}
