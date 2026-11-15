namespace Factory.Sim.Tests;

public class ModuleTests
{
    [Test]
    public async Task Helpers_StartWithGatherAndHaulOnly()
    {
        TestWorld tw = new TestWorld();
        Unit bot = tw.Followers[0];
        await Assert.That(tw.Followers.Count).IsEqualTo(2);
        await Assert.That(tw.Try(new AssignJob(bot.Id, new Job(JobKind.Build)))).Contains("Construction Module");
        await Assert.That(tw.Try(new AssignJob(bot.Id, new Job(JobKind.Gather, Resource: "regolith")))).IsNull();
    }

    [Test]
    public async Task InstallingAConstructionModule_TeachesBuild()
    {
        TestWorld tw = new TestWorld();
        Unit bot = tw.Followers[0];
        tw.Leader.Carry.Add("construction-module", 1);
        tw.Do(new InstallModule(tw.Leader.Id, bot.Id, "construction-module"));
        await Assert.That(bot.Modules).IsEquivalentTo(["construction-module"]);
        await Assert.That(tw.Leader.Carry.Count("construction-module")).IsEqualTo(0);
        tw.Do(new AssignJob(bot.Id, new Job(JobKind.Build)));

        // A Mk1 has one slot, so a second module doesn't fit.
        tw.Leader.Carry.Add("servo-module", 1);
        await Assert.That(tw.Try(new InstallModule(tw.Leader.Id, bot.Id, "servo-module"))).Contains("no free module slot");
    }

    [Test]
    public async Task RemovingTheModule_IdlesABuilder()
    {
        TestWorld tw = new TestWorld();
        Unit bot = tw.Followers[0];
        tw.Equip(bot, "construction-module");
        tw.Do(new AssignJob(bot.Id, new Job(JobKind.Build)));
        tw.Do(new RemoveModule(tw.Leader.Id, bot.Id, 0));
        await Assert.That(bot.Job.Kind).IsEqualTo(JobKind.Idle);
        await Assert.That(tw.Leader.Carry.Count("construction-module")).IsEqualTo(1);
    }

    [Test]
    public async Task Modules_OnlyFitTheirRobots_AndNeedReach()
    {
        TestWorld tw = new TestWorld();
        tw.Leader.Carry.Add("construction-module", 2);
        await Assert.That(tw.Try(new InstallModule(tw.Leader.Id, tw.Leader.Id, "construction-module"))).Contains("only fits helper bots");
        tw.Leader.Carry.Add("siderite-plate", 1);
        await Assert.That(tw.Try(new InstallModule(tw.Leader.Id, tw.Leader.Id, "siderite-plate"))).IsEqualTo("That isn't a module");
        Unit bot = tw.Followers[0];
        bot.Pos = tw.Leader.Pos + new Int2(Fixed.One * 20, 0);
        await Assert.That(tw.Try(new InstallModule(tw.Leader.Id, bot.Id, "construction-module"))).IsEqualTo("Too far away");
    }

    [Test]
    public async Task ServoAndCargoModules_ChangeSpeedAndPockets()
    {
        TestWorld tw = new TestWorld();
        Unit me = tw.Leader;
        int speed = me.Speed, slots = me.Carry.Slots;
        me.Carry.Add("servo-module", 1);
        me.Carry.Add("cargo-module", 1);
        tw.Do(new InstallModule(me.Id, me.Id, "servo-module"));
        tw.Do(new InstallModule(me.Id, me.Id, "cargo-module"));
        await Assert.That(me.Speed).IsEqualTo(speed * 130 / 100);
        await Assert.That(me.Carry.Slots).IsEqualTo(slots + 4);
        tw.Do(new RemoveModule(me.Id, me.Id, 0));
        await Assert.That(me.Speed).IsEqualTo(speed);
        await Assert.That(me.Modules).IsEquivalentTo(["cargo-module"]);
    }

    [Test]
    public async Task ChassisExpansion_GivesTheCommanderMoreSlots()
    {
        TestWorld tw = new TestWorld();
        await Assert.That(tw.P.ModuleSlots(tw.Leader)).IsEqualTo(2);
        tw.W.Research.Completed.Add("chassis-expansion");
        await Assert.That(tw.P.ModuleSlots(tw.Leader)).IsEqualTo(4);
    }

    [Test]
    public async Task Commander_BuildsGhostsInReachByItself()
    {
        TestWorld tw = new TestWorld();
        Int2 at = tw.Leader.Tile + new Int2(3, 0);
        tw.Leader.Carry.Add("regolith", 5);
        tw.Do(new PlaceBuilding("burner-smelter", at, Dir.North));
        tw.RunUntil(() => tw.P.Sites.Count == 0, 60 * 5, "the commander to build the furnace");
        await Assert.That(tw.P.BuildingAt(at)?.Def.Id).IsEqualTo("burner-smelter");
        await Assert.That(tw.Followers.All(f => f.Job.Kind == JobKind.Idle)).IsTrue();
    }

    [Test]
    public async Task Commander_BuildsFromTheLanderInReach()
    {
        TestWorld tw = new TestWorld();
        int stone = tw.Lander.Inv.Count("regolith");
        Int2 at = tw.Leader.Tile + new Int2(-4, 0);
        tw.Do(new PlaceBuilding("burner-smelter", at, Dir.North));
        tw.RunUntil(() => tw.P.Sites.Count == 0, 60 * 5, "the commander to build the furnace from the lander");
        await Assert.That(tw.Lander.Inv.Count("regolith")).IsEqualTo(stone - 5);
        await Assert.That(tw.Leader.Carry.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Commander_LeavesGhostsOutOfReach()
    {
        TestWorld tw = new TestWorld();
        Int2 at = tw.Leader.Tile + new Int2(20, 0);
        tw.Leader.Carry.Add("regolith", 5);
        tw.Do(new PlaceBuilding("burner-smelter", at, Dir.North));
        tw.Run(60 * 5);
        await Assert.That(tw.P.Sites.Count).IsEqualTo(1);
    }

    [Test]
    public async Task WalkTo_GoesAroundObstaclesAndStops()
    {
        TestWorld tw = new TestWorld();
        Int2 start = tw.Leader.Tile;
        tw.Water(start.X + 3, start.Y - 3, 1, 7);
        Int2 goal = start + new Int2(6, 0);
        tw.Do(new WalkTo(tw.Leader.Id, goal));
        tw.RunUntil(() => tw.Leader.WalkTarget is null, 60 * 30, "the commander to arrive");
        await Assert.That(tw.Leader.Tile).IsEqualTo(goal);
    }
}
