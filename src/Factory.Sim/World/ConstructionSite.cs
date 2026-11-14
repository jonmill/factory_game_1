namespace Factory.Sim;

/// <summary>A ghost placed by the player. Followers with the Build job deliver materials and construct it.</summary>
public sealed class ConstructionSite : Entity
{
    /// <summary>Whether this site builds a structure, dismantles one or assembles a train.</summary>
    public SiteKind Kind { get; internal set; }
    /// <summary>Id of the building definition to construct (for build sites).</summary>
    public string DefId { get; internal set; } = "";
    /// <summary>Top-left tile of the site's footprint.</summary>
    public Int2 Origin { get; internal set; }
    /// <summary>Facing direction the finished building will have.</summary>
    public Dir Rot { get; internal set; }
    /// <summary>Footprint width in tiles, already adjusted for rotation.</summary>
    public int W { get; internal set; } = 1;
    /// <summary>Footprint height in tiles, already adjusted for rotation.</summary>
    public int H { get; internal set; } = 1;
    /// <summary>Materials builders have brought to the site so far.</summary>
    public Inventory Delivered { get; internal set; } = null!;
    /// <summary>Full material cost that must be delivered before work can begin.</summary>
    public ItemAmount[] Cost { get; internal set; } = [];
    /// <summary>Builder work required to finish, in ticks of a single builder working.</summary>
    public int WorkNeeded { get; internal set; }
    /// <summary>Builder work performed so far; the site completes when it reaches WorkNeeded.</summary>
    public int WorkDone { get; internal set; }
    /// <summary>Id of the follower currently assigned to this site, or 0 if unclaimed.</summary>
    public int ClaimedBy { get; internal set; }
    /// <summary>Deconstruct: target building id (0 = rail tile at Origin).</summary>
    public int TargetId { get; internal set; }
    /// <summary>BuildTrain: vehicle ids and the rail tiles they will occupy (index 0 = front).</summary>
    public List<string> Vehicles { get; } = [];
    /// <summary>BuildTrain: rail tile each vehicle will occupy, matching Vehicles by index.</summary>
    public List<Int2> TrainTiles { get; } = [];

    /// <summary>Center of the footprint in sub-units (1024 per tile); not wrapped, so callers wrap as needed.</summary>
    public Int2 CenterSub => new((Origin.X * Fixed.One) + (W * Fixed.Half), (Origin.Y * Fixed.One) + (H * Fixed.Half));

    /// <summary>Materials from the cost that have not been delivered yet.</summary>
    public IEnumerable<ItemAmount> Missing()
    {
        foreach (ItemAmount c in Cost)
        {
            int need = c.Count - Delivered.Count(c.Item);
            if (need > 0)
            {
                yield return new ItemAmount(c.Item, need);
            }
        }
    }

    /// <summary>True when every material has been delivered (always true for deconstruction).</summary>
    public bool MaterialsComplete => (Kind == SiteKind.Deconstruct) || (Missing().Any() == false);
}
