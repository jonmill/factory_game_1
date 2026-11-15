namespace Factory.Sim.Tests;

public class MachineTests
{
    [Test]
    public async Task Furnace_SmeltsOreUsingFuel()
    {
        TestWorld tw = new TestWorld();
        Building f = tw.Build("burner-smelter", 10, 10);
        f.Inv.Add("siderite-ore", 5);
        f.Inv.Add("biomass", 2);
        tw.Run((192 * 5) + 10);
        await Assert.That(f.Out.Count("siderite-plate")).IsEqualTo(5);
        await Assert.That(f.Inv.Count("siderite-ore")).IsEqualTo(0);
    }

    [Test]
    public async Task Furnace_WithoutFuel_ReportsNoFuel()
    {
        TestWorld tw = new TestWorld();
        Building f = tw.Build("burner-smelter", 10, 10);
        f.Inv.Add("siderite-ore", 5);
        tw.Run(100);
        await Assert.That(f.Status).IsEqualTo(MachineStatus.NoFuel);
        await Assert.That(f.Out.Count("siderite-plate")).IsEqualTo(0);
    }

    [Test]
    public async Task BurnerDrill_FeedsFurnaceInFront()
    {
        TestWorld tw = new TestWorld();
        tw.Ore("siderite", 10, 10, 2, 2, 100);
        Building drill = tw.Build("burner-drill", 10, 10, Dir.East); // front is x=12
        Building furnace = tw.Build("burner-smelter", 12, 10);
        drill.Inv.Add("compacted-carbon", 5);
        furnace.Inv.Add("compacted-carbon", 5);
        tw.Run(60 * 30);
        await Assert.That(furnace.Out.Count("siderite-plate") >= 5).IsTrue().Because($"plates: {furnace.Out.Count("siderite-plate")}");
        await Assert.That(tw.P.AmountAt(new Int2(10, 10)) + tw.P.AmountAt(new Int2(11, 10)) + tw.P.AmountAt(new Int2(10, 11)) + tw.P.AmountAt(new Int2(11, 11)) < 400).IsTrue();
    }

    [Test]
    public async Task Lander_CraftsQueuedRecipes()
    {
        TestWorld tw = new TestWorld();
        int gears = tw.Lander.Inv.Count("drive-cog");
        int plates = tw.Lander.Inv.Count("siderite-plate");
        tw.Do(new QueueCraft(tw.Lander.Id, "drive-cog", 4));
        tw.Run((4 * 60) + 5); // 30 ticks at 50% speed each
        await Assert.That(tw.Lander.Inv.Count("drive-cog")).IsEqualTo(gears + 4);
        await Assert.That(tw.Lander.Inv.Count("siderite-plate")).IsEqualTo(plates - 8);
        await Assert.That(tw.Lander.CraftQueue).IsEmpty();
    }
}
