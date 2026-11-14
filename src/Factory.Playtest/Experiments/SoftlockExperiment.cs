using Factory.Playtest.Analysis;
using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>
/// Hunts for softlocks and bugs. Each run: a ChaosBot plays badly for a while (random building,
/// dismantling, wasteful crafting, nonsense orders), then a GoalBot takes over and tries to reach the
/// goal. If the GoalBot reaches it from a clean start on the same seed but NOT from the wrecked state,
/// the wrecked state is a candidate softlock; if the bot's planner proves required items are
/// unobtainable (or exceed what's left on the planet) it's a confirmed one. Invariant violations
/// (item dupes/losses, stuck followers/trains, crashes) seen in either phase are reported as bugs.
/// Every finding comes with a replay that reproduces it exactly.
/// </summary>
public sealed class SoftlockExperiment : IExperiment
{
    /// <inheritdoc />
    public string Name => "softlock";
    /// <inheritdoc />
    public string Description => "Chaos play then recovery attempt; flags unrecoverable states, crashes and invariant violations.";

    /// <summary>Game minutes the chaos bot plays before the recovery bot takes over.</summary>
    public double ChaosMinutes { get; init; } = 15;
    /// <summary>How many commands per minute the chaos bot issues.</summary>
    public int ChaosActionsPerMinute { get; init; } = 40;

    private sealed record Outcome(int Run, ulong Seed, Session S, RunResult Chaos, RunResult Recovery, GoalBot Bot, int ChaosActions);

    /// <inheritdoc />
    public Report Run(ExperimentContext ctx)
    {
        Report report = new Report { Experiment = Name };
        report.Parameters["goal"] = ctx.Goal.ToString()!;
        report.Parameters["runs"] = ctx.Iterations.ToString();
        report.Parameters["chaosMinutes"] = ChaosMinutes.ToString();
        report.Parameters["recoveryMinutes"] = ctx.Minutes.ToString();

        // Baselines: can the bot reach the goal from a clean start on each seed?
        List<(ulong seed, Session s, RunResult r)> baselines = Experiments.ParallelMap(ctx.Seeds, ctx.Parallelism, seed =>
        {
            Session s = new Session(ctx.Content, seed);
            s.Bots.Add(new GoalBot(ctx.Goal, ctx.BotParams));
            RunResult r = s.Run(ctx.Goal.IsMet, ctx.MaxTicks);
            ctx.Log($"  baseline seed {seed}: {(r.GoalMet ? $"ok in {ExperimentContext.FormatMinutes(r.Ticks)} min" : r.StopReason)}");

            return (seed, s, r);
        });
        Dictionary<ulong, bool> baselineOk = baselines.ToDictionary(b => b.seed, b => b.r.GoalMet);
        BalanceExperiment.BugChecks(ctx, report, baselines.Select(b => (b.seed, b.s, b.r)), "baseline-");

        List<Outcome> outcomes = Experiments.ParallelMap(Enumerable.Range(0, ctx.Iterations).ToList(), ctx.Parallelism, i =>
        {
            ulong seed = ctx.Seeds[i % ctx.Seeds.Count];
            Session s = new Session(ctx.Content, seed);
            ChaosBot chaos = new ChaosBot((ulong)(1000 + i), ChaosActionsPerMinute);
            s.Bots.Add(chaos);
            RunResult chaosResult = s.Run(_ => false, (long)(ChaosMinutes * 3600));
            GoalBot bot = new GoalBot(ctx.Goal, ctx.BotParams);
            RunResult rec = chaosResult;
            if (chaosResult.Crash is null)
            {
                s.Bots.Clear();
                s.Bots.Add(bot);
                rec = s.Run(ctx.Goal.IsMet, ctx.MaxTicks);
            }

            ctx.Log($"  run {i} (seed {seed}): {(rec.Crash is not null ? rec.StopReason : rec.GoalMet ? $"recovered at {ExperimentContext.FormatMinutes(rec.Ticks)} min" : "NOT recovered")}");

            return new Outcome(i, seed, s, chaosResult, rec, bot, chaos.ActionsSubmitted);
        });

        BalanceExperiment.BugChecks(ctx, report, outcomes.Select(o => (o.Seed, o.S, o.Recovery)), $"chaos-");

        Table table = new Table("Runs", ["Run", "Seed", "Chaos actions", "Recovered", "Minutes", "Final state"], []);
        foreach (Outcome? o in outcomes)
        {
            table.Rows.Add([o.Run.ToString(), o.Seed.ToString(), o.ChaosActions.ToString(), o.Recovery.GoalMet ? "yes" : "no",
                ExperimentContext.FormatMinutes(o.Recovery.Ticks), o.Recovery.GoalMet ? "" : o.S.Describe().Replace("|", "/")]);
            if (o.Recovery.GoalMet || (o.Recovery.Crash is not null))
            {
                continue;
            }

            Classify(ctx, report, o, baselineOk.GetValueOrDefault(o.Seed));
        }

        report.Tables.Add(table);

        int recovered = outcomes.Count(o => o.Recovery.GoalMet);
        report.Summary = $"{ctx.Iterations} chaos runs ({ChaosMinutes} min of chaos, then up to {ctx.Minutes} min recovery) toward **{ctx.Goal.Describe(ctx.Content)}**: " +
                         $"{recovered} recovered. Clean-start baselines: {baselineOk.Count(kv => kv.Value)}/{baselineOk.Count} reached the goal.";

        return report;
    }

    private static void Classify(ExperimentContext ctx, Report report, Outcome o, bool baselineOk)
    {
        Planet p = o.S.World.Planets[0];
        BotPlan? plan = o.Bot.LastPlan;
        string replay = o.S.SaveReplay(ctx.ReplayDir, $"softlock-run{o.Run}-seed{o.Seed}");
        string state = o.S.Describe();
        string planText = plan?.Summarize() ?? "(no plan)";

        // Confirmed: required items have no source at all.
        if (plan is { Unobtainable.Count: > 0 })
        {
            report.Add(new Finding(Severity.Critical, "softlock", "Confirmed softlock: required items can no longer be obtained",
                $"Needed but unobtainable: {string.Join(", ", plan.Unobtainable.Select(kv => $"{kv.Key}×{kv.Value}"))}.\n\nState: {state}\n\nPlan: {planText}",
                o.Seed, o.Recovery.Ticks, replay));

            return;
        }
        // Confirmed: needs more raw material than is left on the planet.
        foreach ((string? resId, long need) in plan?.RawDeficit ?? [])
        {
            long left = WorldProbe.RemainingResource(p, ctx.Content.Resources[resId]);
            if (need > left)
            {
                report.Add(new Finding(Severity.Critical, "softlock", $"Confirmed softlock: needs {need} {resId} but only {left} remain on the planet",
                    $"State: {state}", o.Seed, o.Recovery.Ticks, replay));

                return;
            }
        }

        if (baselineOk == false)
        {
            report.Add(new Finding(Severity.Info, "softlock", "Inconclusive: bot can't reach the goal on this seed even from a clean start",
                $"Raise --minutes or improve the bot. State: {state}", o.Seed, o.Recovery.Ticks, replay));

            return;
        }
        // Candidate: recoverable in principle, but the bot couldn't. Group by the most telling symptom.
        string symptom = plan is { Problems.Count: > 0 } ? plan.Problems[0]
            : p.Units.Where(u => u.IsLeader == false).Select(u => u.Status).GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key;
        report.Add(new Finding(Severity.High, "softlock", $"Candidate softlock: recovery failed ({symptom})",
            $"Baseline on the same seed reaches the goal, but after {o.ChaosActions} chaos actions the bot could not within {ctx.Minutes} min. " +
            $"Load the replay to see whether a human could recover.\n\nState: {state}\n\nPlan: {planText}",
            o.Seed, o.Recovery.Ticks, replay));
    }
}
