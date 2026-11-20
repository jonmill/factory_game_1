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
        tw.W.Research.Completed.UnionWith(["automation", "robot-modules", "robotics", "analysis-cores"]);
        tw.Do(new SetResearch(Planet.RobotEfficiency));
        TechDef t = tw.W.Content.Techs[Planet.RobotEfficiency];
        for (int i = 0; i < tw.W.Research.UnitsFor(t); i++)
        {
            tw.W.AddResearchUnit();
        }

        await Assert.That(tw.W.Research.Level(Planet.RobotEfficiency)).IsEqualTo(1);
        await Assert.That(helper.Speed).IsEqualTo(before * 115 / 100);
        await Assert.That(tw.Leader.Speed).IsGreaterThan(tw.Leader.Robot.Speed).Because("the commander gets it too");
    }
}
