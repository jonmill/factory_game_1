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
        await Assert.That(inv.Add("biomass", 120)).IsEqualTo(100); // 2 stacks of 50
        await Assert.That(inv.Add("regolith", 1)).IsEqualTo(0);
        await Assert.That(inv.IsFull).IsTrue();
        await Assert.That(inv.Remove("biomass", 30)).IsEqualTo(30);
        await Assert.That(inv.SpaceFor("biomass")).IsEqualTo(30);
        await Assert.That(inv.SpaceFor("regolith")).IsEqualTo(0);
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
        foreach (string? id in new[] { "siderite", "cuprite", "carbon", "regolith" })
        {
            Int2? t = p.FindNearestResource(center, TestWorld.Content.Resources[id]);
            await Assert.That(t).IsNotNull();
            await Assert.That(p.Torus.Chebyshev(center, t.Value) <= 22).IsTrue().Because($"{id} too far: {t}");
        }

        Int2? lead = p.FindNearestResource(center, TestWorld.Content.Resources["galena"]);
        await Assert.That(lead).IsNotNull();
        await Assert.That(p.Torus.Chebyshev(center, lead.Value) >= 20).IsTrue().Because("lead should need a railway");
        await Assert.That(p.Units.Count(u => u.IsLeader == false)).IsEqualTo(2);
    }

    [Test]
    public async Task Replay_ReproducesTheWorldThroughJson()
    {
        TestWorld tw = new TestWorld(empty: false, seed: 99);
        Unit follower = tw.Followers[0];
        tw.W.Submit(new AssignJob(follower.Id, new Job(JobKind.Gather, Resource: "siderite")));
        tw.W.Submit(new AssignJob(tw.Followers[1].Id, new Job(JobKind.Gather, Resource: "regolith")));
        tw.W.Submit(new SetMoveIntent(tw.Leader.Id, 1, 0));
        tw.Run(30);
        tw.W.Submit(new PlaceBuilding("chest", new Int2(tw.Lander.Origin.X + 5, tw.Lander.Origin.Y), Dir.North));
        tw.W.Submit(new QueueCraft(tw.Lander.Id, "drive-cog", 3));
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
        string[] highTech = ["arc-smelter", "assembler", "assembler-mk2", "assembler-mk3", "atmo-processor", "bio-incubator", "climate-engine", "conveyor-belt", "electric-drill", "fusion-reactor", "hydro-seeder", "lab", "lander", "logistics-station", "orbital-collector", "orbital-station", "payload-launcher", "plasma-drill", "plasma-smelter", "robot-bay", "settler-uplink", "solar-panel", "sorter", "space-elevator", "wind-turbine"];

        string[] actual = TestWorld.Content.BuildingList.Where(b => b.Look == VisualStyle.HighTech).Select(b => b.Id).Order(StringComparer.Ordinal).ToArray();

        await Assert.That(actual).IsEquivalentTo(highTech);
        await Assert.That(TestWorld.Content.Vehicles.Values.All(v => v.Look == VisualStyle.Industrial)).IsTrue();
    }

    [Test]
    public async Task Mining_HelpersAreSlowest_AndEveryDrillTierIsFaster()
    {
        ContentDb c = TestWorld.Content;
        ResourceDef iron = c.Resources["siderite"];
        static double PerMin(BuildingDef d) => 3600.0 * d.Speed / 100 / d.MiningTicks;
        double helper = c.Robots.Values.Where(r => r.Id != c.Start.CommanderRobot).Max(r => 3600.0 * r.MiningPct / 100 / iron.HandTicks);
        double commander = 3600.0 * c.Robots[c.Start.CommanderRobot].MiningPct / 100 / iron.HandTicks;
        double[] drills = c.BuildingList.Where(b => b.Kind == BuildingKind.Drill).OrderBy(b => c.AgeIndex(b.AgeId)).Select(PerMin).ToArray();
        await Assert.That(helper < commander).IsTrue();
        await Assert.That(helper < drills[0]).IsTrue().Because("helper bots are the slowest way to mine");
        for (int i = 1; i < drills.Length; i++)
        {
            await Assert.That(drills[i] > drills[i - 1]).IsTrue();
        }

        double[] fabs = c.BuildingList.Where(b => (b.Kind is BuildingKind.Assembler or BuildingKind.Lander) && b.Categories.Contains("crafting")).OrderBy(b => b.Speed).Select(b => (double)b.Speed).ToArray();
        await Assert.That(fabs.Distinct().Count()).IsEqualTo(fabs.Length).Because("each fabricator tier is a step up");
    }

    [Test]
    public async Task KerogenFuel_IsWorthRefining()
    {
        ContentDb c = TestWorld.Content;
        RecipeDef refine = c.Recipes["kerogen-fuel"];
        double perCrude = c.Items["kerogen-fuel"].FuelKj * refine.Out["kerogen-fuel"] / (double)refine.In["crude-kerogen"];
        await Assert.That(perCrude >= 2 * c.Items["compacted-carbon"].FuelKj).IsTrue();
    }
}
