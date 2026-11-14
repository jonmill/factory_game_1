namespace Factory.Sim;

/// <summary>Fixed point: 1 tile = 1024 sub-units. All continuous positions are integers for determinism.</summary>
public static class Fixed
{
    /// <summary>Number of sub-units in one tile.</summary>
    public const int One = 1024;
    /// <summary>Half a tile in sub-units; the offset from a tile's corner to its centre.</summary>
    public const int Half = 512;
    /// <summary>Bit shift equivalent to dividing by <see cref="One"/> (log2 of 1024).</summary>
    public const int Shift = 10;
    /// <summary>Sub-unit position of the centre of a tile.</summary>
    /// <param name="tile">Tile coordinate.</param>
    /// <returns>The tile's centre in sub-units.</returns>
    public static Int2 TileCenter(Int2 tile) => new((tile.X * One) + Half, (tile.Y * One) + Half);
    /// <summary>Tile containing a sub-unit position (floors towards negative infinity).</summary>
    /// <param name="sub">Position in sub-units.</param>
    /// <returns>The tile coordinate.</returns>
    public static Int2 ToTile(Int2 sub) => new(sub.X >> Shift, sub.Y >> Shift);

    /// <summary>Deterministic integer square root.</summary>
    public static long ISqrt(long n)
    {
        if (n <= 0)
        {
            return 0;
        }

        long x = (long)System.Math.Sqrt(n); // initial guess, corrected below so result is exact
        while (x * x > n)
        {
            x--;
        }

        while ((x + 1) * (x + 1) <= n)
        {
            x++;
        }

        return x;
    }
}
