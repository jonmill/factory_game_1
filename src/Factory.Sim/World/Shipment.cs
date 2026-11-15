namespace Factory.Sim;

/// <summary>
/// Items in flight between two logistics stations, carried by a drone (same planet) or a vessel (another
/// planet). The carrier counts as busy until it is back home at <see cref="BackTick"/>.
/// </summary>
public sealed class Shipment
{
    /// <summary>Station the carrier belongs to.</summary>
    public int FromId { get; init; }
    /// <summary>Station receiving the items.</summary>
    public int ToId { get; init; }
    /// <summary>Planet of the receiving station.</summary>
    public int ToPlanet { get; init; }
    /// <summary>True for a vessel between planets, false for a drone.</summary>
    public bool Vessel { get; init; }
    /// <summary>Item id.</summary>
    public string Item { get; init; } = "";
    /// <summary>How many are on board; 0 once delivered and on the way back.</summary>
    public int Count { get; internal set; }
    /// <summary>Tick the items arrive.</summary>
    public long ArriveTick { get; init; }
    /// <summary>Tick the carrier is home again.</summary>
    public long BackTick { get; init; }
}
