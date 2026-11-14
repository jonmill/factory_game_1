using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of a mineable map resource (ore deposit, stone, tree), loaded from content JSON.</summary>
public sealed class ResourceDef
{
    /// <summary>Unique content id of the resource.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Id of the item yielded when the resource is mined.</summary>
    public string Item { get; init; } = "";
    /// <summary>Ticks for a unit (follower/leader) to hand-mine one item.</summary>
    public int HandTicks { get; init; } = 75;
    /// <summary>True for trees, which can only be harvested by hand and not by drills.</summary>
    public bool IsTree { get; init; }
    /// <summary>Placeholder render colour as a hex string.</summary>
    public string Color { get; init; } = "#ffffff";
}
