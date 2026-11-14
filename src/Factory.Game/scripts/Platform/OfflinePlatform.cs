namespace Factory.Game.Platform;

/// <summary>Platform backend used when no store client is available; achievements are only logged.</summary>
public sealed class OfflinePlatform : IPlatformServices
{
    /// <inheritdoc />
    public string Name => "Offline";
    /// <inheritdoc />
    public bool IsOnline => false;
    /// <inheritdoc />
    public string PlayerName => System.Environment.UserName;
    /// <inheritdoc />
    public void Update()
    {
    }
    /// <inheritdoc />
    public void UnlockAchievement(string id) => Godot.GD.Print($"[achievement] {id}");
    /// <inheritdoc />
    public void Dispose()
    {
    }
}
