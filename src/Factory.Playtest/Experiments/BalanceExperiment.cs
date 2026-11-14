using Factory.Playtest.Analysis;
using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>
/// Plays the game with the reference GoalBot on many seeds and looks for balance smells in the
/// telemetry: resources that are disproportionately fast to gather, items that pile up unused,
/// machines that are starved most of the time, followers that spend their time waiting, slow or
/// highly seed-dependent progression. Invariant violations (bugs) are reported too.
/// </summary>
public sealed class BalanceExperiment : IExperiment
{
    /// <inheritdoc />
    public string Name => "balance";
    /// <inheritdoc />
    public string Description => "Reference bot plays N seeds; analyzes gather rates, surpluses, starvation, idle time, milestones.";

    private sealed record SeedRun(ulong Seed, Session S, RunResult Result, GoalBot Bot);

    /// <inheritdoc />
    public Report Run(ExperimentContext ctx)
    {
        Report report = new Report { Experiment = Name };
        report.Parameters["goal"] = ctx.Goal.ToString()!;
        report.Parameters["seeds"] = string.Join(",", ctx.Seeds);
        report.Parameters["minutes"] = ctx.Minutes.ToString();
        report.Parameters["bot"] = ctx.BotParams.ToString();

        List<SeedRun> runs = Experiments.ParallelMap(ctx.Seeds, ctx.Parallelism, seed =>
        {
            Session s = new Session(ctx.Content, seed);
            GoalBot bot = new GoalBot(ctx.Goal, ctx.BotParams);
            s.Bots.Add(bot);
            RunResult res = s.Run(ctx.Goal.IsMet, ctx.MaxTicks);
            ctx.Log($"  seed {seed}: {(res.GoalMet ? "goal met" : res.StopReason)} at {ExperimentContext.FormatMinutes(res.Ticks)} min");

            return new SeedRun(seed, s, res, bot);
        });

        AnalyzeOutcome(ctx, report, runs);
        BugChecks(ctx, report, runs);
        GatherRates(ctx, report, runs);
        Surplus(ctx, report, runs);
        Starvation(ctx, report, runs);
        FollowerTime(report, runs);
        Milestones(ctx, report, runs);
        int met = runs.Count(r => r.Result.GoalMet);
        report.Summary = $"Reference bot pursued **{ctx.Goal.Describe(ctx.Content)}** on {runs.Count} seeds for up to {ctx.Minutes} game-minutes: " +
                         $"reached it on {met}/{runs.Count}.";

        return report;
    }

    private static void AnalyzeOutcome(ExperimentContext ctx, Report report, List<SeedRun> runs)
    {
        foreach (SeedRun? r in runs.Where(r => (r.Result.GoalMet == false) && (r.Result.Crash is null)))
        {
            string replay = r.S.SaveReplay(ctx.ReplayDir, $"balance-unfinished-{r.Seed}");
            report.Add(new Finding(Severity.Medium, "progression", $"Goal not reached within {ctx.Minutes} min",
                $"Either the game is too slow/hard or the reference bot can't handle something. State: {r.S.Describe()}\n\nBot plan: {r.Bot.LastPlan?.Summarize()}",
                r.Seed, r.Result.Ticks, replay));
            if (r.Bot.LastPlan is { Unobtainable.Count: > 0 } plan)
            {
                report.Add(new Finding(Severity.High, "progression", "Bot needs items it cannot obtain",
                    string.Join(", ", plan.Unobtainable.Select(kv => $"{kv.Key}×{kv.Value}")), r.Seed, r.Result.Ticks));
            }
        }
    }

    internal static void BugChecks(ExperimentContext ctx, Report report, IEnumerable<(ulong Seed, Session S, RunResult Result)> runs, string prefix = "")
    {
        foreach ((ulong seed, Session? s, RunResult? res) in runs)
        {
            if (res.Crash is { } e)
            {
                report.Add(new Finding(Severity.Critical, "crash", $"Simulation threw {e.GetType().Name}",
                    $"{e.Message}\n\n```\n{e.StackTrace}\n```", seed, res.Ticks, s.SaveReplay(ctx.ReplayDir, $"{prefix}crash-{seed}")));
            }

            foreach (Violation v in s.Invariants.Violations)
            {
                bool benignWait = (v.Kind == "stuck-follower") && (v.Detail.Contains("Waiting for items") || v.Detail.Contains("needs nothing") || v.Detail.Contains("Waiting for construction"));
                Severity sev = v.Kind switch
                {
                    "conservation" or "bounds" => Severity.Critical,
                    "stuck-follower" => benignWait ? Severity.Low : Severity.High,
                    _ => Severity.Medium,
                };
                string title = v.Kind switch
                {
                    "conservation" => "Items created or destroyed without accounting",
                    "stuck-follower" => $"Follower can get stuck: {v.Detail.Split('"').ElementAtOrDefault(1) ?? v.Detail}",
                    "stuck-train" => "Train can get stuck forever",
                    _ => v.Kind,
                };
                report.Add(new Finding(sev, v.Kind, title, v.Detail, seed, v.Tick, s.SaveReplay(ctx.ReplayDir, $"{prefix}{v.Kind}-{seed}")));
            }
        }
    }

    private static void BugChecks(ExperimentContext ctx, Report report, List<SeedRun> runs) =>
        BugChecks(ctx, report, runs.Select(r => (r.Seed, r.S, r.Result)), "balance-");

    private static void GatherRates(ExperimentContext ctx, Report report, List<SeedRun> runs)
    {
        ContentDb c = ctx.Content;
        Table table = new Table("Effective hand-gather rate (includes walking & delivery)", ["Resource", "Items mined", "Follower-min gathering", "Items per follower-min"], []);
        Dictionary<string, double> rates = new Dictionary<string, double>();
        foreach (ResourceDef res in c.ResourceList)
        {
            long items = runs.Sum(r => r.S.World.Stats.Get(ItemFlow.HandMined, res.Item));
            long ticks = runs.Sum(r => r.S.Telemetry.ActivityTicks.GetValueOrDefault($"Gather:{res.Id}"));
            if (ticks < 3600)
            {
                continue;
            }

            double rate = items / (ticks / 3600.0);
            rates[res.Id] = rate;
            table.Rows.Add([res.Name, items.ToString(), $"{ticks / 3600.0:F1}", $"{rate:F1}"]);
        }

        report.Tables.Add(table);
        if (rates.Count < 2)
        {
            return;
        }

        double median = rates.Values.OrderBy(x => x).ElementAt(rates.Count / 2);
        foreach ((string? id, double rate) in rates.Where(kv => kv.Value > median * 1.5))
        {
            report.Add(new Finding(Severity.Medium, "gathering", $"{c.Resources[id].Name} gathers {rate / median:F1}x faster than the median resource in play",
                $"{rate:F1} items per follower-minute vs median {median:F1}. Faster hand time and/or closer deposits make it the obvious thing to stockpile."));
        }
    }

    private static void Surplus(ExperimentContext ctx, Report report, List<SeedRun> runs)
    {
        ContentDb c = ctx.Content;
        Table table = new Table("Item flow over the run (all seeds; excludes starting stock)", ["Item", "Produced", "Consumed", "Stock at end", "Produced/Consumed"], []);
        foreach (ItemDef item in c.ItemList)
        {
            long produced = runs.Sum(r => r.S.World.Stats.Produced(item.Id) - r.S.World.Stats.Get(ItemFlow.Start, item.Id) - r.S.World.Stats.Get(ItemFlow.Refunded, item.Id));
            long consumed = runs.Sum(r => r.S.World.Stats.Consumed(item.Id));
            long stock = runs.Sum(r => WorldProbe.StorageCount(r.S.World.Planets[0], item.Id));
            if ((produced == 0) && (consumed == 0))
            {
                continue;
            }

            double ratio = consumed == 0 ? double.PositiveInfinity : produced / (double)consumed;
            table.Rows.Add([item.Name, produced.ToString(), consumed.ToString(), stock.ToString(), double.IsInfinity(ratio) ? "∞" : $"{ratio:F1}"]);
            if ((produced >= 100 * runs.Count) && (ratio >= 3))
            {
                report.Add(new Finding(Severity.Low, "surplus", $"{item.Name} piles up: produced {ratio:F1}x what was used",
                    $"Produced {produced}, consumed {consumed} across {runs.Count} runs. Either it's too cheap to get, has too few uses, or the bot over-gathers it."));
            }
        }

        report.Tables.Add(table);
    }

    private static void Starvation(ExperimentContext ctx, Report report, List<SeedRun> runs)
    {
        Table table = new Table("Machine time by status", ["Machine", "Working", "Starved (input/fuel/power)", "Blocked (output)", "Idle/other"], []);
        IOrderedEnumerable<string> defs = runs.SelectMany(r => r.S.Telemetry.MachineTicks.Keys.Select(k => k.Def)).Distinct().OrderBy(x => x);
        foreach (string def in defs)
        {
            long Sum(Func<MachineStatus, bool> pred) => runs.Sum(r => r.S.Telemetry.MachineTicks.Where(kv => (kv.Key.Def == def) && pred(kv.Key.Status)).Sum(kv => kv.Value));
            long total = Sum(_ => true);
            if (total == 0)
            {
                continue;
            }

            long working = Sum(s => s == MachineStatus.Working);
            long starved = Sum(s => s is MachineStatus.NoInput or MachineStatus.NoFuel or MachineStatus.NoPower or MachineStatus.NoWater or MachineStatus.NoResource);
            long blocked = Sum(s => s == MachineStatus.OutputFull);
            string Pct(long v) => $"{v * 100 / total}%";
            table.Rows.Add([ctx.Content.Buildings[def].Name, Pct(working), Pct(starved), Pct(blocked), Pct(total - working - starved - blocked)]);
            if ((starved * 100 / total >= 50) && (total >= 3600 * 10))
            {
                report.Add(new Finding(Severity.Medium, "logistics", $"{ctx.Content.Buildings[def].Name}s are starved {starved * 100 / total}% of the time",
                    "Supplying them by follower hauling can't keep up. Expected before automation, but it indicates how much manual logistics the early game demands."));
            }
        }

        report.Tables.Add(table);
    }

    private static void FollowerTime(Report report, List<SeedRun> runs)
    {
        long total = runs.Sum(r => r.S.Telemetry.FollowerTicks);
        if (total == 0)
        {
            return;
        }

        Table table = new Table("Follower time by job", ["Job", "Share"], []);
        foreach (IGrouping<string, KeyValuePair<string, long>>? g in runs.SelectMany(r => r.S.Telemetry.ActivityTicks).GroupBy(kv => kv.Key).OrderByDescending(g => g.Sum(x => x.Value)))
        {
            table.Rows.Add([g.Key, $"{g.Sum(x => x.Value) * 100.0 / total:F1}%"]);
        }

        report.Tables.Add(table);

        List<(string Status, long Ticks)> waits = runs.SelectMany(r => r.S.Telemetry.WaitingTicks).GroupBy(kv => kv.Key).Select(g => (Status: g.Key, Ticks: g.Sum(x => x.Value))).OrderByDescending(x => x.Ticks).ToList();
        long waiting = waits.Sum(w => w.Ticks);
        Table wt = new Table("Follower waiting/idle statuses", ["Status", "Share of follower time"], []);
        foreach ((string? status, long ticks) in waits.Take(12))
        {
            wt.Rows.Add([status, $"{ticks * 100.0 / total:F1}%"]);
        }

        report.Tables.Add(wt);
        if (waiting * 100 / total >= 20)
        {
            report.Add(new Finding(Severity.Medium, "followers", $"Followers spend {waiting * 100 / total}% of their time waiting or idle",
                $"Top reasons: {string.Join("; ", waits.Take(3).Select(w => $"\"{w.Status}\" {w.Ticks * 100.0 / total:F0}%"))}. Followers are the scarcest resource early on."));
        }
    }

    private static void Milestones(ExperimentContext ctx, Report report, List<SeedRun> runs)
    {
        Table table = new Table("Milestones (game minutes)", ["Milestone", .. runs.Select(r => $"seed {r.Seed}"), "mean", "spread (CV)"], []);
        List<string> keys = runs.SelectMany(r => r.S.Telemetry.TechCompletedTick.Keys).Distinct().OrderBy(k => runs.Min(r => r.S.Telemetry.TechCompletedTick.GetValueOrDefault(k, long.MaxValue))).ToList();
        void Row(string name, Func<SeedRun, long?> get)
        {
            List<long?> vals = runs.Select(get).ToList();
            List<double> have = vals.Where(v => v is not null).Select(v => v!.Value / 3600.0).ToList();
            double mean = have.Count > 0 ? have.Average() : 0;
            double cv = (have.Count > 1) && (mean > 0) ? System.Math.Sqrt(have.Sum(x => (x - mean) * (x - mean)) / have.Count) / mean : 0;
            table.Rows.Add([name, .. vals.Select(v => v is null ? "—" : $"{v.Value / 3600.0:F1}"), have.Count > 0 ? $"{mean:F1}" : "—", $"{cv:F2}"]);
            if ((have.Count >= 3) && (cv > 0.35))
            {
                report.Add(new Finding(Severity.Low, "variance", $"'{name}' timing varies a lot between seeds (CV {cv:F2})",
                    "Map generation may make some starts much harder (e.g. water or ore far from the lander)."));
            }
        }

        foreach (string? k in keys)
        {
            Row(ctx.Content.Techs[k].Name, r => r.S.Telemetry.TechCompletedTick.TryGetValue(k, out long t) ? t : null);
        }

        Row("goal", r => r.Result.GoalMet ? r.Result.Ticks : null);
        report.Tables.Add(table);
    }
}
