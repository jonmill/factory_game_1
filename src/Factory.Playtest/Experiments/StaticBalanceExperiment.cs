using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>
/// Analyzes content data only (no simulation): reachability, gathering/fuel economics, machine ratios,
/// power fuel burn, train pull limits vs cargo, and research cost in follower-time. Instant and
/// deterministic — good for CI on every content change.
/// </summary>
public sealed class StaticBalanceExperiment : IExperiment
{
    /// <inheritdoc />
    public string Name => "static";
    /// <inheritdoc />
    public string Description => "Content-only balance & reachability analysis (no simulation).";

    private const int Followers = 3;

    /// <inheritdoc />
    public Report Run(ExperimentContext ctx)
    {
        ContentDb c = ctx.Content;
        Report r = new Report { Experiment = Name };
        Reachability(c, r);
        Gathering(c, r);
        Fuel(c, r);
        Machines(c, r);
        Power(c, r);
        Trains(c, r);
        ResearchCost(c, r);
        r.Summary = $"Static analysis of {c.ItemList.Count} items, {c.RecipeList.Count} recipes, {c.BuildingList.Count} buildings, {c.TechList.Count} techs.";

        return r;
    }

    // ------------------------------------------------------------------ reachability

    private static void Reachability(ContentDb c, Report r)
    {
        // Walk the tech tree in unlock order and check every unlocked thing can actually be made.
        HashSet<string> unlocked = new HashSet<string>(c.Start.Unlocked);
        HashSet<string> done = new HashSet<string>();
        bool Obtainable(string item, HashSet<string> seen)
        {
            if (WorldProbe.ResourceFor(c, item) is not null)
            {
                return true;
            }
            // Finite starting stock in the lander deliberately does NOT count as a source.
            // "seen" is the current chain of items being resolved, so only a real cycle stops the search;
            // an item that two inputs share (ballast plate in a charge cell and in a core) is fine.
            if (seen.Add(item) == false)
            {
                return false;
            }

            bool ok = c.RecipeList.Any(rc => (rc.Effect is null) && rc.Out.ContainsKey(item) && unlocked.Contains(rc.Id) && rc.In.Keys.All(i => Obtainable(i, seen)));
            seen.Remove(item);

            return ok;
        }

        bool progress = true;
        while (progress)
        {
            progress = false;
            foreach (TechDef? t in c.TechList.Where(t => (done.Contains(t.Id) == false) && (t.ComingSoon == false) && t.Prereqs.All(done.Contains)))
            {
                List<string> missing = t.UnitCost.Keys.Where(i => Obtainable(i, []) == false).ToList();
                if (missing.Count > 0)
                {
                    r.Add(new Finding(Severity.Critical, "reachability", $"Tech '{t.Name}' needs unobtainable items",
                        $"No renewable source for {string.Join(", ", missing)} once its prerequisites are researched."));
                    continue;
                }

                done.Add(t.Id);
                foreach (string u in t.Unlocks)
                {
                    unlocked.Add(u);
                }

                progress = true;
            }
        }

        foreach (TechDef? t in c.TechList.Where(t => (t.ComingSoon == false) && (done.Contains(t.Id) == false)))
        {
            r.Add(new Finding(Severity.Critical, "reachability", $"Tech '{t.Name}' can never be researched", "Its prerequisites or costs are unreachable."));
        }

        foreach (BuildingDef? b in c.BuildingList.Where(b => b.Buildable))
        {
            List<string> missing = b.Cost.Keys.Where(i => Obtainable(i, []) == false).ToList();
            if (missing.Count > 0)
            {
                r.Add(new Finding(Severity.High, "reachability", $"{b.Name} cost uses non-renewable items",
                    $"{string.Join(", ", missing)} can't be produced with everything unlockable; only the starting stock can pay for it."));
            }
        }
    }

    // ------------------------------------------------------------------ gathering & fuel

    private static void Gathering(ContentDb c, Report r)
    {
        Table table = new Table("Hand gathering (per follower, excluding walking)", ["Resource", "Ticks/item", "Items/min", "Relative speed"], []);
        double median = Median(c.ResourceList.Select(x => (double)x.HandTicks));
        foreach (ResourceDef? res in c.ResourceList.OrderBy(x => x.HandTicks))
        {
            double rel = median / res.HandTicks;
            table.Rows.Add([res.Name, res.HandTicks.ToString(), $"{3600.0 / res.HandTicks:F0}", $"{rel:F2}x"]);
            if (rel >= 1.5)
            {
                r.Add(new Finding(Severity.Medium, "gathering", $"{res.Name} is {rel:F1}x faster to hand-gather than typical",
                    $"{res.Name} takes {res.HandTicks} ticks/item vs a median of {median:F0}. Followers will stockpile {c.Items[res.Item].Name} far faster than anything else; " +
                    "consider raising its hand time, lowering yield per tree, or giving it more sinks."));
            }
        }

        r.Tables.Add(table);
    }

    private static void Fuel(ContentDb c, Report r)
    {
        List<(ResourceDef Res, ItemDef Item, double PerTick)> fuels = c.ResourceList.Where(x => c.Items[x.Item].FuelKj > 0)
            .Select(x => (Res: x, Item: c.Items[x.Item], PerTick: c.Items[x.Item].FuelKj / (double)x.HandTicks)).OrderByDescending(x => x.PerTick).ToList();
        Table table = new Table("Fuel economics (hand-gathered)", ["Fuel", "kJ/item", "Gather ticks", "kJ per follower-tick", "Mass kg"], []);
        foreach ((ResourceDef Res, ItemDef Item, double PerTick) f in fuels)
        {
            table.Rows.Add([f.Item.Name, f.Item.FuelKj.ToString(), f.Res.HandTicks.ToString(), $"{f.PerTick:F1}", f.Item.Mass.ToString()]);
        }

        r.Tables.Add(table);
        for (int i = 0; i < fuels.Count; i++)
        {
            for (int j = 0; j < fuels.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                ((ResourceDef Res, ItemDef Item, double PerTick) a, (ResourceDef Res, ItemDef Item, double PerTick) b) = (fuels[i], fuels[j]);
                if ((a.Item.FuelKj > b.Item.FuelKj) && (a.PerTick < b.PerTick * 1.25))
                {
                    r.Add(new Finding(Severity.Medium, "fuel", $"{a.Item.Name} offers little advantage over {b.Item.Name}",
                        $"{a.Item.Name} has {a.Item.FuelKj / (double)b.Item.FuelKj:F1}x the energy but yields only {a.PerTick / b.PerTick:F2}x the energy per follower-tick of hand gathering " +
                        $"({a.PerTick:F1} vs {b.PerTick:F1} kJ/tick). Players have little reason to switch fuels until drills; trees are also usually closer."));
                }
            }
        }
    }

    // ------------------------------------------------------------------ machines / power / trains

    private static void Machines(ContentDb c, Report r)
    {
        Table table = new Table("Machine throughput", ["Machine", "Rate", "Notes"], []);
        BuildingDef? furnace = c.BuildingList.FirstOrDefault(b => b.Kind == BuildingKind.Furnace);
        RecipeDef? smelt = c.RecipeList.FirstOrDefault(x => x.Category == "smelting");
        double furnaceOrePerMin = (furnace is null) || (smelt is null) ? 0 : 3600.0 * furnace.Speed / 100 / smelt.Ticks * smelt.Inputs[0].Count;
        foreach (BuildingDef? d in c.BuildingList.Where(b => b.Kind == BuildingKind.Drill))
        {
            double perMin = 3600.0 * d.Speed / 100 / d.MiningTicks;
            string note = furnaceOrePerMin > 0 ? $"feeds {perMin / furnaceOrePerMin:F2} furnaces" : "";
            table.Rows.Add([d.Name, $"{perMin:F1} ore/min", note]);
        }

        if (furnace is not null)
        {
            table.Rows.Add([furnace.Name, $"{furnaceOrePerMin:F1} ore/min", ""]);
        }

        foreach (ResourceDef? res in c.ResourceList.Where(x => x.IsTree == false))
        {
            table.Rows.Add([$"Follower on {res.Name}", $"{3600.0 / res.HandTicks:F1} ore/min", "before walking"]);
        }

        r.Tables.Add(table);

        BuildingDef? burner = c.BuildingList.FirstOrDefault(b => (b.Kind == BuildingKind.Drill) && (b.Energy == EnergyKind.Burner));
        ResourceDef? iron = c.ResourceList.FirstOrDefault(x => x.Id == "siderite");
        if ((burner is not null) && (iron is not null))
        {
            double drill = 3600.0 * burner.Speed / 100 / burner.MiningTicks, hand = 3600.0 / iron.HandTicks;
            if (drill < hand)
            {
                r.Add(new Finding(Severity.Low, "machines", $"{burner.Name} is slower than a follower mining by hand",
                    $"{drill:F0} vs {hand:F0} ore/min. Drills still win long-term (unlimited count, no walking), but early on they feel weak."));
            }
        }
    }

    private static void Power(ContentDb c, Report r)
    {
        Table table = new Table("Power: fuel burned per minute at full output", ["Generator", "Fuel", "Items/min", "Followers to supply (hand)"], []);
        foreach (BuildingDef? g in c.BuildingList.Where(b => b.Kind == BuildingKind.SteamEngine))
        {
            double kjPerMin = g.OutputKw * 60.0 * 100 / g.Efficiency;
            foreach (ResourceDef? res in c.ResourceList.Where(x => c.Items[x.Item].FuelKj > 0))
            {
                double items = kjPerMin / c.Items[res.Item].FuelKj;
                double followers = items / (3600.0 / res.HandTicks);
                table.Rows.Add([g.Name, c.Items[res.Item].Name, $"{items:F1}", $"{followers:F2}"]);
                if (followers > 1)
                {
                    r.Add(new Finding(Severity.Medium, "power", $"A fully loaded {g.Name} needs {followers:F1} followers gathering {c.Items[res.Item].Name}",
                        $"At {g.OutputKw} kW and {g.Efficiency}% efficiency it burns {items:F0} {c.Items[res.Item].Name}/min. With {Followers} followers and no drills/automation, " +
                        "heavy power use stalls everything else. Intentional pressure toward carbon drills, but check the early curve."));
                }
            }
        }

        foreach (BuildingDef? b in c.BuildingList.Where(b => b.Energy == EnergyKind.Electric))
        {
            table.Rows.Add([$"{b.Name} uses {b.PowerKw} kW", "", "", ""]);
        }

        r.Tables.Add(table);
    }

    private static void Trains(ContentDb c, Report r)
    {
        Table table = new Table("Trains: full cart mass vs locomotive pull", ["Cargo", "Full cart kg", "Carts a hand cart pulls", "Carts a steam loco pulls"], []);
        VehicleDef? cart = c.VehicleList.FirstOrDefault(v => v.Kind == VehicleKind.Cart);
        VehicleDef? hand = c.VehicleList.FirstOrDefault(v => v.Kind == VehicleKind.HandCart);
        VehicleDef? steam = c.VehicleList.FirstOrDefault(v => v.Kind == VehicleKind.Steam);
        if (cart is null)
        {
            return;
        }

        foreach (ItemDef? item in c.ItemList.Where(i => c.ResourceList.Any(res => res.Item == i.Id) || i.Id.EndsWith("plate")))
        {
            long full = cart.Mass + ((long)cart.Slots * item.Stack * item.Mass);
            string Pull(VehicleDef? v) => v is null ? "-" : (v.MaxPull / full).ToString();
            table.Rows.Add([item.Name, full.ToString(), Pull(hand), Pull(steam)]);
            ResourceDef? res = c.ResourceList.FirstOrDefault(x => x.Item == item.Id);
            if ((hand is not null) && (full > hand.MaxPull) && (res is not null))
            {
                r.Add(new Finding(Severity.High, "trains", $"A hand cart can't pull one full cart of {item.Name}",
                    $"Full mine cart of {item.Name} = {full} kg > hand cart limit {hand.MaxPull} kg. The train sits 'Overloaded' after loading until the player " +
                    "unloads by hand — likely to read as a bug. Galena is the resource meant to motivate early rail."));
            }
        }

        foreach (ItemDef? i in c.ItemList.Where(i => i.Mass == 0))
        {
            r.Add(new Finding(Severity.Low, "trains", $"{i.Name} has zero mass", "Unlimited quantities ride trains for free; fine if intended."));
        }

        r.Tables.Add(table);
    }

    // ------------------------------------------------------------------ research cost

    /// <summary>Raw follower hand-ticks to produce one item (gathering only; crafting/smelting time listed separately).</summary>
    private static double RawTicks(ContentDb c, string item, int depth = 0)
    {
        if (depth > 10)
        {
            return double.NaN;
        }

        if (WorldProbe.ResourceFor(c, item) is { } res)
        {
            return res.HandTicks;
        }

        RecipeDef? rc = WorldProbe.RecipeFor(c, item);
        if (rc is null)
        {
            return double.NaN;
        }

        return rc.Inputs.Sum(i => i.Count * RawTicks(c, i.Item, depth + 1)) / rc.Out[item];
    }

    private static void ResearchCost(ContentDb c, Report r)
    {
        Table table = new Table("Research cost (gathering only, ignoring walking and crafting)", ["Tech", "Units", "Raw hand-ticks", $"Minutes for {Followers} followers"], []);
        double cumulative = 0;
        foreach (TechDef t in c.TechList)
        {
            double ticks = t.Units * t.UnitCostList.Sum(x => x.Count * RawTicks(c, x.Item));
            cumulative += ticks;
            table.Rows.Add([t.Name + (t.ComingSoon ? " (soon)" : ""), t.Units.ToString(), $"{ticks:F0}", $"{ticks / 3600 / Followers:F1}"]);
        }

        r.Tables.Add(table);

        // Techs whose whole cost is already sitting in the lander at landing (plus a lab to research it).
        Dictionary<string, int> stock = new Dictionary<string, int>(c.Start.Lander);
        bool Pay(IEnumerable<ItemAmount> cost, int times)
        {
            foreach (ItemAmount x in cost)
            {
                if (stock.GetValueOrDefault(x.Item) < x.Count * times)
                {
                    return false;
                }
            }

            foreach (ItemAmount x in cost)
            {
                stock[x.Item] -= x.Count * times;
            }

            return true;
        }

        BuildingDef? lab = c.BuildingList.FirstOrDefault(b => b.Kind == BuildingKind.Lab);
        BuildingDef? engine = c.BuildingList.FirstOrDefault(b => b.Kind == BuildingKind.SteamEngine);
        if ((lab is not null) && (engine is not null) && Pay(lab.CostList, 1) && Pay(engine.CostList, 1))
        {
            foreach (TechDef? t in c.TechList.Where(t => t.Prereqs.Length == 0))
            {
                if (Pay(t.UnitCostList, t.Units))
                {
                    r.Add(new Finding(Severity.Medium, "pacing", $"'{t.Name}' can be researched entirely from the starting supplies",
                        $"The lander's starting stock pays for a lab, a steam engine and all {t.Units} units, so the first tech needs no gathering. " +
                        "Fine as a tutorial beat, but it teaches nothing about the economy; consider making one ingredient require smelting."));
                }
            }
        }

        Table items = new Table("Item raw cost", ["Item", "Raw hand-ticks per item"], []);
        foreach (ItemDef i in c.ItemList)
        {
            items.Rows.Add([i.Name, $"{RawTicks(c, i.Id):F0}"]);
        }

        r.Tables.Add(items);
    }

    private static double Median(IEnumerable<double> values)
    {
        List<double> v = values.OrderBy(x => x).ToList();

        return v.Count == 0 ? 0 : v.Count % 2 == 1 ? v[v.Count / 2] : (v[(v.Count / 2) - 1] + v[v.Count / 2]) / 2;
    }
}
