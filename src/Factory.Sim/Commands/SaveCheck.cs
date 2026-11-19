namespace Factory.Sim;

/// <summary>Whether a save can be loaded by this build, and why not, or what to be wary of.</summary>
/// <param name="CanLoad">True if this build can load the save.</param>
/// <param name="Problem">Why it can't be loaded, for the player; null when it can.</param>
/// <param name="Warning">Something worth telling the player that doesn't stop the load (the game data changed); null if none.</param>
public sealed record SaveCheck(bool CanLoad, string? Problem, string? Warning);
