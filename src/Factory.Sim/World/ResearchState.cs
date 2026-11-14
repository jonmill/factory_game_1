namespace Factory.Sim;

/// <summary>Colony-wide research progress: what is unlocked, what is finished and what is being worked on.</summary>
public sealed class ResearchState
{
    /// <summary>Ids of unlocked content (buildings, recipes) available to the player.</summary>
    public SortedSet<string> Unlocked { get; } = new(StringComparer.Ordinal);
    /// <summary>Ids of technologies that have been fully researched.</summary>
    public SortedSet<string> Completed { get; } = new(StringComparer.Ordinal);
    /// <summary>Id of the technology labs are currently researching, or null if none.</summary>
    public string? Current { get; internal set; }
    /// <summary>Units completed per tech (progress is kept if you switch research).</summary>
    public SortedDictionary<string, int> Progress { get; } = new(StringComparer.Ordinal);
    /// <summary>Research units completed for the current technology (0 when nothing is selected).</summary>
    public int UnitsDone => Current is null ? 0 : Progress.GetValueOrDefault(Current);
}
