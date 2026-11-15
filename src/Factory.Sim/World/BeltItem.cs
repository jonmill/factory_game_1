namespace Factory.Sim;

/// <summary>An item riding a belt tile.</summary>
public sealed class BeltItem
{
    /// <summary>Item id.</summary>
    public string Item { get; init; } = "";
    /// <summary>How far along the tile it is, 0 (back edge) to <see cref="Fixed.One"/> (front edge).</summary>
    public int Pos { get; internal set; }
}
