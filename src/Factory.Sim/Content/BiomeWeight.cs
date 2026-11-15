namespace Factory.Sim;

/// <summary>How much of a planet a biome covers, relative to the planet's other biomes.</summary>
public sealed class BiomeWeight
{
    /// <summary>The biome's id.</summary>
    public string Biome { get; init; } = "";
    /// <summary>Relative share of the planet.</summary>
    public int Weight { get; init; } = 1;
}
