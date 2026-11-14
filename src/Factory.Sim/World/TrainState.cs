namespace Factory.Sim;

/// <summary>What a train is currently doing, or why it is not moving.</summary>
public enum TrainState
{
    /// <summary>The train has no schedule (or no locomotive), so it stays put.</summary>
    NoSchedule,
    /// <summary>Leaving a stop or rerouting; a path to the next stop will be searched for.</summary>
    Pathing,
    /// <summary>Following a path along the rails toward the next stop.</summary>
    Moving,
    /// <summary>Stopped at a scheduled stop, loading, unloading or refuelling.</summary>
    AtStop,
    /// <summary>No rail route to the next stop exists (or the stop was removed); the train retries periodically.</summary>
    NoPath,
    /// <summary>The train needs an operator aboard (hand cart) or has no locomotive.</summary>
    NoOperator,
    /// <summary>A steam locomotive has no fuel burning and none in its cargo.</summary>
    NoFuel,
    /// <summary>A steam locomotive has run out of water.</summary>
    NoWater,
    /// <summary>The pulled mass exceeds what the locomotive can haul.</summary>
    Overloaded,
    /// <summary>The next tile is occupied by another train.</summary>
    Blocked,
}
