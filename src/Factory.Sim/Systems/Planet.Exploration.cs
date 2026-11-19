namespace Factory.Sim;

/// <summary>
/// Exploration: which tiles have been seen. Robots uncover the map around them as they move and buildings
/// around them once built; explored stays explored. Robots only work what has been explored (gathering,
/// mining, building), and a helper bot on the Explore job walks an outward spiral from the base to
/// uncover more. Explored tiles are tracked in chunks so the client only redraws the fog that changed,
/// which keeps it cheap on big maps.
/// </summary>
public sealed partial class Planet
{
    /// <summary>Edge length, in tiles, of the chunks the fog is tracked (and redrawn) in.</summary>
    public const int FogChunk = 32;

    private readonly bool[] _explored;
    private readonly int[] _fogVersions;
    private readonly int _fogChunksX;

    /// <summary>How many tiles have been explored.</summary>
    public int ExploredTiles { get; private set; }

    /// <summary>Bumped whenever any tile is explored.</summary>
    public int FogVersion { get; private set; }

    /// <summary>True if the tile has been seen (coordinates are wrapped).</summary>
    /// <param name="t">The tile.</param>
    /// <returns>Whether it is explored.</returns>
    public bool IsExplored(Int2 t) => _explored[Index(t)];

    /// <summary>True if every tile of a rectangle has been seen.</summary>
    /// <param name="origin">Top-left tile.</param>
    /// <param name="w">Width in tiles.</param>
    /// <param name="h">Height in tiles.</param>
    /// <returns>Whether all of it is explored.</returns>
    public bool IsAreaExplored(Int2 origin, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (IsExplored(origin + new Int2(x, y)) == false)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Bumped whenever a tile in the given fog chunk is explored, so the client redraws only that chunk.</summary>
    /// <param name="cx">Chunk column.</param>
    /// <param name="cy">Chunk row.</param>
    /// <returns>The chunk's version.</returns>
    public int FogChunkVersion(int cx, int cy) => _fogVersions[(cy * _fogChunksX) + cx];

    /// <summary>The resource on a tile as far as anyone knows: null on tiles not yet explored.</summary>
    /// <param name="t">The tile.</param>
    /// <returns>The resource, if the tile is explored and has one.</returns>
    public ResourceDef? KnownResourceAt(Int2 t) => IsExplored(t) ? ResourceAt(t) : null;

    /// <summary>Uncovers every tile within a radius of a tile.</summary>
    /// <param name="center">The tile seen from.</param>
    /// <param name="radius">Sight radius in tiles.</param>
    internal void Reveal(Int2 center, int radius)
    {
        int r2 = (radius * radius) + radius; // a rounder disc than r²
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dy * dy) > r2)
                {
                    continue;
                }

                Int2 t = Torus.Wrap(center + new Int2(dx, dy));
                int i = (t.Y * Width) + t.X;
                if (_explored[i])
                {
                    continue;
                }

                _explored[i] = true;
                ExploredTiles++;
                _fogVersions[(t.Y / FogChunk * _fogChunksX) + (t.X / FogChunk)]++;
                FogVersion++;
            }
        }
    }

    /// <summary>Uncovers the whole planet (empty test worlds and sandbox set-ups).</summary>
    internal void RevealAll()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_explored[(y * Width) + x] == false)
                {
                    Reveal(new Int2(x, y), 0);
                }
            }
        }
    }

    /// <summary>Uncovers the map around a robot when it has moved to a new tile (or another planet).</summary>
    private void RevealAround(Unit u)
    {
        if ((u.Planet != this) || (u.SightFrom is { } from && (from.Planet == Id) && (from.Tile == u.Tile)))
        {
            return;
        }

        u.SightFrom = (Id, u.Tile);
        Reveal(u.Tile, u.Robot.Sight);
    }

    // ------------------------------------------------------------------ explore job

    /// <summary>
    /// Where the outward spiral starts: the lander on its planet, otherwise the storage nearest the robot
    /// (a landed ferry, say), otherwise the robot itself.
    /// </summary>
    private Int2 ExploreHub(Unit u)
    {
        if (World.Get<Building>(LanderId) is { } lander && (lander.Planet == this))
        {
            return Fixed.ToTile(lander.CenterSub);
        }

        return StoragesByDistance(u.Pos).FirstOrDefault() is { } store ? Fixed.ToTile(store.CenterSub) : u.Tile;
    }

    /// <summary>Spacing, in tiles, of the spiral's points: a little under the robot's sight, so the discs overlap.</summary>
    private static int SpiralStep(Unit u) => System.Math.Max(3, u.Robot.Sight - 1);

    /// <summary>How many points the spiral has before it has wrapped all the way round the planet.</summary>
    private int SpiralLength(Unit u)
    {
        int rings = ((System.Math.Max(Width, Height) / 2) + SpiralStep(u) - 1) / SpiralStep(u);
        int side = (2 * rings) + 1;

        return side * side;
    }

    /// <summary>
    /// The n-th point of a square spiral around the origin: (0,0), then ring 1 going round, then ring 2
    /// and so on.
    /// </summary>
    public static Int2 SpiralPoint(int n)
    {
        if (n == 0)
        {
            return Int2.Zero;
        }

        int k = (int)System.Math.Ceiling((System.Math.Sqrt(n + 1) - 1) / 2); // ring
        int side = 2 * k;
        int start = ((2 * k) - 1) * ((2 * k) - 1); // first index on ring k
        int m = n - start;
        // Walk the ring clockwise from just below its top-right corner: down the east side, along the
        // south, up the west and back along the north.
        if (m < side)
        {
            return new Int2(k, -k + 1 + m);
        }

        m -= side;
        if (m < side)
        {
            return new Int2(k - 1 - m, k);
        }

        m -= side;
        if (m < side)
        {
            return new Int2(-k, k - 1 - m);
        }

        m -= side;

        return new Int2(-k + 1 + m, -k);
    }

    /// <summary>True if anything around a spiral point is still unexplored (sampled at its centre and eight points round it).</summary>
    private bool NeedsExploring(Int2 point, int step)
    {
        int d = System.Math.Max(1, step - 1);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (IsExplored(point + new Int2(dx * d, dy * d)) == false)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void UpdateExplore(Unit u)
    {
        if (u.Carry.IsEmpty == false)
        {
            // Drop off anything left over from an earlier job first.
            DeliverToStorage(u);

            return;
        }

        int step = SpiralStep(u);
        switch (u.Phase)
        {
            case UnitPhase.None or UnitPhase.Seek:
                {
                    int length = SpiralLength(u);
                    Int2 hub = ExploreHub(u);
                    // Carry on round the spiral from where the robot got to, skipping points already seen.
                    for (int tries = 0; tries < length; tries++)
                    {
                        if (u.ExploreIndex >= length)
                        {
                            u.ExploreIndex = 0;
                        }

                        Int2 point = Torus.Wrap(hub + (SpiralPoint(u.ExploreIndex) * step));
                        if (NeedsExploring(point, step))
                        {
                            u.TargetTile = point;
                            u.Phase = UnitPhase.MoveTo;
                            // Time to get there and back twice over, so a point it can't reach is given up on.
                            long tiles = (long)System.Math.Sqrt(Torus.DistSq(u.Tile, point)) + 1;
                            u.WorkTicks = (int)System.Math.Min(int.MaxValue, (tiles * Fixed.One * 2 / System.Math.Max(1, u.Speed)) + 600);
                            u.Status = "Exploring";

                            return;
                        }

                        u.ExploreIndex++;
                    }

                    u.Status = "Explored everything it can reach";
                    u.Phase = UnitPhase.Wait;
                    u.WaitTicks = 600;

                    return;
                }
            case UnitPhase.MoveTo:
                {
                    u.Status = "Exploring";
                    // Close enough once the point itself is in sight; walking the last stretch adds nothing.
                    bool there = MoveTowards(u, Fixed.TileCenter(u.TargetTile), System.Math.Max(1, u.Robot.Sight - 2) * Fixed.One);
                    if (there || (NeedsExploring(u.TargetTile, step) == false) || (--u.WorkTicks <= 0))
                    {
                        u.ExploreIndex++;
                        u.Phase = UnitPhase.Seek;
                    }

                    break;
                }
            case UnitPhase.Wait:
                if (--u.WaitTicks <= 0)
                {
                    u.Phase = UnitPhase.Seek;
                }

                break;
            default:
                u.Phase = UnitPhase.Seek;
                break;
        }
    }
}
