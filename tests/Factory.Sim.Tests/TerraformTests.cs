namespace Factory.Sim.Tests;

public class TerraformTests
{
    private static (TestWorld Tw, Building Engine) Powered()
    {
        TestWorld tw = new TestWorld();
        tw.Water(9, 10, 1, 2);
        Building engine = tw.Build("steam-engine", 10, 10);
        engine.Inv.AddUnchecked("compacted-carbon", 500);
        tw.Build("power-pole", 12, 10);

        return (tw, engine);
    }

    [Test]
    public async Task PlanetsStartWithTheirInitialStats()
    {
        TestWorld tw = new TestWorld();
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(8);
        await Assert.That(tw.P.Stats["biosphere"]).IsEqualTo(0);
        await Assert.That(tw.P.Readiness).IsEqualTo((8 + 15 + 0) / 3);
        Planet rime = tw.W.Planets.Single(p => p.Def.Id == "rime");
        await Assert.That(rime.Readiness).IsEqualTo((5 + 60 + 0) / 3).Because("Rime starts with its own stats");
        await Assert.That(tw.W.EnoughPlanetsReady).IsFalse();
    }

    [Test]
    public async Task Terraformer_RaisesItsStatTwoPointsPerItem()
    {
        (TestWorld tw, Building _) = Powered();
        Building atmo = tw.Build("atmo-processor", 13, 9);
        atmo.Inv.AddUnchecked("catalyst-pack", 2);
        int before = tw.P.Stats["atmosphere"];
        tw.RunUntil(() => tw.P.Stats["atmosphere"] == before + 4, (600 * 2) + 120, "two catalysts' worth of atmosphere");
        await Assert.That(atmo.Inv.Count("catalyst-pack")).IsEqualTo(0);
    }

    [Test]
    public async Task Terraformer_StopsAtTheTarget()
    {
        (TestWorld tw, Building _) = Powered();
        Building atmo = tw.Build("atmo-processor", 13, 9);
        tw.P.Stats["atmosphere"] = 100;
        atmo.Inv.AddUnchecked("catalyst-pack", 2);
        tw.Run(120);
        await Assert.That(atmo.Status).IsEqualTo(MachineStatus.Complete);
        await Assert.That(atmo.Inv.Count("catalyst-pack")).IsEqualTo(2);
    }

    [Test]
    public async Task Uplink_WaitsForThreePlanets_ThenCallsTheSettlers()
    {
        (TestWorld tw, Building _) = Powered();
        Building uplink = tw.Build("settler-uplink", 13, 9);
        foreach (ItemAmount c in tw.W.Content.Recipes["call-settlers"].Inputs)
        {
            uplink.Inv.AddUnchecked(c.Item, c.Count);
        }

        tw.Run(120);
        await Assert.That(uplink.Status).IsEqualTo(MachineStatus.NotReady);
        await Assert.That(tw.W.SettlersCalled).IsFalse();

        tw.P.Stats["atmosphere"] = 100;
        tw.P.Stats["hydrosphere"] = 100;
        tw.P.Stats["biosphere"] = 100;
        await Assert.That(tw.W.EnoughPlanetsReady).IsFalse().Because("two more planets must be terraformed");
        Planet[] others = tw.W.TerraformablePlanets.Where(p => p != tw.P).ToArray();
        for (int i = 0; i < 2; i++)
        {
            await Assert.That(tw.W.EnoughPlanetsReady).IsFalse();
            others[i].Stats["atmosphere"] = 100;
            others[i].Stats["hydrosphere"] = 100;
            others[i].Stats["biosphere"] = 100;
        }

        await Assert.That(tw.W.ReadyPlanetCount).IsEqualTo(3);
        await Assert.That(tw.W.EnoughPlanetsReady).IsTrue().Because("any three of the four will do");
        tw.RunUntil(() => tw.W.SettlersCalled, 1800 + 120, "the settlers to be called");
        await Assert.That(tw.W.Events.Any(e => e.Message.Contains("You win"))).IsTrue();
    }
}
