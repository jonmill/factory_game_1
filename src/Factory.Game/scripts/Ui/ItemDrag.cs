using Factory.Sim;

namespace Factory.Game.Ui;

/// <summary>A stack of items being dragged between slots.</summary>
/// <param name="Item">Item id.</param>
/// <param name="Count">How many are being moved.</param>
/// <param name="FromPockets">True when dragged out of the commander's pockets, false when out of a building.</param>
/// <param name="BuildingId">The building it came from, or 0 for the pockets.</param>
/// <param name="Part">Which of the building's inventories it came from.</param>
/// <param name="UnitId">The robot a module was dragged out of, or 0.</param>
/// <param name="ModuleSlot">The module slot it was dragged out of, or -1 when it isn't an installed module.</param>
public sealed record ItemDrag(string Item, int Count, bool FromPockets, int BuildingId = 0, InventoryPart Part = InventoryPart.Any, int UnitId = 0, int ModuleSlot = -1)
{
    /// <summary>True when this is a module being taken out of a robot.</summary>
    public bool IsInstalledModule => ModuleSlot >= 0;
}
