namespace Factory.Sim;

/// <summary>
/// The lander's craft queue, which works like a hand-crafting queue: queueing a recipe sets its
/// ingredients aside from storage at once, and any it is missing that the lander can make from what it has
/// are queued first as parts. Parts go into the reserve, not storage, and are used by the craft they were
/// made for; clearing the queue returns the reserve to storage.
/// </summary>
public sealed partial class Planet
{
    /// <summary>How deep a chain of parts the planner follows (it also stops recipe loops).</summary>
    private const int MaxPartDepth = 6;

    /// <summary>
    /// Queues <paramref name="count"/> crafts of a recipe on the lander: takes the ingredients from storage
    /// into the reserve, and for each one storage doesn't have enough of, plans crafts of it (recursively)
    /// ahead of the recipe. Anything neither in storage nor craftable here (ore the lander can't smelt) is
    /// waited for: the craft starts once it turns up, from a helper bot or the commander's pockets.
    /// </summary>
    /// <param name="lander">The lander.</param>
    /// <param name="recipe">The recipe to queue.</param>
    /// <param name="count">How many crafts.</param>
    public void QueueLanderCraft(Building lander, RecipeDef recipe, int count)
    {
        List<CraftEntry> parts = [];
        ReserveFor(lander, recipe, count, parts, 0);
        lander.CraftQueue.AddRange(parts);
        for (int i = 0; i < count; i++)
        {
            lander.CraftQueue.Add(new CraftEntry(recipe.Id));
        }
    }

    private void ReserveFor(Building lander, RecipeDef recipe, int crafts, List<CraftEntry> parts, int depth)
    {
        Inventory reserve = lander.CraftReserve!;
        foreach (ItemAmount input in recipe.Inputs)
        {
            int need = input.Count * crafts;
            int took = lander.Inv.Remove(input.Item, need);
            reserve.AddUnchecked(input.Item, took);
            int missing = need - took;
            if ((missing <= 0) || (depth >= MaxPartDepth) || (LanderRecipeFor(lander, input.Item) is not { } part))
            {
                continue;
            }

            int perCraft = part.Outputs.First(o => o.Item == input.Item).Count;
            int partCrafts = (missing + perCraft - 1) / perCraft;
            ReserveFor(lander, part, partCrafts, parts, depth + 1);
            for (int i = 0; i < partCrafts; i++)
            {
                parts.Add(new CraftEntry(part.Id, Intermediate: true));
            }
        }
    }

    /// <summary>A researched recipe the lander can craft that makes the item, or null.</summary>
    private RecipeDef? LanderRecipeFor(Building lander, string item) =>
        World.Content.RecipeList.FirstOrDefault(r =>
            (r.Effect is null) && lander.Def.Categories.Contains(r.Category) && World.Research.Unlocked.Contains(r.Id) &&
            r.Outputs.Any(o => o.Item == item));

    /// <summary>How many of an item the lander's crafts can draw on: the reserve first, then storage.</summary>
    private static int LanderCanUse(Building lander, string item) => lander.CraftReserve!.Count(item) + lander.Inv.Count(item);

    /// <summary>True if the lander has everything a craft needs, between its reserve and storage.</summary>
    /// <param name="lander">The lander.</param>
    /// <param name="inputs">The craft's ingredients.</param>
    /// <returns>Whether it can start.</returns>
    public static bool LanderHas(Building lander, IEnumerable<ItemAmount> inputs) => inputs.All(i => LanderCanUse(lander, i.Item) >= i.Count);

    /// <summary>Takes a craft's ingredients for the lander: from the reserve first, the rest from storage.</summary>
    private static void LanderTake(Building lander, IEnumerable<ItemAmount> inputs)
    {
        foreach (ItemAmount i in inputs)
        {
            int fromReserve = lander.CraftReserve!.Remove(i.Item, i.Count);
            lander.Inv.Remove(i.Item, i.Count - fromReserve);
        }
    }

    /// <summary>Puts everything in the lander's reserve back into its storage.</summary>
    /// <param name="lander">The lander.</param>
    public static void ReleaseCraftReserve(Building lander)
    {
        if (lander.CraftReserve is not { IsEmpty: false } reserve)
        {
            return;
        }

        foreach ((string item, int n) in reserve.Items.ToList())
        {
            lander.Inv.AddUnchecked(item, n);
        }

        reserve.Clear();
    }
}
