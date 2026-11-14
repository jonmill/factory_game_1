namespace Factory.Sim;

/// <summary>Integer 2D vector. Used for tile coordinates and sub-tile (fixed point) positions.</summary>
/// <param name="X">Horizontal component; increases eastwards.</param>
/// <param name="Y">Vertical component; increases southwards.</param>
public readonly record struct Int2(int X, int Y)
{
    /// <summary>The zero vector (0, 0).</summary>
    public static readonly Int2 Zero = new(0, 0);
    /// <summary>Component-wise addition.</summary>
    public static Int2 operator +(Int2 a, Int2 b) => new(a.X + b.X, a.Y + b.Y);
    /// <summary>Component-wise subtraction.</summary>
    public static Int2 operator -(Int2 a, Int2 b) => new(a.X - b.X, a.Y - b.Y);
    /// <summary>Scales both components by an integer factor.</summary>
    public static Int2 operator *(Int2 a, int k) => new(a.X * k, a.Y * k);
    /// <summary>Formats the vector as <c>(X,Y)</c>.</summary>
    public override string ToString() => $"({X},{Y})";
}
