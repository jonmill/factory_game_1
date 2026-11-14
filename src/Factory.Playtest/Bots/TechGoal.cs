using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Research a specific technology.</summary>
/// <param name="TechId">Id of the technology to complete.</param>
public sealed record TechGoal(string TechId) : Goal
{
    /// <inheritdoc />
    public override bool IsMet(World w) => w.Research.Completed.Contains(TechId);
    /// <inheritdoc />
    public override string Describe(ContentDb c) => $"research {c.Techs[TechId].Name}";
    /// <inheritdoc />
    public override string ToString() => $"tech:{TechId}";
}
