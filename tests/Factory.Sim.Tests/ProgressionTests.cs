
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
}
