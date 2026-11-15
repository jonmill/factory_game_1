namespace Factory.Sim;

/// <summary>One planet stat that terraforming raises, such as the atmosphere.</summary>
public sealed class PlanetStatDef
{
    /// <summary>Unique id, used by terraform recipe effects ("terraform:&lt;id&gt;:&lt;points&gt;").</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Points needed for the stat to count as fully terraformed.</summary>
    public int Target { get; init; } = 100;
    /// <summary>Points every planet starts with.</summary>
    public int Initial { get; init; }
    /// <summary>What the stat means, for tooltips.</summary>
    public string Description { get; init; } = "";
    /// <summary>Bar colour as a hex string.</summary>
    public string Color { get; init; } = "#ffffff";
}
