namespace Factory.Sim.Tests;

public class PowerTests
{
    private static (TestWorld tw, Building engine, Building lab) Grid(bool fuel)
    {
        TestWorld tw = new TestWorld();
        tw.Water(9, 10, 1, 2);
        Building engine = tw.Build("steam-engine", 10, 10);
        if (fuel)
        {
            engine.Inv.Add("compacted-carbon", 10);
        }

        tw.Build("power-pole", 12, 10);
        Building lab = tw.Build("lab", 13, 11);

        return (tw, engine, lab);
    }

    [Test]
    public async Task SteamEngine_PowersLabWhichResearches()
    {
        (TestWorld? tw, Building _, Building? lab) = Grid(fuel: true);
        tw.Do(new SetResearch("automation"));
        lab.Inv.Add("survey-core", 20);
        tw.Run((600 * 3) + 10);
        await Assert.That(tw.W.Research.UnitsDone).IsEqualTo(3);
        await Assert.That(lab.Satisfaction).IsEqualTo(1000);
    }

    [Test]
    public async Task Lab_WithoutGeneratorFuel_HasNoPower()
    {
        (TestWorld? tw, Building? engine, Building? lab) = Grid(fuel: false);
        tw.Do(new SetResearch("automation"));
        lab.Inv.Add("survey-core", 20);
        tw.Run(100);
        await Assert.That(lab.Status).IsEqualTo(MachineStatus.NoPower);
        await Assert.That(engine.Status).IsEqualTo(MachineStatus.NoFuel);
        await Assert.That(tw.W.Research.UnitsDone).IsEqualTo(0);
    }

    [Test]
    public async Task Battery_StoresSurplusAndCoversOutage()
    {
        (TestWorld? tw, Building? engine, Building? lab) = Grid(fuel: true);
        Building bat = tw.Build("cell-bank", 12, 12);
        tw.Run(600);
        await Assert.That(bat.StoredJ > 0).IsTrue().Because("battery should charge from idle engine");
        // Remove fuel: battery should keep the lab running for a while.
        engine.Inv.Clear();
        engine.FuelJ = 0;
        tw.Do(new SetResearch("automation"));
        lab.Inv.Add("survey-core", 20);
        tw.Run(30);
        await Assert.That(lab.Status).IsEqualTo(MachineStatus.Working);
    }

    [Test]
    public async Task ResearchCompletion_UnlocksFabricator()
    {
        (TestWorld? tw, Building? engine, Building? lab) = Grid(fuel: true);
        engine.Inv.Add("compacted-carbon", 40);
        tw.Do(new SetResearch("automation"));
        int units = tw.W.Research.UnitsFor(tw.W.Content.Techs["automation"]);
        lab.Inv.Add("survey-core", units * tw.W.Content.Techs["automation"].UnitCost["survey-core"]);
        tw.RunUntil(() => tw.W.Research.Completed.Contains("automation"), 600 * (units + 1), "automation research");
        await Assert.That(tw.W.Research.Unlocked).Contains("assembler");
        await Assert.That(tw.W.Research.Current).IsNull();
    }

    [Test]
    public async Task Sorter_MovesPlatesOnlyWhenTheTargetAccepts()
    {
        (TestWorld? tw, Building _, Building _) = Grid(fuel: true);
        tw.Unlock("sorter");
        Building furnace = tw.Build("burner-smelter", 10, 13);   // occupies 10..11, 13..14
        furnace.Out.Add("cupral-bar", 3);
        tw.Build("power-pole", 12, 15);                    // wires to pole (12,10) and covers the sorter
        Building sorter = tw.Build("sorter", 12, 13, Dir.East); // back = (11,13) furnace, front = (13,13) chest
        Building chest = tw.Build("chest", 13, 13);
        tw.Run(60 * 6);
        await Assert.That(sorter.Satisfaction).IsEqualTo(1000);
        await Assert.That(chest.Inv.Count("cupral-bar")).IsEqualTo(3);
        await Assert.That(furnace.Out.Count("cupral-bar")).IsEqualTo(0);
    }
}
