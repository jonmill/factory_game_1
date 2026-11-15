using System.Diagnostics;
using Factory.Playtest.Analysis;
using Factory.Playtest.Bots;
using Factory.Playtest.Experiments;
using Factory.Sim;

// Headless playtest runner.
//
//   dotnet run -c Release --project tools/Factory.Headless -- <command> [options]
//
// Commands:
//   list                         list experiments
//   static | balance | speedrun | softlock    run one experiment
//   suite                        static + balance + softlock (CI-friendly defaults)
//   verify <file.replay.json>    re-simulate a replay and check its checksum
//   trace [--seeds N --goal G --minutes M --every SECONDS]   print a bot's progress timeline
//
// Options:
//   --seeds 1-8 | 1,4,9          map seeds (default 1-4)
//   --minutes N                  game-minutes budget per run (default 60)
//   --goal tech:<id> | helpers:<n> | settlers | item:<id>:<count>   (default tech:automation)
//   --iterations N               speedrun candidates / softlock runs (default 12)
//   --chaos-minutes N            softlock: minutes of chaos before recovery (default 15)
//   --out DIR                    output directory (default playtest-results/<timestamp>)
//   --parallel N                 worker threads (default: all cores)
//   --fail-on info|low|medium|high|critical   exit code 1 if any finding is at least this severe

ContentDb content = ContentDb.LoadDefault();
if ((args.Length == 0) || (args[0] is "-h" or "--help"))
{
    PrintHelp();

    return 0;
}

string command = args[0];
Dictionary<string, string> opts = ParseOptions(args.Skip(1).ToArray());

if (command == "list")
{
    foreach (IExperiment e in Experiments.All)
    {
        Console.WriteLine($"{e.Name,-10} {e.Description}");
    }

    Console.WriteLine($"{"suite",-10} static + balance + softlock with CI-friendly defaults");

    return 0;
}

if (command == "verify")
{
    Replay replay = Replay.FromJson(File.ReadAllText(args[1]));
    if (replay.ContentHash != content.ComputeHash())
    {
        Console.WriteLine("WARNING: content has changed since this replay was recorded");
    }

    World w = replay.Play(content);
    bool ok = w.Checksum() == replay.FinalChecksum;
    Console.WriteLine(ok ? $"OK: replay reproduced tick {w.Tick} checksum {w.Checksum():X16}"
                         : $"DESYNC: expected {replay.FinalChecksum:X16} got {w.Checksum():X16}");

    return ok ? 0 : 1;
}

if (command == "trace")
{
    // Prints a bot's progress timeline: research, key buildings and what each follower is doing.
    ulong seed = ParseSeeds(opts.GetValueOrDefault("seeds") ?? "1")[0];
    Goal g = Goal.Parse(opts.GetValueOrDefault("goal") ?? "tech:automation");
    int every = int.Parse(opts.GetValueOrDefault("every") ?? "30") * 60;
    double mins = double.Parse(opts.GetValueOrDefault("minutes") ?? "10");
    Session s = new Factory.Playtest.Analysis.Session(content, seed);
    if (opts.TryGetValue("chaos-minutes", out string? cm))
    {
        // Same chaos as softlock run N: chaos seed = 1000 + run.
        ulong chaosSeed = 1000 + ulong.Parse(opts.GetValueOrDefault("run") ?? "0");
        s.Bots.Add(new ChaosBot(chaosSeed, 40));
        s.Run(_ => false, (long)(double.Parse(cm) * 3600));
        s.Bots.Clear();
        Console.WriteLine($"after chaos: {s.Describe()}");
        mins += double.Parse(cm);
    }

    GoalBot bot = new GoalBot(g);
    s.Bots.Add(bot);
    while ((s.World.Tick < mins * 3600) && (g.IsMet(s.World) == false))
    {
        RunResult step = s.Run(_ => false, every);
        if (step.Crash is not null)
        {
            Console.WriteLine($"CRASH at tick {s.World.Tick}: {step.Crash}");
            break;
        }

        Planet p = s.World.Planets[0];
        Console.WriteLine($"[{s.World.Tick / 60}s] research={s.World.Research.Current ?? "-"} {s.World.Research.UnitsDone} done=[{string.Join(",", s.World.Research.Completed)}] sites={p.Sites.Count}");
        foreach (Building? b in p.Buildings.Where(b => (b.Def.Energy == EnergyKind.Electric) || (b.Def.Kind is BuildingKind.SteamEngine or BuildingKind.Furnace or BuildingKind.LaunchPad or BuildingKind.Ferry)))
        {
            Console.WriteLine($"    {b.Def.Id,-14} {b.Status,-10} r={b.Recipe?.Id ?? "-"} power={b.Satisfaction / 10}% inv=[{string.Join(",", b.Inv.Items.Select(kv => kv.Key + "=" + kv.Value))}] out=[{string.Join(",", b.Out.Items.Select(kv => kv.Key + "=" + kv.Value))}]");
        }

        foreach (Planet other in s.World.Planets.Skip(1).Where(o => (o.Buildings.Count > 0) || (o.Sites.Count > 0)))
        {
            string built = string.Join(",", other.Buildings.GroupBy(b => $"{b.Def.Id}:{b.Status}").OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}x{g.Count()}"));
            string ghosts = string.Join(",", other.Sites.GroupBy(x => x.DefId).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}x{g.Count()}"));
            Console.WriteLine($"    planet {other.Def.Id}: units={other.Units.Count} stats=[{string.Join(",", other.Stats.Select(kv => kv.Key + "=" + kv.Value))}] built=[{built}] sites=[{ghosts}]");
            foreach (Building b in other.Buildings.Where(b => b.Def.Kind is BuildingKind.OrbitalStation or BuildingKind.Terraformer or BuildingKind.Drill or BuildingKind.SteamEngine))
            {
                string slots = string.Join(",", b.StationSlots.Select(sl => $"{sl.Mode}:{sl.Item}"));
                Console.WriteLine($"      {b.Def.Id} {b.Status} r={b.Recipe?.Id ?? "-"} slots=[{slots}] inv=[{string.Join(",", b.Inv.Items.Select(kv => kv.Key + "=" + kv.Value))}] out=[{string.Join(",", b.Out.Items.Select(kv => kv.Key + "=" + kv.Value))}]");
            }
        }

        Building landerB = s.World.Get<Building>(p.LanderId)!;
        Console.WriteLine($"    lander {landerB.Status} queue=[{string.Join(",", landerB.CraftQueue)}] crafting={landerB.RecipeId} inv=[{string.Join(",", landerB.Inv.Items.Select(kv => kv.Key + "=" + kv.Value))}]");
        foreach (ConstructionSite site in p.Sites.Take(6))
        {
            Console.WriteLine($"    site {site.Kind} {site.DefId} @{site.Origin.X},{site.Origin.Y} missing=[{string.Join(",", site.Missing().Select(m => m.Item + "=" + m.Count))}]");
        }

        foreach (Unit leader in p.Units.Where(u => u.IsLeader))
        {
            Console.WriteLine($"    {leader.Name} {leader.Status} @{leader.Pos.X / Fixed.One},{leader.Pos.Y / Fixed.One} walkTo={leader.WalkTarget?.ToString() ?? "-"} intent={leader.MoveIntent} modules=[{string.Join(",", leader.Modules)}] carry=[{string.Join(",", leader.Carry.Items.Select(kv => kv.Key + "=" + kv.Value))}]");
        }

        // TRACE_DIAG=1 prints the 5x5 tiles around the commander (l=building initial, ~=water, T=tree, g=ghost).
        if (Environment.GetEnvironmentVariable("TRACE_DIAG") is not null)
        {
            Unit ld = p.Units.First(u => u.IsLeader);
            Int2 lt = Fixed.ToTile(ld.Pos);
            for (int dy = -2; dy <= 2; dy++)
            {
                Console.WriteLine("    DIAG " + string.Concat(Enumerable.Range(-2, 5).Select(dx =>
                {
                    Int2 t = p.Torus.Wrap(lt + new Int2(dx, dy));
                    return p.BuildingAt(t) is { } bb ? bb.Def.Id[0] : p.TerrainAt(t) != Terrain.Ground ? '~' : p.ResourceAt(t) is { IsTree: true } ? 'T' : p.OccupantAt(t) != 0 ? 'g' : '.';
                })) + $"  pos={ld.Pos} blocked={p.IsBlockedForPerson(ld.Pos)}");
            }

            Console.WriteLine($"    DIAG walkTo={ld.WalkTarget} path={string.Join(" ", ld.Path.Take(4))}");
        }

        foreach (Unit? u in p.Units.Where(u => u.IsLeader == false))
        {
            Console.WriteLine($"    {u.Name,-6} {u.Job.Kind,-8} {u.Status} @{u.Pos.X / Fixed.One},{u.Pos.Y / Fixed.One} phase={u.Phase} job={u.Job} carry=[{string.Join(",", u.Carry.Items.Select(kv => kv.Key + "=" + kv.Value))}]{((u.Job.Kind == JobKind.Haul) && (s.World.Get<Building>(u.Job.FromId) is { } fb) ? $" from@{fb.Origin.X},{fb.Origin.Y} {fb.Def.Id}" : "")}");
        }

        if (bot.LastPlan is { } plan)
        {
            Console.WriteLine($"    plan: {plan.Summarize()}");
        }
    }

    Console.WriteLine(g.IsMet(s.World) ? $"GOAL MET at {s.World.Tick / 3600.0:F2} min" : "goal not met");
    foreach (Violation v in s.Invariants.Violations)
    {
        Console.WriteLine($"  VIOLATION {v.Kind}: {v.Detail}");
    }

    return 0;
}

string outDir = opts.GetValueOrDefault("out") ?? Path.Combine("playtest-results", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
Goal goal = Goal.Parse(opts.GetValueOrDefault("goal") ?? "tech:automation");
goal.Validate(content);
ExperimentContext ctx = new ExperimentContext
{
    Content = content,
    OutputDir = outDir,
    Seeds = ParseSeeds(opts.GetValueOrDefault("seeds") ?? "1-4"),
    Minutes = double.Parse(opts.GetValueOrDefault("minutes") ?? "60"),
    Goal = goal,
    Iterations = int.Parse(opts.GetValueOrDefault("iterations") ?? "12"),
    Parallelism = int.Parse(opts.GetValueOrDefault("parallel") ?? Environment.ProcessorCount.ToString()),
};
double chaosMinutes = double.Parse(opts.GetValueOrDefault("chaos-minutes") ?? "15");

List<IExperiment> experiments = command == "suite"
    ? new List<IExperiment> { new StaticBalanceExperiment(), new BalanceExperiment(), new SoftlockExperiment { ChaosMinutes = chaosMinutes } }
    : [command == "softlock" ? new SoftlockExperiment { ChaosMinutes = chaosMinutes } : Experiments.Get(command)];

Severity worst = Severity.Info;
foreach (IExperiment exp in experiments)
{
    Console.WriteLine($"== {exp.Name}: {exp.Description}");
    Stopwatch sw = Stopwatch.StartNew();
    Report report = exp.Run(ctx);
    report.Parameters["contentHash"] = content.ComputeHash().ToString("X16");
    report.Write(outDir);
    sw.Stop();
    Console.WriteLine($"   {report.Summary.Replace("**", "")}");
    foreach (IGrouping<Severity, Finding>? g in report.Findings.GroupBy(f => f.Severity).OrderByDescending(g => g.Key))
    {
        Console.WriteLine($"   {g.Key,-8} {g.Count()} finding(s): {string.Join(" | ", g.Take(4).Select(f => f.Title))}{(g.Count() > 4 ? " | …" : "")}");
    }

    Console.WriteLine($"   report: {Path.Combine(outDir, exp.Name)}.md  ({sw.Elapsed.TotalSeconds:F1}s)");
    if ((report.Findings.Count > 0) && (report.MaxSeverity > worst))
    {
        worst = report.MaxSeverity;
    }
}

if (opts.TryGetValue("fail-on", out string? failOn) && Enum.TryParse<Severity>(failOn, true, out Severity threshold) && (worst >= threshold))
{
    Console.WriteLine($"FAIL: findings at severity {worst} (threshold {threshold})");

    return 1;
}

return 0;

static Dictionary<string, string> ParseOptions(string[] a)
{
    Dictionary<string, string> d = new Dictionary<string, string>();
    for (int i = 0; i < a.Length; i++)
    {
        if (a[i].StartsWith("--") && (i + 1 < a.Length))
        {
            d[a[i][2..]] = a[++i];
        }
    }

    return d;
}

static List<ulong> ParseSeeds(string spec)
{
    List<ulong> list = new List<ulong>();
    foreach (string part in spec.Split(','))
    {
        string[] range = part.Split('-');
        if (range.Length == 2)
        {
            for (ulong s = ulong.Parse(range[0]); s <= ulong.Parse(range[1]); s++)
            {
                list.Add(s);
            }
        }
        else
        {
            list.Add(ulong.Parse(part));
        }
    }

    return list;
}

static void PrintHelp()
{
    Console.WriteLine("usage: Factory.Headless <list|static|balance|speedrun|softlock|suite|verify> [options]");
    Console.WriteLine("  --seeds 1-8  --minutes 60  --goal tech:automation|helpers:1|settlers|item:siderite-plate:200");
    Console.WriteLine("  --iterations 12  --chaos-minutes 15  --out DIR  --parallel N  --fail-on high");
}
