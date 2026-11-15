namespace Factory.Sim;

/// <summary>
/// A body in the star system. Surface planets are generated from their definition with seeded
/// variation: which biomes cover them, which resources they hold and how much water or lava they have.
/// Gas giants and belts have no surface and are worked from orbit.
/// </summary>
public sealed class PlanetDef
{
    /// <summary>Unique content id of the planet.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>A short label for the planet's type, such as "Volcanic world".</summary>
    public string Type { get; init; } = "";
    /// <summary>Surface, gas giant or belt.</summary>
    public PlanetKind Kind { get; init; } = PlanetKind.Surface;
    /// <summary>Flavour text for the system map.</summary>
    public string Description { get; init; } = "";
    /// <summary>True for the planet the commander lands on.</summary>
    public bool Start { get; init; }
    /// <summary>True if this planet must be made ready before the settlers can be called.</summary>
    public bool SettlerTarget { get; init; }
    /// <summary>Distance from the star in tenths of the home planet's orbit; sets travel times.</summary>
    public int Orbit { get; init; } = 10;
    /// <summary>Width and height of the surface in tiles (the start planet uses the world settings instead).</summary>
    public int Size { get; init; } = 112;
    /// <summary>Sunlight in percent of the start planet's: scales solar panel output.</summary>
    public int Sunlight { get; init; } = 100;
    /// <summary>Colour of the planet on the system map (hex).</summary>
    public string Color { get; init; } = "#88aacc";
    /// <summary>Biomes covering the surface, with their relative shares.</summary>
    public BiomeWeight[] Biomes { get; init; } = [];
    /// <summary>Deposits scattered across the surface.</summary>
    public PlanetResourceDef[] Resources { get; init; } = [];
    /// <summary>Resources worked from orbit (gas giants and belts), by resource id.</summary>
    public string[] Orbital { get; init; } = [];
    /// <summary>Number of lakes.</summary>
    public int Lakes { get; init; } = 3;
    /// <summary>True if the lakes are frozen over (they still feed boilers and pumps).</summary>
    public bool FrozenWater { get; init; }
    /// <summary>Number of lava flows (impassable, like water but useless for boilers).</summary>
    public int LavaFlows { get; init; }
    /// <summary>Starting planet stats where they differ from the defaults, by stat id.</summary>
    public Dictionary<string, int> Stats { get; init; } = new();
}
