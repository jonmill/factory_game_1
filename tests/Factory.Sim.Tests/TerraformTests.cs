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
        await Assert.That(tw.P.Readiness).IsEqualTo(0).Because("readiness counts terraforming done, not what the planet started with");
        Planet rime = tw.W.Planets.Single(p => p.Def.Id == "rime");
        await Assert.That(rime.Stats["hydrosphere"]).IsEqualTo(6000).Because("Rime starts with its own stats");
        await Assert.That(rime.Readiness).IsEqualTo(0);
        await Assert.That(tw.W.SystemReadiness).IsEqualTo(0).Because("the system starts at 0% ready for settlers");
        await Assert.That(tw.W.EnoughPlanetsReady).IsFalse();
    }

    [Test]
    public async Task Readiness_CountsTheClimbFromTheStartingValue()
    {
        TestWorld tw = new TestWorld();
        Planet rime = tw.W.Planets.Single(p => p.Def.Id == "rime");
        PlanetStatDef hydro = tw.W.Content.Terraform.Stats.Single(s => s.Id == "hydrosphere");
        rime.Stats["hydrosphere"] = 8000; // halfway from 6000 to 10000
        await Assert.That(rime.StatProgress(hydro)).IsEqualTo(50);
        await Assert.That(rime.StatPercent(hydro)).IsEqualTo(80).Because("the raw level is still shown in the planet details");
        foreach (PlanetStatDef s in tw.W.Content.Terraform.Stats)
        {
            rime.Stats[s.Id] = s.Target;
        }

        await Assert.That(rime.Readiness).IsEqualTo(100);
        await Assert.That(rime.IsReady).IsTrue();
    }

    [Test]
    public async Task Terraformer_RaisesItsStatTenPointsPerItem()
    {
        (TestWorld tw, Building _) = Powered();
        Building atmo = tw.Build("atmo-processor", 13, 9);
        atmo.Inv.AddUnchecked("catalyst-pack", 2);
        int before = tw.P.Stats["atmosphere"];
        tw.RunUntil(() => tw.P.Stats["atmosphere"] == before + 20, (400 * 2) + 120, "two catalysts' worth of atmosphere");
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

    /// <summary>
    /// Terraforming is paid like Factorio's rocket: in consumables from a factory the player can grow, not
    /// capped by how many terraformers fit on a planet.
    /// </summary>
    [Test]
    public async Task Terraforming_ScalesWithTheFactory()
    {
        (TestWorld tw, Building _) = Powered();
        tw.Unlock("atmo-processor");
        tw.Build("atmo-processor", 13, 9);
        tw.Build("atmo-processor", 13, 13);
        await Assert.That(tw.Try(new PlaceBuilding("atmo-processor", new Int2(13, 17), Dir.North))).IsNull().Because("build as many as you can feed");

        // A stat takes on the order of a thousand consumables: comparable to an age of research, not tens of thousands.
        ContentDb c = tw.W.Content;
        int pointsPerItem = Planet.TerraformEffect(c.Recipes["release-catalyst"].Effect)!.Value.Points;
        PlanetStatDef atmo = c.Terraform.Stats.First(s => s.Id == "atmosphere");
        int items = (atmo.Target - atmo.Initial) / pointsPerItem;
        await Assert.That(items).IsBetween(500, 2000).Because($"a stat costs {items} catalysts");
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
