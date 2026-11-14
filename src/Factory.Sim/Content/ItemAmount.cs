using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>A quantity of a single item type.</summary>
/// <param name="Item">Item id.</param>
/// <param name="Count">Number of items.</param>
public readonly record struct ItemAmount(string Item, int Count);
