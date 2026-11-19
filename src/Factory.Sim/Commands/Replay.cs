using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>
/// A replay is the world settings + every applied command. Because the simulation is deterministic,
/// replaying reproduces the exact game state (verified by the checksum). For the playtest this doubles
/// as the save format and as a bug-report attachment. (Snapshot saves come later for long games.)
/// <para>
/// Format versions: 1 is settings + commands; 2 adds <see cref="Build"/> and the <see cref="Meta"/> header.
/// Everything but <see cref="Commands"/> is written first, so <see cref="ReadHeader"/> can list a save
/// without parsing its command log. A loader checks <see cref="Version"/> (<see cref="Check"/>) before it
/// parses the rest, since a newer format's commands may not parse at all.
/// </para>
/// </summary>
public sealed class Replay
{
    /// <summary>The format this build writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>The oldest format this build can still load.</summary>
    public const int MinSupportedVersion = 1;

    /// <summary>How much of a file <see cref="ReadHeader"/> reads first; the header is far smaller.</summary>
    private const int HeaderPrefixBytes = 64 * 1024;

    /// <summary>Replay format version (required in the file).</summary>
    [JsonRequired]
    public int Version { get; init; } = CurrentVersion;
    /// <summary>The game build that wrote the save (<see cref="BuildInfo.Version"/>); null in format 1 files.</summary>
    public string? Build { get; init; }
    /// <summary>Name, time and view for the save lists; null in format 1 files and headless replays.</summary>
    public SaveMeta? Meta { get; init; }
    /// <summary>World generation seed.</summary>
    public ulong Seed { get; init; }
    /// <summary>Planet width in tiles.</summary>
    public int PlanetWidth { get; init; }
    /// <summary>Planet height in tiles.</summary>
    public int PlanetHeight { get; init; }
    /// <summary>Number of players in the game.</summary>
    public int Players { get; init; }
    /// <summary>Whether resource, lake and tree generation was skipped.</summary>
    public bool EmptyPlanet { get; init; }
    /// <summary>Hash of the content the replay was recorded with (see <see cref="ContentDb.ComputeHash"/>), to detect mismatched game data.</summary>
    public ulong ContentHash { get; init; }
    /// <summary>Tick the recording ended on; playback runs to here by default.</summary>
    public long FinalTick { get; init; }
    /// <summary>World checksum at <see cref="FinalTick"/>, used to verify playback reproduced the same state.</summary>
    public ulong FinalChecksum { get; init; }
    /// <summary>Every applied command with the tick it was applied on, in order. Keep this the last property: see <see cref="ReadHeader"/>.</summary>
    public List<ScheduledCommand> Commands { get; init; } = [];

    /// <summary>Serializer options used for replay files (compact, string enums, nulls omitted).</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Records a world's settings, content hash, command log and current tick/checksum.</summary>
    /// <param name="w">The world to record.</param>
    /// <param name="meta">Optional save header (name, time, viewed planet).</param>
    /// <returns>The replay, in the current format.</returns>
    public static Replay Capture(World w, SaveMeta? meta = null) => new()
    {
        Version = CurrentVersion,
        Build = BuildInfo.Version,
        Meta = meta,
        Seed = w.Settings.Seed,
        PlanetWidth = w.Settings.PlanetWidth,
        PlanetHeight = w.Settings.PlanetHeight,
        Players = w.Settings.Players,
        EmptyPlanet = w.Settings.EmptyPlanet,
        ContentHash = w.Content.ComputeHash(),
        FinalTick = w.Tick,
        FinalChecksum = w.Checksum(),
        Commands = [.. w.CommandLog],
    };

    /// <summary>Serializes the replay to JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Parses a replay from JSON.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The replay.</returns>
    /// <exception cref="InvalidDataException">The text isn't a readable save; the message says so for the player.</exception>
    public static Replay FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Replay>(json, Json) ?? throw Unreadable(null);
        }
        catch (JsonException e)
        {
            throw Unreadable(e);
        }
        catch (NotSupportedException e)
        {
            throw Unreadable(e);
        }
    }

    /// <summary>
    /// Reads just the header of a save: every property before <see cref="Commands"/> (the version, build,
    /// <see cref="Meta"/>, settings and final tick), leaving <see cref="Commands"/> empty. It reads the first
    /// 64 KB of the stream and only reads on when the header doesn't fit in that.
    /// </summary>
    /// <param name="stream">The save file.</param>
    /// <returns>The header, as a replay with no commands.</returns>
    /// <exception cref="InvalidDataException">The file isn't a readable save.</exception>
    public static Replay ReadHeader(Stream stream)
    {
        byte[] prefix = new byte[HeaderPrefixBytes];
        int read = 0;
        while (read < prefix.Length)
        {
            int n = stream.Read(prefix, read, prefix.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        bool whole = read < prefix.Length;
        try
        {
            if (TryReadHeader(prefix.AsSpan(0, read), whole) is { } header)
            {
                return header;
            }

            using MemoryStream all = new MemoryStream();
            all.Write(prefix, 0, read);
            stream.CopyTo(all);

            return TryReadHeader(all.GetBuffer().AsSpan(0, (int)all.Length), true) ?? throw Unreadable(null);
        }
        catch (JsonException e)
        {
            throw Unreadable(e);
        }
        catch (NotSupportedException e)
        {
            throw Unreadable(e);
        }
    }

    /// <summary>
    /// Copies the top-level properties before <c>Commands</c> into a small object and deserializes that.
    /// Returns null when <paramref name="json"/> is only a prefix and the header runs past its end.
    /// </summary>
    private static Replay? TryReadHeader(ReadOnlySpan<byte> json, bool final)
    {
        Utf8JsonReader reader = new Utf8JsonReader(json, final, default);
        if (reader.Read() == false)
        {
            return final ? throw Unreadable(null) : null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw Unreadable(null);
        }

        using MemoryStream header = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(header))
        {
            writer.WriteStartObject();
            while (true)
            {
                if (reader.Read() == false)
                {
                    return final ? throw Unreadable(null) : null;
                }

                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                string name = reader.GetString() ?? "";
                if (name == nameof(Commands))
                {
                    break;
                }

                if (reader.Read() == false)
                {
                    return final ? throw Unreadable(null) : null;
                }

                int start = (int)reader.TokenStartIndex;
                if ((reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) && (reader.TrySkip() == false))
                {
                    return final ? throw Unreadable(null) : null;
                }

                writer.WritePropertyName(name);
                writer.WriteRawValue(json[start..(int)reader.BytesConsumed], skipInputValidation: true);
            }

            writer.WriteEndObject();
        }

        return JsonSerializer.Deserialize<Replay>(header.GetBuffer().AsSpan(0, (int)header.Length), Json) ?? throw Unreadable(null);
    }

    private static InvalidDataException Unreadable(Exception? inner) =>
        new("This file isn't a readable save: it may be damaged, incomplete or not a save at all.", inner);

    /// <summary>
    /// Whether this build can load the save. Format versions outside
    /// <see cref="MinSupportedVersion"/>..<see cref="CurrentVersion"/> can't be loaded; a save recorded with
    /// different game data (<see cref="ContentHash"/>) still loads, with a warning.
    /// </summary>
    /// <param name="contentHash">The running game's <see cref="ContentDb.ComputeHash"/>.</param>
    /// <returns>The verdict, with player-facing reasons.</returns>
    public SaveCheck Check(ulong contentHash)
    {
        if (Version < MinSupportedVersion)
        {
            return new SaveCheck(false, $"This save is from an older version of the game (format {Version}) and can't be loaded.", null);
        }

        if (Version > CurrentVersion)
        {
            string from = Build is null ? "" : $", {Build}";

            return new SaveCheck(false, $"This save is from a newer version of the game (format {Version}{from}). Update the game to load it.", null);
        }

        string? warning = ContentHash == contentHash ? null : "The game data has changed since this was saved, so it may not play back exactly as it was.";

        return new SaveCheck(true, null, warning);
    }

    /// <summary>World settings reconstructed from the recorded values, for creating a fresh world to replay into.</summary>
    public WorldSettings Settings => new() { Seed = Seed, PlanetWidth = PlanetWidth, PlanetHeight = PlanetHeight, Players = Players, EmptyPlanet = EmptyPlanet };

    /// <summary>Starts playing the replay back a batch of ticks at a time (see <see cref="ReplayPlayback"/>).</summary>
    /// <param name="content">Game content to simulate with.</param>
    /// <returns>The playback, at tick 0.</returns>
    public ReplayPlayback BeginPlayback(ContentDb content) => new(this, content);

    /// <summary>Re-simulates the replay up to <paramref name="untilTick"/> (default: the recorded end).</summary>
    public World Play(ContentDb content, long? untilTick = null)
    {
        World w = new World(content, Settings);
        foreach (ScheduledCommand c in Commands)
        {
            w.SubmitAt(c.Tick, c.Command);
        }

        long end = untilTick ?? FinalTick;
        while (w.Tick < end)
        {
            w.Step();
        }

        return w;
    }
}
