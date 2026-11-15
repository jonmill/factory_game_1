namespace Factory.Sim;

/// <summary>
/// A region type within a planet (meadow, forest, ash waste, snowfield…). Biomes colour the ground,
/// decide how thickly trees grow, and can host particular deposits.
/// </summary>
public sealed class BiomeDef
{
    /// <summary>Unique content id of the biome.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Ground colours as hex: base, dark patches and light patches.</summary>
    public string[] Ground { get; init; } = ["#5a4a33", "#453827", "#615a38"];
    /// <summary>Chance in percent that a ground tile grows a tree.</summary>
    public int Trees { get; init; }
    /// <summary>Flavour text shown when inspecting the planet.</summary>
    public string Description { get; init; } = "";
}
