using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Leader puts carried items into a building (only what it accepts).</summary>
/// <param name="UnitId">Entity id of the issuing player's leader.</param>
/// <param name="BuildingId">Entity id of the receiving building.</param>
/// <param name="Item">Item id to deposit, or null to deposit everything the building accepts.</param>
public sealed record DepositToBuilding(int UnitId, int BuildingId, string? Item = null) : Command
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
    /// <summary>Moves as many carried items as the building will accept into its inventory.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Building b = w.Get<Building>(BuildingId)!;
        foreach ((string? item, int n) in u.Carry.Items.ToList())
        {
            if ((Item is not null) && (item != Item))
            {
                continue;
            }

            int m = System.Math.Min(n, b.Planet.AcceptAmount(b, item));
            if (m > 0)
            {
                u.Carry.Remove(item, m);
                b.Inv.AddUnchecked(item, m);
            }
        }
    }
}
