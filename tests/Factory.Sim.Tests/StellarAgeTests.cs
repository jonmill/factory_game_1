namespace Factory.Sim.Tests;

public class StellarAgeTests
{
    /// <summary>A pole with a ring of fuelled fusion power next to a spot, so big machines run.</summary>
    private static void Power(TestWorld tw, int x, int y)
    {
        Building reactor = tw.Build("fusion-reactor", x, y);
        reactor.Inv.AddUnchecked("fusion-cell", 5);
        tw.Build("power-pole", x + 4, y);
    }

    [Test]
    public async Task FusionReactor_BurnsFusionCellsForTwelveMegawatts()
    {
        TestWorld tw = new TestWorld();
        Building reactor = tw.Build("fusion-reactor", 10, 10);
        reactor.Inv.AddUnchecked("fusion-cell", 2);
        await Assert.That(tw.P.GenCapacityJ(reactor)).IsEqualTo(Planet.PerTickJ(12000));
        await Assert.That(tw.P.AcceptAmount(reactor, "fusion-cell") > 0).IsTrue();
    }

    [Test]
    public async Task OrbitalCollector_SiphonsHeliumWithNoInputs()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("collect-helium-3");
        Power(tw, 10, 10);
        Building collector = tw.Build("orbital-collector", 15, 10);
        tw.Do(new SetRecipe(collector.Id, "collect-helium-3"));
        tw.Run(60 * 30);
        await Assert.That(collector.Out.Count("helium-3") > 0).IsTrue();
    }

    [Test]
    public async Task SpaceElevator_DoublesCollectorSpeedAndMakesLaunchesFree()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("collect-osmiridium");
        Power(tw, 10, 10);
        Building collector = tw.Build("orbital-collector", 15, 10);
        tw.Do(new SetRecipe(collector.Id, "collect-osmiridium"));
        tw.Run(60 * 60);
        int without = collector.Out.Count("osmiridium-ore");
        collector.Out.RemoveAll([new ItemAmount("osmiridium-ore", without)]);
        Building pad = tw.Build("launch-pad", 30, 30);
        await Assert.That(tw.P.LaunchFuel(pad).Length > 0).IsTrue();

        Power(tw, 10, 20);
        tw.Build("space-elevator", 15, 20);
        tw.Run(10);
        await Assert.That(tw.P.HasSpaceElevator).IsTrue();
        await Assert.That(tw.P.LaunchFuel(pad).Length).IsEqualTo(0);
        tw.Run(60 * 60);
        await Assert.That(collector.Out.Count("osmiridium-ore") >= (2 * without) - 2).IsTrue();
    }

    [Test]
    public async Task ClimateEngine_TakesASurgeAndTerraformsFast()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("climate-atmosphere");
        Power(tw, 10, 10);
        Building engine = tw.Build("climate-engine", 15, 10);
        tw.Do(new SetRecipe(engine.Id, "climate-atmosphere"));
        engine.Inv.AddUnchecked("catalyst-pack", 2);
        engine.Inv.AddUnchecked("helium-3", 2);
        int before = tw.P.Stats["atmosphere"];
        tw.Run(60 * 11);
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(before + 60);
    }

    [Test]
    public async Task ClimateAmplifiers_BurnAccelerantToDoubleTheSpeed()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("climate-atmosphere", "climate-amplifier");
        Power(tw, 10, 10);
        Building engine = tw.Build("climate-engine", 15, 10);
        tw.Do(new SetRecipe(engine.Id, "climate-atmosphere"));
        tw.Leader.Carry.AddUnchecked("climate-amplifier", 2);
        tw.Leader.Pos = Fixed.TileCenter(new Int2(14, 12));
        tw.Do(new InstallMachineModule(tw.Leader.Id, engine.Id, "climate-amplifier"));
        tw.Do(new InstallMachineModule(tw.Leader.Id, engine.Id, "climate-amplifier"));
        await Assert.That(tw.Try(new InstallMachineModule(tw.Leader.Id, engine.Id, "climate-amplifier"))).IsNotNull();
        await Assert.That(tw.Try(new InstallModule(tw.Leader.Id, tw.Leader.Id, "climate-amplifier"))).IsNotNull().Because("it goes in machines, not robots");
        engine.Inv.AddUnchecked("catalyst-pack", 4);
        engine.Inv.AddUnchecked("helium-3", 4);
        engine.Inv.AddUnchecked("lanthanide-accelerant", 2);
        int before = tw.P.Stats["atmosphere"];
        tw.Run((600 / 3) + 5);
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(before + 60).Because("two amplifiers make a surge three times as fast");
        await Assert.That(engine.Inv.Count("lanthanide-accelerant")).IsEqualTo(0);
        await Assert.That(tw.W.Stats.Count("lanthanide-accelerant", ItemFlow.CraftInput, by: "climate-engine", via: "climate-amplifier")).IsEqualTo(2);
        // Out of accelerant, the next surge runs at the normal speed.
        tw.Run((600 / 3) + 5);
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(before + 60);
        tw.Run(600);
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(before + 120);
    }

    [Test]
    public async Task ClimateAcceleration_AddsAQuarterPerLevel()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("climate-atmosphere");
        tw.W.Research.Levels[Planet.ClimateAcceleration] = 2;
        Power(tw, 10, 10);
        Building engine = tw.Build("climate-engine", 15, 10);
        tw.Do(new SetRecipe(engine.Id, "climate-atmosphere"));
        engine.Inv.AddUnchecked("catalyst-pack", 2);
        engine.Inv.AddUnchecked("helium-3", 2);
        int before = tw.P.Stats["atmosphere"];
        tw.Run(60 * 11);
        await Assert.That(tw.P.Stats["atmosphere"]).IsEqualTo(before + 90);
    }

    [Test]
    public async Task VeinUtilization_RepeatsAndStretchesDeposits()
    {
        TestWorld tw = new TestWorld();
        TechDef vein = tw.W.Content.Techs[Planet.VeinUtilization];
        foreach (string pre in tw.W.Content.TechList.Select(t => t.Id))
        {
            tw.W.Research.Completed.Add(pre);
        }

        tw.W.Research.Completed.Remove(vein.Id);
        tw.Do(new SetResearch(vein.Id));
        for (int i = 0; i < tw.W.Research.UnitsFor(vein); i++)
        {
            tw.W.AddResearchUnit();
        }

        await Assert.That(tw.W.Research.Level(vein.Id)).IsEqualTo(1);
        await Assert.That(tw.W.Research.Current).IsEqualTo(vein.Id).Because("infinite research carries on");
        await Assert.That(tw.W.Research.UnitsFor(vein)).IsEqualTo(vein.Units + (vein.Units / 2));
        await Assert.That(tw.Try(new SetResearch(vein.Id))).IsNull();

        // Level 10: 200 items mined use up only 100 of the deposit.
        tw.W.Research.Levels[vein.Id] = 10;
        tw.Ore("siderite", 40, 40, 1, 1, 500);
        Int2 tile = new Int2(40, 40);
        for (int i = 0; i < 200; i++)
        {
            tw.P.MineOne(tile, ItemFlow.HandMined);
        }

        await Assert.That(tw.P.AmountAt(tile)).IsEqualTo(400);
    }

    [Test]
    public async Task StellarTechs_AreResearchedWithStellarCores()
    {
        ContentDb c = TestWorld.Content;
        foreach (TechDef t in c.TechList.Where(t => t.Age == "stellar"))
        {
            await Assert.That(t.UnitCost.ContainsKey("stellar-core")).IsTrue().Because(t.Id);
            await Assert.That(t.ComingSoon).IsFalse().Because(t.Id);
        }
    }
}
