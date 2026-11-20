namespace Factory.Sim;

/// <summary>
/// A crate's limits, as on Factorio's chests: how many of its slots may be filled, and optionally the one item
/// it takes. A crate feeding a belt holds a few stacks of its ingredient instead of draining storage into
/// itself, and helpers never drop gathered wood in it. Nothing is added past the limits; what is already
/// inside stays.
/// </summary>
/// <param name="BuildingId">Entity id of the crate.</param>
/// <param name="Slots">How many slots may be used, from 1 to the crate's size; 0 lifts the limit.</param>
/// <param name="Item">The only item the crate takes, or null for anything.</param>
public sealed record SetCrateLimit(int BuildingId, int Slots, string? Item = null) : Command
{
    /// <summary>Checks the building is a crate, the limit fits it and the item is real.</summary>
    public override string? Validate(World w)
    {
        if (w.Get<Building>(BuildingId) is not { Def.Kind: BuildingKind.Chest } b)
        {
            return "Not a crate";
        }

        if ((Item is not null) && (w.Content.Items.ContainsKey(Item) == false))
        {
            return "Unknown item";
        }

        return (Slots < 0) || (Slots > b.Def.Slots) ? "Invalid" : null;
    }

    /// <summary>Sets the limits.</summary>
    public override void Apply(World w)
    {
        Building b = w.Get<Building>(BuildingId)!;
        b.Inv.Slots = Slots == 0 ? b.Def.Slots : Slots;
        b.Inv.Filter = Item;
    }
}
