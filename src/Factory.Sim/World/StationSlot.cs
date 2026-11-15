namespace Factory.Sim;

/// <summary>One item setting on a logistics station.</summary>
/// <param name="Item">Item id.</param>
/// <param name="Mode">Supply or demand.</param>
/// <param name="Max">The most of the item the station keeps; 0 means up to its capacity.</param>
public sealed record StationSlot(string Item, StationMode Mode, int Max = 0);
