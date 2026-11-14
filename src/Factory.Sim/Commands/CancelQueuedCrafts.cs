using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Clears every pending craft in the lander's hand-crafting queue.</summary>
/// <param name="BuildingId">Entity id of the lander.</param>
public sealed record CancelQueuedCrafts(int BuildingId) : Command
{
    /// <summary>Checks the target is a lander.</summary>
    public override string? Validate(World w) => w.Get<Building>(BuildingId) is { Def.Kind: BuildingKind.Lander } ? null : "Not a lander";
    /// <summary>Empties the lander's craft queue.</summary>
    public override void Apply(World w) => w.Get<Building>(BuildingId)!.CraftQueue.Clear();
}
