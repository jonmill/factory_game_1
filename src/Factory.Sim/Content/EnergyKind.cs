using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>How a building is powered.</summary>
public enum EnergyKind
{
    /// <summary>Needs no energy.</summary>
    None,
    /// <summary>Burns fuel items from its own inventory.</summary>
    Burner,
    /// <summary>Draws power from the electric network via nearby poles.</summary>
    Electric
}
