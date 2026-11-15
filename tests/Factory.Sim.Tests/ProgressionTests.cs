
namespace Factory.Sim.Tests;

public class ProgressionTests
{
    [Test]
    public async Task RobotBay_AssemblesANewHelperBot()
    {
        TestWorld tw = new TestWorld();
        tw.Water(9, 10, 1, 2);
        Building engine = tw.Build("steam-engine", 10, 10);
        engine.Inv.AddUnchecked("compacted-carbon", 200);
        tw.Build("power-pole", 12, 10);
        tw.Unlock("robot-bay", "helper-mk2");
        Building bay = tw.Build("robot-bay", 13, 9);
        tw.Do(new SetRecipe(bay.Id, "helper-mk2"));
        foreach (ItemAmount c in tw.W.Content.Recipes["helper-mk2"].Inputs)
        {
            bay.Inv.AddUnchecked(c.Item, c.Count);
        }

        int before = tw.Followers.Count;
        tw.RunUntil(() => tw.Followers.Count == before + 1, 1800 + 120, "new helper bot");
        Unit bot = tw.Followers.Last();
        await Assert.That(tw.W.HelpersBuilt).IsEqualTo(1);
        await Assert.That(bot.Robot.Id).IsEqualTo("helper-mk2");
        await Assert.That(tw.P.ModuleSlots(bot)).IsEqualTo(3);
        await Assert.That(tw.W.Events.Any(e => e.Message.Contains("assembled"))).IsTrue();
    }

    [Test]
    public async Task RobotBay_OnlyTakesRoboticsRecipes()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("robot-bay", "helper-mk1");
        Building bay = tw.Build("robot-bay", 13, 9);
        await Assert.That(tw.Try(new SetRecipe(bay.Id, "drive-cog"))).IsNotNull();
        await Assert.That(tw.Try(new SetRecipe(bay.Id, "helper-mk1"))).IsNull();
    }

    [Test]
    public async Task SetRecipe_MovesIngredientsTheNewRecipeDoesNotUseToTheOutput()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("assembler", "drive-cog", "cupral-filament");
        Building fab = tw.Build("assembler", 20, 20);
        tw.Do(new SetRecipe(fab.Id, "drive-cog"));
        fab.Inv.AddUnchecked("siderite-plate", 6);
        fab.Inv.AddUnchecked("cupral-bar", 3);
        tw.Do(new SetRecipe(fab.Id, "cupral-filament"));
        await Assert.That(fab.Inv.Count("siderite-plate")).IsEqualTo(0);
        await Assert.That(fab.Out.Count("siderite-plate")).IsEqualTo(6).Because("an arm or a helper bot carries it off");
        await Assert.That(fab.Out.Count("cupral-bar")).IsEqualTo(0).Because("the new recipe uses it");
    }

    [Test]
    public async Task ElectricDrill_MinesTheRingAroundItsFootprint()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("electric-drill");
        Building drill = tw.Build("electric-drill", 20, 20);
        List<Int2> area = tw.P.MiningArea(drill).ToList();
        await Assert.That(area.Count).IsEqualTo(12).Because("a 4 x 4 area less the drill's own 2 x 2");
        await Assert.That(area).Contains(new Int2(19, 19));
        await Assert.That(area).DoesNotContain(new Int2(20, 20));
        tw.Ore("siderite", 22, 20, 1, 1);
        await Assert.That(tw.P.FindMinable(drill)).IsEqualTo(new Int2(22, 20));
    }
}
