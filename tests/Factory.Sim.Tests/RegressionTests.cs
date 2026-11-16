namespace Factory.Sim.Tests;

public class RegressionTests
{
    [Test]
    public async Task LanderQueue_SkipsRecipesMissingIngredientsInsteadOfJamming()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("cupral-filament", 1000);
        tw.Lander.Inv.Remove("cupral-bar", 1000);
        int gears = tw.Lander.Inv.Count("drive-cog");
        tw.Do(new QueueCraft(tw.Lander.Id, "logic-board", 1)); // no wire: can't start
        tw.Do(new QueueCraft(tw.Lander.Id, "drive-cog", 2));
        tw.Run(200);
        await Assert.That(tw.Lander.Inv.Count("drive-cog")).IsEqualTo(gears + 2);
        await Assert.That(tw.Lander.CraftQueue).IsEquivalentTo(["logic-board"]);
    }

    [Test]
    public async Task ReassignedHauler_StoresLeftoversInsteadOfWaitingForever()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building chest = tw.Build("chest", l.X + 8, l.Y);
        Building lab = tw.Build("lab", l.X - 8, l.Y);
        Unit f = tw.Followers[0];
        tw.Lander.Inv.AddUnchecked("survey-core", 20);
        int storedWood = tw.Lander.Inv.Count("biomass");
        f.Carry.Add("biomass", 5); // leftovers from a previous job; labs never accept wood
        tw.Do(new SetResearch("automation"));
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: tw.Lander.Id, ToId: lab.Id)));
        tw.RunUntil(() => (f.Carry.Count("biomass") == 0) && (lab.Inv.Count("survey-core") > 0), 60 * 60, "leftover biomass stored and lab fed");
        await Assert.That(chest.Inv.Count("biomass") + tw.Lander.Inv.Count("biomass")).IsEqualTo(storedWood + 5);
    }

    [Test]
    public async Task HaulerToAStation_StoresLeftoversTheStationDoesNotTake()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building station = tw.Build("logistics-station", l.X + 10, l.Y);
        tw.Do(new SetStationSlot(station.Id, 0, "logic-board", StationMode.Supply));
        Unit f = tw.Followers[0];
        tw.Lander.Inv.AddUnchecked("logic-board", 20);
        f.Carry.Add("biomass", 5); // leftovers: the station has no slot for wood
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: tw.Lander.Id, ToId: station.Id, Item: "logic-board")));
        tw.RunUntil(() => (f.Carry.Count("biomass") == 0) && (station.Inv.Count("logic-board") > 0), 60 * 60, "leftover biomass stored and station stocked");
        await Assert.That(station.Inv.Count("biomass")).IsEqualTo(0);
    }

    [Test]
    public async Task Hauler_NeverBringsTwoOreTypesToOneFurnace()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Lander.Inv.AddUnchecked("siderite-ore", 20);
        tw.Lander.Inv.AddUnchecked("cuprite-ore", 20);
        Building furnace = tw.Build("burner-smelter", l.X + 6, l.Y);
        Unit f = tw.Followers[0];
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: tw.Lander.Id, ToId: furnace.Id)));
        tw.Run(60 * 30);
        await Assert.That((furnace.Inv.Count("siderite-ore") > 0) && (furnace.Inv.Count("cuprite-ore") > 0)).IsFalse();
        await Assert.That(f.Status).DoesNotContain("full");
    }
}
