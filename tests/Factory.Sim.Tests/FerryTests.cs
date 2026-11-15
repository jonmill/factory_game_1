namespace Factory.Sim.Tests;

public class FerryTests
{
    /// <summary>A launch pad beside the commander, loaded with its rocket and fuel.</summary>
    private static (TestWorld Tw, Building Pad, int Rime) LoadedPad()
    {
        TestWorld tw = new TestWorld();
        Int2 at = tw.Lander.Origin + new Int2(6, 0);
        Building pad = tw.Build("launch-pad", at.X, at.Y);
        pad.Inv.AddUnchecked("ferry-rocket", 1);
        pad.Inv.AddUnchecked("kerogen-fuel", 20);
        tw.Leader.Pos = Fixed.TileCenter(at + new Int2(-1, 1));

        return (tw, pad, tw.W.Planets.FindIndex(p => p.Def.Id == "rime"));
    }

    [Test]
    public async Task Launch_NeedsTheRocketAndFuelAndASurfaceToLandOn()
    {
        (TestWorld tw, Building pad, int rime) = LoadedPad();
        pad.Inv.Remove("kerogen-fuel", 1);
        await Assert.That(tw.Try(new LaunchFerry(tw.Leader.Id, pad.Id, rime, []))).Contains("Kerogen Fuel");
        pad.Inv.AddUnchecked("kerogen-fuel", 1);
        int vesper = tw.W.Planets.FindIndex(p => p.Def.Id == "vesper");
        await Assert.That(vesper).IsEqualTo(-1).Because("gas giants have no surface planet to simulate");
        await Assert.That(tw.Try(new LaunchFerry(tw.Leader.Id, pad.Id, 0, []))).Contains("can't land");
        pad.Inv.Remove("ferry-rocket", 1);
        await Assert.That(tw.Try(new LaunchFerry(tw.Leader.Id, pad.Id, rime, []))).Contains("Ferry Rocket");
    }

    [Test]
    public async Task Ferry_FliesThereAndBackWithCargoAndPassengers()
    {
        (TestWorld tw, Building pad, int rime) = LoadedPad();
        pad.Inv.AddUnchecked("siderite-plate", 30);
        Unit commander = tw.Leader;
        Unit helper = tw.Followers[0];
        Planet start = tw.P;
        Planet ice = tw.W.Planets[rime];

        tw.Do(new LaunchFerry(commander.Id, pad.Id, rime, [helper.Id]));
        Building ferry = ice.Buildings.Single(b => b.Def.Kind == BuildingKind.Ferry);
        await Assert.That(commander.Planet).IsEqualTo(ice);
        await Assert.That(helper.Planet).IsEqualTo(ice);
        await Assert.That(ice.Units.Count).IsEqualTo(2);
        await Assert.That(ferry.Inv.Count("siderite-plate")).IsEqualTo(30);
        await Assert.That(pad.Inv.Total).IsEqualTo(0).Because("the rocket and fuel are used and the cargo flew");
        await Assert.That(ice.IsBlockedForPerson(commander.Pos)).IsFalse();
        await Assert.That(tw.Try(new DeconstructEntity(ferry.Id))).Contains("way home");

        tw.Run(120);
        ferry.Inv.AddUnchecked("deuterium-ice", 5);
        tw.Do(new ReturnFerry(commander.Id, ferry.Id) { PlanetId = rime });
        await Assert.That(commander.Planet).IsEqualTo(start);
        await Assert.That(helper.Planet).IsEqualTo(start);
        await Assert.That(ice.Units.Count).IsEqualTo(0);
        await Assert.That(ice.Buildings.Any(b => b.Def.Kind == BuildingKind.Ferry)).IsFalse();
        await Assert.That(pad.Inv.Count("ferry-rocket")).IsEqualTo(1).Because("the rocket goes back on its pad");
        await Assert.That(pad.Inv.Count("siderite-plate")).IsEqualTo(30);
        await Assert.That(pad.Inv.Count("deuterium-ice")).IsEqualTo(5);
        await Assert.That(tw.W.Stats.Produced("ferry-rocket") - tw.W.Stats.Consumed("ferry-rocket")).IsEqualTo(0L);
    }

    [Test]
    public async Task Ferry_WhosePadIsGone_LandsAtTheLander()
    {
        (TestWorld tw, Building pad, int rime) = LoadedPad();
        tw.Do(new LaunchFerry(tw.Leader.Id, pad.Id, rime, []));
        tw.P.RemoveBuilding(pad);
        Building ferry = tw.W.Planets[rime].Buildings.Single(b => b.Def.Kind == BuildingKind.Ferry);
        Unit commander = tw.W.Planets[rime].Units.Single();
        tw.Do(new ReturnFerry(commander.Id, ferry.Id));
        await Assert.That(commander.Planet).IsEqualTo(tw.P);
        await Assert.That(tw.Lander.Inv.Count("ferry-rocket")).IsEqualTo(1);
    }

    [Test]
    public async Task KerogenGenerator_NeedsNoWater()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("kerogen-generator");
        Int2 at = tw.Lander.Origin + new Int2(-8, 8);
        await Assert.That(tw.P.CanPlace(tw.W.Content.Buildings["kerogen-generator"], at, Dir.North)).IsNull();
        await Assert.That(tw.P.CanPlace(tw.W.Content.Buildings["steam-engine"], at, Dir.North)).IsNotNull();
    }
}
