namespace Factory.Sim;

/// <summary>
/// The small header a save carries so the save and load lists can show it without replaying it: what the
/// player called it, when it was written and which planet they were looking at. None of it affects the game.
/// </summary>
public sealed class SaveMeta
{
    /// <summary>The name the player gave the save (the file name is derived from it).</summary>
    public string Name { get; init; } = "";
    /// <summary>Real date and time the save was written, with the player's UTC offset.</summary>
    public DateTimeOffset SavedAt { get; init; }
    /// <summary>Name of the planet being viewed when the game was saved.</summary>
    public string? Planet { get; init; }
    /// <summary>Index of the planet being viewed, so loading can show it again.</summary>
    public int PlanetId { get; init; }
}
