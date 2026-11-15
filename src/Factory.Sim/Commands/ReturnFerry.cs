namespace Factory.Sim;

/// <summary>Fly a landed ferry home with its cargo, the leader and every one of the leader's helper bots on that planet.</summary>
/// <param name="UnitId">Entity id of the issuing player's leader, who must be within reach of the ferry.</param>
/// <param name="FerryId">Entity id of the landed ferry.</param>
public sealed record ReturnFerry(int UnitId, int FerryId) : Command
{
    /// <summary>Checks the ferry exists and the leader is next to it.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        if ((u is null) || (w.Get<Building>(FerryId) is not { Def.Kind: BuildingKind.Ferry } ferry))
        {
            return "Invalid";
        }

        return (ferry.Planet == u.Planet) && u.Planet.InLeaderReach(u, ferry.Origin) ? null : "Too far away";
    }

    /// <summary>Flies the ferry home.</summary>
    public override void Apply(World w)
    {
        Building ferry = w.Get<Building>(FerryId)!;
        ferry.Planet.FlyHome(ferry, Leader(w, UnitId)!);
    }
}
