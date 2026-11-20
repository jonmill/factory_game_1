namespace Factory.Sim.Tests;

public class MetricsTests
{
    [Test]
    public async Task HandGathering_IsLoggedByTheHelperWithTheResourceAndPlanet()
    {
        TestWorld tw = new TestWorld();
        tw.Ore("siderite", 20, 20, 2, 2, 500);
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Gather, Resource: "siderite")));
        tw.Run(60 * 30);
        ProductionStats s = tw.W.Stats;
        string helper = tw.Followers[0].Robot.Id;
        long mined = s.Count("siderite-ore", ItemFlow.HandMined, by: helper, via: "siderite", planet: tw.P.Id);
        await Assert.That(mined > 0).IsTrue();
        await Assert.That(mined).IsEqualTo(s.Get(ItemFlow.HandMined, "siderite-ore"));
        await Assert.That(s.FirstTick("siderite-ore", ItemFlow.HandMined) > 0).IsTrue();
        await Assert.That(s.Series("siderite-ore", ItemFlow.HandMined).Values.Sum()).IsEqualTo(mined);
    }

    [Test]
    public async Task DrillsAndMachines_AreLoggedByBuildingAndRecipe()
    {
        TestWorld tw = new TestWorld();
        tw.Ore("siderite", 10, 10, 2, 2, 500);
        Building drill = tw.Build("burner-drill", 10, 10, Dir.East);
        drill.Inv.AddUnchecked("compacted-carbon", 10);
        Building furnace = tw.Build("burner-smelter", 12, 10);
        furnace.Inv.AddUnchecked("compacted-carbon", 10);
        tw.Run(60 * 60);
        ProductionStats s = tw.W.Stats;
        await Assert.That(s.Count("siderite-ore", ItemFlow.DrillMined, by: "burner-drill") > 0).IsTrue();
        await Assert.That(s.Count("siderite-plate", ItemFlow.Smelted, by: "burner-smelter", via: "siderite-plate") > 0).IsTrue();
        long ore = s.Count("siderite-ore", ItemFlow.SmeltInput, by: "burner-smelter");
        long plates = s.Count("siderite-plate", ItemFlow.Smelted);
        await Assert.That((ore >= plates) && (ore <= plates + 1)).IsTrue().Because("one smelt may still be under way");
        await Assert.That(s.Count("compacted-carbon", ItemFlow.Fuel, by: "burner-drill") > 0).IsTrue();
    }

    [Test]
    public async Task LanderCrafting_IsLoggedApartFromFabricators()
    {
        TestWorld tw = new TestWorld();
        tw.Do(new QueueCraft(tw.Lander.Id, "drive-cog", 3));
        tw.Run(60 * 10);
        ProductionStats s = tw.W.Stats;
        await Assert.That(s.Count("drive-cog", ItemFlow.Crafted, by: "lander")).IsEqualTo(3);
        await Assert.That(s.Count("drive-cog", ItemFlow.Crafted, by: "assembler")).IsEqualTo(0);
        await Assert.That(s.Count("siderite-plate", ItemFlow.CraftInput, by: "lander", via: "drive-cog")).IsEqualTo(6);
    }

    [Test]
    public async Task Buildings_AreCountedUpAndDown_WithThePeak()
    {
        TestWorld tw = new TestWorld();
        Building a = tw.Build("chest", 10, 10);
        tw.Build("chest", 12, 10);
        tw.P.RemoveBuilding(a);
        tw.Build("chest", 14, 10);
        BuildingLine line = tw.W.Stats.BuildingLines.Single(l => (l.Def == "chest") && (l.Planet == tw.P.Id));
        await Assert.That(line.Built).IsEqualTo(3);
        await Assert.That(line.Removed).IsEqualTo(1);
        await Assert.That(line.Peak).IsEqualTo(2);
        await Assert.That(tw.W.Stats.Built("lander")).IsEqualTo(1);
    }

    [Test]
    public async Task Construction_IsLoggedByBuilderAndWhatWasBuilt()
    {
        TestWorld tw = new TestWorld(empty: false, stocked: true);
        tw.Do(new PlaceBuilding("chest", new Int2(tw.Lander.Origin.X + 5, tw.Lander.Origin.Y), Dir.North));
        tw.RunUntil(() => tw.P.Sites.Count == 0, 60 * 60, "the crate to be built");
        long used = tw.W.Stats.Count("biomass", ItemFlow.Construction, via: "chest");
        await Assert.That(used).IsEqualTo(tw.W.Content.Buildings["chest"].Cost["biomass"]);
        await Assert.That(tw.W.Stats.Lines.Where(l => l.Flow == ItemFlow.Construction).All(l => l.By.Length > 0)).IsTrue();
    }

    [Test]
    public async Task Research_IsLoggedByTechWithWhenItFinished()
    {
        TestWorld tw = new TestWorld();
        TechDef t = tw.W.Content.Techs["automation"];
        tw.Do(new SetResearch(t.Id));
        for (int i = 0; i < t.Units; i++)
        {
            tw.W.AddResearchUnit();
        }

        await Assert.That(tw.W.Stats.ResearchLines.Single(r => r.Tech == t.Id).Levels).IsEqualTo(1);
    }
}
