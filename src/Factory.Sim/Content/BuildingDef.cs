using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of a placeable building type, loaded from content JSON.</summary>
public sealed class BuildingDef
{
    /// <summary>Unique content id (e.g. "stone-furnace"); referenced by recipes, techs and saves.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Which simulation behaviour the building uses.</summary>
    public BuildingKind Kind { get; init; }
    /// <summary>Art direction the client draws this in; matches the technology era.</summary>
    public VisualStyle Look { get; init; } = VisualStyle.Industrial;
    /// <summary>Footprint width in tiles (before rotation).</summary>
    public int W { get; init; } = 1;
    /// <summary>Footprint height in tiles (before rotation).</summary>
    public int H { get; init; } = 1;
    /// <summary>Items consumed to construct the building, keyed by item id.</summary>
    public Dictionary<string, int> Cost { get; init; } = new();
    /// <summary>Work time to construct, in ticks (60 ticks = 1 second); deconstruction takes half.</summary>
    public int BuildTicks { get; init; } = 60;
    /// <summary>How the building is powered while working.</summary>
    public EnergyKind Energy { get; init; }
    /// <summary>Consumption in kW while working.</summary>
    public int PowerKw { get; init; }
    /// <summary>Generation in kW (steam engine).</summary>
    public int OutputKw { get; init; }
    /// <summary>Fuel→energy efficiency in percent (generators).</summary>
    public int Efficiency { get; init; } = 100;
    /// <summary>Number of input/storage inventory slots (also fuel slots for burners).</summary>
    public int Slots { get; init; }
    /// <summary>Number of output inventory slots; storage buildings share their main inventory instead.</summary>
    public int OutSlots { get; init; } = 1;
    /// <summary>Crafting/mining speed in percent (100 = 1x).</summary>
    public int Speed { get; init; } = 100;
    /// <summary>Recipe categories this building can craft (e.g. "smelting", "crafting").</summary>
    public string[] Categories { get; init; } = [];
    /// <summary>Ticks to mine one item at 100% speed (drills).</summary>
    public int MiningTicks { get; init; }
    /// <summary>Maximum distance in tiles a pole can wire to another pole.</summary>
    public int WireReach { get; init; }
    /// <summary>Radius in tiles within which a pole powers machines.</summary>
    public int SupplyRadius { get; init; }
    /// <summary>Energy storage capacity in kJ (batteries).</summary>
    public int CapacityKj { get; init; }
    /// <summary>Maximum charge/discharge rate in kW (batteries).</summary>
    public int RateKw { get; init; }
    /// <summary>Whether the building must be placed touching water (steam engines, water towers).</summary>
    public bool NeedsWater { get; init; }
    /// <summary>Fixed recipe id the building always runs, or null if the player chooses one.</summary>
    public string? Recipe { get; init; }
    /// <summary>Ticks per transfer (sorter).</summary>
    public int Period { get; init; } = 60;
    /// <summary>Whether the player can place this building; false for pre-placed buildings such as the lander.</summary>
    public bool Buildable { get; init; } = true;
    /// <summary>Placeholder render colour as a hex string.</summary>
    public string Color { get; init; } = "#88aaff";
    /// <summary>Tooltip text describing the building to the player.</summary>
    public string Description { get; init; } = "";
    /// <summary><see cref="Cost"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] CostList { get; internal set; } = [];
    /// <summary>True for buildings whose inventory is general storage (lander, chest, rail stop), so input and output share one inventory.</summary>
    [JsonIgnore] public bool IsStorage => Kind is BuildingKind.Lander or BuildingKind.Chest or BuildingKind.RailStop;
}
