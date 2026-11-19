namespace Factory.Sim;

/// <summary>The commander (player-controlled) and helper bots (AI-driven from their Job) are both Units.</summary>
public sealed class Unit : Entity
{
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; internal set; } = "";
    /// <summary>True for the player's own character, the commander; false for helper bots.</summary>
    public bool IsLeader { get; internal set; }
    /// <summary>The robot model, which sets base speed, cargo and module slots.</summary>
    public RobotDef Robot { get; internal set; } = null!;
    /// <summary>Item ids of the modules installed, in slot order.</summary>
    public List<string> Modules { get; } = [];
    /// <summary>Player who owns/commands this unit. Multiplayer-ready: commands are validated against this.</summary>
    public int OwnerPlayer { get; internal set; }
    /// <summary>Position in sub-units (1024 per tile), wrapped onto the planet.</summary>
    public Int2 Pos { get; internal set; }
    /// <summary>Walking speed in sub-units per tick.</summary>
    public int Speed { get; internal set; }
    /// <summary>Items the unit is carrying.</summary>
    public Inventory Carry { get; internal set; } = null!;
    /// <summary>Current assignment, which selects the AI routine run each tick.</summary>
    public Job Job { get; internal set; } = Job.Idle;

    // AI state
    /// <summary>Current step within the job's state machine.</summary>
    public UnitPhase Phase { get; internal set; }
    /// <summary>Target position in sub-units for movement.</summary>
    public Int2 TargetSub { get; internal set; }
    /// <summary>Tile the current job step is aimed at (e.g. the resource tile being gathered).</summary>
    public Int2 TargetTile { get; internal set; }
    /// <summary>Id of the entity the current job step is aimed at (e.g. the claimed construction site), or 0.</summary>
    public int TargetEntity { get; internal set; }
    /// <summary>Secondary target (e.g. the storage a builder is fetching from).</summary>
    public int SourceEntity { get; internal set; }
    /// <summary>Ticks of work accumulated toward the current action, such as mining one item.</summary>
    public int WorkTicks { get; internal set; }
    /// <summary>Ticks left to wait in the Wait phase before trying again.</summary>
    public int WaitTicks { get; internal set; }
    /// <summary>True once a train operator has reached and boarded its train.</summary>
    public bool Boarded { get; internal set; }
    /// <summary>Short human-readable description of what the unit is doing, for the UI.</summary>
    public string Status { get; internal set; } = "Idle";

    // Leader manual control
    /// <summary>Leader: direction the player is steering, each component -1, 0 or 1; zero means standing still.</summary>
    public Int2 MoveIntent { get; internal set; }
    /// <summary>Leader: tile being hand-mined, or null when not mining.</summary>
    public Int2? MiningTile { get; internal set; }
    /// <summary>Leader: resource being hand-mined, so mining can move on to the next tile of the same deposit.</summary>
    public string? MiningResource { get; internal set; }
    /// <summary>Leader: ticks of progress toward the next hand-mined item.</summary>
    public int MiningProgress { get; internal set; }
    /// <summary>Leader: tile it is walking to by itself, or null.</summary>
    public Int2? WalkTarget { get; internal set; }
    /// <summary>Leader: the construction site it built a step of this tick, or 0.</summary>
    public int BuildingSiteId { get; internal set; }

    // Walking
    /// <summary>Waypoints (sub-unit positions) of the path being walked, next one first; empty when walking straight.</summary>
    public List<Int2> Path { get; } = [];
    /// <summary>Explore job: the index of the spiral point the robot is heading for or will look at next.</summary>
    public int ExploreIndex { get; internal set; }
    /// <summary>The tile and planet the robot last uncovered the map from, so it only does so when it moves.</summary>
    internal (int Planet, Int2 Tile)? SightFrom { get; set; }
    /// <summary>Where the current path leads, so a moved target triggers a new one.</summary>
    internal Int2 PathGoal { get; set; }
    /// <summary>How close the current path needs to get, so a changed reach triggers a new one.</summary>
    internal int PathStop { get; set; } = -1;
    /// <summary>Ticks in a row the unit has been blocked while walking; enough of them triggers a new path.</summary>
    internal int StuckTicks { get; set; }
    /// <summary>Path plans in a row made from the same spot without getting anywhere.</summary>
    internal int FailedPlans { get; set; }
    /// <summary>Where the last path was planned from.</summary>
    internal Int2 LastPlanPos { get; set; }

    /// <summary>Tile the unit is standing on.</summary>
    public Int2 Tile => Fixed.ToTile(Pos);
}
