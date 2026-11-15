namespace Factory.Sim;

/// <summary>Placing ghosts, validating placement, completing / cancelling construction.</summary>
public sealed partial class Planet
{
    /// <summary>Footprint of a building definition when placed with the given rotation (East/West swap width and height).</summary>
    public static (int W, int H) SizeOf(BuildingDef def, Dir rot) =>
        rot is Dir.East or Dir.West ? (def.H, def.W) : (def.W, def.H);

    /// <summary>Returns null if the building can be placed here, otherwise a human-readable reason.</summary>
    public string? CanPlace(BuildingDef def, Int2 origin, Dir rot)
    {
        if (def.Buildable == false)
        {
            return $"{def.Name} can't be built";
        }

        if (World.Research.Unlocked.Contains(def.Id) == false)
        {
            return $"{def.Name} has not been researched";
        }

        (int w, int h) = SizeOf(def, rot);
        if (def.Kind == BuildingKind.Rail)
        {
            if (IsRail(origin))
            {
                return "There is already track here";
            }

            if (IsAreaFree(origin, 1, 1) == false)
            {
                return "Something is in the way";
            }

            return null;
        }

        if (IsAreaFree(origin, w, h) == false)
        {
            return "Something is in the way";
        }

        if (def.NeedsWater && (IsAdjacentToWater(origin, w, h) == false))
        {
            return $"{def.Name} must be placed next to water";
        }

        if (def.Kind == BuildingKind.RailStop)
        {
            Int2 front = Torus.Wrap(origin + rot.Vec());
            bool railPending = Sites.Any(s => (s.Kind == SiteKind.Build) && (s.DefId == "rail") && (s.Origin == front));
            if ((IsRail(front) == false) && (railPending == false))
            {
                return "A rail stop's arrow must point at a rail";
            }
        }

        return null;
    }

    internal ConstructionSite PlaceSite(BuildingDef def, Int2 origin, Dir rot)
    {
        (int w, int h) = SizeOf(def, rot);
        ConstructionSite s = CreateSite(SiteKind.Build, def.Id, origin, rot, w, h, def.CostList, def.BuildTicks);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                SetOccupant(new Int2(origin.X + x, origin.Y + y), s.Id);
            }
        }

        return s;
    }

    /// <summary>Called by a builder when work is finished. Returns false if completion is blocked (e.g. a train is on the tracks).</summary>
    internal bool CompleteSite(ConstructionSite s, Unit builder)
    {
        ContentDb content = World.Content;
        switch (s.Kind)
        {
            case SiteKind.Build:
                {
                    BuildingDef def = content.Buildings[s.DefId];
                    ConsumeDelivered(s);
                    RemoveSite(s);
                    if (def.Kind == BuildingKind.Rail)
                    {
                        SetRail(s.Origin, true);
                    }
                    else
                    {
                        _ = CreateBuildingRotated(def, s.Origin, s.Rot);
                        World.Emit($"{builder.Name} finished building {("AEIOU".Contains(def.Name[0], StringComparison.Ordinal) ? "an" : "a")} {def.Name}", Id, s.Origin);
                        PushOutOfSolids();
                    }

                    return true;
                }
            case SiteKind.BuildTrain:
                {
                    if (s.TrainTiles.Any(t => (IsRail(t) == false) || Trains.Any(tr => tr.Tiles.Contains(t))))
                    {
                        builder.Status = "Track is blocked or missing";

                        return false;
                    }

                    ConsumeDelivered(s);
                    RemoveSite(s);
                    Train train = CreateTrain(s.Vehicles, s.TrainTiles);
                    World.Emit($"{builder.Name} assembled {train.Name}", Id, s.Origin);

                    return true;
                }
            case SiteKind.Deconstruct:
                {
                    RemoveSite(s);
                    if (s.TargetId == 0)
                    {
                        if (IsRail(s.Origin) && (TrainOccupies(s.Origin) == false))
                        {
                            SetRail(s.Origin, false);
                            Refund(builder, content.Buildings["rail"].CostList);
                        }
                    }
                    else if (World.Get<Building>(s.TargetId) is { } b)
                    {
                        Refund(builder, b.Def.CostList);
                        foreach ((string? k, int v) in b.Inv.Items)
                        {
                            builder.Carry.AddUnchecked(k, v);
                        }

                        if (b.Def.IsStorage == false)
                        {
                            foreach ((string? k, int v) in b.Out.Items)
                            {
                                builder.Carry.AddUnchecked(k, v);
                            }
                        }

                        if ((b.Recipe is { } rr) && b.Crafting && (b.Def.Kind != BuildingKind.Lab))
                        {
                            Refund(builder, rr.Inputs);
                        }

                        RemoveBuilding(b);
                    }
                    else if (World.Get<Train>(s.TargetId) is { } t)
                    {
                        foreach (Vehicle v in t.Vehicles)
                        {
                            Refund(builder, v.Def.CostList);
                            foreach ((string? k, int n) in v.Cargo.Items)
                            {
                                builder.Carry.AddUnchecked(k, n);
                            }
                        }

                        foreach (Unit? u in Units.Where(u => (u.Job.Kind == JobKind.OperateTrain) && (u.Job.TrainId == t.Id)).ToList())
                        {
                            SetJob(u, Job.Idle);
                        }

                        Trains.Remove(t);
                        World.Unregister(t);
                    }

                    return true;
                }
        }

        return true;
    }

    private void ConsumeDelivered(ConstructionSite s)
    {
        foreach ((string? k, int v) in s.Delivered.Items)
        {
            World.Stats.Record(ItemFlow.Construction, k, v);
        }

        s.Delivered.Clear();
    }

    private void Refund(Unit builder, IEnumerable<ItemAmount> items)
    {
        foreach (ItemAmount c in items)
        {
            builder.Carry.AddUnchecked(c.Item, c.Count);
        }

        World.Stats.Record(ItemFlow.Refunded, items);
    }

    private Building CreateBuildingRotated(BuildingDef def, Int2 origin, Dir rot)
    {
        // Rotated footprint for non-square buildings: we create with swapped size via a cloned def view.
        (int w, int h) = SizeOf(def, rot);
        if ((w == def.W) && (h == def.H))
        {
            return CreateBuilding(def, origin, rot);
        }

        return CreateBuilding(_rotatedDefs.GetValue(def, CloneRotated), origin, rot);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BuildingDef, BuildingDef> _rotatedDefs = new();

    private static BuildingDef CloneRotated(BuildingDef d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Kind = d.Kind,
        Look = d.Look,
        Age = d.Age,
        AgeId = d.AgeId,
        W = d.H,
        H = d.W,
        Cost = d.Cost,
        CostList = d.CostList,
        BuildTicks = d.BuildTicks,
        Energy = d.Energy,
        PowerKw = d.PowerKw,
        OutputKw = d.OutputKw,
        Efficiency = d.Efficiency,
        Slots = d.Slots,
        OutSlots = d.OutSlots,
        Speed = d.Speed,
        Categories = d.Categories,
        MiningTicks = d.MiningTicks,
        WireReach = d.WireReach,
        SupplyRadius = d.SupplyRadius,
        CapacityKj = d.CapacityKj,
        RateKw = d.RateKw,
        NeedsWater = d.NeedsWater,
        Recipe = d.Recipe,
        Period = d.Period,
        Buildable = d.Buildable,
        Vessel = d.Vessel,
        LaunchFuel = d.LaunchFuel,
        LaunchFuelList = d.LaunchFuelList,
        LandsAs = d.LandsAs,
        Fuels = d.Fuels,
        Drones = d.Drones,
        DroneLoad = d.DroneLoad,
        Vessels = d.Vessels,
        VesselLoad = d.VesselLoad,
        StationSlots = d.StationSlots,
        ItemCap = d.ItemCap,
        Color = d.Color,
        Description = d.Description,
    };

    /// <summary>Cancels a ghost; anything already delivered is returned to the nearest storage.</summary>
    internal void CancelSite(ConstructionSite s)
    {
        Building? store = StoragesByDistance(SubTorus.Wrap(s.CenterSub)).FirstOrDefault();
        if (store is not null)
        {
            foreach ((string? k, int v) in s.Delivered.Items)
            {
                store.Inv.AddUnchecked(k, v);
            }
        }

        foreach (Unit u in Units)
        {
            if (u.TargetEntity == s.Id)
            {
                u.TargetEntity = 0;
                u.Phase = UnitPhase.Seek;
            }
        }

        RemoveSite(s);
    }
}
