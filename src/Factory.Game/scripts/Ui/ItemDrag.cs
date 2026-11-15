using Factory.Sim;

namespace Factory.Game.Ui;

/// <summary>A stack of items being dragged between slots.</summary>
/// <param name="Item">Item id.</param>
/// <param name="Count">How many are being moved.</param>
/// <param name="FromPockets">True when dragged out of the commander's pockets, false when out of a building.</param>
/// <param name="BuildingId">The building it came from, or 0 for the pockets.</param>
/// <param name="Part">Which of the building's inventories it came from.</param>
public sealed record ItemDrag(string Item, int Count, bool FromPockets, int BuildingId = 0, InventoryPart Part = InventoryPart.Any);
