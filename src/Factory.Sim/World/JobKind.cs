namespace Factory.Sim;

/// <summary>The kind of work a follower has been assigned; selects which AI routine drives the unit each tick.</summary>
public enum JobKind
{
    /// <summary>No assignment; the unit drops off anything it carries and then stands around.</summary>
    Idle,
    /// <summary>Repeatedly hand-mine the nearest tile of a resource and deliver batches to storage.</summary>
    Gather,
    /// <summary>Fetch materials for construction sites, deliver them and do the building or dismantling work.</summary>
    Build,
    /// <summary>Shuttle items from one building to another.</summary>
    Haul,
    /// <summary>Board a train and operate it (hand carts need an operator aboard to move).</summary>
    OperateTrain,
    /// <summary>Directly controlled by the player (the leader); no AI runs.</summary>
    Manual,
}
