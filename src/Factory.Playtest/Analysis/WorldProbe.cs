using Factory.Sim;

namespace Factory.Playtest.Analysis;

/// <summary>Read-only queries over world state used by bots and analyzers.</summary>
public static class WorldProbe
{
    /// <summary>The buildings that act as general storage on a planet (the lander and chests).</summary>
    public static IEnumerable<Building> Storages(Planet p) =>
        p.Buildings.Where(b => b.Def.Kind is BuildingKind.Lander or BuildingKind.Chest);

    /// <summary>Total amount of an item held across all storage buildings on a planet.</summary>
    public static long StorageCount(Planet p, string item) => Storages(p).Sum(b => (long)b.Inv.Count(item));

    /// <summary>Per-item totals held across all storage buildings on a planet.</summary>
    public static Dictionary<string, long> StorageTotals(Planet p)
    {
        Dictionary<string, long> d = new Dictionary<string, long>();
        foreach (Building b in Storages(p))
        {
            foreach ((string? k, int v) in b.Inv.Items)
            {
                d[k] = d.GetValueOrDefault(k) + v;
            }
        }

        return d;
    }

    /// <summary>Every item that physically exists anywhere (buildings, carried, ghosts, train cargo).</summary>
    public static SortedDictionary<string, long> AllItems(World w)
    {
        SortedDictionary<string, long> d = new SortedDictionary<string, long>(StringComparer.Ordinal);
        void Add(Inventory inv)
        {
            foreach ((string? k, int v) in inv.Items)
            {
                d[k] = d.GetValueOrDefault(k) + v;
            }
        }

        foreach (Planet p in w.Planets)
        {
            foreach (Building b in p.Buildings)
            {
                Add(b.Inv);
                if (ReferenceEquals(b.Inv, b.Out) == false)
                {
                    Add(b.Out);
                }
            }

            foreach (Unit u in p.Units)
            {
                Add(u.Carry);
            }

            foreach (ConstructionSite s in p.Sites)
            {
                Add(s.Delivered);
            }

            foreach (Train t in p.Trains)
            {
                foreach (Vehicle v in t.Vehicles)
                {
                    Add(v.Cargo);
                }
            }
        }

        return d;
    }

    /// <summary>Sum of the remaining amount of a resource across every tile of a planet.</summary>
    public static long RemainingResource(Planet p, ResourceDef r)
    {
        long total = 0;
        for (int y = 0; y < p.Height; y++)
        {
            for (int x = 0; x < p.Width; x++)
            {
                if (p.ResourceAt(new Int2(x, y)) == r)
                {
                    total += p.AmountAt(new Int2(x, y));
                }
            }
        }

        return total;
    }

    /// <summary>The gatherable resource that yields an item, or <c>null</c> if the item cannot be gathered.</summary>
    public static ResourceDef? ResourceFor(ContentDb c, string item) => c.ResourceList.FirstOrDefault(r => r.Item == item);

    /// <summary>The recipe that makes an item (first match), ignoring special-effect recipes.</summary>
    public static RecipeDef? RecipeFor(ContentDb c, string item) =>
        c.RecipeList.FirstOrDefault(r => (r.Effect is null) && r.Out.ContainsKey(item));

    /// <summary>Telemetry key describing what a unit is doing: its job kind, plus the resource for gather jobs.</summary>
    public static string ActivityKey(Unit u) => u.Job.Kind switch
    {
        JobKind.Gather => $"Gather:{u.Job.Resource}",
        _ => u.Job.Kind.ToString(),
    };

    /// <summary>Heuristic: is this unit status text a "not doing useful work" state?</summary>
    public static bool IsWaitingStatus(string status) =>
        status.StartsWith("Waiting", StringComparison.Ordinal) || status.Contains("full", StringComparison.OrdinalIgnoreCase) ||
        status.StartsWith("No ", StringComparison.Ordinal) || (status == "Idle") || status.Contains("broken") || status.Contains("blocked");
}
