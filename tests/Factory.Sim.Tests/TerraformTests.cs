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
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(800);
        await Assert.That(tw.P.Stats["biosphere"]).IsEqualTo(0);
        await Assert.That(tw.P.Readiness).IsEqualTo((8 + 15 + 0) / 3).Because("stats run to 10000 points, readiness is a percent");
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
        tw.RunUntil(() => tw.P.Stats["atmosphere"] == before + 4, (1200 * 2) + 120, "two catalysts' worth of atmosphere");
        await Assert.That(atmo.Inv.Count("catalyst-pack")).IsEqualTo(0);
    }

    [Test]
    public async Task Terraformer_StopsAtTheTarget()
    {
        (TestWorld tw, Building _) = Powered();
        Building atmo = tw.Build("atmo-processor", 13, 9);
        tw.P.Stats["atmosphere"] = 10000;
        atmo.Inv.AddUnchecked("catalyst-pack", 2);
        tw.Run(120);
        await Assert.That(atmo.Status).IsEqualTo(MachineStatus.Complete);
        await Assert.That(atmo.Inv.Count("catalyst-pack")).IsEqualTo(2);
    }

    [Test]
    public async Task EarlyTerraformers_AreLimitedPerPlanet_AndTakeDozensOfHours()
    {
        (TestWorld tw, Building _) = Powered();
        tw.Unlock("atmo-processor");
        tw.Build("atmo-processor", 13, 9);
        tw.Build("atmo-processor", 13, 13);
        await Assert.That(tw.Try(new PlaceBuilding("atmo-processor", new Int2(13, 17), Dir.North))).Contains("Only 2");

        // Two processors working flat out on the home planet's atmosphere.
        ContentDb c = tw.W.Content;
        RecipeDef r = c.Recipes["release-catalyst"];
        int pointsPerCraft = Planet.TerraformEffect(r.Effect)!.Value.Points;
        PlanetStatDef atmo = c.Terraform.Stats.First(s => s.Id == "atmosphere");
        double hours = (atmo.Target - atmo.Initial) / (double)pointsPerCraft * r.Ticks / 2 / 3600 / 60;
        await Assert.That(hours >= 10).IsTrue().Because($"early terraforming alone should take many hours, got {hours:F1}");
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

        tw.P.Stats["atmosphere"] = 10000;
        tw.P.Stats["hydrosphere"] = 10000;
        tw.P.Stats["biosphere"] = 10000;
        await Assert.That(tw.W.EnoughPlanetsReady).IsFalse().Because("two more planets must be terraformed");
        Planet[] others = tw.W.TerraformablePlanets.Where(p => p != tw.P).ToArray();
        for (int i = 0; i < 2; i++)
        {
            await Assert.That(tw.W.EnoughPlanetsReady).IsFalse();
            others[i].Stats["atmosphere"] = 10000;
            others[i].Stats["hydrosphere"] = 10000;
            others[i].Stats["biosphere"] = 10000;
        }

        await Assert.That(tw.W.ReadyPlanetCount).IsEqualTo(3);
        await Assert.That(tw.W.EnoughPlanetsReady).IsTrue().Because("any three of the four will do");
        tw.RunUntil(() => tw.W.SettlersCalled, 1800 + 120, "the settlers to be called");
        await Assert.That(tw.W.Events.Any(e => e.Message.Contains("You win"))).IsTrue();
    }
}
