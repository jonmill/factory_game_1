namespace Factory.Sim;

/// <summary>Whether a logistics station hands an item out or wants it brought in.</summary>
public enum StationMode
{
    /// <summary>Other stations may take this item from here.</summary>
    Supply,
    /// <summary>Other stations bring this item here until it is full.</summary>
    Demand,
}
