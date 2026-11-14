using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Leader takes items from a building's output (or storage).</summary>
/// <param name="UnitId">Entity id of the issuing player's leader.</param>
/// <param name="BuildingId">Entity id of the building to take from.</param>
/// <param name="Item">Item id to take, or null for any item.</param>
/// <param name="Count">Maximum number of items to take (default: as many as fit).</param>
public sealed record TakeFromBuilding(int UnitId, int BuildingId, string? Item = null, int Count = int.MaxValue) : Command
{
    /// <summary>Checks the leader and building exist and the building is within the leader's reach.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        Building? b = w.Get<Building>(BuildingId);
        if ((u is null) || (b is null))
        {
            return "Invalid";
        }

        return u.Planet.InLeaderReach(u, b.Origin) ? null : "Too far away";
    }
    /// <summary>Moves items into the leader's carried inventory, from the output first and then the input inventory for non-storage buildings.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Building b = w.Get<Building>(BuildingId)!;
        int left = Count;
        foreach (Inventory? inv in b.Def.IsStorage ? new[] { b.Inv } : new[] { b.Out, b.Inv })
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

                left -= inv.MoveTo(u.Carry, item, System.Math.Min(n, left));
            }
        }
    }
}
