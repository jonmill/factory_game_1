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

    /// <summary>Levels reached in repeatable (infinite) research, by tech id.</summary>
    public SortedDictionary<string, int> Levels { get; } = new(StringComparer.Ordinal);

    /// <summary>The level reached in a repeatable tech, 0 if never researched.</summary>
    /// <param name="techId">Tech id.</param>
    /// <returns>The level.</returns>
    public int Level(string techId) => Levels.GetValueOrDefault(techId);

    /// <summary>Units needed to finish a tech; repeatable techs cost half as much again for every level done.</summary>
    /// <param name="t">The tech.</param>
    /// <returns>Units of research.</returns>
    public int UnitsFor(TechDef t) => t.Repeatable ? t.Units + (t.Units * Level(t.Id) / 2) : t.Units;
}
