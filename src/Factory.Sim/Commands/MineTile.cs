using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>
/// Leader starts (Tile set) or stops (Tile null) hand-mining. The tile can be anywhere: the leader walks
/// over to it, and moves on to the next tile of the same deposit when one runs out.
/// </summary>
/// <param name="UnitId">Entity id of the issuing player's leader.</param>
/// <param name="Tile">Resource tile to mine, or null to stop.</param>
public sealed record MineTile(int UnitId, Int2? Tile) : Command
{
    /// <summary>Checks the unit is the player's leader and the tile has a resource.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        if (u is null)
        {
            return "Not your leader";
        }

        if ((Tile is { } t) && (u.Planet.ResourceAt(t) is null))
        {
            return "Nothing to mine there";
        }

        return null;
    }

    /// <summary>Sets (or clears) the leader's mining target and resets mining progress.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        u.MiningTile = Tile is { } t ? u.Planet.Torus.Wrap(t) : null;
        u.MiningResource = u.MiningTile is { } mt ? u.Planet.ResourceAt(mt)?.Id : null;
        u.MiningProgress = 0;
    }
}
