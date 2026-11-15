using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of an item type, loaded from content JSON.</summary>
public sealed class ItemDef
{
    /// <summary>Unique content id (e.g. "siderite-plate").</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Maximum number of this item that fits in one inventory slot.</summary>
    public int Stack { get; init; } = 50;
    /// <summary>Mass in kg per item, used for train pulling limits.</summary>
    public int Mass { get; init; } = 1;
    /// <summary>Fuel value in kJ (0 = not a fuel).</summary>
    public int FuelKj { get; init; }
    /// <summary>Placeholder render colour as a hex string.</summary>
    public string Color { get; init; } = "#ffffff";
    /// <summary>Set if the item is a robot module that can be installed in a module slot.</summary>
    public ModuleDef? Module { get; init; }
}
