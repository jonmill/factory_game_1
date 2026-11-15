using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Places a construction site for a building, to be built by followers from delivered materials.</summary>
/// <param name="DefId">Building definition id.</param>
/// <param name="Origin">Top-left tile of the footprint.</param>
/// <param name="Rot">Facing; east/west swap the footprint's width and height.</param>
public sealed record PlaceBuilding(string DefId, Int2 Origin, Dir Rot) : Command
{
    /// <summary>Checks the building exists, is buildable and researched, and fits at the location.</summary>
    public override string? Validate(World w)
    {
        if (ValidPlanet(w) == false)
        {
            return "Bad planet";
        }

        if (w.Content.Buildings.TryGetValue(DefId, out BuildingDef? def) == false)
        {
            return $"Unknown building {DefId}";
        }

        if (P(w).Units.Any(u => u.OwnerPlayer == Player) == false)
        {
            return $"None of your robots are on {P(w).Name}";
        }

        return P(w).CanPlace(def, Origin, Rot);
    }
    /// <summary>Creates the construction site.</summary>
    public override void Apply(World w) => P(w).PlaceSite(w.Content.Buildings[DefId], Origin, Rot);
}
