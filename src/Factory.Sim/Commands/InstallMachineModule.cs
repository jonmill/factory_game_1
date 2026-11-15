namespace Factory.Sim;

/// <summary>The commander puts a machine module from its pockets into a free module slot of a building in reach.</summary>
/// <param name="UnitId">Entity id of the issuing player's commander.</param>
/// <param name="BuildingId">Entity id of the machine.</param>
/// <param name="Item">Item id of the module.</param>
public sealed record InstallMachineModule(int UnitId, int BuildingId, string Item) : Command
{
    /// <summary>Checks the module is in the commander's pockets, fits machines, and the machine has a free slot in reach.</summary>
    /// <param name="w">The world.</param>
    /// <returns>Null if legal, otherwise the reason.</returns>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        Building? b = w.Get<Building>(BuildingId);
        if ((u is null) || (b is null) || (b.Planet != u.Planet))
        {
            return "Invalid";
        }

        if ((w.Content.Items.TryGetValue(Item, out ItemDef? item) == false) || (item.Module is not { Fits: ModuleFit.Machine }))
        {
            return "That isn't a machine module";
        }

        if (u.Carry.Count(Item) < 1)
        {
            return $"No {item.Name} in your pockets";
        }

        if (b.Modules.Count >= b.Def.ModuleSlots)
        {
            return b.Def.ModuleSlots == 0 ? $"{b.Def.Name} takes no modules" : $"{b.Def.Name} has no free module slot";
        }

        return u.Planet.InLeaderReach(u, b.Origin) ? null : "Too far away";
    }

    /// <summary>Moves the module from the commander's pockets into the machine.</summary>
    /// <param name="w">The world.</param>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Building b = w.Get<Building>(BuildingId)!;
        u.Carry.Remove(Item, 1);
        b.Modules.Add(Item);
    }
}
