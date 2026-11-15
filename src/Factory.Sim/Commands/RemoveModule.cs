namespace Factory.Sim;

/// <summary>The commander takes a module out of itself or a helper bot in reach, back into its pockets.</summary>
/// <param name="UnitId">Entity id of the issuing player's commander.</param>
/// <param name="TargetId">Entity id of the robot the module is in.</param>
/// <param name="Slot">Index of the module slot.</param>
public sealed record RemoveModule(int UnitId, int TargetId, int Slot) : Command
{
    /// <summary>Checks the slot holds a module, the robot is in reach, and everything still fits once it's out.</summary>
    public override string? Validate(World w)
    {
        Unit? u = Leader(w, UnitId);
        Unit? target = w.Get<Unit>(TargetId);
        if ((u is null) || (target is null) || (target.OwnerPlayer != Player) || (target.Planet != u.Planet) ||
            (Slot < 0) || (Slot >= target.Modules.Count))
        {
            return "Invalid";
        }

        if ((target != u) && (u.Planet.InLeaderReach(u, target.Tile) == false))
        {
            return "Too far away";
        }

        string item = target.Modules[Slot];
        int lost = w.Content.Item(item).Module!.CarrySlots;
        if (target.Carry.UsedSlots > target.Carry.Slots - lost)
        {
            return $"Empty {target.Name}'s cargo first";
        }

        // The module itself needs room in the commander's pockets (which may be the ones shrinking).
        bool fits = target == u ? (u.Carry.SpaceFor(item) > 0) && ((u.Carry.Count(item) > 0) || (u.Carry.UsedSlots + 1 <= u.Carry.Slots - lost)) : u.Carry.SpaceFor(item) > 0;

        return fits ? null : "No room in your pockets";
    }

    /// <summary>Moves the module to the commander's pockets and drops its effects; a helper bot that loses a job's module goes idle.</summary>
    public override void Apply(World w)
    {
        Unit u = Leader(w, UnitId)!;
        Unit target = w.Get<Unit>(TargetId)!;
        string item = target.Modules[Slot];
        target.Modules.RemoveAt(Slot);
        target.Planet.RefreshRobot(target);
        u.Carry.AddUnchecked(item, 1);
    }
}
