using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Mark a single rail tile for removal by builders.</summary>
/// <param name="Tile">Tile coordinate of the rail (wrapped onto the planet).</param>
public sealed record DeconstructRail(Int2 Tile) : Command
{
    /// <summary>Checks the tile holds rail that is not already marked.</summary>
    public override string? Validate(World w)
    {
        if (ValidPlanet(w) == false)
        {
            return "Bad planet";
        }

        Planet p = P(w);
        if (p.IsRail(Tile) == false)
        {
            return "No track here";
        }

        if (p.Sites.Any(s => (s.Kind == SiteKind.Deconstruct) && (s.TargetId == 0) && (s.Origin == p.Torus.Wrap(Tile))))
        {
            return "Already marked";
        }

        return null;
    }
    /// <summary>Creates a 10-tick deconstruction site on the rail tile.</summary>
    public override void Apply(World w) => P(w).CreateSite(SiteKind.Deconstruct, "rail", Tile, Dir.North, 1, 1, [], 10);
}
