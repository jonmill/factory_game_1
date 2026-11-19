namespace Factory.Sim.Tests;

/// <summary>Labs craft research units as recipes; a unit counts toward the tech it was started for.</summary>
public class ResearchUnitTests
{
    private static Building PoweredLab(TestWorld tw)
    {
        tw.Build("solar-panel", 10, 10);
        tw.Build("power-pole", 12, 10);

        return tw.Build("lab", 13, 11);
    }

    [Test]
    public async Task EveryTech_HasAUnitRecipe_ThatNoRecipePickerOffers()
    {
        foreach (TechDef t in TestWorld.Content.TechList)
        {
            await Assert.That(TestWorld.Content.Recipes[ContentDb.ResearchEffect(t.Id)]).IsSameReferenceAs(t.UnitRecipe);
            await Assert.That(t.UnitRecipe.Ticks).IsEqualTo(t.UnitTicks);
            await Assert.That(t.UnitRecipe.Inputs).IsEquivalentTo(t.UnitCostList);
        }

        await Assert.That(TestWorld.Content.RecipeList.Any(r => r.Category == ContentDb.ResearchCategory)).IsFalse();
    }

    [Test]
    public async Task Lab_CraftsTheCurrentTechsUnit()
    {
        TestWorld tw = new TestWorld();
        Building lab = PoweredLab(tw);
        tw.Do(new SetResearch("automation"));
        lab.Inv.AddUnchecked("survey-core", 2);
        tw.Run(10);
        await Assert.That(lab.RecipeId).IsEqualTo(ContentDb.ResearchEffect("automation"));
        await Assert.That(lab.Status).IsEqualTo(MachineStatus.Working);

        tw.RunUntil(() => tw.W.Research.Progress.GetValueOrDefault("automation") == 1, 700, "one unit researched");
        await Assert.That(tw.W.Stats.Get(ItemFlow.Research, "survey-core")).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task SwitchingResearch_MidUnit_CreditsTheTechTheUnitStartedOn()
    {
        TestWorld tw = new TestWorld();
        Building lab = PoweredLab(tw);
        tw.Do(new SetResearch("automation"));
        lab.Inv.AddUnchecked("survey-core", 5);
        tw.Run(300); // about half a unit

        tw.Do(new SetResearch("robot-modules"));
        await Assert.That(lab.RecipeId).IsEqualTo(ContentDb.ResearchEffect("automation"));
        tw.RunUntil(() => tw.W.Research.Progress.GetValueOrDefault("automation") == 1, 600, "the unit under way finished");

        await Assert.That(tw.W.Research.Progress.GetValueOrDefault("robot-modules")).IsEqualTo(0);
        tw.Run(5);
        // The next unit is for the new research.
        await Assert.That(lab.RecipeId).IsEqualTo(ContentDb.ResearchEffect("robot-modules"));
    }

    [Test]
    public async Task UnitForATechFinishedMeanwhile_GoesToTheCurrentResearch()
    {
        TestWorld tw = new TestWorld();
        tw.Do(new SetResearch("robot-modules"));
        tw.W.Research.Completed.Add("automation");

        tw.W.AddResearchUnit("automation");

        await Assert.That(tw.W.Research.Progress.GetValueOrDefault("robot-modules")).IsEqualTo(1);
        await Assert.That(tw.W.Research.Progress.GetValueOrDefault("automation")).IsEqualTo(0);
    }

    [Test]
    public async Task FinishingANonCurrentTech_KeepsTheCurrentResearch()
    {
        TestWorld tw = new TestWorld();
        TechDef automation = TestWorld.Content.Techs["automation"];
        tw.Do(new SetResearch("automation"));
        for (int i = 0; i < automation.Units - 1; i++)
        {
            tw.W.AddResearchUnit();
        }

        tw.Do(new SetResearch("robot-modules"));
        tw.W.AddResearchUnit("automation"); // the last unit, started before the switch

        await Assert.That(tw.W.Research.Completed).Contains("automation");
        await Assert.That(tw.W.Research.Current).IsEqualTo("robot-modules");
    }
}
