using System.Diagnostics;
using System.Text;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Save slots, the quicksave, autosaves and loading. A save is a <see cref="Replay"/> (seed + command log)
/// in <c>user://saves/&lt;name&gt;.json</c> with a small header (<see cref="SaveMeta"/>) the lists read on
/// their own. Loading replays the log a batch of ticks per frame behind the HUD's loading overlay, and
/// only swaps the rebuilt world in when it is done; until then the old game stays on screen, frozen, and
/// input is ignored.
/// </summary>
public partial class GameRoot
{
    /// <summary>The slot F5 and F9 use.</summary>
    public const string QuicksaveName = "Quicksave";

    /// <summary>Where saves are kept (FACTORY_SAVE_DIR overrides it, for automated runs).</summary>
    public static string SaveDir => System.Environment.GetEnvironmentVariable("FACTORY_SAVE_DIR") is { Length: > 0 } dir ? dir : ProjectSettings.GlobalizePath("user://saves");

    private const string LegacyQuicksave = "user://quicksave.replay.json";
    private const string AutosavePrefix = "Autosave ";
    private const int AutosaveSlots = 3;
    private const int MaxNameLength = 48;

    /// <summary>Real seconds of unpaused play between autosaves (10 minutes; FACTORY_AUTOSAVE_SECONDS overrides it).</summary>
    private static double AutosaveSeconds => double.TryParse(System.Environment.GetEnvironmentVariable("FACTORY_AUTOSAVE_SECONDS"), System.Globalization.CultureInfo.InvariantCulture, out double s) && (s > 0) ? s : 600;

    private double _sinceAutosave;
    private ulong? _contentHash;
    private Replay? _loadReplay;
    private ReplayPlayback? _playback;
    private int _loadWaitFrames;

    /// <summary>True while a save is being replayed; the game takes no input and doesn't run meanwhile.</summary>
    public bool IsLoading => _loadReplay is not null;

    /// <summary>Name of the save being loaded.</summary>
    public string LoadingName { get; private set; } = "";

    /// <summary>Tick the load has replayed up to.</summary>
    public long LoadingTick => _playback?.World.Tick ?? 0;

    /// <summary>Tick the load finishes on.</summary>
    public long LoadingEndTick => _loadReplay?.FinalTick ?? 0;

    /// <summary>How far the load has got, from 0 to 1.</summary>
    public double LoadingProgress => _playback?.Progress ?? 0;

    private ulong ContentHash => _contentHash ??= Content.ComputeHash();

    private static string PathOf(string stem) => System.IO.Path.Combine(SaveDir, stem + ".json");

    /// <summary>
    /// The file name a save name is stored under: letters, digits, spaces and <c>-_.()'</c> are kept, anything
    /// else becomes <c>_</c>, and it is trimmed to <see cref="MaxNameLength"/> characters. Empty if nothing usable is left.
    /// </summary>
    /// <param name="name">The name the player typed.</param>
    /// <returns>The file stem.</returns>
    public static string FileStem(string name)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char ch in name.Trim())
        {
            sb.Append(char.IsLetterOrDigit(ch) || (ch is ' ' or '-' or '_' or '.' or '(' or ')' or '\'') ? ch : '_');
        }

        string stem = sb.ToString().Trim().TrimEnd('.');
        if (stem.Length > MaxNameLength)
        {
            stem = stem[..MaxNameLength].TrimEnd();
        }

        // Names Windows reserves for devices can't be file names there.
        string upper = stem.ToUpperInvariant();
        bool device = (upper.Length == 4) && char.IsDigit(upper[3]) && (upper.StartsWith("COM", System.StringComparison.Ordinal) || upper.StartsWith("LPT", System.StringComparison.Ordinal));
        if ((upper is "CON" or "PRN" or "AUX" or "NUL") || device)
        {
            stem += "_";
        }

        return stem;
    }

    /// <summary>The save whose file name matches <paramref name="name"/>, ignoring case as Windows and macOS do.</summary>
    /// <param name="slots">The current list.</param>
    /// <param name="name">A typed save name.</param>
    /// <returns>The existing slot, or null.</returns>
    public static SaveSlot? FindSlot(IEnumerable<SaveSlot> slots, string name)
    {
        string stem = FileStem(name);

        return slots.FirstOrDefault(s => string.Equals(s.Stem, stem, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every save in the saves folder, newest first, read from their headers only.</summary>
    /// <returns>The saves.</returns>
    public List<SaveSlot> ListSaves()
    {
        List<SaveSlot> slots = [];
        try
        {
            System.IO.Directory.CreateDirectory(SaveDir);
            AdoptLegacyQuicksave();
            foreach (string path in System.IO.Directory.GetFiles(SaveDir, "*.json"))
            {
                slots.Add(ReadSlot(path));
            }
        }
        catch (System.IO.IOException e)
        {
            GD.PrintErr($"Couldn't list saves in {SaveDir}: {e.Message}");
        }
        catch (System.UnauthorizedAccessException e)
        {
            GD.PrintErr($"Couldn't list saves in {SaveDir}: {e.Message}");
        }

        return [.. slots.OrderByDescending(s => s.SavedAt)];
    }

    /// <summary>Earlier builds kept one quicksave outside the saves folder; it becomes the Quicksave slot.</summary>
    private static void AdoptLegacyQuicksave()
    {
        string legacy = ProjectSettings.GlobalizePath(LegacyQuicksave);
        string target = PathOf(QuicksaveName);
        if ((System.Environment.GetEnvironmentVariable("FACTORY_SAVE_DIR") is null) && System.IO.File.Exists(legacy) && (System.IO.File.Exists(target) == false))
        {
            System.IO.File.Copy(legacy, target);
        }
    }

    private SaveSlot ReadSlot(string path)
    {
        string stem = System.IO.Path.GetFileNameWithoutExtension(path);
        DateTimeOffset written = System.IO.File.GetLastWriteTime(path);
        try
        {
            using System.IO.FileStream f = System.IO.File.OpenRead(path);
            Replay header = Replay.ReadHeader(f);

            return new SaveSlot(stem, header.Meta?.Name is { Length: > 0 } n ? n : stem, header.Meta?.SavedAt ?? written, header.FinalTick,
                header.Meta?.Planet, header.Build, header.Version, header.Check(ContentHash));
        }
        catch (System.IO.InvalidDataException e)
        {
            return new SaveSlot(stem, stem, written, null, null, null, 0, new SaveCheck(false, e.Message, null));
        }
        catch (System.IO.IOException e)
        {
            return new SaveSlot(stem, stem, written, null, null, null, 0, new SaveCheck(false, $"Couldn't read this file: {e.Message}", null));
        }
    }

    /// <summary>
    /// Writes the game to the slot for <paramref name="name"/>, replacing any save of that name. It writes
    /// a temporary file first, so a failed write never leaves a half-written save behind.
    /// </summary>
    /// <param name="name">The save's name.</param>
    /// <returns>Null on success, otherwise why it failed, for the player.</returns>
    public string? SaveGame(string name)
    {
        if (IsLoading)
        {
            return "Wait for the game to finish loading.";
        }

        string stem = FileStem(name);
        if (stem.Length == 0)
        {
            return "Give the save a name.";
        }

        SaveMeta meta = new SaveMeta { Name = name.Trim(), SavedAt = DateTimeOffset.Now, Planet = Planet.Def.Name, PlanetId = PlanetId };
        try
        {
            System.IO.Directory.CreateDirectory(SaveDir);
            string path = PathOf(stem);
            string temp = path + ".tmp";
            System.IO.File.WriteAllText(temp, Replay.CaptureSave(World, meta).ToJson());
            System.IO.File.Move(temp, path, overwrite: true);
        }
        catch (System.IO.IOException e)
        {
            return $"Couldn't write the save: {e.Message}";
        }
        catch (System.UnauthorizedAccessException e)
        {
            return $"Couldn't write the save: {e.Message}";
        }

        return null;
    }

    /// <summary>Deletes a save.</summary>
    /// <param name="stem">The save's file stem.</param>
    /// <returns>Null on success, otherwise why it failed.</returns>
    public static string? DeleteSave(string stem)
    {
        try
        {
            System.IO.File.Delete(PathOf(stem));
        }
        catch (System.IO.IOException e)
        {
            return $"Couldn't delete the save: {e.Message}";
        }
        catch (System.UnauthorizedAccessException e)
        {
            return $"Couldn't delete the save: {e.Message}";
        }

        return null;
    }

    /// <summary>F5: saves to the Quicksave slot.</summary>
    public void QuickSave()
    {
        string? error = SaveGame(QuicksaveName);
        Notify(error ?? "Quicksaved (F9 loads it)", error is null ? Colors.LightGreen : Colors.OrangeRed);
    }

    /// <summary>F9: loads the Quicksave slot.</summary>
    public void QuickLoad()
    {
        if (System.IO.File.Exists(PathOf(QuicksaveName)) == false)
        {
            AdoptLegacyQuicksave();
        }

        if (System.IO.File.Exists(PathOf(QuicksaveName)) == false)
        {
            Notify("No quicksave yet (F5 saves one)", Colors.OrangeRed);

            return;
        }

        if (BeginLoad(QuicksaveName) is { } error)
        {
            Notify(error, Colors.OrangeRed);
        }
    }

    /// <summary>
    /// Starts loading a save: checks its format version from the header, reads it, and hands it to the
    /// loading overlay, which replays it over the next frames. The running game is untouched if it fails.
    /// </summary>
    /// <param name="stem">The save's file stem.</param>
    /// <returns>Null if loading started, otherwise why it can't load, for the player.</returns>
    public string? BeginLoad(string stem)
    {
        if (IsLoading)
        {
            return "A save is already loading.";
        }

        string path = PathOf(stem);
        Replay replay;
        try
        {
            if (System.IO.File.Exists(path) == false)
            {
                return $"The save \"{stem}\" doesn't exist any more.";
            }

            // The version comes first: a newer format's command log may not even parse.
            using (System.IO.FileStream f = System.IO.File.OpenRead(path))
            {
                SaveCheck check = Replay.ReadHeader(f).Check(ContentHash);
                if (check.CanLoad == false)
                {
                    return check.Problem;
                }
            }

            replay = Replay.FromJson(System.IO.File.ReadAllText(path));
        }
        catch (System.IO.InvalidDataException e)
        {
            return e.Message;
        }
        catch (System.IO.IOException e)
        {
            return $"Couldn't read the save: {e.Message}";
        }
        catch (System.UnauthorizedAccessException e)
        {
            return $"Couldn't read the save: {e.Message}";
        }

        _loadReplay = replay;
        _playback = null;
        // Let the overlay draw before building the planets, which takes a moment.
        _loadWaitFrames = 2;
        LoadingName = replay.Meta?.Name is { Length: > 0 } n ? n : stem;
        _mining = false;
        _hud.CloseWindows();

        return null;
    }

    /// <summary>Abandons the load in progress; the game carries on as it was.</summary>
    public void CancelLoad()
    {
        if (IsLoading)
        {
            Notify($"Stopped loading \"{LoadingName}\"", Colors.Orange);
        }

        _loadReplay = null;
        _playback = null;
        _acc = 0;
    }

    /// <summary>
    /// One frame of loading: replays ticks for about 20 ms (a frame or so), and swaps the world in when the
    /// replay reaches its end.
    /// </summary>
    private void StepLoading()
    {
        if (_loadReplay is null)
        {
            return;
        }

        if (_loadWaitFrames > 0)
        {
            _loadWaitFrames--;

            return;
        }

        World world;
        if (_loadReplay.IsSnapshot)
        {
            // A snapshot save: the world is read back as it was, at once.
            try
            {
                world = _loadReplay.Restore(Content);
            }
            catch (System.IO.InvalidDataException e)
            {
                Notify($"Couldn't load \"{LoadingName}\": {e.Message}", Colors.OrangeRed);
                _loadReplay = null;

                return;
            }

            FinishLoad(world);

            return;
        }

        if (_playback is null)
        {
            _playback = _loadReplay.BeginPlayback(Content);

            return;
        }

        Stopwatch watch = Stopwatch.StartNew();
        double budget = LoadBudgetMs;
        int batch = budget < 5 ? 1 : 20;
        while ((_playback.Done == false) && (watch.Elapsed.TotalMilliseconds < budget))
        {
            _playback.Step(batch);
            // The sim only writes events, for the message log; a replay's old news would pile up unread.
            _playback.World.Events.Clear();
        }

        if (_playback.Done == false)
        {
            return;
        }

        FinishLoad(_playback.World);
    }

    /// <summary>Swaps a loaded world in and reports how the load went.</summary>
    /// <param name="world">The loaded world.</param>
    private void FinishLoad(World world)
    {
        Replay replay = _loadReplay!;
        _loadReplay = null;
        _playback = null;
        Adopt(world);
        // The save may end mid-walk: send the keys' real state next frame so the commander doesn't keep walking.
        _lastIntent = new Int2(int.MaxValue, int.MaxValue);
        if (replay.Meta is { } meta && (meta.PlanetId >= 0) && (meta.PlanetId < World.Planets.Count))
        {
            ViewPlanet(meta.PlanetId);
        }

        _sinceAutosave = 0;
        Notify($"Loaded \"{LoadingName}\"", Colors.LightGreen);
        // Short lines: the message log is narrow and clips. The load list has the long version of the warning.
        if (replay.Check(ContentHash).Warning is not null)
        {
            Notify("The game data changed since this save", Colors.Orange);
        }

        if (World.Checksum() != replay.FinalChecksum)
        {
            Notify("Checksum mismatch: it may have replayed differently", Colors.OrangeRed);
            GD.PrintErr($"Load of \"{LoadingName}\": checksum {World.Checksum():X16} at tick {World.Tick}, the save recorded {replay.FinalChecksum:X16}");
        }
    }

    /// <summary>
    /// Automation: FACTORY_SAVE=&lt;name&gt; saves the game once it is set up (e.g. after FACTORY_DEMO), and
    /// FACTORY_LOAD=&lt;name&gt; starts loading that save; FACTORY_LOAD_BUDGET_MS slows loading down for screenshots.
    /// </summary>
    private void SaveLoadHooks()
    {
        if (System.Environment.GetEnvironmentVariable("FACTORY_SAVE") is { Length: > 0 } save)
        {
            GD.Print(SaveGame(save) ?? $"[save] wrote {PathOf(FileStem(save))}");
        }

        if (System.Environment.GetEnvironmentVariable("FACTORY_LOAD") is { Length: > 0 } load)
        {
            GD.Print(BeginLoad(FileStem(load)) ?? $"[load] loading {PathOf(FileStem(load))}");
        }
    }

    private static double LoadBudgetMs => double.TryParse(System.Environment.GetEnvironmentVariable("FACTORY_LOAD_BUDGET_MS"), System.Globalization.CultureInfo.InvariantCulture, out double ms) && (ms > 0) ? ms : 20;

    /// <summary>Counts unpaused real time and autosaves every <see cref="AutosaveSeconds"/> into the oldest of three Autosave slots.</summary>
    /// <param name="delta">Seconds since last frame.</param>
    private void TickAutosave(double delta)
    {
        if (Paused || IsLoading)
        {
            return;
        }

        _sinceAutosave += delta;
        if (_sinceAutosave < AutosaveSeconds)
        {
            return;
        }

        _sinceAutosave = 0;
        string slot = Enumerable.Range(1, AutosaveSlots).Select(i => AutosavePrefix + i)
            .OrderBy(n => System.IO.File.Exists(PathOf(n)) ? System.IO.File.GetLastWriteTimeUtc(PathOf(n)) : System.DateTime.MinValue)
            .First();
        string? error = SaveGame(slot);
        Notify(error is null ? $"Autosaved to \"{slot}\"" : $"Autosave failed: {error}", error is null ? Colors.LightGreen : Colors.OrangeRed);
    }
}
