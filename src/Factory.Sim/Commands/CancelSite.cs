using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Cancels a pending construction or deconstruction order.</summary>
/// <param name="SiteId">Entity id of the construction site.</param>
public sealed record CancelSite(int SiteId) : Command
{
    /// <summary>Checks the construction site exists.</summary>
    public override string? Validate(World w) => w.Get<ConstructionSite>(SiteId) is null ? "No such construction order" : null;
    /// <summary>Removes the site, returning any materials already delivered to the nearest storage and releasing units working on it.</summary>
    public override void Apply(World w)
    {
        ConstructionSite s = w.Get<ConstructionSite>(SiteId)!;
        s.Planet.CancelSite(s);
    }
}
