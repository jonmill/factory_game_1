namespace Factory.Sim;

/// <summary>Saving and loading the ledger as part of a world snapshot.</summary>
public sealed partial class ProductionStats
{
    internal void WriteState(StateWriter s)
    {
        s.I64(Tick);
        s.I32(_byFlow.Length);
        foreach (SortedDictionary<string, long> d in _byFlow)
        {
            WriteLongs(s, d);
        }

        s.I32(_items.Count);
        foreach ((LedgerKey k, LedgerEntry e) in _items.OrderBy(kv => kv.Key))
        {
            s.Str(k.Item);
            s.I32((int)k.Flow);
            s.Str(k.By);
            s.Str(k.Via);
            s.I32(k.Planet);
            s.I64(e.Count);
            s.I64(e.FirstTick);
            s.I64(e.LastTick);
        }

        s.I32(_series.Count);
        foreach (((string item, ItemFlow flow), SortedDictionary<int, long> buckets) in _series.OrderBy(kv => kv.Key.Item, StringComparer.Ordinal).ThenBy(kv => kv.Key.Flow))
        {
            s.Str(item);
            s.I32((int)flow);
            WriteBuckets(s, buckets);
        }

        s.I32(_actorSeries.Count);
        foreach (((ItemFlow flow, string by), SortedDictionary<int, long> buckets) in _actorSeries.OrderBy(kv => kv.Key.Flow).ThenBy(kv => kv.Key.By, StringComparer.Ordinal))
        {
            s.I32((int)flow);
            s.Str(by);
            WriteBuckets(s, buckets);
        }

        s.I32(_buildings.Count);
        foreach (((string def, int planet), BuildingTally t) in _buildings.OrderBy(kv => kv.Key.Def, StringComparer.Ordinal).ThenBy(kv => kv.Key.Planet))
        {
            s.Str(def);
            s.I32(planet);
            s.I32(t.Built);
            s.I32(t.Removed);
            s.I32(t.Peak);
            s.I64(t.FirstTick);
            s.I64(t.LastTick);
        }

        s.I32(_techs.Count);
        foreach ((string tech, TechTally t) in _techs)
        {
            s.Str(tech);
            s.I32(t.Levels);
            s.I64(t.FirstTick);
            s.I64(t.LastTick);
        }

        s.I32(_terraform.Count);
        foreach (((int planet, string stat, string by), long points) in _terraform.OrderBy(kv => kv.Key.Planet).ThenBy(kv => kv.Key.Stat, StringComparer.Ordinal).ThenBy(kv => kv.Key.By, StringComparer.Ordinal))
        {
            s.I32(planet);
            s.Str(stat);
            s.Str(by);
            s.I64(points);
        }
    }

    internal void ReadState(StateReader r)
    {
        Tick = r.I64();
        int flows = r.I32();
        if (flows != _byFlow.Length)
        {
            throw new InvalidDataException("This save's ledger doesn't match this version of the game.");
        }

        foreach (SortedDictionary<string, long> d in _byFlow)
        {
            ReadLongs(r, d);
        }

        _items.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            LedgerKey k = new LedgerKey(r.Text(), (ItemFlow)r.I32(), r.Text(), r.Text(), r.I32());
            _items[k] = new LedgerEntry { Count = r.I64(), FirstTick = r.I64(), LastTick = r.I64() };
        }

        _series.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            string item = r.Text();
            ItemFlow flow = (ItemFlow)r.I32();
            _series[(item, flow)] = ReadBuckets(r);
        }

        _actorSeries.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            ItemFlow flow = (ItemFlow)r.I32();
            string by = r.Text();
            _actorSeries[(flow, by)] = ReadBuckets(r);
        }

        _buildings.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            string def = r.Text();
            int planet = r.I32();
            _buildings[(def, planet)] = new BuildingTally { Built = r.I32(), Removed = r.I32(), Peak = r.I32(), FirstTick = r.I64(), LastTick = r.I64() };
        }

        _techs.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            string tech = r.Text();
            _techs[tech] = new TechTally { Levels = r.I32(), FirstTick = r.I64(), LastTick = r.I64() };
        }

        _terraform.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            int planet = r.I32();
            string stat = r.Text();
            string by = r.Text();
            _terraform[(planet, stat, by)] = r.I64();
        }
    }

    private static void WriteLongs(StateWriter s, SortedDictionary<string, long> d)
    {
        s.I32(d.Count);
        foreach ((string k, long v) in d)
        {
            s.Str(k);
            s.I64(v);
        }
    }

    private static void ReadLongs(StateReader r, SortedDictionary<string, long> d)
    {
        d.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            string k = r.Text();
            d[k] = r.I64();
        }
    }

    private static void WriteBuckets(StateWriter s, SortedDictionary<int, long> d)
    {
        s.I32(d.Count);
        foreach ((int k, long v) in d)
        {
            s.I32(k);
            s.I64(v);
        }
    }

    private static SortedDictionary<int, long> ReadBuckets(StateReader r)
    {
        SortedDictionary<int, long> d = [];
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            int k = r.I32();
            d[k] = r.I64();
        }

        return d;
    }
}
