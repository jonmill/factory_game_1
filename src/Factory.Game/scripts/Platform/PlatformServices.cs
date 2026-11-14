namespace Factory.Game.Platform;

/// <summary>Chooses and creates the platform backend for this run.</summary>
public static class PlatformServices
{
    /// <summary>Tries Steam first; falls back to offline if the client/native library isn't available.</summary>
    public static IPlatformServices Create()
    {
        if (System.Environment.GetEnvironmentVariable("FACTORY_NO_STEAM") == "1")
        {
            return new OfflinePlatform();
        }
#if STEAM

        return SteamPlatform.TryCreate() ?? (IPlatformServices)new OfflinePlatform();
#else

        return new OfflinePlatform();
#endif
    }
}
