namespace Factory.Sim;

/// <summary>Why an item entered or left the world. Used for production statistics and conservation checks.</summary>
public enum ItemFlow
{
    // Sources
    /// <summary>Items present in the starting inventory.</summary>
    Start,
    /// <summary>Mined by hand by the leader or a gathering follower.</summary>
    HandMined,
    /// <summary>Extracted by a mining drill.</summary>
    DrillMined,
    /// <summary>Output of a crafting (non-smelting) recipe.</summary>
    Crafted,
    /// <summary>Output of a smelting recipe.</summary>
    Smelted,
    /// <summary>Returned to the player, e.g. by deconstruction or by ingredients given back when a machine's recipe is changed.</summary>
    Refunded,
    // Sinks
    /// <summary>Consumed as an ingredient of a crafting recipe.</summary>
    CraftInput,
    /// <summary>Consumed as an ingredient of a smelting recipe.</summary>
    SmeltInput,
    /// <summary>Burned as fuel by a machine or locomotive.</summary>
    Fuel,
    /// <summary>Used up building a structure or train.</summary>
    Construction,
    /// <summary>Consumed by labs to advance research.</summary>
    Research,
}
