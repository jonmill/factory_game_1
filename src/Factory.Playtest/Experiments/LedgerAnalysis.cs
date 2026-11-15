using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>
/// Reads the sim's ledger after bot runs to prove how the game was played: how much was mined by hand
/// versus by drills and crafted in the lander versus in machines, how output grew hour by hour, what was
/// built on each planet, where the terraforming came from, and which content was never touched.
/// </summary>
internal static class LedgerAnalysis
{
    /// <summary>Below this share of drill mining or machine crafting the bot isn't really automating.</summary>
    private const int AutomatedPct = 60;

    /// <summary>Adds the ledger tables and findings for a set of runs.</summary>
    /// <param name="ctx">The experiment context.</param>
    /// <param name="report">The report to add to.</param>
    /// <param name="worlds">The finished worlds, one per seed.</param>
    public static void Analyze(ExperimentContext ctx, Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        if (worlds.Count == 0)
        {
            return;
        }

        Automation(ctx, report, worlds);
        OutputByHour(report, worlds);
        ItemOrigins(ctx, report, worlds);
        BuildingsByPlanet(ctx, report, worlds);
        Terraforming(ctx, report, worlds);
        Coverage(ctx, report, worlds);
    }

    /// <summary>The crafting machine that made something: the lander's fabricator counts as hand crafting.</summary>
    private static bool IsHandCraft(string by) => by == "lander";

    private static void Automation(ExperimentContext ctx, Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        ContentDb c = ctx.Content;
        Table t = new Table("Automation (share of all mining and crafting done by machines)",
            ["Seed", "Ore mined by drills", "Crafts in fabricators", "Smelted in furnaces", "Machines standing at peak", "Planets built on"], []);
        foreach ((ulong seed, World w) in worlds)
        {
            ProductionStats s = w.Stats;
            long hand = s.Lines.Where(l => (l.Flow == ItemFlow.HandMined) && c.Resources.TryGetValue(l.Via, out ResourceDef? r) && (r.IsTree == false)).Sum(l => l.Count);
            long drill = s.Count(flow: ItemFlow.DrillMined);
            long landerCrafts = s.Lines.Where(l => (l.Flow == ItemFlow.Crafted) && IsHandCraft(l.By)).Sum(l => l.Count);
            long machineCrafts = s.Lines.Where(l => (l.Flow == ItemFlow.Crafted) && (IsHandCraft(l.By) == false)).Sum(l => l.Count);
            long smelted = s.Count(flow: ItemFlow.Smelted);
            int machines = s.BuildingLines.Where(b => c.Buildings[b.Def].Kind is BuildingKind.Drill or BuildingKind.Furnace or BuildingKind.Assembler).Sum(b => b.Peak);
            int planets = s.BuildingLines.Where(b => c.Buildings[b.Def].Kind is not (BuildingKind.Lander or BuildingKind.Ferry)).Select(b => b.Planet).Distinct().Count();
            int drillPct = Pct(drill, drill + hand);
            int craftPct = Pct(machineCrafts, machineCrafts + landerCrafts);
            t.Rows.Add([seed.ToString(), $"{drillPct}% ({drill} of {drill + hand})", $"{craftPct}% ({machineCrafts} of {machineCrafts + landerCrafts})", smelted.ToString(), machines.ToString(), planets.ToString()]);
            bool automationResearched = HeldFor(w, "automation", GraceTicks);
            if (automationResearched && (drillPct < AutomatedPct))
            {
                report.Add(new Finding(Severity.High, "automation", $"Only {drillPct}% of ore was mined by drills",
                    $"{hand} ore was dug by hand. A player who automates puts drills on every patch.", seed, w.Tick));
            }

            if (automationResearched && (craftPct < AutomatedPct))
            {
                report.Add(new Finding(Severity.High, "automation", $"Only {craftPct}% of crafting ran in fabricators",
                    $"{landerCrafts} items were made in the lander. Fabricators should make the bulk of parts once Automation is researched.", seed, w.Tick));
            }
        }

        report.Tables.Add(t);
    }

    /// <summary>How long a run must go on after a tech before the ledger judges how it was used.</summary>
    private const long GraceTicks = 60L * 3600;

    /// <summary>Whether a tech was researched at least this many ticks before the run ended.</summary>
    private static bool HeldFor(World w, string tech, long ticks) =>
        w.Stats.ResearchLines.Any(r => (r.Tech == tech) && (w.Tick - r.FirstTick >= ticks));

    private static int Pct(long part, long whole) => whole == 0 ? 0 : (int)(part * 100 / whole);

    private static void OutputByHour(Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        // Totals across seeds, in one-hour rows: hand vs drill mining, lander vs machine crafting.
        int perHour = 3600 * 60 / ProductionStats.BucketTicks;
        long maxTick = worlds.Max(w => w.World.Tick);
        int hours = (int)(maxTick / (3600L * 60)) + 1;
        long[] handMine = new long[hours], drillMine = new long[hours], landerCraft = new long[hours], machineCraft = new long[hours], smelt = new long[hours];
        foreach ((ulong _, World w) in worlds)
        {
            ProductionStats s = w.Stats;
            void Into(long[] into, ItemFlow flow, Func<string, bool> who)
            {
                foreach (string by in s.ActorsOf(flow).Where(who))
                {
                    foreach ((int bucket, long n) in s.SeriesBy(flow, by))
                    {
                        into[System.Math.Min(hours - 1, bucket / perHour)] += n;
                    }
                }
            }

            Into(handMine, ItemFlow.HandMined, _ => true);
            Into(drillMine, ItemFlow.DrillMined, _ => true);
            Into(landerCraft, ItemFlow.Crafted, IsHandCraft);
            Into(machineCraft, ItemFlow.Crafted, by => IsHandCraft(by) == false);
            Into(smelt, ItemFlow.Smelted, _ => true);
        }

        Table t = new Table($"Output per game hour (all {worlds.Count} seeds together)", ["Hour", "Hand-mined", "Drill-mined", "Crafted in lander", "Crafted in machines", "Smelted"], []);
        for (int h = 0; h < hours; h++)
        {
            t.Rows.Add([$"{h}–{h + 1}", handMine[h].ToString(), drillMine[h].ToString(), landerCraft[h].ToString(), machineCraft[h].ToString(), smelt[h].ToString()]);
        }

        report.Tables.Add(t);
    }

    private static void ItemOrigins(ExperimentContext ctx, Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        ContentDb c = ctx.Content;
        Table t = new Table("How each item was made (all seeds; first time made is the earliest seed's, in game minutes)", ["Item", "Made", "By hand", "By machine", "Top makers", "First made (min)", "Used for"], []);
        foreach (ItemDef item in c.ItemList)
        {
            List<LedgerLine> made = worlds.SelectMany(w => w.World.Stats.Lines).Where(l => (l.Item == item.Id) && (l.Flow is ItemFlow.HandMined or ItemFlow.DrillMined or ItemFlow.Crafted or ItemFlow.Smelted)).ToList();
            List<LedgerLine> used = worlds.SelectMany(w => w.World.Stats.Lines).Where(l => (l.Item == item.Id) && (ProductionStats.IsSource(l.Flow) == false)).ToList();
            long total = made.Sum(l => l.Count);
            if ((total == 0) && (used.Count == 0))
            {
                continue;
            }

            long byHand = made.Where(l => (l.Flow == ItemFlow.HandMined) || IsHandCraft(l.By)).Sum(l => l.Count);
            string makers = string.Join(", ", made.GroupBy(l => l.By).OrderByDescending(g => g.Sum(l => l.Count)).Take(3)
                .Select(g => $"{Who(c, g.Key)} {Pct(g.Sum(l => l.Count), total)}%"));
            string uses = string.Join(", ", used.GroupBy(l => l.Flow).OrderByDescending(g => g.Sum(l => l.Count)).Take(3)
                .Select(g => $"{g.Key} {g.Sum(l => l.Count)}"));
            string first = made.Count == 0 ? "—" : $"{made.Min(l => l.FirstTick) / 3600.0:F0}";
            t.Rows.Add([item.Name, total.ToString(), $"{Pct(byHand, total)}%", $"{Pct(total - byHand, total)}%", makers, first, uses]);
        }

        report.Tables.Add(t);
    }

    private static string Who(ContentDb c, string by) =>
        c.Buildings.TryGetValue(by, out BuildingDef? b) ? b.Name : c.Robots.TryGetValue(by, out RobotDef? r) ? r.Name : by.Length == 0 ? "unknown" : by;

    private static void BuildingsByPlanet(ExperimentContext ctx, Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        ContentDb c = ctx.Content;
        World first = worlds[0].World;
        Table t = new Table("Buildings put up, per planet (mean per seed; peak standing in brackets)", ["Building", .. first.Planets.Select(p => p.Name)], []);
        foreach (BuildingDef def in c.BuildingList)
        {
            string[] cells = first.Planets.Select(p =>
            {
                List<BuildingLine> lines = worlds.SelectMany(w => w.World.Stats.BuildingLines).Where(l => (l.Def == def.Id) && (l.Planet == p.Id)).ToList();
                double built = lines.Sum(l => l.Built) / (double)worlds.Count;
                int peak = lines.Count == 0 ? 0 : lines.Max(l => l.Peak);

                return built == 0 ? "" : $"{built:0.#} ({peak})";
            }).ToArray();
            if (cells.Any(x => x.Length > 0))
            {
                t.Rows.Add([def.Name, .. cells]);
            }
        }

        report.Tables.Add(t);

        // Each planet with resources of its own should have been put to work, in runs that reached outposts.
        string? outposts = c.TechList.FirstOrDefault(tech => tech.Unlocks.Contains("orbital-station"))?.Id;
        List<World> stellar = worlds.Select(w => w.World).Where(w => (outposts is not null) && HeldFor(w, outposts, GraceTicks * 2)).ToList();
        foreach (Planet p in first.Planets.Where(p => (p.Id != 0) && (p.Def.Resources.Length > 0)))
        {
            int seedsUsed = stellar.Count(w => w.Stats.BuildingLines.Any(l => (l.Planet == p.Id) && (c.Buildings[l.Def].Kind is not (BuildingKind.Ferry or BuildingKind.Lander))));
            if (seedsUsed < stellar.Count)
            {
                report.Add(new Finding(Severity.Medium, "planets", $"Nothing was built on {p.Name} in {stellar.Count - seedsUsed} of {stellar.Count} runs that reached the Stellar Age",
                    $"{p.Name} has {string.Join(", ", p.Def.Resources.Select(r => c.Resources[r.Resource].Name).Distinct())}. Players are meant to set up outposts there."));
            }
        }
    }

    private static void Terraforming(ExperimentContext ctx, Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        ContentDb c = ctx.Content;
        World first = worlds[0].World;
        Table t = new Table("Terraforming points added (mean per seed), by planet, stat and machine", ["Planet", "Stat", "Machine", "Points"], []);
        foreach (IGrouping<(int Planet, string Stat, string By), (int Planet, string Stat, string By, long Points)> g in worlds.SelectMany(w => w.World.Stats.TerraformLines)
                     .GroupBy(x => (x.Planet, x.Stat, x.By)).OrderBy(g => g.Key.Planet).ThenBy(g => g.Key.Stat, StringComparer.Ordinal).ThenBy(g => g.Key.By, StringComparer.Ordinal))
        {
            t.Rows.Add([first.Planets[g.Key.Planet].Name, g.Key.Stat, Who(c, g.Key.By), $"{g.Sum(x => x.Points) / (double)worlds.Count:0}"]);
        }

        report.Tables.Add(t);
    }

    private static void Coverage(ExperimentContext ctx, Report report, IReadOnlyList<(ulong Seed, World World)> worlds)
    {
        ContentDb c = ctx.Content;
        HashSet<string> made = worlds.SelectMany(w => w.World.Stats.Lines).Where(l => l.Flow is ItemFlow.HandMined or ItemFlow.DrillMined or ItemFlow.Crafted or ItemFlow.Smelted)
            .Select(l => l.Item).ToHashSet();
        HashSet<string> recipes = worlds.SelectMany(w => w.World.Stats.Lines).Where(l => l.Flow is ItemFlow.CraftInput or ItemFlow.SmeltInput or ItemFlow.Crafted or ItemFlow.Smelted)
            .Select(l => l.Via).ToHashSet();
        HashSet<string> built = worlds.SelectMany(w => w.World.Stats.BuildingLines).Select(l => l.Def).ToHashSet();
        HashSet<string> researched = worlds.SelectMany(w => w.World.Stats.ResearchLines).Select(r => r.Tech).ToHashSet();
        string[] neverMade = c.ItemList.Where(i => made.Contains(i.Id) == false).Select(i => i.Name).ToArray();
        string[] neverRun = c.RecipeList.Where(r => (recipes.Contains(r.Id) == false) && (r.Effect is null)).Select(r => r.Name).ToArray();
        string[] neverBuilt = c.BuildingList.Where(b => b.Buildable && (built.Contains(b.Id) == false)).Select(b => b.Name).ToArray();
        string[] neverResearched = c.TechList.Where(tc => (researched.Contains(tc.Id) == false) && (tc.ComingSoon == false)).Select(tc => tc.Name).ToArray();

        Table t = new Table("Content coverage across all seeds", ["Kind", "Used", "Never used"], []);
        t.Rows.Add(["Items made", $"{c.ItemList.Count - neverMade.Length}/{c.ItemList.Count}", string.Join(", ", neverMade)]);
        t.Rows.Add(["Recipes run", $"{c.RecipeList.Count(r => r.Effect is null) - neverRun.Length}/{c.RecipeList.Count(r => r.Effect is null)}", string.Join(", ", neverRun)]);
        t.Rows.Add(["Buildings built", $"{c.BuildingList.Count(b => b.Buildable) - neverBuilt.Length}/{c.BuildingList.Count(b => b.Buildable)}", string.Join(", ", neverBuilt)]);
        t.Rows.Add(["Techs researched", $"{c.TechList.Count(tc => tc.ComingSoon == false) - neverResearched.Length}/{c.TechList.Count(tc => tc.ComingSoon == false)}", string.Join(", ", neverResearched)]);
        report.Tables.Add(t);

        void Flag(string kind, string[] names)
        {
            if (names.Length > 0)
            {
                report.Add(new Finding(Severity.Medium, "coverage", $"{names.Length} {kind} never used by the bot",
                    $"{string.Join(", ", names)}. Either the bot doesn't play that part of the game or nothing needs them."));
            }
        }

        Flag("items", neverMade);
        Flag("buildings", neverBuilt);
        Flag("techs", neverResearched);
    }
}
