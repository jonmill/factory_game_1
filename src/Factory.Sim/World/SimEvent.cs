namespace Factory.Sim;

/// <summary>A notable occurrence reported to the player (e.g. a follower arriving or research finishing).</summary>
/// <param name="Tick">Tick on which the event happened.</param>
/// <param name="Message">Human-readable text shown to the player.</param>
/// <param name="PlanetId">Planet the event relates to.</param>
/// <param name="Tile">Optional tile location, so the client can point the camera at it.</param>
public sealed record SimEvent(long Tick, string Message, int PlanetId = 0, Int2? Tile = null);
