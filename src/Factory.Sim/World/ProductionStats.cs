namespace Factory.Sim;

/// <summary>
/// Cumulative item production/consumption counters, split by reason. Deterministic and part of the sim,
/// so it powers both the in-game production screen (later) and headless balance analysis. Every place
/// that creates or destroys items must record here: the conservation invariant (world stock ==
/// produced − consumed) is checked by the playtest harness and catches item duplication/loss bugs.
/// </summary>
public sealed class ProductionStats
{
    private readonly SortedDictionary<string, long>[] _byFlow =
        Enumerable.Range(0, Enum.GetValues<ItemFlow>().Length).Select(_ => new SortedDictionary<string, long>(StringComparer.Ordinal)).ToArray();

    /// <summary>True if the flow brings items into the world; false if it removes them.</summary>
    public static bool IsSource(ItemFlow f) => f <= ItemFlow.Refunded;

    /// <summary>Adds count of an item to the counter for the given flow.</summary>
    public void Record(ItemFlow flow, string item, long count)
    {
        if (count == 0)
        {
            return;
        }

        SortedDictionary<string, long> d = _byFlow[(int)flow];
        d[item] = d.GetValueOrDefault(item) + count;
    }

    /// <summary>Records every listed amount, multiplied by times, under the given flow.</summary>
    public void Record(ItemFlow flow, IEnumerable<ItemAmount> items, int times = 1)
    {
        foreach (ItemAmount i in items)
        {
            Record(flow, i.Item, (long)i.Count * times);
        }
    }

    /// <summary>Cumulative count of an item recorded under a flow.</summary>
    public long Get(ItemFlow flow, string item) => _byFlow[(int)flow].GetValueOrDefault(item);
    /// <summary>All item counters for one flow, keyed by item id.</summary>
    public IReadOnlyDictionary<string, long> ByFlow(ItemFlow flow) => _byFlow[(int)flow];

    /// <summary>Total of an item that has entered the world through any source flow.</summary>
    public long Produced(string item) => Enum.GetValues<ItemFlow>().Where(IsSource).Sum(f => Get(f, item));
    /// <summary>Total of an item that has left the world through any sink flow.</summary>
    public long Consumed(string item) => Enum.GetValues<ItemFlow>().Where(f => IsSource(f) == false).Sum(f => Get(f, item));

    /// <summary>Every item id with any recorded flow, in ordinal order.</summary>
    public IEnumerable<string> AllItems() => _byFlow.SelectMany(d => d.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal);

    /// <summary>Feeds all counters into a determinism hash.</summary>
    public void Hash(StateHasher h)
    {
        foreach (SortedDictionary<string, long> d in _byFlow)
        {
            h.Add(d.Count);
            foreach ((string? k, long v) in d)
            {
                h.Add(k);
                h.Add(v);
            }
        }
    }
}
