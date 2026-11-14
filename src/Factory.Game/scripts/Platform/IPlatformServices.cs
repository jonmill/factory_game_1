namespace Factory.Game.Platform;

/// <summary>
/// Everything platform/store specific goes through here so the game runs without Steam (dev, CI,
/// other stores later). Multiplayer transport (Steam lobbies/P2P) will also hang off this interface.
/// </summary>
public interface IPlatformServices : System.IDisposable
{
    /// <summary>Display name of the platform backend, such as "Steam" or "Offline".</summary>
    string Name { get; }
    /// <summary>Whether the platform is connected to its online service.</summary>
    bool IsOnline { get; }
    /// <summary>The local player's display name.</summary>
    string PlayerName { get; }
    /// <summary>Pumps platform callbacks; called once per frame.</summary>
    void Update();
    /// <summary>Unlocks the achievement with the given id.</summary>
    /// <param name="id">Platform achievement id.</param>
    void UnlockAchievement(string id);
}
