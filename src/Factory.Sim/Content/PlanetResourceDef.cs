namespace Factory.Sim;

/// <summary>How a resource is scattered across one planet: how many patches, how big and how rich.</summary>
public sealed class PlanetResourceDef
{
    /// <summary>The resource's id.</summary>
    public string Resource { get; init; } = "";
    /// <summary>Number of patches.</summary>
    public int Patches { get; init; } = 2;
    /// <summary>Smallest patch radius in tiles.</summary>
    public int MinRadius { get; init; } = 4;
    /// <summary>Largest patch radius in tiles.</summary>
    public int MaxRadius { get; init; } = 8;
    /// <summary>Units of resource at a patch's centre.</summary>
    public int Richness { get; init; } = 1000;
    /// <summary>Nearest a patch may be to the landing site, in tiles.</summary>
    public int MinDistance { get; init; }
    /// <summary>Farthest a patch may be from the landing site, in tiles (0 for anywhere).</summary>
    public int MaxDistance { get; init; }
    /// <summary>The biome patches prefer, if any.</summary>
    public string? Biome { get; init; }
}
