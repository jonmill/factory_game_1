namespace Factory.Sim.Tests;

public class RegressionTests
{
    [Test]
    public async Task LanderQueue_SkipsRecipesMissingIngredientsInsteadOfJamming()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("copper-wire", 1000);
        tw.Lander.Inv.Remove("copper-plate", 1000);
        int gears = tw.Lander.Inv.Count("iron-gear");
        tw.Do(new QueueCraft(tw.Lander.Id, "basic-circuit", 1)); // no wire: can't start
        tw.Do(new QueueCraft(tw.Lander.Id, "iron-gear", 2));
        tw.Run(200);
        await Assert.That(tw.Lander.Inv.Count("iron-gear")).IsEqualTo(gears + 2);
        await Assert.That(tw.Lander.CraftQueue).IsEquivalentTo(["basic-circuit"]);
    }

    [Test]
    public async Task ReassignedHauler_StoresLeftoversInsteadOfWaitingForever()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building chest = tw.Build("chest", l.X + 8, l.Y);
        Building lab = tw.Build("lab", l.X - 8, l.Y);
        Unit f = tw.Followers[0];
        int storedWood = tw.Lander.Inv.Count("wood");
        f.Carry.Add("wood", 5); // leftovers from a previous job; labs never accept wood
        tw.Do(new SetResearch("automation"));
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: tw.Lander.Id, ToId: lab.Id)));
        tw.RunUntil(() => (f.Carry.Count("wood") == 0) && (lab.Inv.Count("iron-gear") > 0), 60 * 60, "leftover wood stored and lab fed");
        await Assert.That(chest.Inv.Count("wood") + tw.Lander.Inv.Count("wood")).IsEqualTo(storedWood + 5);
    }

    [Test]
    public async Task Hauler_NeverBringsTwoOreTypesToOneFurnace()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Lander.Inv.AddUnchecked("iron-ore", 20);
        tw.Lander.Inv.AddUnchecked("copper-ore", 20);
        Building furnace = tw.Build("stone-furnace", l.X + 6, l.Y);
        Unit f = tw.Followers[0];
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: tw.Lander.Id, ToId: furnace.Id)));
        tw.Run(60 * 30);
        await Assert.That((furnace.Inv.Count("iron-ore") > 0) && (furnace.Inv.Count("copper-ore") > 0)).IsFalse();
        await Assert.That(f.Status).DoesNotContain("full");
    }
}
