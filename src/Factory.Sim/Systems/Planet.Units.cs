namespace Factory.Sim;

/// <summary>Leader movement/hand-mining and follower job AI (gather, build, haul, operate train).</summary>
public sealed partial class Planet
{
    /// <summary>How far, in tiles, the leader can reach to build, mine or interact.</summary>
    public const int LeaderReachTiles = 10;
    private const int GatherBatch = 25;
    private const int TileReach = 700;

    internal static int ReachFor(Building b) => (System.Math.Max(b.W, b.H) * Fixed.Half) + Fixed.Half + 200;

    internal void SetJob(Unit u, Job job)
    {
        ReleaseClaims(u);
        u.Job = job;
        u.Phase = UnitPhase.None;
        u.TargetEntity = 0;
        u.SourceEntity = 0;
        u.WorkTicks = 0;
        u.WaitTicks = 0;
        u.Boarded = false;
        u.MiningTile = null;
        u.Status = job.Kind == JobKind.Idle ? "Idle" : "Starting job";
    }

    private void ReleaseClaims(Unit u)
    {
        foreach (ConstructionSite s in Sites)
        {
            if (s.ClaimedBy == u.Id)
            {
                s.ClaimedBy = 0;
            }
        }
    }

    /// <summary>Moves toward a sub-unit target; returns true once within stopDist.</summary>
    private bool MoveTowards(Unit u, Int2 targetSub, int stopDist)
    {
        Int2 d = SubTorus.Delta(u.Pos, targetSub);
        long dist = Fixed.ISqrt(((long)d.X * d.X) + ((long)d.Y * d.Y));
        if (dist <= stopDist)
        {
            return true;
        }

        long step = System.Math.Min(u.Speed, dist - stopDist);
        u.Pos = SubTorus.Wrap(u.Pos + new Int2((int)(d.X * step / dist), (int)(d.Y * step / dist)));

        return dist - step <= stopDist;
    }

    /// <summary>True if the tile's center is within the leader's reach of the unit.</summary>
    public bool InLeaderReach(Unit u, Int2 tile) =>
        SubTorus.DistSq(u.Pos, Fixed.TileCenter(tile)) <= (long)LeaderReachTiles * Fixed.One * LeaderReachTiles * Fixed.One;

    private void UpdateUnit(Unit u)
    {
        switch (u.Job.Kind)
        {
            case JobKind.Manual:
                UpdateLeader(u);
                break;
            case JobKind.Gather:
                UpdateGather(u);
                break;
            case JobKind.Build:
                UpdateBuilder(u);
                break;
            case JobKind.Haul:
                UpdateHaul(u);
                break;
            case JobKind.OperateTrain:
                UpdateOperator(u);
                break;
            default:
                // Idle followers drop off anything they carry so nothing gets stranded.
                if (u.Carry.IsEmpty == false)
                {
                    DeliverToStorage(u);
                }
                else
                {
                    u.Status = "Idle";
                }

                break;
        }
    }

    // ------------------------------------------------------------------ leader

    private void UpdateLeader(Unit u)
    {
        if (u.MoveIntent != Int2.Zero)
        {
            int dx = System.Math.Sign(u.MoveIntent.X), dy = System.Math.Sign(u.MoveIntent.Y);
            int s = (dx != 0) && (dy != 0) ? u.Speed * 181 / 256 : u.Speed;
            u.Pos = SubTorus.Wrap(u.Pos + new Int2(dx * s, dy * s));
        }

        if (u.MiningTile is not { } t)
        {
            u.Status = u.MoveIntent == Int2.Zero ? "Standing by" : "Walking";

            return;
        }

        ResourceDef? res = ResourceAt(t);
        if (res is null)
        {
            u.MiningTile = null;
            u.MiningProgress = 0;

            return;
        }

        if (SubTorus.DistSq(u.Pos, Fixed.TileCenter(t)) > 3L * Fixed.One * 3L * Fixed.One)
        {
            u.Status = "Too far to mine";

            return;
        }

        if (u.Carry.SpaceFor(res.Item) <= 0)
        {
            u.Status = "Pockets full";

            return;
        }

        u.Status = $"Mining {res.Name}";
        if (++u.MiningProgress < res.HandTicks * 2 / 3)
        {
            return;
        }

        u.MiningProgress = 0;
        string? item = MineOne(t, ItemFlow.HandMined);
        if (item is not null)
        {
            u.Carry.Add(item, 1);
        }
    }

    // ------------------------------------------------------------------ shared helpers

    /// <summary>Walks carried items to the nearest storage with room. Returns true once empty.</summary>
    private bool DeliverToStorage(Unit u)
    {
        if (u.Carry.IsEmpty)
        {
            return true;
        }

        Building? target = World.Get<Building>(u.SourceEntity);
        bool HasRoom(Building s) => u.Carry.Items.Any(kv => s.Inv.SpaceFor(kv.Key) > 0);
        if ((target is null) || (target.Def.Kind is not (BuildingKind.Lander or BuildingKind.Chest)) || (HasRoom(target) == false))
        {
            target = StoragesByDistance(u.Pos).FirstOrDefault(HasRoom);
            if (target is null)
            {
                u.Status = "All storage is full!";

                return false;
            }

            u.SourceEntity = target.Id;
        }

        u.Status = $"Storing items in {target.Name}";
        if (MoveTowards(u, SubTorus.Wrap(target.CenterSub), ReachFor(target)) == false)
        {
            return false;
        }

        foreach ((string? item, int n) in u.Carry.Items.ToList())
        {
            u.Carry.MoveTo(target.Inv, item, n);
        }

        u.SourceEntity = 0;

        return u.Carry.IsEmpty;
    }

    /// <summary>Nearest unoccupied tile holding the given resource, or null if none remain.</summary>
    /// <param name="fromTile">Tile to measure distance from (wrapped).</param>
    /// <param name="res">Resource to look for.</param>
    public Int2? FindNearestResource(Int2 fromTile, ResourceDef res)
    {
        Int2? best = null;
        long bestD = long.MaxValue;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Int2 t = new Int2(x, y);
                if ((ResourceAt(t) != res) || (OccupantAt(t) != 0))
                {
                    continue;
                }

                long d = Torus.DistSq(fromTile, t);
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ gather

    private void UpdateGather(Unit u)
    {
        if ((u.Job.Resource is null) || (World.Content.Resources.TryGetValue(u.Job.Resource, out ResourceDef? res) == false))
        {
            SetJob(u, Job.Idle);

            return;
        }

        switch (u.Phase)
        {
            case UnitPhase.None or UnitPhase.Seek:
                {
                    if ((u.Carry.Count(res.Item) >= GatherBatch) || (u.Carry.SpaceFor(res.Item) == 0) || u.Carry.Items.Any(kv => kv.Key != res.Item))
                    {
                        u.Phase = UnitPhase.Deliver;

                        return;
                    }

                    Int2? t = FindNearestResource(u.Tile, res);
                    if (t is null)
                    {
                        u.Status = $"No {res.Name} left nearby";
                        u.Phase = UnitPhase.Wait;
                        u.WaitTicks = 180;

                        return;
                    }

                    u.TargetTile = t.Value;
                    u.Phase = UnitPhase.MoveTo;
                    u.Status = $"Heading to {res.Name}";
                    break;
                }
            case UnitPhase.MoveTo:
                if (ResourceAt(u.TargetTile) != res)
                {
                    u.Phase = UnitPhase.Seek;

                    return;
                }

                if (MoveTowards(u, Fixed.TileCenter(u.TargetTile), TileReach))
                {
                    u.Phase = UnitPhase.Work;
                    u.WorkTicks = 0;
                }

                break;
            case UnitPhase.Work:
                if (ResourceAt(u.TargetTile) != res)
                {
                    u.Phase = UnitPhase.Seek;

                    return;
                }

                u.Status = $"Gathering {res.Name}";
                if (++u.WorkTicks < res.HandTicks)
                {
                    return;
                }

                u.WorkTicks = 0;
                if (MineOne(u.TargetTile, ItemFlow.HandMined) is { } item)
                {
                    u.Carry.AddUnchecked(item, 1);
                }

                if ((u.Carry.Count(res.Item) >= GatherBatch) || (u.Carry.SpaceFor(res.Item) == 0))
                {
                    u.Phase = UnitPhase.Deliver;
                }

                break;
            case UnitPhase.Deliver:
                if (DeliverToStorage(u))
                {
                    u.Phase = UnitPhase.Seek;
                }

                break;
            case UnitPhase.Wait:
                if (--u.WaitTicks <= 0)
                {
                    u.Phase = UnitPhase.Seek;
                }

                break;
        }
    }

    // ------------------------------------------------------------------ build

    private static int CarriedTowards(Unit u, ConstructionSite s) => s.Missing().Sum(m => System.Math.Min(m.Count, u.Carry.Count(m.Item)));

    private Building? StorageWithAny(Unit u, ConstructionSite s)
    {
        List<ItemAmount> missing = s.Missing().Where(m => m.Count > u.Carry.Count(m.Item)).ToList();
        if (missing.Count == 0)
        {
            return null;
        }

        return StoragesByDistance(u.Pos).FirstOrDefault(st => missing.Any(m => st.Inv.Count(m.Item) > 0));
    }

    private ConstructionSite? PickSite(Unit u)
    {
        foreach (ConstructionSite s in Sites)
        {
            if ((s.ClaimedBy != 0) && (s.ClaimedBy != u.Id))
            {
                Unit? claimant = World.Get<Unit>(s.ClaimedBy);
                if ((claimant is not null) && (claimant.Job.Kind == JobKind.Build))
                {
                    continue;
                }

                s.ClaimedBy = 0;
            }

            if (s.Kind == SiteKind.Deconstruct)
            {
                return s;
            }

            if (s.MaterialsComplete)
            {
                return s;
            }

            if (s.Missing().All(m => u.Carry.Count(m.Item) >= m.Count))
            {
                return s;
            }

            if (StorageWithAny(u, s) is not null)
            {
                return s;
            }
        }

        return null;
    }

    private void UpdateBuilder(Unit u)
    {
        ConstructionSite? site = World.Get<ConstructionSite>(u.TargetEntity);
        if ((u.Phase is not (UnitPhase.None or UnitPhase.Seek)) && (site is null))
        {
            u.Phase = UnitPhase.Seek;
            u.TargetEntity = 0;

            return;
        }

        switch (u.Phase)
        {
            case UnitPhase.None or UnitPhase.Seek:
                {
                    site = PickSite(u);
                    if ((site is null) || ((u.Carry.IsEmpty == false) && (site.Kind != SiteKind.Deconstruct) && (CarriedTowards(u, site) == 0)))
                    {
                        if (u.Carry.IsEmpty == false)
                        {
                            DeliverToStorage(u);

                            return;
                        }

                        u.Status = Sites.Count == 0 ? "Waiting for construction orders" : "Waiting for materials";

                        return;
                    }

                    if ((u.Carry.IsEmpty == false) && (site.Kind == SiteKind.Deconstruct))
                    {
                        DeliverToStorage(u);

                        return;
                    }

                    site.ClaimedBy = u.Id;
                    u.TargetEntity = site.Id;
                    if ((site.Kind == SiteKind.Deconstruct) || site.MaterialsComplete || (StorageWithAny(u, site) is null))
                    {
                        u.Phase = UnitPhase.Deliver;
                    }
                    else
                    {
                        u.SourceEntity = StorageWithAny(u, site)!.Id;
                        u.Phase = UnitPhase.MoveTo;
                    }

                    break;
                }
            case UnitPhase.MoveTo: // fetching materials
                {
                    Building? store = World.Get<Building>(u.SourceEntity);
                    if (store is null)
                    {
                        u.Phase = UnitPhase.Seek;

                        return;
                    }

                    u.Status = $"Fetching materials from {store.Name}";
                    if (MoveTowards(u, SubTorus.Wrap(store.CenterSub), ReachFor(store)) == false)
                    {
                        return;
                    }

                    int took = 0;
                    foreach (ItemAmount m in site!.Missing().ToList())
                    {
                        int want = m.Count - u.Carry.Count(m.Item);
                        if (want > 0)
                        {
                            took += store.Inv.MoveTo(u.Carry, m.Item, want);
                        }
                    }

                    u.SourceEntity = 0;
                    u.Phase = (took > 0) || (CarriedTowards(u, site) > 0) ? UnitPhase.Deliver : UnitPhase.Seek;
                    break;
                }
            case UnitPhase.Deliver: // walking to site
                {
                    u.Status = site!.Kind == SiteKind.Deconstruct ? $"Going to dismantle {SiteLabel(site)}" : $"Delivering to {SiteLabel(site)}";
                    if (MoveTowards(u, SubTorus.Wrap(site.CenterSub), (System.Math.Max(site.W, site.H) * Fixed.Half) + Fixed.Half + 200) == false)
                    {
                        return;
                    }

                    foreach (ItemAmount m in site.Missing().ToList())
                    {
                        u.Carry.MoveTo(site.Delivered, m.Item, m.Count);
                    }

                    u.Phase = site.MaterialsComplete ? UnitPhase.Work : UnitPhase.Seek;
                    break;
                }
            case UnitPhase.Work:
                {
                    u.Status = site!.Kind == SiteKind.Deconstruct ? $"Dismantling {SiteLabel(site)}" : $"Building {SiteLabel(site)}";
                    if (site.WorkDone < site.WorkNeeded)
                    {
                        site.WorkDone++;
                    }

                    if ((site.WorkDone >= site.WorkNeeded) && CompleteSite(site, u))
                    {
                        u.TargetEntity = 0;
                        u.Phase = UnitPhase.Seek;
                    }

                    break;
                }
        }
    }

    /// <summary>Short name of what a site builds or dismantles, for unit status text.</summary>
    public string SiteLabel(ConstructionSite s) => s.Kind switch
    {
        SiteKind.BuildTrain => "train",
        SiteKind.Deconstruct when s.TargetId == 0 => "rail",
        SiteKind.Deconstruct => World.Get<Building>(s.TargetId)?.Def.Name ?? World.Get<Train>(s.TargetId)?.Name ?? "structure",
        _ => World.Content.Buildings.TryGetValue(s.DefId, out BuildingDef? d) ? d.Name : s.DefId,
    };

    // ------------------------------------------------------------------ haul

    private void UpdateHaul(Unit u)
    {
        Building? from = World.Get<Building>(u.Job.FromId);
        Building? to = World.Get<Building>(u.Job.ToId);
        if ((from is null) || (to is null))
        {
            SetJob(u, Job.Idle);
            u.Status = "Haul route broken (building removed)";

            return;
        }

        switch (u.Phase)
        {
            case UnitPhase.None or UnitPhase.Seek:
                // Carrying things the destination won't take (e.g. leftovers from a previous job)?
                // Put them in storage first instead of waiting at the destination forever.
                if ((u.Carry.IsEmpty == false) && (to.Def.IsStorage == false) && u.Carry.Items.Any(kv => AcceptAmount(to, kv.Key) == 0))
                {
                    if (DeliverToStorage(u) == false)
                    {
                        return;
                    }
                }

                u.Phase = u.Carry.IsEmpty ? UnitPhase.MoveTo : UnitPhase.Deliver;
                break;
            case UnitPhase.MoveTo:
                {
                    u.Status = $"Going to {from.Name}";
                    if (MoveTowards(u, SubTorus.Wrap(from.CenterSub), ReachFor(from)) == false)
                    {
                        return;
                    }

                    int took = 0;
                    foreach ((string? item, int count) in from.Out.Items.ToList())
                    {
                        if ((u.Job.Item is not null) && (item != u.Job.Item))
                        {
                            continue;
                        }
                        // Furnaces refuse mixed ores, so never pick up a second ore type for one.
                        if ((to.Def.Kind == BuildingKind.Furnace) && (World.Content.IsFuel(item) == false) &&
                            u.Carry.Items.Any(kv => (kv.Key != item) && (World.Content.IsFuel(kv.Key) == false)))
                        {
                            continue;
                        }

                        int want = System.Math.Min(count, AcceptAmount(to, item) - u.Carry.Count(item));
                        if (want > 0)
                        {
                            took += from.Out.MoveTo(u.Carry, item, want);
                        }
                    }

                    if (took == 0)
                    {
                        bool sourceHasStuff = from.Out.Items.Any(kv => (u.Job.Item is null) || (kv.Key == u.Job.Item));
                        u.Status = sourceHasStuff ? $"{to.Name} needs nothing right now" : $"Waiting for items at {from.Name}";
                        u.Phase = UnitPhase.Wait;
                        u.WaitTicks = 90;

                        return;
                    }

                    u.Phase = UnitPhase.Deliver;
                    break;
                }
            case UnitPhase.Deliver:
                {
                    u.Status = $"Carrying to {to.Name}";
                    if (MoveTowards(u, SubTorus.Wrap(to.CenterSub), ReachFor(to)) == false)
                    {
                        return;
                    }

                    foreach ((string? item, int count) in u.Carry.Items.ToList())
                    {
                        int n = System.Math.Min(count, AcceptAmount(to, item));
                        if (n > 0)
                        {
                            u.Carry.Remove(item, n);
                            to.Inv.AddUnchecked(item, n);
                        }
                    }

                    if (u.Carry.IsEmpty)
                    {
                        u.Phase = UnitPhase.MoveTo;
                    }
                    else
                    {
                        u.Status = $"{to.Name} is full, waiting";
                        u.Phase = UnitPhase.Wait;
                        u.WaitTicks = 90;
                    }

                    break;
                }
            case UnitPhase.Wait:
                if (--u.WaitTicks <= 0)
                {
                    u.Phase = UnitPhase.Seek;
                }

                break;
        }
    }

    // ------------------------------------------------------------------ operate train

    private void UpdateOperator(Unit u)
    {
        Train? t = World.Get<Train>(u.Job.TrainId);
        if (t is null)
        {
            SetJob(u, Job.Idle);

            return;
        }

        int locoIndex = System.Math.Max(0, t.Vehicles.FindIndex(v => v.Def.IsLocomotive));
        Int2 at = VehicleSub(t, locoIndex);
        if (u.Boarded == false)
        {
            if (u.Carry.IsEmpty == false)
            {
                DeliverToStorage(u);

                return;
            }

            u.Status = $"Walking to {t.Name}";
            if (MoveTowards(u, at, 600))
            {
                u.Boarded = true;
            }

            return;
        }

        u.Pos = at;
        u.Status = t.Speed > 0 ? $"Pumping {t.Name}" : $"Aboard {t.Name}";
    }
}
