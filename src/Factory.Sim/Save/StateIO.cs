using System.IO.Compression;
using System.Text;

namespace Factory.Sim;

/// <summary>
/// Writes the binary world snapshot. Every value is written in a fixed order and read back in the same
/// order by <see cref="StateReader"/>, so the two must be kept in step: when a field is added to the sim,
/// it goes in both places and <see cref="WorldState.Format"/> goes up.
/// </summary>
public sealed class StateWriter : IDisposable
{
    private readonly BinaryWriter _w;

    /// <summary>Creates a writer over a stream.</summary>
    /// <param name="s">The stream.</param>
    public StateWriter(Stream s) => _w = new BinaryWriter(s, Encoding.UTF8, leaveOpen: true);

    /// <summary>Writes an int.</summary>
    /// <param name="v">The value.</param>
    public void I32(int v) => _w.Write(v);

    /// <summary>Writes a long.</summary>
    /// <param name="v">The value.</param>
    public void I64(long v) => _w.Write(v);

    /// <summary>Writes a bool.</summary>
    /// <param name="v">The value.</param>
    public void Bool(bool v) => _w.Write(v);

    /// <summary>Writes a byte.</summary>
    /// <param name="v">The value.</param>
    public void Byte(byte v) => _w.Write(v);

    /// <summary>Writes a string that may be null.</summary>
    /// <param name="v">The value.</param>
    public void Str(string? v)
    {
        _w.Write(v is not null);
        if (v is not null)
        {
            _w.Write(v);
        }
    }

    /// <summary>Writes a tile or sub-tile position.</summary>
    /// <param name="v">The value.</param>
    public void Pos(Int2 v)
    {
        _w.Write(v.X);
        _w.Write(v.Y);
    }

    /// <summary>Writes a position that may be absent.</summary>
    /// <param name="v">The value.</param>
    public void Pos(Int2? v)
    {
        _w.Write(v.HasValue);
        if (v is { } p)
        {
            Pos(p);
        }
    }

    /// <summary>Writes a list of positions.</summary>
    /// <param name="list">The positions.</param>
    public void Positions(IReadOnlyCollection<Int2> list)
    {
        _w.Write(list.Count);
        foreach (Int2 p in list)
        {
            Pos(p);
        }
    }

    /// <summary>Writes a list of strings.</summary>
    /// <param name="list">The strings.</param>
    public void Strings(IReadOnlyCollection<string> list)
    {
        _w.Write(list.Count);
        foreach (string s in list)
        {
            _w.Write(s);
        }
    }

    /// <summary>Writes a string-to-int map in its own order.</summary>
    /// <param name="map">The map.</param>
    public void Counts(IEnumerable<KeyValuePair<string, int>> map)
    {
        List<KeyValuePair<string, int>> list = [.. map];
        _w.Write(list.Count);
        foreach ((string k, int v) in list)
        {
            _w.Write(k);
            _w.Write(v);
        }
    }

    /// <summary>Writes raw bytes with their length.</summary>
    /// <param name="bytes">The bytes.</param>
    public void Bytes(ReadOnlySpan<byte> bytes)
    {
        _w.Write(bytes.Length);
        _w.Write(bytes);
    }

    /// <summary>Writes an inventory: its slot count and contents.</summary>
    /// <param name="inv">The inventory.</param>
    public void Inv(Inventory inv)
    {
        _w.Write(inv.Slots);
        Counts(inv.Items);
    }

    /// <inheritdoc />
    public void Dispose() => _w.Dispose();
}

/// <summary>Reads what <see cref="StateWriter"/> wrote, in the same order.</summary>
public sealed class StateReader : IDisposable
{
    private readonly BinaryReader _r;

    /// <summary>Creates a reader over a stream.</summary>
    /// <param name="s">The stream.</param>
    public StateReader(Stream s) => _r = new BinaryReader(s, Encoding.UTF8, leaveOpen: true);

    /// <summary>Reads an int.</summary>
    /// <returns>The value.</returns>
    public int I32() => _r.ReadInt32();

    /// <summary>Reads a long.</summary>
    /// <returns>The value.</returns>
    public long I64() => _r.ReadInt64();

    /// <summary>Reads a bool.</summary>
    /// <returns>The value.</returns>
    public bool Bool() => _r.ReadBoolean();

    /// <summary>Reads a byte.</summary>
    /// <returns>The value.</returns>
    public byte Byte() => _r.ReadByte();

    /// <summary>Reads a string that may be null.</summary>
    /// <returns>The value.</returns>
    public string? Str() => _r.ReadBoolean() ? _r.ReadString() : null;

    /// <summary>Reads a string that must be there.</summary>
    /// <returns>The value.</returns>
    public string Text() => Str() ?? throw new InvalidDataException("Missing text in the saved state");

    /// <summary>Reads a position.</summary>
    /// <returns>The value.</returns>
    public Int2 Pos() => new(_r.ReadInt32(), _r.ReadInt32());

    /// <summary>Reads a position that may be absent.</summary>
    /// <returns>The value.</returns>
    public Int2? MaybePos() => _r.ReadBoolean() ? Pos() : null;

    /// <summary>Reads a list of positions into a list.</summary>
    /// <param name="into">The list to fill.</param>
    public void Positions(List<Int2> into)
    {
        int n = _r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            into.Add(Pos());
        }
    }

    /// <summary>Reads a list of strings into a list.</summary>
    /// <param name="into">The list to fill.</param>
    public void Strings(ICollection<string> into)
    {
        int n = _r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            into.Add(_r.ReadString());
        }
    }

    /// <summary>Reads a string-to-int map.</summary>
    /// <returns>The pairs, in the order written.</returns>
    public List<KeyValuePair<string, int>> Counts()
    {
        int n = _r.ReadInt32();
        List<KeyValuePair<string, int>> list = new(n);
        for (int i = 0; i < n; i++)
        {
            list.Add(new(_r.ReadString(), _r.ReadInt32()));
        }

        return list;
    }

    /// <summary>Reads raw bytes.</summary>
    /// <returns>The bytes.</returns>
    public byte[] Bytes() => _r.ReadBytes(_r.ReadInt32());

    /// <summary>Reads an inventory.</summary>
    /// <param name="content">The content database.</param>
    /// <returns>The inventory.</returns>
    public Inventory Inv(ContentDb content)
    {
        Inventory inv = new Inventory(content, _r.ReadInt32());
        inv.RestoreItems(Counts());

        return inv;
    }

    /// <inheritdoc />
    public void Dispose() => _r.Dispose();
}

/// <summary>
/// A snapshot of the whole simulation: everything needed to carry on exactly as if the game had never
/// stopped, so loading is instant however long the game has run. Derived data (power networks, caches) is
/// rebuilt on the first tick. Stored gzipped.
/// </summary>
public static class WorldState
{
    /// <summary>Layout version of the binary snapshot; bump it whenever what is written changes.</summary>
    public const int Format = 1;

    private const int Magic = 0x46534156; // "VASF"

    /// <summary>Captures a world.</summary>
    /// <param name="w">The world.</param>
    /// <returns>The gzipped snapshot.</returns>
    public static byte[] Capture(World w)
    {
        using MemoryStream raw = new MemoryStream();
        using (GZipStream gz = new GZipStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        using (StateWriter s = new StateWriter(gz))
        {
            s.I32(Magic);
            s.I32(Format);
            w.WriteState(s);
        }

        return raw.ToArray();
    }

    /// <summary>Rebuilds a world from a snapshot.</summary>
    /// <param name="content">The content database (must be the one it was saved with, or compatible).</param>
    /// <param name="settings">The world settings it was saved with.</param>
    /// <param name="snapshot">The gzipped snapshot.</param>
    /// <returns>The world, at the tick it was saved.</returns>
    public static World Restore(ContentDb content, WorldSettings settings, byte[] snapshot)
    {
        try
        {
            using MemoryStream raw = new MemoryStream(snapshot);
            using GZipStream gz = new GZipStream(raw, CompressionMode.Decompress);
            using StateReader r = new StateReader(gz);
            if (r.I32() != Magic)
            {
                throw new InvalidDataException("This save's game state is damaged.");
            }

            int format = r.I32();
            if (format != Format)
            {
                throw new InvalidDataException($"This save's game state is in format {format}; this version reads format {Format}.");
            }

            World w = new World(content, settings, generate: false);
            w.ReadState(r);

            return w;
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("This save's game state is cut short: the file may be incomplete.", e);
        }
        catch (KeyNotFoundException e)
        {
            throw new InvalidDataException("This save mentions something the game no longer has, so it can't be loaded.", e);
        }
    }
}
