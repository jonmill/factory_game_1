using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Deposits, as DSP shows veins: each planet's explored ore patches with what's left in them (hover a
/// deposit, or see the labels on the map), and a warning when the ground under the drills runs low or dry.
/// </summary>
public partial class Hud
{
    /// <summary>A drill warns once when the ore within its reach drops under this share of the most it has had.</summary>
    private const double LowShare = 0.1;

    private readonly Dictionary<int, Deposits> _deposits = [];
    private readonly Dictionary<int, (long Max, string Resource, bool Low, bool Dry)> _drillWatch = [];
    private double _drillCheckAt;

    /// <summary>The deposits on the planet being viewed, kept up to date.</summary>
    private Deposits DepositsHere => DepositsOf(P);

    /// <summary>The deposit index for a planet, created on first use.</summary>
    /// <param name="p">The planet.</param>
    /// <returns>Its deposits, refreshed.</returns>
    public Deposits DepositsOf(Planet p)
    {
        if (_deposits.TryGetValue(p.Id, out Deposits? d) == false)
        {
            d = new Deposits(p);
            _deposits[p.Id] = d;
        }

        d.Refresh();

        return d;
    }

    /// <summary>
    /// Every two seconds, looks at what's left under each drill on every planet and posts one message per
    /// planet and ore for drills that have just run low or dry, rather than one per drill.
    /// </summary>
    private void UpdateDrillWarnings()
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _drillCheckAt < 2)
        {
            return;
        }

        _drillCheckAt = now;
        Dictionary<(string Planet, string Resource), (int Drills, long Left)> low = [];
        Dictionary<(string Planet, string Resource), int> dry = [];
        HashSet<int> seen = [];
        foreach (Planet p in W.Planets)
        {
            foreach (Building b in p.Buildings)
            {
                if (b.Def.Kind != BuildingKind.Drill)
                {
                    continue;
                }

                seen.Add(b.Id);
                (long max, string resource, bool wasLow, bool wasDry) = _drillWatch.GetValueOrDefault(b.Id, (0, "", false, false));
                if (p.FindMinable(b) is { } t)
                {
                    ResourceDef res = p.ResourceAt(t)!;
                    long left = b.Footprint().Concat(p.MiningArea(b)).Where(x => p.ResourceAt(x) == res).Sum(x => (long)p.AmountAt(x));
                    max = System.Math.Max(max, left);
                    bool isLow = left < max * LowShare;
                    if (isLow && (wasLow == false))
                    {
                        (int n, long sum) = low.GetValueOrDefault((p.Name, res.Name));
                        low[(p.Name, res.Name)] = (n + 1, sum + left);
                    }

                    _drillWatch[b.Id] = (max, res.Name, isLow, false);
                }
                else
                {
                    // Only a drill that was mining counts as having run dry; one built on bare ground just says so itself.
                    if ((max > 0) && (wasDry == false))
                    {
                        dry[(p.Name, resource)] = dry.GetValueOrDefault((p.Name, resource)) + 1;
                    }

                    _drillWatch[b.Id] = (max, resource, wasLow, max > 0);
                }
            }
        }

        foreach (int gone in _drillWatch.Keys.Where(id => seen.Contains(id) == false).ToList())
        {
            _drillWatch.Remove(gone);
        }

        foreach (((string planet, string res), (int n, long left)) in low)
        {
            Root.Notify($"{res} running low under {Drills(n)} on {planet}: about {left:N0} left", HudStyle.Orange);
        }

        foreach (((string planet, string res), int n) in dry)
        {
            Root.Notify($"{Drills(n)} on {planet} mined out the {res}: move {(n == 1 ? "it" : "them")} to a fresh deposit", Colors.OrangeRed);
        }
    }

    private static string Drills(int n) => n == 1 ? "a drill" : $"{n} drills";
}
