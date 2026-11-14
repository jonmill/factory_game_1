using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>
/// A replay is the world settings + every applied command. Because the simulation is deterministic,
/// replaying reproduces the exact game state (verified by the checksum). For the playtest this doubles
/// as the save format and as a bug-report attachment. (Snapshot saves come later for long games.)
/// </summary>
public sealed class Replay
{
    /// <summary>Replay format version.</summary>
    public int Version { get; init; } = 1;
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
    /// <summary>Every applied command with the tick it was applied on, in order.</summary>
    public List<ScheduledCommand> Commands { get; init; } = [];

    /// <summary>Serializer options used for replay files (compact, string enums, nulls omitted).</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Records a world's settings, content hash, command log and current tick/checksum.</summary>
    public static Replay Capture(World w) => new()
    {
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
    /// <summary>Parses a replay from JSON, throwing <see cref="InvalidDataException"/> if it is empty.</summary>
    public static Replay FromJson(string json) => JsonSerializer.Deserialize<Replay>(json, Json) ?? throw new InvalidDataException("Bad replay");

    /// <summary>World settings reconstructed from the recorded values, for creating a fresh world to replay into.</summary>
    public WorldSettings Settings => new() { Seed = Seed, PlanetWidth = PlanetWidth, PlanetHeight = PlanetHeight, Players = Players, EmptyPlanet = EmptyPlanet };

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
