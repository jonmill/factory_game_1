using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Type of rail vehicle.</summary>
public enum VehicleKind
{
    /// <summary>Hand-pumped cart: a locomotive that needs no fuel or water.</summary>
    HandCart,
    /// <summary>Steam locomotive: burns fuel and consumes water, refilled at rail stops near a water tower.</summary>
    Steam,
    /// <summary>Unpowered cargo cart pulled by a locomotive.</summary>
    Cart
}
