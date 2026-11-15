namespace Factory.Sim.Tests;

public class CleanAgeTests
{
    private static int Index(TestWorld tw, string planet) => tw.W.Planets.FindIndex(p => p.Def.Id == planet);

    [Test]
    public async Task SolarPanel_PowersALab_ScaledBySunlight()
    {
        TestWorld tw = new TestWorld();
        Building panel = tw.Build("solar-panel", 10, 10);
        tw.Build("power-pole", 12, 10);
        Building lab = tw.Build("lab", 13, 11);
        tw.Do(new SetResearch("automation"));
        lab.Inv.AddUnchecked("survey-core", 5);
        tw.Run(60);
        await Assert.That(lab.Status).IsEqualTo(MachineStatus.Working);
        await Assert.That(tw.P.GenCapacityJ(panel)).IsEqualTo(Planet.PerTickJ(90));
        Planet rime = tw.W.Planets[Index(tw, "rime")];
        await Assert.That(rime.GenCapacityJ(panel)).IsEqualTo(Planet.PerTickJ(90) * 55 / 100);
    }

    [Test]
    public async Task WindTurbine_GrowsWithTheAtmosphere()
    {
        TestWorld tw = new TestWorld();
        Building turbine = tw.Build("wind-turbine", 10, 10);
        long thin = tw.P.GenCapacityJ(turbine);
        tw.P.Stats["atmosphere"] = 10000;
        await Assert.That(tw.P.GenCapacityJ(turbine)).IsEqualTo(Planet.PerTickJ(300));
        await Assert.That(thin < Planet.PerTickJ(300) / 3).IsTrue();
    }

    [Test]
    public async Task KerogenGenerator_RunsAwayFromWater()
    {
        TestWorld tw = new TestWorld();
        Building gen = tw.Build("kerogen-generator", 10, 10);
        gen.Inv.AddUnchecked("kerogen-fuel", 5);
        tw.Build("power-pole", 12, 10);
        Building lab = tw.Build("lab", 13, 11);
        tw.Do(new SetResearch("automation"));
        lab.Inv.AddUnchecked("survey-core", 5);
        tw.Run(60);
        await Assert.That(gen.Status).IsEqualTo(MachineStatus.Working);
        await Assert.That(lab.Status).IsEqualTo(MachineStatus.Working);
    }

    [Test]
    public async Task HoverModule_LetsTheCommanderCrossWater()
    {
        TestWorld tw = new TestWorld();
        Int2 start = tw.Lander.Origin + new Int2(-20, 12);
        tw.Leader.Pos = Fixed.TileCenter(start);
        tw.Water(start.X + 2, start.Y - 30, 3, 60);
        tw.Do(new WalkTo(tw.Leader.Id, start + new Int2(8, 0)));
        tw.Run(240);
        await Assert.That(tw.Leader.Tile.X < start.X + 2).IsTrue().Because("without hover the lake is in the way");

        tw.Leader.Pos = Fixed.TileCenter(start);
        tw.Equip(tw.Leader, "hover-module");
        tw.Do(new WalkTo(tw.Leader.Id, start + new Int2(8, 0)));
        tw.RunUntil(() => tw.Leader.Tile == start + new Int2(8, 0), 600, "the commander to float across");
    }

    [Test]
    public async Task PayloadLauncher_TerraformsItsTarget()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("launch-catalyst", "launch-hydro", "launch-seeds");
        Building panel = tw.Build("solar-panel", 8, 10);
        Building panel2 = tw.Build("solar-panel", 8, 12);
        Building panel3 = tw.Build("solar-panel", 8, 14);
        Building panel4 = tw.Build("solar-panel", 8, 16);
        Building panel5 = tw.Build("solar-panel", 8, 18);
        tw.Build("power-pole", 10, 13);
        Building launcher = tw.Build("payload-launcher", 11, 12);
        launcher.Inv.AddUnchecked("seed-pod", 3);
        tw.Run(60);
        await Assert.That(launcher.Status).IsEqualTo(MachineStatus.NoTarget);

        Planet rime = tw.W.Planets[Index(tw, "rime")];
        await Assert.That(tw.Try(new SetLaunchTarget(launcher.Id, 0))).IsNotNull();
        tw.Do(new SetLaunchTarget(launcher.Id, rime.Id));
        tw.RunUntil(() => rime.Stats["biosphere"] >= 6, 60 * 60, "three seed payloads to land on Rime");
        await Assert.That(tw.P.Stats["biosphere"]).IsEqualTo(0);
        await Assert.That(launcher.Inv.Count("seed-pod")).IsEqualTo(0);
        _ = (panel, panel2, panel3, panel4, panel5);
    }
}
