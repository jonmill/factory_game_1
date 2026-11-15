using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Simulation behaviour a building uses; each kind is updated by its own system.</summary>
public enum BuildingKind
{
    /// <summary>The crash-landed ship: the colony's starting storage and hand-crafting hub.</summary>
    Lander,
    /// <summary>Plain item storage.</summary>
    Chest,
    /// <summary>Mines the resource deposit beneath it.</summary>
    Drill,
    /// <summary>Smelts ores into plates.</summary>
    Furnace,
    /// <summary>Automatically crafts a selected recipe.</summary>
    Assembler,
    /// <summary>Consumes items to progress the current research.</summary>
    Lab,
    /// <summary>Burns fuel next to water to generate electricity.</summary>
    SteamEngine,
    /// <summary>Connects the power network and supplies nearby machines.</summary>
    Pole,
    /// <summary>Stores surplus electricity and releases it on demand.</summary>
    Battery,
    /// <summary>A piece of track for trains.</summary>
    Rail,
    /// <summary>A depot beside the track where trains load, unload and refuel.</summary>
    RailStop,
    /// <summary>Refills steam locomotives at nearby rail stops.</summary>
    WaterTower,
    /// <summary>An arm that moves items from the building behind it to the one in front.</summary>
    Sorter,
    /// <summary>The robot assembly bay, which crafts robotics recipes that assemble new helper bots.</summary>
    RobotBay,
    /// <summary>Runs a fixed recipe that raises one of the planet's stats.</summary>
    Terraformer,
    /// <summary>The Settler Uplink, which calls the settlers once every planet is ready.</summary>
    Uplink,
    /// <summary>A launch pad: holds a ferry rocket, its fuel and cargo, and flies them to another planet.</summary>
    LaunchPad,
    /// <summary>A ferry that has landed on another planet: storage for its cargo, and the way home.</summary>
    Ferry,
    /// <summary>A solar panel: power from sunlight, scaled by the planet's sunlight.</summary>
    SolarPanel,
    /// <summary>A wind turbine: power from wind, which grows with the planet's atmosphere.</summary>
    WindTurbine,
    /// <summary>Fires terraforming payloads at another planet.</summary>
    PayloadLauncher,
    /// <summary>A conveyor belt tile: carries items forward; sorters load and unload it. People walk over it.</summary>
    Belt,
    /// <summary>A planetary logistics station: drones carry its supplied items to stations that demand them on the same planet.</summary>
    LogisticsStation,
    /// <summary>An orbital logistics station: drones on its own planet, and vessels to orbital stations on other planets.</summary>
    OrbitalStation,
}
