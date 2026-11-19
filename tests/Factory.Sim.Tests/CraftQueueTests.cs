namespace Factory.Sim.Tests;

/// <summary>The lander's queue crafts missing parts first, sets ingredients aside, and refunds on cancel.</summary>
public class CraftQueueTests
{
    private static long Total(TestWorld tw, string item) => tw.Lander.Inv.Count(item) + tw.Lander.CraftReserve!.Count(item);

    [Test]
    public async Task Queue_CraftsMissingPartsFirst_FromStorage()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("drive-cog", 1000);
        int plates = tw.Lander.Inv.Count("siderite-plate");
        int bars = tw.Lander.Inv.Count("cupral-bar");

        tw.Do(new QueueCraft(tw.Lander.Id, "survey-core", 2));

        // Two cogs (parts) ahead of the two cores, and the ingredients are set aside at once.
        // (The first cog starts on the tick the command is applied, so it's the craft under way.)
        await Assert.That(tw.Lander.Crafting && tw.Lander.CraftingIntermediate && (tw.Lander.RecipeId == "drive-cog")).IsTrue();
        await Assert.That(tw.Lander.CraftQueue.Select(e => (e.RecipeId, e.Intermediate)).Prepend(("drive-cog", true)))
            .IsEquivalentTo([("drive-cog", true), ("drive-cog", true), ("survey-core", false), ("survey-core", false)], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(tw.Lander.Inv.Count("siderite-plate")).IsEqualTo(plates - 4);
        await Assert.That(tw.Lander.Inv.Count("cupral-bar")).IsEqualTo(bars - 2);

        tw.RunUntil(() => (tw.Lander.CraftQueue.Count == 0) && (tw.Lander.Crafting == false), 60 * 60, "cores crafted");
        await Assert.That(tw.Lander.Inv.Count("survey-core")).IsEqualTo(2);
        // The cogs went into the cores, not into storage.
        await Assert.That(tw.Lander.Inv.Count("drive-cog")).IsEqualTo(0);
        await Assert.That(tw.Lander.CraftReserve!.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Queue_SparePartsGoBackToStorage()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("cupral-filament", 1000);
        tw.Do(new QueueCraft(tw.Lander.Id, "logic-board", 1)); // 3 filament: two crafts of 2

        tw.RunUntil(() => (tw.Lander.CraftQueue.Count == 0) && (tw.Lander.Crafting == false), 60 * 60, "board crafted");
        await Assert.That(tw.Lander.Inv.Count("logic-board")).IsGreaterThanOrEqualTo(1);
        await Assert.That(tw.Lander.Inv.Count("cupral-filament")).IsEqualTo(1);
    }

    [Test]
    public async Task Cancel_RefundsIngredientsAndKeepsPartsMade()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("drive-cog", 1000);
        int plates = tw.Lander.Inv.Count("siderite-plate");
        int bars = tw.Lander.Inv.Count("cupral-bar");
        tw.Do(new QueueCraft(tw.Lander.Id, "survey-core", 3));
        // Let the first cog finish and the second start.
        tw.RunUntil(() => Total(tw, "drive-cog") >= 1, 60 * 10, "first cog made");

        tw.Do(new CancelQueuedCrafts(tw.Lander.Id));
        tw.RunUntil(() => tw.Lander.Crafting == false, 60 * 10, "craft under way finished");

        await Assert.That(tw.Lander.CraftQueue).IsEmpty();
        await Assert.That(tw.Lander.CraftReserve!.IsEmpty).IsTrue();
        // Everything is back in storage: bars untouched, and each plate either refunded or in a cog.
        await Assert.That(tw.Lander.Inv.Count("cupral-bar")).IsEqualTo(bars);
        int cogs = tw.Lander.Inv.Count("drive-cog");
        await Assert.That(cogs).IsGreaterThanOrEqualTo(1);
        await Assert.That(tw.Lander.Inv.Count("siderite-plate")).IsEqualTo(plates - (2 * cogs));
    }

    [Test]
    public async Task Queue_WaitsForIngredientsItCannotMake()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("cupral-bar", 1000); // smelted, so the lander can't make it
        tw.Do(new QueueCraft(tw.Lander.Id, "survey-core", 1));
        tw.Run(600);
        await Assert.That(tw.Lander.Inv.Count("survey-core")).IsEqualTo(0);

        tw.Lander.Inv.Add("cupral-bar", 1);
        tw.RunUntil(() => tw.Lander.Inv.Count("survey-core") == 1, 60 * 20, "core crafted once the bar arrives");
    }
}
