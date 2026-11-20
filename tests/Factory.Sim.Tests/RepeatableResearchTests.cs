namespace Factory.Sim.Tests;

/// <summary>
/// Endless research in the style of DSP: Terraforming Efficiency, Research Speed and Robot Efficiency each
/// repeat forever, costing more every level, and each level makes something measurably better.
/// </summary>
public class RepeatableResearchTests
{
    private static Building Powered(TestWorld tw)
    {
        tw.Build("solar-panel", 10, 10);
        tw.Build("solar-panel", 10, 12);
        tw.Build("solar-panel", 10, 14);

        return tw.Build("power-pole", 12, 12);
    }

    [Test]
    public async Task TheNewEndlessTechs_Repeat_AndCostMoreEachLevel()
    {
        foreach (string id in new[] { Planet.TerraformingEfficiency, Planet.ResearchSpeed, Planet.RobotEfficiency })
        {
            TestWorld tw = new TestWorld();
            TechDef t = tw.W.Content.Techs[id];
            await Assert.That(t.Repeatable).IsTrue();
            int first = tw.W.Research.UnitsFor(t);
            tw.W.Research.Levels[id] = 4;
            await Assert.That(tw.W.Research.UnitsFor(t)).IsGreaterThan(first);
        }
    }

    [Test]
    public async Task TerraformingEfficiency_MakesEachConsumableCountForMore()
    {
        TestWorld tw = new TestWorld();
        Powered(tw);
        Building atmo = tw.Build("atmo-processor", 13, 9);
        tw.W.Research.Levels[Planet.TerraformingEfficiency] = 2; // +40%
        atmo.Inv.AddUnchecked("catalyst-pack", 1);
        int before = tw.P.Stats["atmosphere"];
        tw.RunUntil(() => atmo.Inv.Count("catalyst-pack") == 0 && (atmo.Crafting == false), 1000, "the catalyst to be released");

        await Assert.That(tw.P.Stats["atmosphere"] - before).IsEqualTo(14);
        await Assert.That(tw.P.TerraformPoints(60, climateEngine: true)).IsEqualTo(84);
        tw.W.Research.Levels[Planet.ClimateAcceleration] = 1;
        await Assert.That(tw.P.TerraformPoints(60, climateEngine: true)).IsEqualTo(99).Because("climate acceleration adds its quarter on top for engines");
        await Assert.That(tw.P.TerraformPoints(10, climateEngine: false)).IsEqualTo(14).Because("but not for the first terraformers");
    }

    [Test]
    public async Task ResearchSpeed_MakesStationsFaster()
    {
        static long TicksForOneUnit(int level)
        {
            TestWorld tw = new TestWorld();
            Powered(tw);
            Building lab = tw.Build("lab", 13, 11);
            tw.W.Research.Levels[Planet.ResearchSpeed] = level;
            tw.Do(new SetResearch("automation"));
            lab.Inv.AddUnchecked("survey-core", 5);
            long start = tw.W.Tick;
            tw.RunUntil(() => tw.W.Research.Progress.GetValueOrDefault("automation") >= 1, 2000, "one unit");

            return tw.W.Tick - start;
        }

        long plain = TicksForOneUnit(0);
        long fast = TicksForOneUnit(5); // +100%
        await Assert.That(fast).IsLessThan(plain * 6 / 10).Because($"{plain} ticks plain, {fast} at level 5");
    }

    [Test]
    public async Task RobotEfficiency_SpeedsUpEveryRobot_WhenALevelFinishes()
    {
        TestWorld tw = new TestWorld();
        Unit helper = tw.Followers[0];
        int before = helper.Speed;
        tw.W.Research.Completed.UnionWith(["automation", "robot-modules", "robotics"]);
        tw.Do(new SetResearch("robot-efficiency-1"));
        TechDef t = tw.W.Content.Techs["robot-efficiency-1"];
        for (int i = 0; i < tw.W.Research.UnitsFor(t); i++)
        {
            tw.W.AddResearchUnit();
        }

        await Assert.That(tw.W.Research.Level(Planet.RobotEfficiency)).IsEqualTo(1).Because("Robot Efficiency I is the series' first level");
        await Assert.That(helper.Speed).IsEqualTo(before * 115 / 100);
        await Assert.That(tw.Leader.Speed).IsGreaterThan(tw.Leader.Robot.Speed).Because("the commander gets it too");
    }

    [Test]
    public async Task UpgradeSeries_OneOffLevelsCountTowardTheEndlessTech_ButNotItsPrice()
    {
        TestWorld tw = new TestWorld();
        TechDef vein = tw.W.Content.Techs[Planet.VeinUtilization];
        int fresh = tw.W.Research.UnitsFor(vein);
        string[] early = tw.W.Content.TechList.Where(t => t.LevelOf == Planet.VeinUtilization).Select(t => t.Id).ToArray();
        await Assert.That(early.Length).IsEqualTo(3);
        await Assert.That(early.Select(id => tw.W.Content.Techs[id].Age).ToArray()).IsEquivalentTo(new[] { "salvage", "combustion", "clean" });

        tw.W.Research.Completed.UnionWith(early);
        await Assert.That(tw.W.Research.Level(Planet.VeinUtilization)).IsEqualTo(3);
        await Assert.That(tw.W.Research.UnitsFor(vein)).IsEqualTo(fresh).Because("the endless tail starts at its own base price");

        // Saves rebuild the series from content: a restored world still sees the three levels.
        World back = WorldState.Restore(tw.W.Content, tw.W.Settings, WorldState.Capture(tw.W));
        await Assert.That(back.Research.Level(Planet.VeinUtilization)).IsEqualTo(3);
    }
}
