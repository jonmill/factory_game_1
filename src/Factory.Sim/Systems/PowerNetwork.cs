namespace Factory.Sim;

/// <summary>A connected set of power poles and the generators, batteries and consumers they supply.</summary>
public sealed class PowerNetwork
{
    /// <summary>Network id (the id of its root pole).</summary>
    public int Id { get; init; }
    /// <summary>Poles linked into this network.</summary>
    public List<Building> Poles { get; } = [];
    /// <summary>Electric machines drawing power from this network.</summary>
    public List<Building> Consumers { get; } = [];
    /// <summary>Generators (steam engines) feeding this network.</summary>
    public List<Building> Generators { get; } = [];
    /// <summary>Batteries that store surplus and cover shortfalls.</summary>
    public List<Building> Batteries { get; } = [];
    /// <summary>Last tick stats in joules per tick.</summary>
    public long DemandJ { get; internal set; }
    /// <summary>Energy delivered to consumers last tick (from generators and batteries), in joules.</summary>
    public long SuppliedJ { get; internal set; }
    /// <summary>Maximum generation available last tick from runnable generators, in joules.</summary>
    public long GenCapacityJ { get; internal set; }
    /// <summary>Energy held in all batteries, in joules.</summary>
    public long StoredJ { get; internal set; }
    /// <summary>Total battery capacity, in joules.</summary>
    public long StorageCapacityJ { get; internal set; }
    /// <summary>Fraction of demand met last tick, 0..1000 (1000 when there is no demand).</summary>
    public int Satisfaction { get; internal set; }
}
