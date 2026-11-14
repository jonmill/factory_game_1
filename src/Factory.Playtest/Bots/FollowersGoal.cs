using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Summon this many extra followers with the Colony Beacon.</summary>
public sealed record FollowersGoal(int Count) : Goal
{
    /// <inheritdoc />
    public override bool IsMet(World w) => w.FollowersSummoned >= Count;
    /// <inheritdoc />
    public override string Describe(ContentDb c) => $"summon {Count} follower(s)";
    /// <inheritdoc />
    public override string ToString() => $"followers:{Count}";
}
