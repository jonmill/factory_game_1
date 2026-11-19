namespace Factory.Sim.Tests;

/// <summary>Fog of war: robots and buildings uncover the map; robots only work what has been explored.</summary>
public class ExplorationTests
{
    private static World NewWorld(ulong seed = 42) => new World(TestWorld.Content, new WorldSettings { Seed = seed });

    [Test]
    public async Task NewGame_ShowsOnlyTheLandingSite()
    {
        World w = NewWorld();
        Planet p = w.Planets[0];
        Int2 center = new Int2(p.Width / 2, p.Height / 2);

        await Assert.That(p.IsExplored(center)).IsTrue();
        await Assert.That(p.IsExplored(center + new Int2(15, 0))).IsTrue();
        await Assert.That(p.IsExplored(center + new Int2(p.Width / 2, p.Height / 2))).IsFalse();
        await Assert.That(p.ExploredTiles).IsLessThan(p.Width * p.Height / 8);
        // The other planets haven't been visited at all.
        await Assert.That(w.Planets[1].ExploredTiles).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyTestWorlds_AreFullyExplored()
    {
        TestWorld tw = new TestWorld();
        await Assert.That(tw.P.ExploredTiles).IsEqualTo(tw.P.Width * tw.P.Height);
    }

    [Test]
    public async Task WalkingUncoversTheMap()
    {
        World w = NewWorld();
        Planet p = w.Planets[0];
        Unit leader = p.Units.First(u => u.IsLeader);
        Int2 far = leader.Tile + new Int2(30, 0);
        await Assert.That(p.IsExplored(far)).IsFalse();

        w.Submit(new WalkTo(leader.Id, far));
        for (int i = 0; (i < 60 * 60) && (p.IsExplored(far) == false); i++)
        {
            w.Step();
        }

        await Assert.That(p.IsExplored(far)).IsTrue();
    }

    [Test]
    public async Task NothingCanBeBuiltOrMinedInUnexploredGround()
    {
        World w = NewWorld();
        Planet p = w.Planets[0];
        Int2 corner = new Int2(p.Width / 2, p.Height / 2) + new Int2(p.Width / 2, p.Height / 2);
        await Assert.That(p.CanPlace(TestWorld.Content.Buildings["lab"], corner, Dir.North)).Contains("explored");

        Unit leader = p.Units.First(u => u.IsLeader);
        await Assert.That(new MineTile(leader.Id, corner).Validate(w)).Contains("explored");
    }

    [Test]
    public async Task GatherersOnlyFindExploredDeposits()
    {
        World w = NewWorld();
        Planet p = w.Planets[0];
        Int2 center = new Int2(p.Width / 2, p.Height / 2);
        ResourceDef lead = TestWorld.Content.Resources["galena"]; // generated well away from the lander

        await Assert.That(p.FindNearestResource(center, lead)).IsNull();
        p.RevealAll();
        await Assert.That(p.FindNearestResource(center, lead)).IsNotNull();
    }

    [Test]
    public async Task SpiralPoints_GoRoundRingByRing()
    {
        List<Int2> first = Enumerable.Range(0, 25).Select(Planet.SpiralPoint).ToList();

        await Assert.That(first.Distinct().Count()).IsEqualTo(25);
        await Assert.That(first.Take(9).All(pt => (System.Math.Abs(pt.X) <= 1) && (System.Math.Abs(pt.Y) <= 1))).IsTrue();
        await Assert.That(first.Skip(9).All(pt => System.Math.Max(System.Math.Abs(pt.X), System.Math.Abs(pt.Y)) == 2)).IsTrue();
    }

    [Test]
    public async Task ExploreJob_SpiralsOutAndFindsFarDeposits()
    {
        World w = NewWorld();
        Planet p = w.Planets[0];
        Unit helper = p.Units.First(u => u.IsLeader == false);
        int before = p.ExploredTiles;
        Int2 center = new Int2(p.Width / 2, p.Height / 2);
        ResourceDef lead = TestWorld.Content.Resources["galena"];

        w.Submit(new AssignJob(helper.Id, new Job(JobKind.Explore)));
        for (int i = 0; (i < 60 * 60 * 20) && (p.FindNearestResource(center, lead) is null); i++)
        {
            w.Step();
        }

        await Assert.That(p.FindNearestResource(center, lead)).IsNotNull();
        await Assert.That(p.ExploredTiles).IsGreaterThan(before * 2);
        await Assert.That(helper.Status).IsEqualTo("Exploring");
    }
}
