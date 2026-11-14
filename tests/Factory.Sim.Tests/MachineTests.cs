namespace Factory.Sim.Tests;

public class MachineTests
{
    [Test]
    public async Task Furnace_SmeltsOreUsingFuel()
    {
        TestWorld tw = new TestWorld();
        Building f = tw.Build("stone-furnace", 10, 10);
        f.Inv.Add("iron-ore", 5);
        f.Inv.Add("wood", 2);
        tw.Run((192 * 5) + 10);
        await Assert.That(f.Out.Count("iron-plate")).IsEqualTo(5);
        await Assert.That(f.Inv.Count("iron-ore")).IsEqualTo(0);
    }

    [Test]
    public async Task Furnace_WithoutFuel_ReportsNoFuel()
    {
        TestWorld tw = new TestWorld();
        Building f = tw.Build("stone-furnace", 10, 10);
        f.Inv.Add("iron-ore", 5);
        tw.Run(100);
        await Assert.That(f.Status).IsEqualTo(MachineStatus.NoFuel);
        await Assert.That(f.Out.Count("iron-plate")).IsEqualTo(0);
    }

    [Test]
    public async Task BurnerDrill_FeedsFurnaceInFront()
    {
        TestWorld tw = new TestWorld();
        tw.Ore("iron", 10, 10, 2, 2, 100);
        Building drill = tw.Build("burner-drill", 10, 10, Dir.East); // front is x=12
        Building furnace = tw.Build("stone-furnace", 12, 10);
        drill.Inv.Add("coal", 5);
        furnace.Inv.Add("coal", 5);
        tw.Run(60 * 30);
        await Assert.That(furnace.Out.Count("iron-plate") >= 5).IsTrue().Because($"plates: {furnace.Out.Count("iron-plate")}");
        await Assert.That(tw.P.AmountAt(new Int2(10, 10)) + tw.P.AmountAt(new Int2(11, 10)) + tw.P.AmountAt(new Int2(10, 11)) + tw.P.AmountAt(new Int2(11, 11)) < 400).IsTrue();
    }

    [Test]
    public async Task Lander_CraftsQueuedRecipes()
    {
        TestWorld tw = new TestWorld();
        int gears = tw.Lander.Inv.Count("iron-gear");
        int plates = tw.Lander.Inv.Count("iron-plate");
        tw.Do(new QueueCraft(tw.Lander.Id, "iron-gear", 4));
        tw.Run((4 * 60) + 5); // 30 ticks at 50% speed each
        await Assert.That(tw.Lander.Inv.Count("iron-gear")).IsEqualTo(gears + 4);
        await Assert.That(tw.Lander.Inv.Count("iron-plate")).IsEqualTo(plates - 8);
        await Assert.That(tw.Lander.CraftQueue).IsEmpty();
    }
}
