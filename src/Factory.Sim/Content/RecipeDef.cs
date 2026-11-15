using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of a crafting or smelting recipe, loaded from content JSON.</summary>
public sealed class RecipeDef
{
    /// <summary>Unique content id; also used as an unlock id by techs.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Recipe category; only buildings listing it in their categories can craft it (e.g. "smelting", "crafting").</summary>
    public string Category { get; init; } = "crafting";
    /// <summary>Ingredients consumed per craft, keyed by item id.</summary>
    public Dictionary<string, int> In { get; init; } = new();
    /// <summary>Products produced per craft, keyed by item id.</summary>
    public Dictionary<string, int> Out { get; init; } = new();
    /// <summary>Craft time in ticks at 100% speed (60 ticks = 1 second).</summary>
    public int Ticks { get; init; } = 60;
    /// <summary>Special effect on completion (e.g. "helper:helper-mk1" assembles a helper bot of that model) instead of/as well as item output.</summary>
    public string? Effect { get; init; }
    /// <summary><see cref="In"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] Inputs { get; internal set; } = [];
    /// <summary><see cref="Out"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] Outputs { get; internal set; } = [];
}
