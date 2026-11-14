using Factory.Playtest.Analysis;
using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>
/// "Reach goal X as fast as possible": searches the GoalBot's strategy space (random restarts, then
/// hill-climbing mutations of the best) across all seeds. The winning strategy reveals the dominant
/// path through the early game, and its time is a regression metric for pacing changes.
/// </summary>
public sealed class SpeedrunExperiment : IExperiment
{
    /// <inheritdoc />
    public string Name => "speedrun";
    /// <inheritdoc />
    public string Description => "Search bot strategies to reach the goal fastest; reports best strategy, time, and replay.";

    private sealed record Candidate(int Index, BotParams Params, string Origin);
    private sealed record Score(Candidate C, double MeanTicks, int Successes, long BestTicks, ulong BestSeed);

    /// <inheritdoc />
    public Report Run(ExperimentContext ctx)
    {
        Report report = new Report { Experiment = Name };
        report.Parameters["goal"] = ctx.Goal.ToString()!;
        report.Parameters["seeds"] = string.Join(",", ctx.Seeds);
        report.Parameters["iterations"] = ctx.Iterations.ToString();
        report.Parameters["minutes"] = ctx.Minutes.ToString();

        Rng rng = new Factory.Sim.Rng(0xC0FFEE);
        List<Score> scores = new List<Score>();
        Score? best = null;
        int explore = System.Math.Max(1, ctx.Iterations / 3);

        for (int i = 0; i < ctx.Iterations; i++)
        {
            Candidate cand = i == 0 ? new Candidate(i, ctx.BotParams, "baseline")
                : (i < explore) || (best is null) ? new Candidate(i, BotParams.Random(rng), "random")
                : new Candidate(i, best.C.Params.Mutate(rng).Mutate(rng), $"mutation of #{best.C.Index}");
            Score score = Evaluate(ctx, cand);
            scores.Add(score);
            if ((best is null) || (score.MeanTicks < best.MeanTicks))
            {
                best = score;
            }

            ctx.Log($"  #{i} ({cand.Origin}): mean {score.MeanTicks / 3600:F1} min, {score.Successes}/{ctx.Seeds.Count} ok — best so far #{best.C.Index} {best.MeanTicks / 3600:F1} min");
        }

        Table table = new Table("Strategies (sorted by mean time; failures count as 1.5× the time limit)", ["#", "Origin", "Mean min", "Reached", "Params"], []);
        foreach (Score? s in scores.OrderBy(s => s.MeanTicks))
        {
            table.Rows.Add([s.C.Index.ToString(), s.C.Origin, $"{s.MeanTicks / 3600:F1}", $"{s.Successes}/{ctx.Seeds.Count}", s.C.Params.ToString()]);
        }

        report.Tables.Add(table);

        Score baseline = scores[0];
        string replay = "";
        if (best!.Successes > 0)
        {
            Session s = new Session(ctx.Content, best.BestSeed);
            s.Bots.Add(new GoalBot(ctx.Goal, best.C.Params));
            s.Run(ctx.Goal.IsMet, ctx.MaxTicks);
            replay = s.SaveReplay(ctx.ReplayDir, $"speedrun-best-{best.BestSeed}");
        }

        report.Add(new Finding(Severity.Info, "speedrun", $"Fastest strategy: {best.MeanTicks / 3600:F1} min mean ({best.BestTicks / 3600.0:F1} min best run)",
            $"Params: {best.C.Params}\n\nBaseline params took {baseline.MeanTicks / 3600:F1} min mean.", best.BestSeed, best.BestTicks, replay.Length > 0 ? replay : null));
        if (best.Successes == 0)
        {
            report.Add(new Finding(Severity.High, "speedrun", "No strategy reached the goal in the time limit",
                "Increase --minutes, or the goal is currently unreachable for bots (check the balance report)."));
        }
        else if (baseline.MeanTicks > best.MeanTicks * 1.5)
        {
            report.Add(new Finding(Severity.Low, "speedrun", $"Strategy matters a lot: best is {baseline.MeanTicks / best.MeanTicks:F1}x faster than baseline",
                "Large gaps hint at a dominant strategy players will discover (see the params diff)."));
        }

        report.Summary = $"Searched {ctx.Iterations} strategies on {ctx.Seeds.Count} seeds to **{ctx.Goal.Describe(ctx.Content)}**. Best mean: {best.MeanTicks / 3600:F1} game-min.";

        return report;
    }

    private static Score Evaluate(ExperimentContext ctx, Candidate cand)
    {
        List<(ulong seed, RunResult r)> results = Experiments.ParallelMap(ctx.Seeds, ctx.Parallelism, seed =>
        {
            Session s = new Session(ctx.Content, seed) { InvariantEvery = int.MaxValue };
            s.Bots.Add(new GoalBot(ctx.Goal, cand.Params));
            RunResult r = s.Run(ctx.Goal.IsMet, ctx.MaxTicks);

            return (seed, r);
        });
        double mean = results.Average(x => x.r.GoalMet ? x.r.Ticks : ctx.MaxTicks * 1.5);
        List<(ulong seed, RunResult r)> ok = results.Where(x => x.r.GoalMet).ToList();
        (ulong seed, RunResult r) bestRun = ok.OrderBy(x => x.r.Ticks).FirstOrDefault();

        return new Score(cand, mean, ok.Count, ok.Count > 0 ? bestRun.r.Ticks : 0, ok.Count > 0 ? bestRun.seed : 0);
    }
}
