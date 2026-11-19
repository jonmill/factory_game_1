using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Deterministic spiral search for valid building spots.</summary>
public static class Placement
{
    /// <summary>Searches rings outward from <paramref name="near"/> for the first valid origin for a building, keeping walkways clear and avoiding ore for non-drills.</summary>
    /// <param name="p">The planet to search.</param>
    /// <param name="def">The building to place.</param>
    /// <param name="rot">Placement rotation.</param>
    /// <param name="near">Centre of the search.</param>
    /// <param name="minR">Smallest ring radius to try.</param>
    /// <param name="maxR">Largest ring radius to try.</param>
    /// <param name="accept">Optional extra filter on candidate origins.</param>
    /// <returns>A valid origin, or <c>null</c> if none was found.</returns>
    public static Int2? FindSpot(Planet p, BuildingDef def, Dir rot, Int2 near, int minR, int maxR, Func<Int2, bool>? accept = null)
    {
        (int w, int h) = Planet.SizeOf(def, rot);
        for (int r = minR; r <= maxR; r++)
        {
            foreach (Int2 d in Ring(r))
            {
                Int2 origin = p.Torus.Wrap(near + d);
                if (p.CanPlace(def, origin, rot) is not null)
                {
                    continue;
                }

                // Shore buildings too: water around them is fine, but a building beside them could wall them in.
                if (Margin(p, origin, w, h) == false)
                {
                    continue;
                }

                if (HasOre(p, origin, w, h) && (def.Kind != BuildingKind.Drill))
                {
                    continue;
                }

                if ((accept is not null) && (accept(origin) == false))
                {
                    continue;
                }

                return origin;
            }
        }

        return null;
    }

    /// <summary>Keep a 1-tile walkway around buildings so bots don't wall things in.</summary>
    private static bool Margin(Planet p, Int2 origin, int w, int h)
    {
        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                Int2 t = new Int2(origin.X + x, origin.Y + y);
                if ((p.OccupantAt(t) != 0) || p.IsRail(t))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasOre(Planet p, Int2 origin, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (p.KnownResourceAt(new Int2(origin.X + x, origin.Y + y)) is { IsTree: false })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Offsets forming the square ring at Chebyshev radius <paramref name="r"/>, in a fixed order.</summary>
    public static IEnumerable<Int2> Ring(int r)
    {
        if (r == 0)
        {
            yield return Int2.Zero;
            yield break;
        }

        for (int x = -r; x <= r; x++)
        {
            yield return new Int2(x, -r);
            yield return new Int2(x, r);
        }

        for (int y = -r + 1; y <= r - 1; y++)
        {
            yield return new Int2(-r, y);
            yield return new Int2(r, y);
        }
    }

    /// <summary>The tile at the centre of a building's footprint, wrapped onto the planet.</summary>
    public static Int2 Center(Building b) => b.Planet.Torus.Wrap(new Int2(b.Origin.X + (b.W / 2), b.Origin.Y + (b.H / 2)));

    /// <summary>Chebyshev distance from a tile to the nearest tile of a rectangle (torus aware).</summary>
    public static int DistToRect(Planet p, Int2 t, Int2 origin, int w, int h)
    {
        Int2 d = p.Torus.Delta(origin, t);
        int dx = d.X < 0 ? -d.X : d.X >= w ? d.X - w + 1 : 0;
        int dy = d.Y < 0 ? -d.Y : d.Y >= h ? d.Y - h + 1 : 0;

        return System.Math.Max(dx, dy);
    }
}
