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
    /// <summary>The colony beacon, which runs a fixed recipe to summon new followers.</summary>
    Beacon
}
