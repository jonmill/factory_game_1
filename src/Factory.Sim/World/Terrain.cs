namespace Factory.Sim;

/// <summary>Base terrain type of a planet tile.</summary>
public enum Terrain : byte
{
    /// <summary>Solid ground that can be built on.</summary>
    Ground,
    /// <summary>Water: impassable to construction, but a source for pumps placed beside it.</summary>
    Water,
}
