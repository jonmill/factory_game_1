using System.Text.Json;

namespace Factory.Sim;

/// <summary>Saving and loading the world's own state as part of a snapshot (see <see cref="WorldState"/>).</summary>
public sealed partial class World
{
    internal void WriteState(StateWriter s)
    {
        s.I64(Tick);
        s.I64((long)Rng.State);
        s.I32(HelpersBuilt);
        s.Bool(SettlersCalled);
        s.I32(_nextId);
        s.I32(_commandSeq);
        // Commands queued for a later tick (rare at save time): kept as JSON, the replay's own format.
        s.Str(JsonSerializer.Serialize(_pending, Replay.Json));

        s.Strings(Research.Unlocked);
        s.Strings(Research.Completed);
        s.Str(Research.Current);
        s.Counts(Research.Progress);
        s.Counts(Research.Levels);

        Stats.WriteState(s);
        s.I32(Planets.Count);
        foreach (Planet p in Planets)
        {
            p.WriteState(s);
        }
    }

    internal void ReadState(StateReader r)
    {
        Tick = r.I64();
        Rng.Restore((ulong)r.I64());
        HelpersBuilt = r.I32();
        SettlersCalled = r.Bool();
        _nextId = r.I32();
        _commandSeq = r.I32();
        _pending.Clear();
        _pending.AddRange(JsonSerializer.Deserialize<List<ScheduledCommand>>(r.Text(), Replay.Json) ?? []);

        Research.Unlocked.Clear();
        r.Strings(Research.Unlocked);
        Research.Completed.Clear();
        r.Strings(Research.Completed);
        Research.Current = r.Str();
        Research.Progress.Clear();
        foreach ((string k, int v) in r.Counts())
        {
            Research.Progress[k] = v;
        }

        Research.Levels.Clear();
        foreach ((string k, int v) in r.Counts())
        {
            Research.Levels[k] = v;
        }

        Stats.ReadState(r);
        int planets = r.I32();
        if (planets != Planets.Count)
        {
            throw new InvalidDataException($"This save has {planets} planets; this version of the game has {Planets.Count}.");
        }

        foreach (Planet p in Planets)
        {
            p.ReadState(r);
        }
    }
}
