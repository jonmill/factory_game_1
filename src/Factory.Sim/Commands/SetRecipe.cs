using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Selects (or clears) the recipe a fabricator crafts.</summary>
/// <param name="BuildingId">Entity id of the fabricator (assembler).</param>
/// <param name="RecipeId">Recipe to craft, or null to clear.</param>
public sealed record SetRecipe(int BuildingId, string? RecipeId) : Command
{
    /// <summary>Checks the target is a fabricator or robot bay and the recipe exists, is researched and matches its categories.</summary>
    public override string? Validate(World w)
    {
        Building? b = w.Get<Building>(BuildingId);
        bool takesRecipe = (b is not null) && ((b.Def.Kind is BuildingKind.Assembler or BuildingKind.RobotBay) || ((b.Def.Kind == BuildingKind.Terraformer) && (b.Def.Recipe is null)));
        if ((b is null) || (takesRecipe == false))
        {
            return "Only fabricators, robot bays and climate engines take a recipe";
        }

        if (RecipeId is null)
        {
            return null;
        }

        if (w.Content.Recipes.TryGetValue(RecipeId, out RecipeDef? r) == false)
        {
            return "Unknown recipe";
        }

        if (w.Research.Unlocked.Contains(RecipeId) == false)
        {
            return $"{r.Name} has not been researched";
        }

        if (b.Def.Categories.Contains(r.Category) == false)
        {
            return $"{b.Def.Name} can't make {r.Name}";
        }

        return null;
    }
    /// <summary>Refunds the inputs of any craft in progress and switches to the new recipe.</summary>
    public override void Apply(World w)
    {
        Building b = w.Get<Building>(BuildingId)!;
        if (b.Crafting && (b.Recipe is { } old))
        {
            foreach (ItemAmount i in old.Inputs)
            {
                b.Inv.AddUnchecked(i.Item, i.Count);
            }

            w.Stats.Record(ItemFlow.Refunded, old.Inputs);
        }

        b.Crafting = false;
        b.Progress = 0;
        b.RecipeId = RecipeId;
    }
}
