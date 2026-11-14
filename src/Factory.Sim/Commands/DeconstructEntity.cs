using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Mark a building or train for dismantling by builders.</summary>
/// <param name="TargetId">Entity id of the building or train.</param>
public sealed record DeconstructEntity(int TargetId) : Command
{
    /// <summary>Checks the target exists, is not the lander and is not already marked.</summary>
    public override string? Validate(World w)
    {
        if (w.Get<Building>(TargetId) is { } b)
        {
            if (b.Def.Kind == BuildingKind.Lander)
            {
                return "You can't dismantle the lander";
            }

            if (b.Planet.Sites.Any(s => (s.Kind == SiteKind.Deconstruct) && (s.TargetId == TargetId)))
            {
                return "Already marked";
            }

            return null;
        }

        if (w.Get<Train>(TargetId) is { } t)
        {
            return t.Planet.Sites.Any(s => (s.Kind == SiteKind.Deconstruct) && (s.TargetId == TargetId)) ? "Already marked" : null;
        }

        return "Nothing to dismantle";
    }

    /// <summary>Creates a deconstruction site over the target; buildings take half their build time (minimum 20 ticks), trains 60 ticks.</summary>
    public override void Apply(World w)
    {
        if (w.Get<Building>(TargetId) is { } b)
        {
            b.Planet.CreateSite(SiteKind.Deconstruct, b.Def.Id, b.Origin, b.Rot, b.W, b.H, [], System.Math.Max(20, b.Def.BuildTicks / 2)).TargetId = TargetId;
        }
        else if (w.Get<Train>(TargetId) is { } t)
        {
            t.Planet.CreateSite(SiteKind.Deconstruct, "train", t.Tiles[0], Dir.North, 1, 1, [], 60).TargetId = TargetId;
        }
    }
}
