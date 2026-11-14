namespace Factory.Sim;

/// <summary>One rail vehicle (locomotive or cart) within a train.</summary>
public sealed class Vehicle
{
    /// <summary>Content definition: kind, mass, pulling power, speed and capacity.</summary>
    public VehicleDef Def { get; internal set; } = null!;
    /// <summary>Items carried; for steam locomotives this also holds fuel.</summary>
    public Inventory Cargo { get; internal set; } = null!;
}
