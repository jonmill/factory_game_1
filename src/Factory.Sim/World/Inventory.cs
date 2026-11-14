namespace Factory.Sim;

/// <summary>
/// Slot-limited item store. Items are kept as counts (sorted by id for deterministic iteration);
/// capacity is checked as the number of stacks needed. Slots = int.MaxValue means unlimited.
/// </summary>
public sealed class Inventory(ContentDb content, int slots)
{
    private readonly SortedDictionary<string, int> _items = new(StringComparer.Ordinal);
    private readonly ContentDb _content = content;

    /// <summary>Number of stack slots; int.MaxValue means unlimited capacity.</summary>
    public int Slots { get; set; } = slots;
    /// <summary>Item counts keyed by item id, in ordinal id order.</summary>
    public IEnumerable<KeyValuePair<string, int>> Items => _items;
    /// <summary>True when the inventory holds no items.</summary>
    public bool IsEmpty => _items.Count == 0;
    /// <summary>Number of the given item held (0 if none).</summary>
    public int Count(string item) => _items.TryGetValue(item, out int n) ? n : 0;
    /// <summary>Total number of items held across all ids.</summary>
    public int Total => _items.Values.Sum();

    private int StacksFor(string item, int count)
    {
        int s = _content.Stack(item);

        return (count + s - 1) / s;
    }

    /// <summary>Number of slots occupied, counting each item's partial stack as a full slot.</summary>
    public int UsedSlots
    {
        get
        {
            int used = 0;
            foreach ((string? k, int v) in _items)
            {
                used += StacksFor(k, v);
            }

            return used;
        }
    }

    /// <summary>How many more of this item fit.</summary>
    public int SpaceFor(string item)
    {
        if (Slots == int.MaxValue)
        {
            return int.MaxValue / 2;
        }

        int stack = _content.Stack(item);
        int have = Count(item);
        int partial = have % stack == 0 ? 0 : stack - (have % stack);
        int free = Slots - UsedSlots;

        return (System.Math.Max(0, free) * stack) + partial;
    }

    /// <summary>True when every slot is used and every stack is full, so nothing more can be added.</summary>
    public bool IsFull => (Slots != int.MaxValue) && (UsedSlots >= Slots) && _items.All(kv => kv.Value % _content.Stack(kv.Key) == 0);

    /// <summary>Adds up to n (respecting capacity). Returns the amount added.</summary>
    public int Add(string item, int n)
    {
        int add = System.Math.Min(n, SpaceFor(item));
        if (add <= 0)
        {
            return 0;
        }

        _items[item] = Count(item) + add;

        return add;
    }

    /// <summary>Adds ignoring capacity (used for refunds so nothing is ever destroyed).</summary>
    public void AddUnchecked(string item, int n)
    {
        if (n <= 0)
        {
            return;
        }

        _items[item] = Count(item) + n;
    }

    /// <summary>Removes up to n of an item.</summary>
    /// <param name="item">Item id to remove.</param>
    /// <param name="n">Maximum amount to remove.</param>
    /// <returns>The amount actually removed.</returns>
    public int Remove(string item, int n)
    {
        int have = Count(item);
        int take = System.Math.Min(have, n);
        if (take <= 0)
        {
            return 0;
        }

        if (take == have)
        {
            _items.Remove(item);
        }
        else
        {
            _items[item] = have - take;
        }

        return take;
    }

    /// <summary>True if the inventory holds at least every listed amount.</summary>
    public bool Has(IEnumerable<ItemAmount> req) => req.All(r => Count(r.Item) >= r.Count);

    /// <summary>Removes every listed amount (as much as is available of each).</summary>
    public void RemoveAll(IEnumerable<ItemAmount> req)
    {
        foreach (ItemAmount r in req)
        {
            Remove(r.Item, r.Count);
        }
    }

    /// <summary>True if all the listed amounts would fit together, respecting slot capacity.</summary>
    public bool CanAddAll(IEnumerable<ItemAmount> add)
    {
        if (Slots == int.MaxValue)
        {
            return true;
        }
        // Conservative: simulate adding into a copy.
        Inventory copy = new Inventory(_content, Slots);
        foreach ((string? k, int v) in _items)
        {
            copy.AddUnchecked(k, v);
        }

        foreach (ItemAmount a in add)
        {
            if (copy.Add(a.Item, a.Count) < a.Count)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Combined mass of all items held, used for train pulling limits.</summary>
    public long Mass()
    {
        long m = 0;
        foreach ((string? k, int v) in _items)
        {
            m += (long)_content.Item(k).Mass * v;
        }

        return m;
    }

    /// <summary>Removes every item.</summary>
    public void Clear() => _items.Clear();

    /// <summary>Feeds the contents into a determinism hash.</summary>
    public void Hash(StateHasher h)
    {
        h.Add(_items.Count);
        foreach ((string? k, int v) in _items)
        {
            h.Add(k);
            h.Add(v);
        }
    }

    /// <summary>Moves up to n of item into another inventory, returns moved amount.</summary>
    public int MoveTo(Inventory dest, string item, int n)
    {
        int amount = System.Math.Min(n, Count(item));
        amount = System.Math.Min(amount, dest.SpaceFor(item));
        if (amount <= 0)
        {
            return 0;
        }

        Remove(item, amount);
        dest.AddUnchecked(item, amount);

        return amount;
    }
}
