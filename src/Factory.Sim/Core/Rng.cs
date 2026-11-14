namespace Factory.Sim;

/// <summary>Deterministic RNG (SplitMix64). Never use System.Random in the simulation.</summary>
public sealed class Rng(ulong seed)
{
    /// <summary>Current generator state; starts at the seed and is saved/restored to reproduce the sequence.</summary>
    public ulong State { get; private set; } = seed;

    /// <summary>Advances the state and returns the next 64-bit pseudo-random value.</summary>
    public ulong Next()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

        return z ^ (z >> 31);
    }

    /// <summary>Returns a pseudo-random integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    /// <param name="minInclusive">Lowest value that can be returned.</param>
    /// <param name="maxExclusive">One past the highest value that can be returned.</param>
    /// <returns>The value; <paramref name="minInclusive"/> if the range is empty (no state is consumed in that case).</returns>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        return minInclusive + (int)(Next() % (ulong)(maxExclusive - minInclusive));
    }
}
