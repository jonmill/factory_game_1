namespace Factory.Sim;

/// <summary>The commander installs a module from its pockets into a free slot of itself or a helper bot in reach.</summary>
/// <param name="UnitId">Entity id of the issuing player's commander.</param>
/// <param name="TargetId">Entity id of the robot to upgrade (the commander itself or one of its helper bots).</param>
/// <param name="Item">Item id of the module.</param>
public sealed record InstallModule(int UnitId, int TargetId, string Item) : Command
{
    /// <summary>Checks the module is in the commander's pockets, fits the robot, and there is a free slot in reach.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        Unit? target = w.Get<Unit>(TargetId);
        if ((u is null) || (target is null) || (target.OwnerPlayer != Player) || (target.Planet != u.Planet))
        {
            return "Invalid";
        }

        if ((w.Content.Items.TryGetValue(Item, out ItemDef? item) == false) || (item.Module is not { } module))
        {
            return "That isn't a module";
        }

        if (u.Carry.Count(Item) < 1)
        {
            return $"No {item.Name} in your pockets";
        }

        if (module.FitsRobot(target.IsLeader) == false)
        {
            return target.IsLeader ? $"{item.Name} only fits helper bots" : $"{item.Name} only fits the commander";
        }

        if (target.Modules.Count >= target.Planet.ModuleSlots(target))
        {
            return $"{target.Name} has no free module slot";
        }

        return (target == u) || ((target.Planet == u.Planet) && u.Planet.InLeaderReach(u, target.Tile)) ? null : "Too far away";
    }

    /// <summary>Moves the module from the commander's pockets into the robot and applies its effects.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Unit target = w.Get<Unit>(TargetId)!;
        u.Carry.Remove(Item, 1);
        target.Modules.Add(Item);
        target.Planet.RefreshRobot(target);
    }
}
