namespace Factory.Sim;

/// <summary>Wrap-around (toroidal) space. Planets wrap east-west and north-south.</summary>
public readonly struct Torus(int width, int height)
{
    /// <summary>Map width in tiles; X coordinates wrap modulo this.</summary>
    public int Width { get; } = width;
    /// <summary>Map height in tiles; Y coordinates wrap modulo this.</summary>
    public int Height { get; } = height;

    /// <summary>Euclidean modulo: like <c>%</c> but always returns a value in [0, <paramref name="m"/>).</summary>
    public static int Mod(int v, int m)
    {
        int r = v % m;

        return r < 0 ? r + m : r;
    }

    /// <summary>Maps a tile coordinate into the canonical range [0, Width) x [0, Height).</summary>
    public Int2 Wrap(Int2 p) => new(Mod(p.X, Width), Mod(p.Y, Height));

    private static int D(int a, int b, int m)
    {
        int d = Mod(b - a, m);
        if (d > m / 2)
        {
            d -= m;
        }

        return d;
    }

    /// <summary>Shortest vector from a to b across the wrap.</summary>
    public Int2 Delta(Int2 a, Int2 b) => new(D(a.X, b.X, Width), D(a.Y, b.Y, Height));

    /// <summary>Chessboard distance in tiles between two points, taking the shortest path across the wrap.</summary>
    public int Chebyshev(Int2 a, Int2 b)
    {
        Int2 d = Delta(a, b);

        return System.Math.Max(System.Math.Abs(d.X), System.Math.Abs(d.Y));
    }
    /// <summary>Squared Euclidean distance in tiles between two points, taking the shortest path across the wrap.</summary>
    public long DistSq(Int2 a, Int2 b)
    {
        Int2 d = Delta(a, b);

        return ((long)d.X * d.X) + ((long)d.Y * d.Y);
    }
}
