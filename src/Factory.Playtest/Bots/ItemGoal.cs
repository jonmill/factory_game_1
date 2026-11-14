using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Stockpile items in storage (lander + crates).</summary>
public sealed record ItemGoal(string Item, int Count) : Goal
{
    /// <inheritdoc />
    public override bool IsMet(World w) => Analysis.WorldProbe.StorageCount(w.Planets[0], Item) >= Count;
    /// <inheritdoc />
    public override string Describe(ContentDb c) => $"stockpile {Count} {c.Items[Item].Name}";
    /// <inheritdoc />
    public override string ToString() => $"item:{Item}:{Count}";
}
