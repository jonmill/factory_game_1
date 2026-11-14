namespace Factory.Sim;

/// <summary>Base of every simulation object addressed by id (buildings, sites, units, trains).</summary>
public abstract class Entity
{
    /// <summary>World-unique id, allocated sequentially; 0 is never a valid entity id.</summary>
    public int Id { get; internal set; }
    /// <summary>Planet the entity lives on.</summary>
    public Planet Planet { get; internal set; } = null!;
}
