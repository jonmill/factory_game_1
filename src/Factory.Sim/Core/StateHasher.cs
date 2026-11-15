namespace Factory.Sim;

/// <summary>FNV-1a 64 bit hasher used for state checksums (desync detection, regression tests).</summary>
public sealed class StateHasher
{
    /// <summary>The running hash of everything added so far.</summary>
    public ulong Value { get; private set; } = 14695981039346656037UL;
    /// <summary>Mixes all 8 bytes of a 64-bit value into the hash, least significant first.</summary>
    public void Add(long v)
    {
        for (int i = 0; i < 8; i++)
        {
            Value ^= (byte)(v >> (i * 8));
            Value *= 1099511628211UL;
        }
    }
    /// <summary>Mixes a 32-bit value into the hash (sign-extended to 64 bits).</summary>
    public void Add(int v) => Add((long)v);
    /// <summary>Mixes a boolean into the hash as 1 or 0.</summary>
    public void Add(bool v) => Add(v ? 1L : 0L);

    /// <summary>A stable 64-bit hash of a string (same on every machine and run).</summary>
    /// <param name="s">The string to hash.</param>
    /// <returns>Its hash.</returns>
    public static ulong Of(string s)
    {
        StateHasher h = new StateHasher();
        h.Add(s);

        return h.Value;
    }
    /// <summary>Mixes a string's characters and length into the hash; null hashes differently from empty.</summary>
    public void Add(string? s)
    {
        if (s is null)
        {
            Add(-1);

            return;
        }

        foreach (char c in s)
        {
            Value ^= c;
            Value *= 1099511628211UL;
        }

        Add(s.Length);
    }
    /// <summary>Mixes both components of a vector into the hash.</summary>
    public void Add(Int2 p)
    {
        Add(p.X);
        Add(p.Y);
    }
}
