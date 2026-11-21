using Godot;

namespace Factory.Game;

/// <summary>
/// Player preferences that outlive a game: interface size, sound volumes, whether the guide's hints show, and which
/// one-off tips have been seen. Kept in <c>user://settings.cfg</c>; the future options menu edits these.
/// </summary>
public static class GameSettings
{
    private const string Path = "user://settings.cfg";

    /// <summary>The smallest and largest interface size the player can pick, as a multiple of the automatic size.</summary>
    public const float MinUiScale = 0.7f, MaxUiScale = 1.6f;

    private static readonly HashSet<string> _seenTips = [];
    private static bool _loaded;
    private static float _uiScale = 1f;
    private static bool _showHints = true;
    private static float _masterVolume = 0.8f, _sfxVolume = 0.8f, _ambientVolume = 0.6f;

    /// <summary>
    /// Interface size as a multiple of the automatic size, which follows the game window. 1 is automatic;
    /// 1.2 makes everything 20% larger.
    /// </summary>
    public static float UiScale
    {
        get
        {
            Load();

            return _uiScale;
        }
        set
        {
            Load();
            _uiScale = Mathf.Clamp(Mathf.Snapped(value, 0.05f), MinUiScale, MaxUiScale);
            Save();
        }
    }

    /// <summary>Whether the guide (objectives and tips for new players) is shown.</summary>
    public static bool ShowHints
    {
        get
        {
            Load();

            return _showHints;
        }
        set
        {
            Load();
            _showHints = value;
            Save();
        }
    }

    /// <summary>Overall volume, 0 to 1.</summary>
    public static float MasterVolume
    {
        get => Get(ref _masterVolume);
        set => Set(ref _masterVolume, value);
    }

    /// <summary>Volume of sound effects and interface clicks, 0 to 1.</summary>
    public static float SfxVolume
    {
        get => Get(ref _sfxVolume);
        set => Set(ref _sfxVolume, value);
    }

    /// <summary>Volume of wind, machinery and the background drone, 0 to 1.</summary>
    public static float AmbientVolume
    {
        get => Get(ref _ambientVolume);
        set => Set(ref _ambientVolume, value);
    }

    private static float Get(ref float field)
    {
        Load();

        return field;
    }

    private static void Set(ref float field, float value)
    {
        Load();
        field = Mathf.Clamp(Mathf.Snapped(value, 0.1f), 0f, 1f);
        Save();
    }

    /// <summary>True if the player has already seen (and dismissed or outlasted) a one-off tip.</summary>
    /// <param name="id">The tip's id.</param>
    /// <returns>Whether it has been seen.</returns>
    public static bool TipSeen(string id)
    {
        Load();

        return _seenTips.Contains(id);
    }

    /// <summary>Records a tip as seen so it doesn't come back in later games.</summary>
    /// <param name="id">The tip's id.</param>
    public static void MarkTipSeen(string id)
    {
        Load();
        if (_seenTips.Add(id))
        {
            Save();
        }
    }

    /// <summary>Forgets every seen tip, so they show again.</summary>
    public static void ResetTips()
    {
        Load();
        _seenTips.Clear();
        Save();
    }

    private static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        // Debug/screenshot runs can force the guide on or off without touching the saved file.
        if (System.Environment.GetEnvironmentVariable("FACTORY_HINTS") is { } hints)
        {
            _showHints = hints != "0";
            if (hints == "fresh")
            {
                return;
            }
        }

        ConfigFile cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok)
        {
            return;
        }

        _uiScale = Mathf.Clamp((float)cfg.GetValue("interface", "scale", 1f), MinUiScale, MaxUiScale);
        _masterVolume = Mathf.Clamp((float)cfg.GetValue("audio", "master", _masterVolume), 0f, 1f);
        _sfxVolume = Mathf.Clamp((float)cfg.GetValue("audio", "effects", _sfxVolume), 0f, 1f);
        _ambientVolume = Mathf.Clamp((float)cfg.GetValue("audio", "ambient", _ambientVolume), 0f, 1f);
        if (System.Environment.GetEnvironmentVariable("FACTORY_HINTS") is null)
        {
            _showHints = (bool)cfg.GetValue("guide", "show_hints", true);
        }

        foreach (string id in (string[])cfg.GetValue("guide", "seen_tips", System.Array.Empty<string>()))
        {
            _seenTips.Add(id);
        }
    }

    private static void Save()
    {
        if (System.Environment.GetEnvironmentVariable("FACTORY_HINTS") == "fresh")
        {
            return;
        }

        ConfigFile cfg = new ConfigFile();
        cfg.SetValue("interface", "scale", _uiScale);
        cfg.SetValue("guide", "show_hints", _showHints);
        cfg.SetValue("audio", "master", _masterVolume);
        cfg.SetValue("audio", "effects", _sfxVolume);
        cfg.SetValue("audio", "ambient", _ambientVolume);
        cfg.SetValue("guide", "seen_tips", _seenTips.Order(System.StringComparer.Ordinal).ToArray());
        cfg.Save(Path);
    }
}
