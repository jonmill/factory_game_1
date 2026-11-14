namespace Factory.Sim;

/// <summary>A chain of rail vehicles that follows a schedule of stops along tile-based track.</summary>
public sealed class Train : Entity
{
    /// <summary>Vehicles in the consist; index 0 is the front in the current direction of travel.</summary>
    public List<Vehicle> Vehicles { get; } = [];
    /// <summary>Tile of each vehicle; [0] is the front of travel.</summary>
    public List<Int2> Tiles { get; } = [];
    /// <summary>0..1023 progress of every vehicle toward the tile ahead of it.</summary>
    public int Progress { get; internal set; }
    /// <summary>Current speed in sub-units per tick (1024 sub-units per tile).</summary>
    public int Speed { get; internal set; }
    /// <summary>Rail tiles from the front vehicle's tile to the next stop; empty when no route is planned.</summary>
    public List<Int2> Path { get; } = [];
    /// <summary>Index in Path of the tile the front vehicle currently occupies.</summary>
    public int PathIndex { get; internal set; }
    /// <summary>Stops visited in order, looping back to the start.</summary>
    public List<ScheduleStop> Schedule { get; } = [];
    /// <summary>Index in Schedule of the stop the train is heading to or waiting at.</summary>
    public int StopIndex { get; internal set; }
    /// <summary>What the train is doing, or why it cannot move.</summary>
    public TrainState State { get; internal set; }
    /// <summary>Ticks spent at the current stop.</summary>
    public int StopTicks { get; internal set; }
    /// <summary>Consecutive ticks at the current stop with no cargo moved; used to decide when loading is finished.</summary>
    public int IdleTicks { get; internal set; }
    /// <summary>Ticks to wait before searching for a path again after a failed attempt.</summary>
    public int RetryTicks { get; internal set; }
    /// <summary>Remaining burner energy of a steam locomotive, in joules.</summary>
    public long FuelJ { get; internal set; }
    /// <summary>Water left in a steam locomotive; one unit is used per tick of motion, refilled at stops near a water tower.</summary>
    public int Water { get; internal set; }
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; internal set; } = "";

    /// <summary>The first locomotive in the consist, or null if it has none.</summary>
    public Vehicle? Locomotive => Vehicles.FirstOrDefault(v => v.Def.IsLocomotive);
    /// <summary>Tile the front vehicle is moving into (only meaningful while Progress > 0).</summary>
    public Int2? NextTile => PathIndex + 1 < Path.Count ? Path[PathIndex + 1] : null;
}
