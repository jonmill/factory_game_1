using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Queue hand-crafting in the lander's fabricator.</summary>
/// <param name="BuildingId">Entity id of the lander.</param>
/// <param name="RecipeId">Recipe to craft.</param>
/// <param name="Count">Number of crafts to queue.</param>
public sealed record QueueCraft(int BuildingId, string RecipeId, int Count) : Command
{
    /// <summary>Checks the target is a lander, the recipe is craftable there and researched, and the queue stays within 100 entries.</summary>
    public override string? Validate(World w)
    {
        Building? b = w.Get<Building>(BuildingId);
        if ((b is null) || (b.Def.Kind != BuildingKind.Lander))
        {
            return "Crafting queues are only on the lander";
        }

        if ((w.Content.Recipes.TryGetValue(RecipeId, out RecipeDef? r) == false) || (b.Def.Categories.Contains(r.Category) == false))
        {
            return "Can't craft that here";
        }

        if (w.Research.Unlocked.Contains(RecipeId) == false)
        {
            return $"{r.Name} has not been researched";
        }

        if ((Count < 1) || (b.CraftQueue.Count + Count > 100))
        {
            return "Queue limit is 100";
        }

        return null;
    }
    /// <summary>Appends the recipe to the craft queue <see cref="Count"/> times.</summary>
    public override void Apply(World w)
    {
        Building b = w.Get<Building>(BuildingId)!;
        for (int i = 0; i < Count; i++)
        {
            b.CraftQueue.Add(RecipeId);
        }
    }
}
