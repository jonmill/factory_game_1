namespace Factory.Sim;

/// <summary>Helpers and extension methods for <see cref="Dir"/>.</summary>
public static class Dirs
{
    /// <summary>All four directions in clockwise order starting from <see cref="Dir.North"/>.</summary>
    public static readonly Dir[] All = [Dir.North, Dir.East, Dir.South, Dir.West];

    /// <summary>Unit tile offset for a direction (north is negative Y).</summary>
    public static Int2 Vec(this Dir d) => d switch
    {
        Dir.North => new Int2(0, -1),
        Dir.East => new Int2(1, 0),
        Dir.South => new Int2(0, 1),
        _ => new Int2(-1, 0),
    };

    /// <summary>The direction a quarter turn clockwise from <paramref name="d"/>.</summary>
    public static Dir CW(this Dir d) => (Dir)(((int)d + 1) & 3);
    /// <summary>The direction facing the opposite way to <paramref name="d"/>.</summary>
    public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) & 3);
}
