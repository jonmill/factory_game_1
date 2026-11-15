namespace Factory.Sim;

/// <summary>The commander takes a module out of a machine in reach, back into its pockets.</summary>
/// <param name="UnitId">Entity id of the issuing player's commander.</param>
/// <param name="BuildingId">Entity id of the machine.</param>
/// <param name="Slot">Index of the module slot.</param>
public sealed record RemoveMachineModule(int UnitId, int BuildingId, int Slot) : Command
{
    /// <summary>Checks the slot holds a module, the machine is in reach and the pockets have room.</summary>
    /// <param name="w">The world.</param>
    /// <returns>Null if legal, otherwise the reason.</returns>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        Building? b = w.Get<Building>(BuildingId);
        if ((u is null) || (b is null) || (b.Planet != u.Planet) || (Slot < 0) || (Slot >= b.Modules.Count))
        {
            return "Invalid";
        }

        if (u.Planet.InLeaderReach(u, b.Origin) == false)
        {
            return "Too far away";
        }

        return u.Carry.SpaceFor(b.Modules[Slot]) > 0 ? null : "No room in your pockets";
    }

    /// <summary>Moves the module to the commander's pockets; the craft under way keeps its boost.</summary>
    /// <param name="w">The world.</param>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Building b = w.Get<Building>(BuildingId)!;
        string item = b.Modules[Slot];
        b.Modules.RemoveAt(Slot);
        u.Carry.AddUnchecked(item, 1);
    }
}
