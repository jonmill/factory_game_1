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
    /// <summary>Names given to helper bots in the order they are built.</summary>
    public string[] FollowerNames { get; init; } = [];
    /// <summary>Number of helper bots that land with the commander.</summary>
    public int StartingFollowers { get; init; } = 2;
    /// <summary>Robot model of the commander.</summary>
    public string CommanderRobot { get; init; } = "commander";
    /// <summary>Robot model of the helper bots that land with the commander.</summary>
    public string HelperRobot { get; init; } = "helper-mk1";
}
