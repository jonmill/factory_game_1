using Godot;

namespace Factory.Game;

/// <summary>
/// Player preferences that outlive a game: interface size, whether the guide's hints show, and which
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
        cfg.SetValue("guide", "seen_tips", _seenTips.Order(System.StringComparer.Ordinal).ToArray());
        cfg.Save(Path);
    }
}
