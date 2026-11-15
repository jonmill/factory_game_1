using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of a rail vehicle (locomotive or cart), loaded from content JSON.</summary>
public sealed class VehicleDef
{
    /// <summary>Unique content id of the vehicle.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Whether this is a hand cart, steam locomotive or unpowered cart.</summary>
    public VehicleKind Kind { get; init; }
    /// <summary>Art direction the client draws this in; matches the technology era.</summary>
    public VisualStyle Look { get; init; } = VisualStyle.Industrial;
    /// <summary>Items consumed to build the vehicle, keyed by item id.</summary>
    public Dictionary<string, int> Cost { get; init; } = new();
    /// <summary>Work time to build the vehicle, in ticks (60 ticks = 1 second).</summary>
    public int BuildTicks { get; init; } = 120;
    /// <summary>Empty mass in kg.</summary>
    public int Mass { get; init; }
    /// <summary>Max mass (kg) of the pulled consist (everything except locomotives) this can move.</summary>
    public int MaxPull { get; init; }
    /// <summary>Top speed in sub-units per tick.</summary>
    public int MaxSpeed { get; init; }
    /// <summary>Base acceleration in sub-units per tick per tick; reduced as pulled mass increases.</summary>
    public int Accel { get; init; } = 1;
    /// <summary>Fuel consumption in kW at full speed (steam locomotives).</summary>
    public int PowerKw { get; init; }
    /// <summary>Number of cargo inventory slots (a locomotive's slots also hold its fuel).</summary>
    public int Slots { get; init; }
    /// <summary>Water tank size in ticks of running; a steam locomotive uses one unit per tick and stops when empty.</summary>
    public int WaterCapacity { get; init; }
    /// <summary>Placeholder render colour as a hex string.</summary>
    public string Color { get; init; } = "#cccccc";
    /// <summary>The age this vehicle belongs to: its unlocking technology's age (resolved when content loads).</summary>
    [JsonIgnore] public string AgeId { get; internal set; } = "";
    /// <summary><see cref="Cost"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] CostList { get; internal set; } = [];
    /// <summary>True for powered vehicles (anything other than a cart).</summary>
    [JsonIgnore] public bool IsLocomotive => Kind != VehicleKind.Cart;
}
