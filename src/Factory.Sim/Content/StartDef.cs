using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Starting conditions for a new game.</summary>
public sealed class StartDef
{
    /// <summary>Items initially stored in the lander, keyed by item id.</summary>
    public Dictionary<string, int> Lander { get; init; } = new();
    /// <summary>Recipe, building and vehicle ids available from the start without research.</summary>
    public string[] Unlocked { get; init; } = [];
    /// <summary>Names assigned to followers in order as they join the colony.</summary>
    public string[] FollowerNames { get; init; } = [];
    /// <summary>Number of followers that arrive with the leader.</summary>
    public int StartingFollowers { get; init; } = 3;
}
