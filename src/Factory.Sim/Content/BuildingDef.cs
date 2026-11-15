using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Static definition of a placeable building type, loaded from content JSON.</summary>
public sealed class BuildingDef
{
    /// <summary>Unique content id (e.g. "burner-smelter"); referenced by recipes, techs and saves.</summary>
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
    /// <summary>Launch pads: the item that flies (a ferry rocket), used up on launch and returned when the ferry comes home.</summary>
    public string? Vessel { get; init; }
    /// <summary>Launch pads: items burned for each launch, keyed by item id.</summary>
    public Dictionary<string, int> LaunchFuel { get; init; } = new();
    /// <summary>Launch pads: the building the vessel becomes once it lands (a landed ferry).</summary>
    public string? LandsAs { get; init; }
    /// <summary>Burner buildings: the only fuels it takes, or empty for any fuel.</summary>
    public string[] Fuels { get; init; } = [];
    /// <summary>Logistics stations: how many drones fly its supplies around the planet.</summary>
    public int Drones { get; init; }
    /// <summary>Logistics stations: items one drone carries.</summary>
    public int DroneLoad { get; init; }
    /// <summary>Orbital stations: how many vessels carry its supplies to other planets.</summary>
    public int Vessels { get; init; }
    /// <summary>Orbital stations: items one vessel carries.</summary>
    public int VesselLoad { get; init; }
    /// <summary>Logistics stations: how many items it can be set to supply or demand.</summary>
    public int StationSlots { get; init; } = 5;
    /// <summary>Logistics stations: the most of each item it stocks.</summary>
    public int ItemCap { get; init; }
    /// <summary>The age this building belongs to, if set explicitly; otherwise its unlocking technology's age.</summary>
    public string? Age { get; init; }
    /// <summary>The age this building belongs to (resolved when content loads).</summary>
    [JsonIgnore] public string AgeId { get; internal set; } = "";
    /// <summary><see cref="Cost"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] CostList { get; internal set; } = [];
    /// <summary>True for buildings whose inventory is general storage (lander, chest, rail stop), so input and output share one inventory.</summary>
    [JsonIgnore] public bool IsStorage => Kind is BuildingKind.Lander or BuildingKind.Chest or BuildingKind.RailStop or BuildingKind.Ferry or BuildingKind.LaunchPad or BuildingKind.LogisticsStation or BuildingKind.OrbitalStation;
    /// <summary>True for planetary and orbital logistics stations.</summary>
    [JsonIgnore] public bool IsStation => Kind is BuildingKind.LogisticsStation or BuildingKind.OrbitalStation;
    /// <summary>True for buildings people walk over, such as belts.</summary>
    [JsonIgnore] public bool IsWalkable => Kind == BuildingKind.Belt;
    /// <summary>True for buildings that feed power into a grid.</summary>
    [JsonIgnore] public bool IsGenerator => Kind is BuildingKind.SteamEngine or BuildingKind.SolarPanel or BuildingKind.WindTurbine;
    /// <summary>True for generators that burn nothing.</summary>
    [JsonIgnore] public bool IsRenewable => Kind is BuildingKind.SolarPanel or BuildingKind.WindTurbine;
    /// <summary>True for general stores (lander, crates, a landed ferry): helper bots deliver to them and construction draws from them.</summary>
    [JsonIgnore] public bool IsDepot => Kind is BuildingKind.Lander or BuildingKind.Chest or BuildingKind.Ferry;
    /// <summary><see cref="LaunchFuel"/> as an id-sorted list, for deterministic iteration.</summary>
    [JsonIgnore] public ItemAmount[] LaunchFuelList { get; internal set; } = [];
}
