namespace Factory.Sim;

/// <summary>One craft in the lander's queue.</summary>
/// <param name="RecipeId">The recipe to craft.</param>
/// <param name="Intermediate">
/// True for a part queued to make a later entry possible (drive cogs for a survey core): its output is set
/// aside in <see cref="Building.CraftReserve"/> for that entry instead of going into storage.
/// </param>
public sealed record CraftEntry(string RecipeId, bool Intermediate = false);
