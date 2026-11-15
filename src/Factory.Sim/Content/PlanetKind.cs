namespace Factory.Sim;

/// <summary>What kind of body a planet definition describes.</summary>
public enum PlanetKind
{
    /// <summary>A world with a surface to land on and build across.</summary>
    Surface,
    /// <summary>A gas giant: no surface, only worked from orbit.</summary>
    GasGiant,
    /// <summary>An asteroid belt: worked from space.</summary>
    Belt,
}
