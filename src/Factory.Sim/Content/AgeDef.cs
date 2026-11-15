namespace Factory.Sim;

/// <summary>
/// One of the tech ages (Salvage, Combustion, Clean, Stellar). An age groups technologies and buildings
/// that share a look, a fuel and a research Core.
/// </summary>
public sealed class AgeDef
{
    /// <summary>Unique content id of the age.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>The research Core this age introduces, if any.</summary>
    public string? Core { get; init; }
    /// <summary>Accent colour for the age in the research tree (hex).</summary>
    public string Color { get; init; } = "#ffffff";
    /// <summary>What the age is about, shown in the research tree.</summary>
    public string Description { get; init; } = "";
}
