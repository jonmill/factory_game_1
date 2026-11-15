namespace Factory.Sim;

/// <summary>
/// What makes a planet ready for the settlers: the planet stats terraformers raise, and how far each
/// planet's readiness has to get before the settlers can be called.
/// </summary>
public sealed class TerraformDef
{
    /// <summary>The planet stats, in display order.</summary>
    public PlanetStatDef[] Stats { get; init; } = [];
    /// <summary>Readiness, in percent, a planet must reach to count as ready.</summary>
    public int ReadyBar { get; init; } = 100;
    /// <summary>How many terraformable planets must be ready before the settlers can be called; the player picks which.</summary>
    public int PlanetsNeeded { get; init; } = 1;
}
