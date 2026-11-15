namespace Factory.Sim;

/// <summary>
/// The commander walks to a tile by itself, path-finding around obstacles (null stops it). Steering by
/// hand or starting to mine cancels it.
/// </summary>
/// <param name="UnitId">Entity id of the issuing player's commander.</param>
/// <param name="Tile">Where to walk, or null to stop.</param>
public sealed record WalkTo(int UnitId, Int2? Tile) : Command
{
    /// <summary>Checks the unit is the player's commander.</summary>
    public override string? Validate(World w) => Leader(w, UnitId) is null ? "Not your leader" : null;

    /// <summary>Sets (or clears) where the commander is walking.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        u.WalkTarget = Tile is { } t ? u.Planet.Torus.Wrap(t) : null;
        if (u.WalkTarget is not null)
        {
            u.MoveIntent = Int2.Zero;
            u.MiningTile = null;
            u.MiningResource = null;
            u.MiningProgress = 0;
        }
    }
}
