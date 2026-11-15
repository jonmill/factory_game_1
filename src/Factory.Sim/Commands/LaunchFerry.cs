namespace Factory.Sim;

/// <summary>Fly a launch pad's ferry to another planet, with the leader, the chosen helper bots and all the pad's cargo.</summary>
/// <param name="UnitId">Entity id of the issuing player's leader, who must be within reach of the pad.</param>
/// <param name="PadId">Entity id of the launch pad.</param>
/// <param name="ToPlanet">Index of the destination planet in <see cref="World.Planets"/>.</param>
/// <param name="Passengers">Entity ids of the helper bots to take along.</param>
public sealed record LaunchFerry(int UnitId, int PadId, int ToPlanet, int[] Passengers) : Command
{
    /// <summary>Checks the pad is loaded with its rocket and fuel, the destination is a different surface planet and the passengers are the player's helper bots on this planet.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        if ((u is null) || (w.Get<Building>(PadId) is not { Def.Kind: BuildingKind.LaunchPad } pad) || (ToPlanet < 0) || (ToPlanet >= w.Planets.Count))
        {
            return "Invalid";
        }

        Planet to = w.Planets[ToPlanet];
        if ((pad.Planet != u.Planet) || (u.Planet.InLeaderReach(u, pad.Origin) == false))
        {
            return "Too far away";
        }

        if ((to == pad.Planet) || (to.Def.Kind != PlanetKind.Surface))
        {
            return $"The ferry can't land on {to.Name}";
        }

        if (pad.Inv.Count(pad.Def.Vessel!) < 1)
        {
            return $"Load a {w.Content.Items[pad.Def.Vessel!].Name} first";
        }

        foreach (ItemAmount f in pad.Def.LaunchFuelList.Where(f => pad.Inv.Count(f.Item) < f.Count))
        {
            return $"Needs {f.Count} {w.Content.Items[f.Item].Name} to launch";
        }

        if (Passengers.Distinct().Count() != Passengers.Length)
        {
            return "Invalid";
        }

        foreach (int id in Passengers)
        {
            if ((w.Get<Unit>(id) is not { IsLeader: false } p) || (p.OwnerPlayer != Player) || (p.Planet != u.Planet))
            {
                return "Only your own helper bots on this planet can come along";
            }
        }

        BuildingDef landed = w.Content.Buildings[pad.Def.LandsAs!];

        return to.FindLandingSite(landed.W, landed.H) is null ? $"Nowhere to land on {to.Name}" : null;
    }

    /// <summary>Launches: burns the fuel, lands the ferry on the destination with the cargo and passengers.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Building pad = w.Get<Building>(PadId)!;
        pad.Planet.Launch(pad, w.Planets[ToPlanet], u, Passengers.Select(id => w.Get<Unit>(id)!).ToList());
    }
}
