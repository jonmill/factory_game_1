using Factory.Playtest.Analysis;
using Factory.Playtest.Bots;
using Factory.Playtest.Experiments;

namespace Factory.Sim.Tests;

/// <summary>The headless playtest framework: goals, bots, invariants and experiments.</summary>
public class PlaytestTests
{
    [Test]
    [Arguments("tech:automation", typeof(TechGoal))]
    [Arguments("helpers:2", typeof(HelpersGoal))]
    [Arguments("settlers", typeof(SettlersGoal))]
    [Arguments("item:siderite-plate:200", typeof(ItemGoal))]
    public async Task GoalParse_AcceptsEachGoalForm(string spec, Type expected)
    {
        Goal goal = Goal.Parse(spec);

        await Assert.That(goal.GetType()).IsEqualTo(expected);
    }

    [Test]
    public async Task GoalParse_KeepsTheParsedValues()
    {
        await Assert.That(Goal.Parse("tech:automation")).IsEqualTo(new TechGoal("automation"));
        await Assert.That(Goal.Parse("helpers:3")).IsEqualTo(new HelpersGoal(3));
        await Assert.That(Goal.Parse("item:drive-cog:25")).IsEqualTo(new ItemGoal("drive-cog", 25));
    }

    [Test]
    [Arguments("")]
    [Arguments("tech")]
    [Arguments("tech:a:b")]
    [Arguments("item:siderite-plate")]
    [Arguments("planet:2")]
    public async Task GoalParse_RejectsMalformedSpecs(string spec)
    {
        await Assert.That(() => Goal.Parse(spec)).Throws<ArgumentException>();
    }

    [Test]
    public async Task GoalParse_RejectsNonNumericCounts()
    {
        await Assert.That(() => Goal.Parse("helpers:many")).Throws<FormatException>();
    }

    [Test]
    public async Task GoalValidate_RejectsUnknownTechsAndItems()
    {
        await Assert.That(() => new TechGoal("warp-drive").Validate(TestWorld.Content)).Throws<ArgumentException>();
        await Assert.That(() => new ItemGoal("unobtainium", 1).Validate(TestWorld.Content)).Throws<ArgumentException>();
    }

    [Test]
    public async Task GoalBot_ReachesTheFirstTechFromACleanStart()
    {
        Session s = new Session(TestWorld.Content, 1);
        TechGoal goal = new TechGoal("automation");
        s.Bots.Add(new GoalBot(goal));

        RunResult r = s.Run(goal.IsMet, 10 * 60 * World.TicksPerSecond);

        await Assert.That(r.GoalMet).IsTrue();
        await Assert.That(s.Invariants.Violations).IsEmpty();
    }

    [Test]
    public async Task ChaosPlay_NeverCreatesOrDestroysItems()
    {
        Session s = new Session(TestWorld.Content, 2);
        s.Bots.Add(new ChaosBot(7, 60));

        RunResult r = s.Run(_ => false, 5 * 60 * World.TicksPerSecond);

        await Assert.That(r.Crash).IsNull();
        await Assert.That(s.Invariants.Violations.Where(v => v.Kind is "conservation" or "bounds")).IsEmpty();
    }

    [Test]
    public async Task BotSession_ReplaysToTheSameChecksum()
    {
        Session s = new Session(TestWorld.Content, 3);
        s.Bots.Add(new GoalBot(new TechGoal("automation")));
        s.Run(_ => false, 3 * 60 * World.TicksPerSecond);

        World replayed = Replay.Capture(s.World).Play(TestWorld.Content);

        await Assert.That(replayed.Checksum()).IsEqualTo(s.World.Checksum());
    }

    [Test]
    public async Task StaticBalance_FlagsTheFastestHandGatheredResource()
    {
        ExperimentContext ctx = new ExperimentContext { Content = TestWorld.Content, Log = _ => { } };

        Report report = new StaticBalanceExperiment().Run(ctx);

        Finding? gathering = report.Findings.FirstOrDefault(f => f.Category == "gathering");
        await Assert.That(gathering).IsNotNull();
        await Assert.That(gathering!.Title).Contains("Xylo Tree");
    }
}
