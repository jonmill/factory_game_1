namespace Factory.Sim;

/// <summary>One item setting on a logistics station.</summary>
/// <param name="Item">Item id.</param>
/// <param name="Mode">Supply or demand.</param>
public sealed record StationSlot(string Item, StationMode Mode);
