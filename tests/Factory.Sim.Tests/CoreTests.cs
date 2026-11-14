namespace Factory.Sim.Tests;

public class CoreTests
{
    [Test]
    public async Task DefaultContent_LoadsAndPassesValidation()
    {
        ContentDb c = ContentDb.LoadDefault();
        await Assert.That(c.Items.Count > 10).IsTrue();
        await Assert.That(c.Buildings.Keys).Contains("lander");
        foreach (TechDef t in c.TechList)
        {
            foreach (string p in t.Prereqs)
            {
                await Assert.That(c.Techs.Keys).Contains(p);
            }
        }
    }

    [Test]
    public async Task Content_WithUnknownItem_IsRejected()
    {
        string json = """{ "items": [], "recipes": [ { "id": "x", "in": { "nope": 1 }, "out": {} } ] }""";
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => ContentDb.FromJson(json));
        await Assert.That(ex.Message).Contains("nope");
    }

    [Test]
    public async Task Torus_WrapsAndFindsShortestDelta()
    {
        Torus t = new Torus(100, 50);
        await Assert.That(t.Wrap(new Int2(-1, -1))).IsEqualTo(new Int2(99, 49));
        await Assert.That(t.Delta(new Int2(99, 0), new Int2(1, 0))).IsEqualTo(new Int2(2, 0));
        await Assert.That(t.Delta(new Int2(1, 0), new Int2(99, 0))).IsEqualTo(new Int2(-2, 0));
        await Assert.That(t.Chebyshev(new Int2(0, 0), new Int2(0, 49))).IsEqualTo(1);
    }

    [Test]
    public async Task ISqrt_IsExact()
    {
        for (long n = 0; n < 5000; n++)
        {
            long r = Fixed.ISqrt(n);
            await Assert.That((r * r <= n) && ((r + 1) * (r + 1) > n)).IsTrue();
        }
    }

    [Test]
    public async Task Inventory_RespectsSlotAndStackLimits()
    {
        Inventory inv = new Inventory(TestWorld.Content, 2);
        await Assert.That(inv.Add("wood", 120)).IsEqualTo(100); // 2 stacks of 50
        await Assert.That(inv.Add("stone", 1)).IsEqualTo(0);
        await Assert.That(inv.IsFull).IsTrue();
        await Assert.That(inv.Remove("wood", 30)).IsEqualTo(30);
        await Assert.That(inv.SpaceFor("wood")).IsEqualTo(30);
        await Assert.That(inv.SpaceFor("stone")).IsEqualTo(0);
    }

    [Test]
    public async Task WorldGen_SameSeedMatchesAndDifferentSeedsDiffer()
    {
        World a = new World(TestWorld.Content, new WorldSettings { Seed = 7 });
        World b = new World(TestWorld.Content, new WorldSettings { Seed = 7 });
        World c = new World(TestWorld.Content, new WorldSettings { Seed = 8 });
        await Assert.That(b.Checksum()).IsEqualTo(a.Checksum());
        await Assert.That(c.Checksum()).IsNotEqualTo(a.Checksum());
    }

    [Test]
    public async Task WorldGen_PlacesStarterResourcesNearLanderAndLeadFarAway()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 42 });
        Planet p = w.Planets[0];
        Int2 center = new Int2(p.Width / 2, p.Height / 2);
        foreach (string? id in new[] { "iron", "copper", "coal", "stone" })
        {
            Int2? t = p.FindNearestResource(center, TestWorld.Content.Resources[id]);
            await Assert.That(t).IsNotNull();
            await Assert.That(p.Torus.Chebyshev(center, t.Value) <= 22).IsTrue().Because($"{id} too far: {t}");
        }

        Int2? lead = p.FindNearestResource(center, TestWorld.Content.Resources["lead"]);
        await Assert.That(lead).IsNotNull();
        await Assert.That(p.Torus.Chebyshev(center, lead.Value) >= 20).IsTrue().Because("lead should need a railway");
        await Assert.That(p.Units.Count(u => u.IsLeader == false)).IsEqualTo(3);
    }

    [Test]
    public async Task Replay_ReproducesTheWorldThroughJson()
    {
        TestWorld tw = new TestWorld(empty: false, seed: 99);
        Unit follower = tw.Followers[0];
        tw.W.Submit(new AssignJob(follower.Id, new Job(JobKind.Gather, Resource: "iron")));
        tw.W.Submit(new AssignJob(tw.Followers[1].Id, new Job(JobKind.Build)));
        tw.W.Submit(new SetMoveIntent(tw.Leader.Id, 1, 0));
        tw.Run(30);
        tw.W.Submit(new PlaceBuilding("chest", new Int2(tw.Lander.Origin.X + 5, tw.Lander.Origin.Y), Dir.North));
        tw.W.Submit(new QueueCraft(tw.Lander.Id, "iron-gear", 3));
        tw.W.Submit(new SetMoveIntent(tw.Leader.Id, 0, 0));
        tw.Run(1200);

        string json = Replay.Capture(tw.W).ToJson();
        Replay replay = Replay.FromJson(json);
        World again = replay.Play(TestWorld.Content);
        await Assert.That(again.Tick).IsEqualTo(tw.W.Tick);
        await Assert.That(again.Checksum()).IsEqualTo(tw.W.Checksum());
        await Assert.That(again.Checksum()).IsEqualTo(replay.FinalChecksum);
    }

    [Test]
    public async Task InvalidCommands_AreRejectedWithReasons()
    {
        TestWorld tw = new TestWorld();
        tw.Water(10, 10, 3, 3);
        await Assert.That(tw.Try(new PlaceBuilding("chest", new Int2(10, 10), Dir.North))).IsNotNull();
        await Assert.That(tw.Try(new PlaceBuilding("assembler", new Int2(20, 20), Dir.North))).Contains("researched");
        await Assert.That(tw.Try(new PlaceBuilding("steam-engine", new Int2(30, 30), Dir.North))).Contains("water");
        await Assert.That(tw.Try(new SetResearch("conveyor-belts"))).IsNotNull();
        await Assert.That(tw.Try(new SetResearch("mechanical-arms"))).IsNotNull(); // needs prereq
        await Assert.That(tw.Try(new DeconstructEntity(tw.Lander.Id))).IsNotNull();
        await Assert.That(tw.Try(new AssignJob(tw.Leader.Id, Job.Idle))).IsNotNull();
        await Assert.That(tw.Try(new PlaceBuilding("rail-stop", new Int2(40, 40), Dir.East))).IsNotNull();
    }

    [Test]
    public async Task Content_DrawsShipAndElectricTechAsHighTechAndEverythingElseAsIndustrial()
    {
        string[] highTech = ["assembler", "colony-beacon", "electric-drill", "lab", "lander", "sorter"];

        string[] actual = TestWorld.Content.BuildingList.Where(b => b.Look == VisualStyle.HighTech).Select(b => b.Id).Order(StringComparer.Ordinal).ToArray();

        await Assert.That(actual).IsEquivalentTo(highTech);
        await Assert.That(TestWorld.Content.Vehicles.Values.All(v => v.Look == VisualStyle.Industrial)).IsTrue();
    }
}
