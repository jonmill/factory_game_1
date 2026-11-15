namespace Factory.Sim;

/// <summary>Sets one of a logistics station's item slots: which item, and whether it is supplied or demanded.</summary>
/// <param name="BuildingId">Entity id of the station.</param>
/// <param name="Index">Slot index, from 0.</param>
/// <param name="Item">Item id, or null to clear the slot.</param>
/// <param name="Mode">Supply or demand.</param>
public sealed record SetStationSlot(int BuildingId, int Index, string? Item, StationMode Mode) : Command
{
    /// <summary>Checks the building is a station, the slot exists and the item is real and not already set.</summary>
    public override string? Validate(World w)
    {
        if ((w.Get<Building>(BuildingId) is not { Def.IsStation: true } b) || (Index < 0) || (Index >= b.Def.StationSlots))
        {
            return "Invalid";
        }

        if (Item is null)
        {
            return null;
        }

        if (w.Content.Items.ContainsKey(Item) == false)
        {
            return "Unknown item";
        }

        return b.StationSlots.Where((s, i) => (i != Index) && (s.Item == Item)).Any() ? "Already set in another slot" : null;
    }

    /// <summary>Sets or clears the slot.</summary>
    public override void Apply(World w)
    {
        Building b = w.Get<Building>(BuildingId)!;
        if (Item is null)
        {
            if (Index < b.StationSlots.Count)
            {
                b.StationSlots.RemoveAt(Index);
            }

            return;
        }

        StationSlot slot = new StationSlot(Item, Mode);
        if (Index < b.StationSlots.Count)
        {
            b.StationSlots[Index] = slot;
        }
        else
        {
            b.StationSlots.Add(slot);
        }
    }
}
