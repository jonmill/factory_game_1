#if STEAM
using Steamworks;

namespace Factory.Game.Platform;

/// <summary>
/// Steamworks.NET backend. During development put steam_appid.txt (containing 480, Valve's test app,
/// or your real App ID) next to the executable/project and the native steam_api library from the
/// Steamworks SDK where the runtime can find it.
/// </summary>
public sealed class SteamPlatform : IPlatformServices
{
    private SteamPlatform()
    {
    }

    /// <inheritdoc />
    public string Name => "Steam";

    /// <inheritdoc />
    public bool IsOnline => true;

    /// <inheritdoc />
    public string PlayerName => SteamFriends.GetPersonaName();

    /// <summary>Starts Steam if it is running and the native library is present; returns null to play offline.</summary>
    /// <returns>The Steam backend, or null when Steam is unavailable.</returns>
    public static SteamPlatform? TryCreate()
    {
        try
        {
            if ((Packsize.Test() == false) || (DllCheck.Test() == false))
            {
                return null;
            }

            if (SteamAPI.Init() == false)
            {
                Godot.GD.Print("Steam not running or no app id; playing offline.");

                return null;
            }

            return new SteamPlatform();
        }
        catch (System.DllNotFoundException)
        {
            Godot.GD.Print("Steam native library not found; playing offline.");
        }
        catch (System.Exception e)
        {
            Godot.GD.Print($"Steam init failed ({e.GetType().Name}); playing offline.");
        }

        return null;
    }

    /// <inheritdoc />
    public void Update() => SteamAPI.RunCallbacks();

    /// <inheritdoc />
    public void UnlockAchievement(string id)
    {
        if (SteamUserStats.SetAchievement(id))
        {
            SteamUserStats.StoreStats();
        }
    }

    /// <inheritdoc />
    public void Dispose() => SteamAPI.Shutdown();
}
#endif
