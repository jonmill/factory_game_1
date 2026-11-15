namespace Factory.Sim;

/// <summary>
/// Path-finding for people: A* over tiles (eight directions, no corner cutting, wrapping around the
/// planet), then straightened wherever a direct line is clear, so people walk naturally around
/// buildings, water and trees instead of zig-zagging between tile centres. A path is planned when the
/// target changes or moves, or when the walker gets stuck (say, something was built in the way), and
/// it is walked with the same sliding collision as manual movement. Deterministic: ties in the open
/// list break on insertion order.
/// </summary>
public sealed partial class Planet
{
    /// <summary>Most tiles one search may expand before settling for the closest point it found.</summary>
    private const int MaxSearchNodes = 6000;

    /// <summary>Blocked ticks in a row before a walker plans again.</summary>
    private const int StuckLimit = 20;

    /// <summary>How many waypoints ahead straightening looks for a clear line.</summary>
    private const int StraightenLookahead = 16;

    /// <summary>
    /// True if a person can stand in the middle of a tile. Buildings' boxes always cover the centres of
    /// their own tiles and never reach a neighbour's, and a tree's trunk always covers its own tile's
    /// centre, so this needs no geometry.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <returns>Whether the tile's centre is free.</returns>
    public bool IsWalkable(Int2 tile) =>
        (TerrainAt(tile) != Terrain.Water) && (BuildingAt(tile) is null) && (ResourceAt(tile) is not { IsTree: true });

    /// <summary>
    /// Walks a person toward a target along a planned path, colliding as they go. Returns true once
    /// within <paramref name="stopDist"/> of the target.
    /// </summary>
    private bool WalkTowards(Unit u, Int2 targetSub, int stopDist)
    {
        if (DistTo(u.Pos, targetSub) <= stopDist)
        {
            u.Path.Clear();
            u.PathStop = -1;

            return true;
        }

        bool moved = (u.PathStop != stopDist) || (SubTorus.DistSq(u.PathGoal, targetSub) > (long)Fixed.One * Fixed.One);
        if (moved || (u.StuckTicks >= StuckLimit))
        {
            PlanPath(u, targetSub, stopDist);
        }

        // Follow the waypoints, then go straight for the target itself.
        Int2 next = u.Path.Count > 0 ? u.Path[0] : targetSub;
        long toNext = DistTo(u.Pos, next);
        long step = System.Math.Min(u.Speed, u.Path.Count > 0 ? toNext : toNext - stopDist);
        Int2 before = u.Pos;
        if ((step > 0) && (toNext > 0))
        {
            Int2 d = SubTorus.Delta(u.Pos, next);
            Int2 move = new Int2((int)(d.X * step / toNext), (int)(d.Y * step / toNext));
            if (move == Int2.Zero)
            {
                // A tiny step can round to nothing; nudge along the main axis so the last bit gets covered.
                move = System.Math.Abs(d.X) >= System.Math.Abs(d.Y) ? new Int2(System.Math.Sign(d.X), 0) : new Int2(0, System.Math.Sign(d.Y));
            }

            MoveWithCollision(u, move);
        }

        if ((u.Path.Count > 0) && (DistTo(u.Pos, u.Path[0]) <= 16))
        {
            u.Path.RemoveAt(0);
        }

        u.StuckTicks = u.Pos == before ? u.StuckTicks + 1 : 0;

        return DistTo(u.Pos, targetSub) <= stopDist;
    }

    private long DistTo(Int2 a, Int2 b)
    {
        Int2 d = SubTorus.Delta(a, b);

        return Fixed.ISqrt(((long)d.X * d.X) + ((long)d.Y * d.Y));
    }

    private void PlanPath(Unit u, Int2 targetSub, int stopDist)
    {
        u.Path.Clear();
        u.PathGoal = targetSub;
        u.PathStop = stopDist;
        u.StuckTicks = 0;
        List<Int2> tiles = FindPath(u.Pos, targetSub, stopDist);
        Straighten(u.Pos, tiles.Select(Fixed.TileCenter).ToList(), u.Path);
    }

    /// <summary>
    /// The tiles to walk through, start excluded, to reach a tile whose centre is within
    /// <paramref name="stopDist"/> of the target; if there is no such tile in range, the path to the
    /// closest point found. Empty when already there or when a straight walk will do.
    /// </summary>
    internal List<Int2> FindPath(Int2 fromSub, Int2 targetSub, int stopDist)
    {
        Int2 start = Torus.Wrap(Fixed.ToTile(fromSub));
        Int2 goal = Torus.Wrap(Fixed.ToTile(targetSub));
        long stop2 = (long)stopDist * stopDist;
        bool IsGoal(Int2 t) => SubTorus.DistSq(Fixed.TileCenter(t), targetSub) <= stop2;
        int H(Int2 t)
        {
            Int2 d = Torus.Delta(t, goal);
            int ax = System.Math.Abs(d.X), ay = System.Math.Abs(d.Y);

            return (10 * System.Math.Max(ax, ay)) + (4 * System.Math.Min(ax, ay));
        }

        if (IsGoal(start))
        {
            return [];
        }

        Dictionary<Int2, Int2> cameFrom = [];
        Dictionary<Int2, int> cost = new() { [start] = 0 };
        PriorityQueue<Int2, (int F, int H, int Seq)> open = new PriorityQueue<Int2, (int, int, int)>();
        int seq = 0;
        open.Enqueue(start, (H(start), H(start), seq++));
        Int2 best = start;
        int bestH = H(start);
        int expanded = 0;
        Int2? reached = null;
        while ((open.Count > 0) && (expanded < MaxSearchNodes))
        {
            open.TryDequeue(out Int2 cur, out (int F, int H, int Seq) pri);
            if (pri.F - pri.H > cost[cur])
            {
                continue; // stale entry
            }

            expanded++;
            if (IsGoal(cur) && (cur != start))
            {
                reached = cur;
                break;
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0) && (dy == 0))
                    {
                        continue;
                    }

                    Int2 n = Torus.Wrap(cur + new Int2(dx, dy));
                    if (IsWalkable(n) == false)
                    {
                        continue;
                    }

                    // No cutting corners past something solid.
                    if ((dx != 0) && (dy != 0) &&
                        ((IsWalkable(Torus.Wrap(cur + new Int2(dx, 0))) == false) || (IsWalkable(Torus.Wrap(cur + new Int2(0, dy))) == false)))
                    {
                        continue;
                    }

                    int g = cost[cur] + ((dx != 0) && (dy != 0) ? 14 : 10);
                    if (cost.TryGetValue(n, out int old) && (old <= g))
                    {
                        continue;
                    }

                    cost[n] = g;
                    cameFrom[n] = cur;
                    int h = H(n);
                    open.Enqueue(n, (g + h, h, seq++));
                    if (h < bestH)
                    {
                        best = n;
                        bestH = h;
                    }
                }
            }
        }

        List<Int2> path = [];
        for (Int2 t = reached ?? best; t != start; t = cameFrom[t])
        {
            path.Add(t);
        }

        path.Reverse();

        return path;
    }

    /// <summary>
    /// Drops waypoints that a straight walk can skip, looking a few ahead from each kept point, and
    /// writes the result to <paramref name="into"/>.
    /// </summary>
    private void Straighten(Int2 from, List<Int2> waypoints, List<Int2> into)
    {
        Int2 anchor = from;
        int i = 0;
        while (i < waypoints.Count)
        {
            int far = i;
            for (int j = System.Math.Min(waypoints.Count - 1, i + StraightenLookahead); j > i; j--)
            {
                if (ClearLine(anchor, waypoints[j]))
                {
                    far = j;
                    break;
                }
            }

            into.Add(waypoints[far]);
            anchor = waypoints[far];
            i = far + 1;
        }
    }

    /// <summary>True if a person could walk straight from one point to another without touching anything solid.</summary>
    private bool ClearLine(Int2 a, Int2 b)
    {
        Int2 d = SubTorus.Delta(a, b);
        long len = Fixed.ISqrt(((long)d.X * d.X) + ((long)d.Y * d.Y));
        int steps = (int)(len / 128) + 1;
        for (int k = 1; k <= steps; k++)
        {
            Int2 p = a + new Int2((int)(d.X * k / steps), (int)(d.Y * k / steps));
            if (IsBlockedForPerson(p))
            {
                return false;
            }
        }

        return true;
    }
}
