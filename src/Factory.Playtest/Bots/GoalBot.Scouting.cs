using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// Scouting: the bot only knows what has been explored, as a player does. While a planet still hides a
/// resource it has, one helper bot explores at home, and away from home the commander walks an outward
/// spiral from the ferry when what it came for hasn't turned up yet.
/// </summary>
public sealed partial class GoalBot
{
    /// <summary>Planets whose explorer has seen everything it can reach: no point sending another.</summary>
    private readonly HashSet<int> _scoutedOut = [];

    /// <summary>The commander's place on its scouting spiral, per planet.</summary>
    private readonly Dictionary<int, int> _scoutIndex = [];

    /// <summary>Resources not yet found on each planet, cached against the planet's fog version.</summary>
    private readonly Dictionary<int, (int FogVersion, HashSet<string> Unfound)> _unfound = [];

    /// <summary>Resources the planet has that nobody has seen a tile of yet.</summary>
    private HashSet<string> UnfoundResources(Planet p)
    {
        if (_unfound.TryGetValue(p.Id, out (int FogVersion, HashSet<string> Unfound) cached) && (cached.FogVersion == p.FogVersion))
        {
            return cached.Unfound;
        }

        HashSet<string> unfound = C.ResourceList.Where(r => (r.IsTree == false) && p.HasResource(r.Id)).Select(r => r.Id).ToHashSet();
        for (int y = 0; (y < p.Height) && (unfound.Count > 0); y++)
        {
            for (int x = 0; x < p.Width; x++)
            {
                if (p.KnownResourceAt(new Int2(x, y)) is { } r)
                {
                    unfound.Remove(r.Id);
                }
            }
        }

        _unfound[p.Id] = (p.FogVersion, unfound);

        return unfound;
    }

    /// <summary>True while one helper bot on the focus planet should be exploring.</summary>
    private bool WantExplorer()
    {
        if (P.Units.Any(u => (u.Job.Kind == JobKind.Explore) && (u.OwnerPlayer == Player) && (u.Status == "Explored everything it can reach")))
        {
            _scoutedOut.Add(P.Id);
        }

        return (_scoutedOut.Contains(P.Id) == false) && (UnfoundResources(P).Count > 0);
    }

    /// <summary>
    /// Walks the commander to the next unexplored point of an outward spiral round a hub (the ferry).
    /// </summary>
    /// <returns>False once it has walked the whole spiral and found nothing new to look at.</returns>
    private bool Scout(Unit leader, Planet there, Int2 hub)
    {
        int step = System.Math.Max(3, leader.Robot.Sight - 1);
        if ((leader.WalkTarget is { } going) && there.Torus.DistSq(going, leader.Tile) > 4)
        {
            if (Unexplored(there, going, step))
            {
                return true; // still on the way
            }
        }

        int rings = ((System.Math.Max(there.Width, there.Height) / 2) + step - 1) / step;
        int length = ((2 * rings) + 1) * ((2 * rings) + 1);
        int index = _scoutIndex.GetValueOrDefault(there.Id);
        for (; index < length; index++)
        {
            Int2 point = there.Torus.Wrap(hub + (Planet.SpiralPoint(index) * step));
            if (Unexplored(there, point, step))
            {
                _scoutIndex[there.Id] = index + 1;
                if (leader.MiningTile is not null)
                {
                    Send(new MineTile(leader.Id, null));
                }

                Send(new WalkTo(leader.Id, point));

                return true;
            }
        }

        _scoutIndex[there.Id] = length;

        return false;
    }

    private static bool Unexplored(Planet p, Int2 point, int step)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (p.IsExplored(point + new Int2(dx * (step - 1), dy * (step - 1))) == false)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
