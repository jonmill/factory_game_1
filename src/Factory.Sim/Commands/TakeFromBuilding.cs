using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Leader takes items from a building's output (or storage).</summary>
/// <param name="UnitId">Entity id of the issuing player's leader.</param>
/// <param name="BuildingId">Entity id of the building to take from.</param>
/// <param name="Item">Item id to take, or null for any item.</param>
/// <param name="Count">Maximum number of items to take (default: as many as fit).</param>
/// <param name="Part">Which of the building's inventories to take from.</param>
public sealed record TakeFromBuilding(int UnitId, int BuildingId, string? Item = null, int Count = int.MaxValue, InventoryPart Part = InventoryPart.Any) : Command
{
    /// <summary>Checks the leader and building exist and the building is within the leader's reach.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        Building? b = w.Get<Building>(BuildingId);
        if ((u is null) || (b is null) || (Count < 1))
        {
            return "Invalid";
        }

        return (b.Planet == u.Planet) && u.Planet.InLeaderReach(u, b.Origin) ? null : "Too far away";
    }

    /// <summary>Moves items into the leader's carried inventory, from the output first and then the input inventory for non-storage buildings.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Building b = w.Get<Building>(BuildingId)!;
        int left = Count;
        Inventory[] from = b.Def.IsStorage ? [b.Inv] : Part switch
        {
            InventoryPart.Input => [b.Inv],
            InventoryPart.Output => [b.Out],
            _ => [b.Out, b.Inv],
        };
        foreach (Inventory inv in from)
        {
            foreach ((string? item, int n) in inv.Items.ToList())
            {
                if ((Item is not null) && (item != Item))
                {
                    continue;
                }

                if (left <= 0)
                {
                    return;
                }

                int moved = inv.MoveTo(u.Carry, item, System.Math.Min(n, left));
                left -= moved;
                if (b.Def.Kind == BuildingKind.Belt)
                {
                    // Keep the belt's lane in step with its inventory.
                    for (int i = 0; i < moved; i++)
                    {
                        b.Lane.Remove(b.Lane.Last(x => x.Item == item));
                    }
                }
            }
        }
    }
}
