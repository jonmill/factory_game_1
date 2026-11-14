namespace Factory.Sim;

/// <summary>Parameters for creating a new world.</summary>
public sealed class WorldSettings
{
    /// <summary>Seed for the deterministic random generator; the same seed and commands reproduce the same game.</summary>
    public ulong Seed { get; init; } = 12345;
    /// <summary>Width of the starting planet in tiles.</summary>
    public int PlanetWidth { get; init; } = 128;
    /// <summary>Height of the starting planet in tiles.</summary>
    public int PlanetHeight { get; init; } = 128;
    /// <summary>Number of players, each getting their own leader unit.</summary>
    public int Players { get; init; } = 1;
    /// <summary>Skip resource/lake/tree generation (useful for tests that build their own layout).</summary>
    public bool EmptyPlanet { get; init; }
}
