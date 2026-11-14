namespace Factory.Sim;

/// <summary>Step within a follower's current job, used by the job AI as a small state machine.</summary>
public enum UnitPhase
{
    /// <summary>Just assigned; the job routine will pick its first step.</summary>
    None,
    /// <summary>Looking for the next target (resource tile, construction site, pickup).</summary>
    Seek,
    /// <summary>Walking toward the chosen target (for builders, to the storage holding needed materials).</summary>
    MoveTo,
    /// <summary>Performing timed work at the target, such as mining or building.</summary>
    Work,
    /// <summary>Carrying items to their destination (storage, a site or a haul target).</summary>
    Deliver,
    /// <summary>Pausing for <see cref="Unit.WaitTicks"/> before trying again.</summary>
    Wait,
    /// <summary>Getting aboard a train.</summary>
    Board,
}
