namespace Factory.Sim;

/// <summary>Static definition of a robot model (the commander or a helper bot), loaded from content JSON.</summary>
public sealed class RobotDef
{
    /// <summary>Unique content id (e.g. "helper-mk1").</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; init; } = "";
    /// <summary>Module slots built into the chassis.</summary>
    public int ModuleSlots { get; init; } = 1;
    /// <summary>Walking speed in sub-units per tick before modules.</summary>
    public int Speed { get; init; } = 90;
    /// <summary>Hand-mining speed in percent of a resource's hand time: helper bots dig slower than any drill.</summary>
    public int MiningPct { get; init; } = 100;
    /// <summary>Stack slots in the robot's cargo hold (the commander's pockets) before modules.</summary>
    public int CarrySlots { get; init; } = 4;
    /// <summary>How far, in tiles, the robot builds ghosts by itself as it goes; 0 for none.</summary>
    public int BuildReach { get; init; }
    /// <summary>Short description for the robot window.</summary>
    public string Description { get; init; } = "";
}
