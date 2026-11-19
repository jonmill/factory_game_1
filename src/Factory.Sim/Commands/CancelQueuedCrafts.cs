using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Clears every pending craft in the lander's hand-crafting queue.</summary>
/// <param name="BuildingId">Entity id of the lander.</param>
public sealed record CancelQueuedCrafts(int BuildingId) : Command
{
    /// <summary>Checks the target is a lander.</summary>
    public override string? Validate(World w) => w.Get<Building>(BuildingId) is { Def.Kind: BuildingKind.Lander } ? null : "Not a lander";
    /// <summary>
    /// Empties the lander's craft queue and puts the ingredients set aside for it, and any parts already
    /// made, back in storage. A craft under way finishes, and its output goes to storage too.
    /// </summary>
    public override void Apply(World w)
    {
        Building b = w.Get<Building>(BuildingId)!;
        b.CraftQueue.Clear();
        Planet.ReleaseCraftReserve(b);
    }
}
