namespace Factory.Sim;

/// <summary>
/// The game's ledger: every item that enters or leaves the world, every building put up or taken down,
/// every finished research and every terraforming point, with who did it, how, where and when.
/// Deterministic and part of the sim, so the same numbers drive the production screen, achievements and
/// the headless playtest reports. Every place that creates or destroys items must record here: the
/// conservation invariant (world stock == produced − consumed) is checked by the playtest harness and
/// catches item duplication and loss bugs.
/// </summary>
public sealed partial class ProductionStats
{
    /// <summary>Length of one time-series bucket, in ticks (five game minutes).</summary>
    public const int BucketTicks = 5 * 60 * 60;

    private readonly SortedDictionary<string, long>[] _byFlow =
        Enumerable.Range(0, Enum.GetValues<ItemFlow>().Length).Select(_ => new SortedDictionary<string, long>(StringComparer.Ordinal)).ToArray();

    private readonly Dictionary<LedgerKey, LedgerEntry> _items = [];
    private readonly Dictionary<(string Item, ItemFlow Flow), SortedDictionary<int, long>> _series = [];
    private readonly Dictionary<(ItemFlow Flow, string By), SortedDictionary<int, long>> _actorSeries = [];
    private readonly Dictionary<(string Def, int Planet), BuildingTally> _buildings = [];
    private readonly SortedDictionary<string, TechTally> _techs = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Planet, string Stat, string By), long> _terraform = [];

    /// <summary>The current tick, stamped on every record. The world sets it each step.</summary>
    public long Tick { get; internal set; }

    /// <summary>True if the flow brings items into the world; false if it removes them.</summary>
    /// <param name="f">The flow.</param>
    /// <returns>Whether it is a source.</returns>
    public static bool IsSource(ItemFlow f) => f <= ItemFlow.Refunded;

    /// <summary>Adds a count of an item to the ledger.</summary>
    /// <param name="flow">How it entered or left the world.</param>
    /// <param name="item">Item id.</param>
    /// <param name="count">How many.</param>
    /// <param name="by">Who did it: a building id ("assembler"), "commander", a robot id ("helper-mk1"), or "" if unknown.</param>
    /// <param name="via">What for: the recipe, the building being built, the tech researched; "" if nothing.</param>
    /// <param name="planet">The planet it happened on, or -1.</param>
    public void Record(ItemFlow flow, string item, long count, string by = "", string via = "", int planet = -1)
    {
        if (count == 0)
        {
            return;
        }

        SortedDictionary<string, long> d = _byFlow[(int)flow];
        d[item] = d.GetValueOrDefault(item) + count;

        LedgerKey key = new LedgerKey(item, flow, by, via, planet);
        if (_items.TryGetValue(key, out LedgerEntry? e) == false)
        {
            e = new LedgerEntry { FirstTick = Tick };
            _items[key] = e;
        }

        e.Count += count;
        e.LastTick = Tick;

        if (_series.TryGetValue((item, flow), out SortedDictionary<int, long>? s) == false)
        {
            s = [];
            _series[(item, flow)] = s;
        }

        int bucket = (int)(Tick / BucketTicks);
        s[bucket] = s.GetValueOrDefault(bucket) + count;
        if (_actorSeries.TryGetValue((flow, by), out SortedDictionary<int, long>? a) == false)
        {
            a = [];
            _actorSeries[(flow, by)] = a;
        }

        a[bucket] = a.GetValueOrDefault(bucket) + count;
    }

    /// <summary>Records every listed amount, multiplied by times, under the given flow.</summary>
    /// <param name="flow">How they entered or left the world.</param>
    /// <param name="items">The amounts.</param>
    /// <param name="times">A multiplier.</param>
    /// <param name="by">Who did it.</param>
    /// <param name="via">What for.</param>
    /// <param name="planet">The planet, or -1.</param>
    public void Record(ItemFlow flow, IEnumerable<ItemAmount> items, int times = 1, string by = "", string via = "", int planet = -1)
    {
        foreach (ItemAmount i in items)
        {
            Record(flow, i.Item, (long)i.Count * times, by, via, planet);
        }
    }

    /// <summary>Counts a building that has appeared on a planet: built, landed or placed at the start.</summary>
    /// <param name="def">Building id.</param>
    /// <param name="planet">Planet id.</param>
    /// <param name="standing">How many of it now stand on that planet, including this one.</param>
    public void RecordBuilt(string def, int planet, int standing)
    {
        BuildingTally t = Tally(def, planet);
        t.Built++;
        t.LastTick = Tick;
        t.Peak = System.Math.Max(t.Peak, standing);
    }

    /// <summary>Counts a building taken down.</summary>
    /// <param name="def">Building id.</param>
    /// <param name="planet">Planet id.</param>
    public void RecordRemoved(string def, int planet)
    {
        BuildingTally t = Tally(def, planet);
        t.Removed++;
        t.LastTick = Tick;
    }

    private BuildingTally Tally(string def, int planet)
    {
        if (_buildings.TryGetValue((def, planet), out BuildingTally? t) == false)
        {
            t = new BuildingTally { FirstTick = Tick };
            _buildings[(def, planet)] = t;
        }

        return t;
    }

    /// <summary>Counts a finished research: a tech, or one more level of a repeatable one.</summary>
    /// <param name="tech">Tech id.</param>
    public void RecordResearch(string tech)
    {
        if (_techs.TryGetValue(tech, out TechTally? t) == false)
        {
            t = new TechTally { FirstTick = Tick };
            _techs[tech] = t;
        }

        t.Levels++;
        t.LastTick = Tick;
    }

    /// <summary>Counts terraforming points added to a planet stat.</summary>
    /// <param name="planet">Planet id.</param>
    /// <param name="stat">Stat id.</param>
    /// <param name="points">Points added.</param>
    /// <param name="by">The building that added them (a launcher for a payload).</param>
    public void RecordTerraform(int planet, string stat, long points, string by)
    {
        _terraform[(planet, stat, by)] = _terraform.GetValueOrDefault((planet, stat, by)) + points;
    }

    // ------------------------------------------------------------------ queries

    /// <summary>Cumulative count of an item recorded under a flow.</summary>
    /// <param name="flow">The flow.</param>
    /// <param name="item">Item id.</param>
    /// <returns>The total.</returns>
    public long Get(ItemFlow flow, string item) => _byFlow[(int)flow].GetValueOrDefault(item);

    /// <summary>All item counters for one flow, keyed by item id.</summary>
    /// <param name="flow">The flow.</param>
    /// <returns>Totals by item.</returns>
    public IReadOnlyDictionary<string, long> ByFlow(ItemFlow flow) => _byFlow[(int)flow];

    /// <summary>Total of an item that has entered the world through any source flow.</summary>
    /// <param name="item">Item id.</param>
    /// <returns>The total.</returns>
    public long Produced(string item) => Enum.GetValues<ItemFlow>().Where(IsSource).Sum(f => Get(f, item));

    /// <summary>Total of an item that has left the world through any sink flow.</summary>
    /// <param name="item">Item id.</param>
    /// <returns>The total.</returns>
    public long Consumed(string item) => Enum.GetValues<ItemFlow>().Where(f => IsSource(f) == false).Sum(f => Get(f, item));

    /// <summary>Every item id with any recorded flow, in ordinal order.</summary>
    /// <returns>Item ids.</returns>
    public IEnumerable<string> AllItems() => _byFlow.SelectMany(d => d.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal);

    /// <summary>Every ledger line, in a stable order: item, flow, who, what for, planet.</summary>
    public IEnumerable<LedgerLine> Lines => _items.OrderBy(kv => kv.Key)
        .Select(kv => new LedgerLine(kv.Key.Item, kv.Key.Flow, kv.Key.By, kv.Key.Via, kv.Key.Planet, kv.Value.Count, kv.Value.FirstTick, kv.Value.LastTick));

    /// <summary>The total of the ledger lines that match every filter given.</summary>
    /// <param name="item">Item id, or null for any.</param>
    /// <param name="flow">Flow, or null for any.</param>
    /// <param name="by">Who, or null for anyone.</param>
    /// <param name="via">What for, or null for anything.</param>
    /// <param name="planet">Planet id, or null for any.</param>
    /// <returns>The total count.</returns>
    public long Count(string? item = null, ItemFlow? flow = null, string? by = null, string? via = null, int? planet = null) =>
        Matching(item, flow, by, via, planet).Sum(kv => kv.Value.Count);

    /// <summary>The first tick anything matching the filters was recorded, or null if never.</summary>
    /// <param name="item">Item id, or null for any.</param>
    /// <param name="flow">Flow, or null for any.</param>
    /// <param name="by">Who, or null for anyone.</param>
    /// <param name="via">What for, or null for anything.</param>
    /// <param name="planet">Planet id, or null for any.</param>
    /// <returns>The tick.</returns>
    public long? FirstTick(string? item = null, ItemFlow? flow = null, string? by = null, string? via = null, int? planet = null) =>
        Matching(item, flow, by, via, planet).Select(kv => (long?)kv.Value.FirstTick).Min();

    private IEnumerable<KeyValuePair<LedgerKey, LedgerEntry>> Matching(string? item, ItemFlow? flow, string? by, string? via, int? planet) =>
        _items.Where(kv => ((item is null) || (kv.Key.Item == item)) && ((flow is null) || (kv.Key.Flow == flow)) && ((by is null) || (kv.Key.By == by))
                           && ((via is null) || (kv.Key.Via == via)) && ((planet is null) || (kv.Key.Planet == planet)));

    /// <summary>How much of an item moved through a flow in each five-minute bucket, by bucket index.</summary>
    /// <param name="item">Item id.</param>
    /// <param name="flow">The flow.</param>
    /// <returns>Counts by bucket (bucket × <see cref="BucketTicks"/> is its start tick).</returns>
    public IReadOnlyDictionary<int, long> Series(string item, ItemFlow flow) =>
        _series.TryGetValue((item, flow), out SortedDictionary<int, long>? s) ? s : new SortedDictionary<int, long>();

    /// <summary>How many items of any kind one producer or consumer moved through a flow in each five-minute bucket.</summary>
    /// <param name="flow">The flow.</param>
    /// <param name="by">Who: a building id, "commander", a robot id.</param>
    /// <returns>Counts by bucket.</returns>
    public IReadOnlyDictionary<int, long> SeriesBy(ItemFlow flow, string by) =>
        _actorSeries.TryGetValue((flow, by), out SortedDictionary<int, long>? s) ? s : new SortedDictionary<int, long>();

    /// <summary>Everyone who has moved items through a flow, in ordinal order.</summary>
    /// <param name="flow">The flow.</param>
    /// <returns>Producer or consumer ids.</returns>
    public IEnumerable<string> ActorsOf(ItemFlow flow) => _actorSeries.Keys.Where(k => k.Flow == flow).Select(k => k.By).Order(StringComparer.Ordinal);

    /// <summary>Every building tally, by building id then planet.</summary>
    public IEnumerable<BuildingLine> BuildingLines => _buildings.OrderBy(kv => kv.Key.Def, StringComparer.Ordinal).ThenBy(kv => kv.Key.Planet)
        .Select(kv => new BuildingLine(kv.Key.Def, kv.Key.Planet, kv.Value.Built, kv.Value.Removed, kv.Value.Peak, kv.Value.FirstTick, kv.Value.LastTick));

    /// <summary>How many of a building have been put up, on one planet or all.</summary>
    /// <param name="def">Building id.</param>
    /// <param name="planet">Planet id, or null for every planet.</param>
    /// <returns>The count.</returns>
    public int Built(string def, int? planet = null) => _buildings.Where(kv => (kv.Key.Def == def) && ((planet is null) || (kv.Key.Planet == planet))).Sum(kv => kv.Value.Built);

    /// <summary>Every finished research: tech id, levels done, first and last tick.</summary>
    public IEnumerable<(string Tech, int Levels, long FirstTick, long LastTick)> ResearchLines => _techs.Select(kv => (kv.Key, kv.Value.Levels, kv.Value.FirstTick, kv.Value.LastTick));

    /// <summary>Terraforming points added, by planet, stat and the building that added them.</summary>
    public IEnumerable<(int Planet, string Stat, string By, long Points)> TerraformLines => _terraform
        .OrderBy(kv => kv.Key.Planet).ThenBy(kv => kv.Key.Stat, StringComparer.Ordinal).ThenBy(kv => kv.Key.By, StringComparer.Ordinal)
        .Select(kv => (kv.Key.Planet, kv.Key.Stat, kv.Key.By, kv.Value));

    /// <summary>Feeds every counter into a determinism hash.</summary>
    /// <param name="h">The hasher.</param>
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

        foreach (LedgerLine l in Lines)
        {
            h.Add(l.Item);
            h.Add((int)l.Flow);
            h.Add(l.By);
            h.Add(l.Via);
            h.Add(l.Planet);
            h.Add(l.Count);
            h.Add(l.FirstTick);
        }

        foreach (BuildingLine b in BuildingLines)
        {
            h.Add(b.Def);
            h.Add(b.Planet);
            h.Add(b.Built);
            h.Add(b.Removed);
        }

        foreach ((string tech, int levels, long first, long _) in ResearchLines)
        {
            h.Add(tech);
            h.Add(levels);
            h.Add(first);
        }

        foreach ((int planet, string stat, string by, long points) in TerraformLines)
        {
            h.Add(planet);
            h.Add(stat);
            h.Add(by);
            h.Add(points);
        }
    }

    private readonly record struct LedgerKey(string Item, ItemFlow Flow, string By, string Via, int Planet) : IComparable<LedgerKey>
    {
        public int CompareTo(LedgerKey o)
        {
            int c = string.CompareOrdinal(Item, o.Item);
            c = c != 0 ? c : Flow.CompareTo(o.Flow);
            c = c != 0 ? c : string.CompareOrdinal(By, o.By);
            c = c != 0 ? c : string.CompareOrdinal(Via, o.Via);

            return c != 0 ? c : Planet.CompareTo(o.Planet);
        }
    }

    private sealed class LedgerEntry
    {
        public long Count;
        public long FirstTick;
        public long LastTick;
    }

    private sealed class BuildingTally
    {
        public int Built;
        public int Removed;
        public int Peak;
        public long FirstTick;
        public long LastTick;
    }

    private sealed class TechTally
    {
        public int Levels;
        public long FirstTick;
        public long LastTick;
    }
}

/// <summary>One line of the item ledger.</summary>
/// <param name="Item">Item id.</param>
/// <param name="Flow">How it entered or left the world.</param>
/// <param name="By">Who did it: a building id, "commander", a robot id, or "".</param>
/// <param name="Via">What for: a recipe, a building being built, a tech; or "".</param>
/// <param name="Planet">Planet id, or -1.</param>
/// <param name="Count">How many, in total.</param>
/// <param name="FirstTick">When it first happened.</param>
/// <param name="LastTick">When it last happened.</param>
public readonly record struct LedgerLine(string Item, ItemFlow Flow, string By, string Via, int Planet, long Count, long FirstTick, long LastTick);

/// <summary>How many of a building have stood on a planet.</summary>
/// <param name="Def">Building id.</param>
/// <param name="Planet">Planet id.</param>
/// <param name="Built">Times one appeared (built, landed or placed at the start).</param>
/// <param name="Removed">Times one was taken down.</param>
/// <param name="Peak">The most that stood at once.</param>
/// <param name="FirstTick">When the first appeared.</param>
/// <param name="LastTick">When the count last changed.</param>
public readonly record struct BuildingLine(string Def, int Planet, int Built, int Removed, int Peak, long FirstTick, long LastTick);
