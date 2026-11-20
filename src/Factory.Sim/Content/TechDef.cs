using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of a research technology, loaded from content JSON.</summary>
public sealed class TechDef
{
    /// <summary>Unique content id of the technology.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Flavour/explanatory text shown in the research screen.</summary>
    public string Description { get; init; } = "";
    /// <summary>Ids of technologies that must be completed before this one can be researched.</summary>
    public string[] Prereqs { get; init; } = [];
    /// <summary>Items a lab consumes for each research unit, keyed by item id.</summary>
    public Dictionary<string, int> UnitCost { get; init; } = new();
    /// <summary>Number of research units needed to complete the technology.</summary>
    public int Units { get; init; } = 10;
    /// <summary>Lab work time per research unit in ticks at 100% speed (60 ticks = 1 second).</summary>
    public int UnitTicks { get; init; } = 600;
    /// <summary>Recipe, building and vehicle ids unlocked on completion.</summary>
    public string[] Unlocks { get; init; } = [];
    /// <summary>Extra module slots the commander gains on completion.</summary>
    public int CommanderSlots { get; init; }
    /// <summary>The age the technology belongs to (see <see cref="AgeDef"/>).</summary>
    public string Age { get; init; } = "salvage";
    /// <summary>Shown in the tree but not yet researchable (placeholder for future content).</summary>
    public bool ComingSoon { get; init; }
    /// <summary>True for infinite research: it can be researched again and again, costing more each level.</summary>
    public bool Repeatable { get; init; }
    /// <summary>
    /// For a one-off upgrade (Vein Utilization I, II, ...): the repeatable tech whose level it adds one to.
    /// As in DSP, the first levels of an upgrade come early and cheap; the endless tail follows later.
    /// </summary>
    public string? LevelOf { get; init; }
    /// <summary><see cref="UnitCost"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] UnitCostList { get; internal set; } = [];
    /// <summary>
    /// One research unit as a recipe: the unit cost in, nothing out, <see cref="UnitTicks"/> long, and a
    /// <c>research:&lt;id&gt;</c> effect. Labs craft it like any machine; it is in <see cref="ContentDb.Recipes"/>
    /// but not <see cref="ContentDb.RecipeList"/>, so no recipe picker offers it.
    /// </summary>
    [JsonIgnore] public RecipeDef UnitRecipe { get; internal set; } = null!;
}
