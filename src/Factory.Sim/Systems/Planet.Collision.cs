namespace Factory.Sim;

/// <summary>
/// Collision for people walking the planet, in the usual factory-game pattern: a person is a small
/// square box, and buildings, water and tree trunks are boxes it can't overlap. Buildings' boxes sit a
/// little inside their tiles, so a one-tile gap between two buildings is walkable but touching
/// buildings form a wall. Movement is resolved one axis at a time, so walking diagonally into a wall
/// slides along it. Ghosts, rail and ore never block. All in integer sub-units, so it is deterministic.
/// </summary>
public sealed partial class Planet
{
    /// <summary>Half the width of a person's collision box: 0.4 × 0.4 tiles in all.</summary>
    public const int PersonHalf = 205;

    /// <summary>Half the width of a tree trunk's collision box.</summary>
    public const int TrunkHalf = 205;

    /// <summary>How far a building's box sits inside its tiles: machines and other multi-tile buildings.</summary>
    private const int LargeInset = 307;

    /// <summary>How far a single-tile building's box sits inside its tile, e.g. a crate.</summary>
    private const int SmallInset = 154;

    /// <summary>Half the width of thin posts such as poles and sorter arms.</summary>
    private const int PostHalf = 154;

    /// <summary>
    /// Where a tree's trunk stands within its tile, in sub-units from the tile's top-left corner.
    /// Trees are scattered a little within their tiles; drawing uses the same spot so what you bump
    /// into is what you see.
    /// </summary>
    /// <param name="tile">The tree's tile.</param>
    /// <returns>The trunk's offset within the tile.</returns>
    public static Int2 TreeTrunkOffset(Int2 tile)
    {
        uint h = (uint)((tile.X * 73856093) ^ (tile.Y * 19349663));
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;

        return new Int2(358 + (int)((h & 7) * 43), 389 + (int)(((h >> 3) & 7) * 43));
    }

    /// <summary>
    /// Moves a person by up to <paramref name="delta"/> sub-units, stopping at anything solid. Anything the
    /// person already overlaps is ignored, so a building finished on top of someone can't trap them.
    /// </summary>
    /// <param name="u">The person to move.</param>
    /// <param name="delta">The step to take.</param>
    internal void MoveWithCollision(Unit u, Int2 delta)
    {
        Int2 pos = u.Pos;
        int reach = PersonHalf + System.Math.Max(System.Math.Abs(delta.X), System.Math.Abs(delta.Y)) + Fixed.One;
        List<Box> solids = SolidsAround(pos, reach);
        List<Box> inside = solids.Where(s => s.Overlaps(PersonBox(pos))).ToList();

        int x = pos.X + delta.X;
        foreach (Box s in solids)
        {
            if ((inside.Contains(s) == false) && s.Overlaps(PersonBox(new Int2(x, pos.Y))))
            {
                x = delta.X > 0 ? System.Math.Min(x, s.X0 - PersonHalf) : System.Math.Max(x, s.X1 + PersonHalf);
            }
        }

        int y = pos.Y + delta.Y;
        foreach (Box s in solids)
        {
            if ((inside.Contains(s) == false) && s.Overlaps(PersonBox(new Int2(x, y))))
            {
                y = delta.Y > 0 ? System.Math.Min(y, s.Y0 - PersonHalf) : System.Math.Max(y, s.Y1 + PersonHalf);
            }
        }

        u.Pos = SubTorus.Wrap(new Int2(x, y));
    }

    /// <summary>
    /// Moves anyone a new building was finished on top of, or walled into a small pocket by, to the
    /// nearest free tile with room to move, like Factorio nudging the player out of a freshly placed entity.
    /// </summary>
    internal void PushOutOfSolids()
    {
        foreach (Unit u in Units.Where(u => (u.Boarded == false) && (IsBlockedForPerson(u.Pos) || IsPocket(u.Tile))).ToList())
        {
            if (NearestOpenTile(u.Tile) is { } t)
            {
                u.Pos = Fixed.TileCenter(t);
                u.Path.Clear();
                u.FailedPlans = 0;
            }
        }
    }

    private const int PocketTiles = 48;

    /// <summary>True if only a few tiles can be reached from this one without crossing water or a building (trees can be cut, so they count as open).</summary>
    private bool IsPocket(Int2 start)
    {
        if (IsOpen(start) == false)
        {
            return true;
        }

        HashSet<Int2> seen = [start];
        Queue<Int2> open = new Queue<Int2>();
        open.Enqueue(start);
        while (open.Count > 0)
        {
            Int2 t = open.Dequeue();
            foreach (Int2 d in new[] { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) })
            {
                Int2 n = Torus.Wrap(t + d);
                if (IsOpen(n) && seen.Add(n))
                {
                    if (seen.Count >= PocketTiles)
                    {
                        return false;
                    }

                    open.Enqueue(n);
                }
            }
        }

        return true;
    }

    private bool IsOpen(Int2 tile) => (TerrainAt(tile) == Terrain.Ground) && (BuildingAt(tile) is null);

    private Int2? NearestOpenTile(Int2 from)
    {
        for (int r = 1; r <= 12; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if ((System.Math.Abs(dx) != r) && (System.Math.Abs(dy) != r))
                    {
                        continue;
                    }

                    Int2 t = Torus.Wrap(from + new Int2(dx, dy));
                    if (IsWalkable(t) && (IsBlockedForPerson(Fixed.TileCenter(t)) == false) && (IsPocket(t) == false))
                    {
                        return t;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>True if a person standing at the position would overlap something solid.</summary>
    /// <param name="pos">Position in sub-units.</param>
    /// <returns>Whether that spot is blocked.</returns>
    public bool IsBlockedForPerson(Int2 pos) => SolidsAround(pos, PersonHalf + Fixed.One).Any(s => s.Overlaps(PersonBox(pos)));

    private static Box PersonBox(Int2 p) => new(p.X - PersonHalf, p.Y - PersonHalf, p.X + PersonHalf, p.Y + PersonHalf);

    /// <summary>
    /// Solid boxes near a position, in the same unwrapped coordinates as the position, so the seam where
    /// the planet wraps needs no special case.
    /// </summary>
    private List<Box> SolidsAround(Int2 pos, int reach)
    {
        List<Box> solids = [];
        HashSet<int> seen = [];
        Int2 lo = Fixed.ToTile(pos - new Int2(reach, reach));
        Int2 hi = Fixed.ToTile(pos + new Int2(reach, reach));
        for (int ty = lo.Y; ty <= hi.Y; ty++)
        {
            for (int tx = lo.X; tx <= hi.X; tx++)
            {
                Int2 tile = new Int2(tx, ty);
                Int2 corner = new Int2(tx * Fixed.One, ty * Fixed.One);
                if ((TerrainAt(tile) != Terrain.Ground) && (_hoverWalk == false))
                {
                    solids.Add(new Box(corner.X, corner.Y, corner.X + Fixed.One, corner.Y + Fixed.One));
                }

                if (ResourceAt(tile) is { IsTree: true })
                {
                    Int2 trunk = corner + TreeTrunkOffset(Torus.Wrap(tile));
                    solids.Add(new Box(trunk.X - TrunkHalf, trunk.Y - TrunkHalf, trunk.X + TrunkHalf, trunk.Y + TrunkHalf));
                }

                if ((BuildingAt(tile) is { } b) && seen.Add(b.Id))
                {
                    // Place the building's box next to this tile in unwrapped space.
                    Int2 into = Torus.Delta(b.Origin, Torus.Wrap(tile));
                    Int2 origin = new Int2((tx - into.X) * Fixed.One, (ty - into.Y) * Fixed.One);
                    solids.Add(BuildingBox(b, origin));
                }
            }
        }

        return solids;
    }

    private static Box BuildingBox(Building b, Int2 origin)
    {
        int w = b.W * Fixed.One, h = b.H * Fixed.One;
        if (b.Def.Kind is BuildingKind.Pole or BuildingKind.Sorter)
        {
            int cx = origin.X + (w / 2), cy = origin.Y + (h / 2);

            return new Box(cx - PostHalf, cy - PostHalf, cx + PostHalf, cy + PostHalf);
        }

        int inset = (b.W > 1) || (b.H > 1) ? LargeInset : SmallInset;

        return new Box(origin.X + inset, origin.Y + inset, origin.X + w - inset, origin.Y + h - inset);
    }

    /// <summary>An axis-aligned box in sub-units; touching edges don't count as overlapping.</summary>
    private readonly record struct Box(int X0, int Y0, int X1, int Y1)
    {
        public bool Overlaps(Box o) => (X0 < o.X1) && (X1 > o.X0) && (Y0 < o.Y1) && (Y1 > o.Y0);
    }
}
