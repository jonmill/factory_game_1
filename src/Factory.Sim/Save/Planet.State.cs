using System.Runtime.InteropServices;

namespace Factory.Sim;

/// <summary>
/// Saving and loading a planet as part of a world snapshot: its tile layers as raw arrays, then every
/// building, site, robot, train and shipment in list order (the order the sim updates them in). Power
/// networks aren't saved; they are rebuilt from the buildings on the first tick.
/// </summary>
public sealed partial class Planet
{
    internal void WriteState(StateWriter s)
    {
        s.Str(Def.Id);
        s.I32(Width);
        s.I32(Height);
        s.Bytes(MemoryMarshal.AsBytes(_terrain.AsSpan()));
        s.Bytes(_biome);
        s.Bytes(_resource);
        s.Bytes(MemoryMarshal.AsBytes(_amount.AsSpan()));
        s.Bytes(MemoryMarshal.AsBytes(_rail.AsSpan()));
        s.Bytes(MemoryMarshal.AsBytes(_occupant.AsSpan()));
        s.Bytes(MemoryMarshal.AsBytes(_explored.AsSpan()));
        s.I32(ExploredTiles);
        s.I32(_veinCredit);
        s.I32(LanderId);
        s.Counts(Stats);

        s.I32(Buildings.Count);
        foreach (Building b in Buildings)
        {
            WriteBuilding(s, b);
        }

        s.I32(Sites.Count);
        foreach (ConstructionSite site in Sites)
        {
            WriteSite(s, site);
        }

        s.I32(Units.Count);
        foreach (Unit u in Units)
        {
            WriteUnit(s, u);
        }

        s.I32(Trains.Count);
        foreach (Train t in Trains)
        {
            WriteTrain(s, t);
        }

        s.I32(Shipments.Count);
        foreach (Shipment sh in Shipments)
        {
            s.I32(sh.FromId);
            s.I32(sh.ToId);
            s.I32(sh.ToPlanet);
            s.Bool(sh.Vessel);
            s.Str(sh.Item);
            s.I32(sh.Count);
            s.I64(sh.ArriveTick);
            s.I64(sh.BackTick);
        }
    }

    internal void ReadState(StateReader r)
    {
        string id = r.Text();
        int w = r.I32(), h = r.I32();
        if ((id != Def.Id) || (w != Width) || (h != Height))
        {
            throw new InvalidDataException($"This save's planet {id} ({w}×{h}) doesn't match this game's {Def.Id} ({Width}×{Height}).");
        }

        ReadInto(r, MemoryMarshal.AsBytes(_terrain.AsSpan()));
        ReadInto(r, _biome);
        ReadInto(r, _resource);
        ReadInto(r, MemoryMarshal.AsBytes(_amount.AsSpan()));
        ReadInto(r, MemoryMarshal.AsBytes(_rail.AsSpan()));
        ReadInto(r, MemoryMarshal.AsBytes(_occupant.AsSpan()));
        ReadInto(r, MemoryMarshal.AsBytes(_explored.AsSpan()));
        ExploredTiles = r.I32();
        _veinCredit = r.I32();
        LanderId = r.I32();
        Stats.Clear();
        foreach ((string k, int v) in r.Counts())
        {
            Stats[k] = v;
        }

        Buildings.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            Building b = ReadBuilding(r);
            Buildings.Add(b);
            World.Register(b);
        }

        Sites.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            ConstructionSite site = ReadSite(r);
            Sites.Add(site);
            World.Register(site);
        }

        Units.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            Unit u = ReadUnit(r);
            Units.Add(u);
            World.Register(u);
        }

        Trains.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            Train t = ReadTrain(r);
            Trains.Add(t);
            World.Register(t);
        }

        Shipments.Clear();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            Shipments.Add(new Shipment
            {
                FromId = r.I32(),
                ToId = r.I32(),
                ToPlanet = r.I32(),
                Vessel = r.Bool(),
                Item = r.Text(),
                Count = r.I32(),
                ArriveTick = r.I64(),
                BackTick = r.I64(),
            });
        }

        // Derived: power networks are rebuilt from the buildings, and the client redraws everything.
        Networks.Clear();
        PowerDirty = true;
        TerrainVersion++;
        for (int i = 0; i < _fogVersions.Length; i++)
        {
            _fogVersions[i]++;
        }

        FogVersion++;
    }

    private static void ReadInto(StateReader r, Span<byte> into)
    {
        byte[] bytes = r.Bytes();
        if (bytes.Length != into.Length)
        {
            throw new InvalidDataException("This save's map data is the wrong size.");
        }

        bytes.CopyTo(into);
    }

    // ---------------------------------------------------------------- buildings

    private void WriteBuilding(StateWriter s, Building b)
    {
        s.I32(b.Id);
        s.Str(b.Def.Id);
        // Non-square buildings turned on their side use a rotated copy of their definition.
        s.Bool(ReferenceEquals(b.Def, World.Content.Buildings[b.Def.Id]) == false);
        s.Pos(b.Origin);
        s.Byte((byte)b.Rot);
        s.Inv(b.Inv);
        bool sameOut = ReferenceEquals(b.Out, b.Inv);
        s.Bool(sameOut);
        if (sameOut == false)
        {
            s.Inv(b.Out);
        }

        s.Str(b.RecipeId);
        s.Bool(b.Crafting);
        s.I64(b.Progress);
        s.I64(b.FuelJ);
        s.I64(b.StoredJ);
        s.I32((int)b.Status);
        s.I32(b.Satisfaction);
        s.I32(b.NetworkId);
        s.Str(b.BurningFuel);
        s.Str(b.Name);
        s.I32(b.CraftQueue.Count);
        foreach (CraftEntry e in b.CraftQueue)
        {
            s.Str(e.RecipeId);
            s.Bool(e.Intermediate);
        }

        s.Bool(b.CraftReserve is not null);
        if (b.CraftReserve is { } reserve)
        {
            s.Inv(reserve);
        }

        s.Bool(b.CraftingIntermediate);
        s.I32(b.HomePadId);
        s.I32(b.HomePlanetId);
        s.I32(b.TargetPlanetId);
        s.I32(b.Lane.Count);
        foreach (BeltItem item in b.Lane)
        {
            s.Str(item.Item);
            s.I32(item.Pos);
        }

        s.I32(b.StationSlots.Count);
        foreach (StationSlot slot in b.StationSlots)
        {
            s.Str(slot.Item);
            s.I32((int)slot.Mode);
            s.I32(slot.Max);
        }

        s.Strings(b.Modules);
        s.I32(b.BoostPct);
        s.I64(b.LastOutputJ);
    }

    private Building ReadBuilding(StateReader r)
    {
        ContentDb c = World.Content;
        int id = r.I32();
        BuildingDef def = c.Buildings[r.Text()];
        if (r.Bool())
        {
            def = _rotatedDefs.GetValue(def, CloneRotated);
        }

        Building b = new Building { Id = id, Planet = this, Def = def, Origin = r.Pos(), Rot = (Dir)r.Byte() };
        b.Inv = r.Inv(c);
        b.Out = r.Bool() ? b.Inv : r.Inv(c);
        b.RecipeId = r.Str();
        b.Crafting = r.Bool();
        b.Progress = r.I64();
        b.FuelJ = r.I64();
        b.StoredJ = r.I64();
        b.Status = (MachineStatus)r.I32();
        b.Satisfaction = r.I32();
        b.NetworkId = r.I32();
        b.BurningFuel = r.Str();
        b.Name = r.Text();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            b.CraftQueue.Add(new CraftEntry(r.Text(), r.Bool()));
        }

        b.CraftReserve = r.Bool() ? r.Inv(c) : null;
        b.CraftingIntermediate = r.Bool();
        b.HomePadId = r.I32();
        b.HomePlanetId = r.I32();
        b.TargetPlanetId = r.I32();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            b.Lane.Add(new BeltItem { Item = r.Text(), Pos = r.I32() });
        }

        for (int i = 0, n = r.I32(); i < n; i++)
        {
            b.StationSlots.Add(new StationSlot(r.Text(), (StationMode)r.I32(), r.I32()));
        }

        r.Strings(b.Modules);
        b.BoostPct = r.I32();
        b.LastOutputJ = r.I64();

        return b;
    }

    // ---------------------------------------------------------------- construction sites

    private static void WriteSite(StateWriter s, ConstructionSite site)
    {
        s.I32(site.Id);
        s.I32((int)site.Kind);
        s.Str(site.DefId);
        s.Pos(site.Origin);
        s.Byte((byte)site.Rot);
        s.I32(site.W);
        s.I32(site.H);
        s.Inv(site.Delivered);
        s.I32(site.Cost.Length);
        foreach (ItemAmount a in site.Cost)
        {
            s.Str(a.Item);
            s.I32(a.Count);
        }

        s.I32(site.WorkNeeded);
        s.I32(site.WorkDone);
        s.I32(site.ClaimedBy);
        s.I32(site.TargetId);
        s.Strings(site.Vehicles);
        s.Positions(site.TrainTiles);
    }

    private ConstructionSite ReadSite(StateReader r)
    {
        ConstructionSite site = new ConstructionSite
        {
            Id = r.I32(),
            Planet = this,
            Kind = (SiteKind)r.I32(),
            DefId = r.Text(),
            Origin = r.Pos(),
            Rot = (Dir)r.Byte(),
            W = r.I32(),
            H = r.I32(),
            Delivered = r.Inv(World.Content),
        };
        ItemAmount[] cost = new ItemAmount[r.I32()];
        for (int i = 0; i < cost.Length; i++)
        {
            cost[i] = new ItemAmount(r.Text(), r.I32());
        }

        site.Cost = cost;
        site.WorkNeeded = r.I32();
        site.WorkDone = r.I32();
        site.ClaimedBy = r.I32();
        site.TargetId = r.I32();
        r.Strings(site.Vehicles);
        r.Positions(site.TrainTiles);

        return site;
    }

    // ---------------------------------------------------------------- robots

    private static void WriteUnit(StateWriter s, Unit u)
    {
        s.I32(u.Id);
        s.Str(u.Name);
        s.Bool(u.IsLeader);
        s.Str(u.Robot.Id);
        s.Strings(u.Modules);
        s.I32(u.OwnerPlayer);
        s.Pos(u.Pos);
        s.I32(u.Speed);
        s.Inv(u.Carry);
        s.I32((int)u.Job.Kind);
        s.Str(u.Job.Resource);
        s.I32(u.Job.FromId);
        s.I32(u.Job.ToId);
        s.Str(u.Job.Item);
        s.I32(u.Job.TrainId);
        s.I32((int)u.Phase);
        s.Pos(u.TargetSub);
        s.Pos(u.TargetTile);
        s.I32(u.TargetEntity);
        s.I32(u.SourceEntity);
        s.I32(u.WorkTicks);
        s.I32(u.WaitTicks);
        s.Bool(u.Boarded);
        s.Str(u.Status);
        s.Pos(u.MoveIntent);
        s.Pos(u.MiningTile);
        s.Str(u.MiningResource);
        s.I32(u.MiningProgress);
        s.Pos(u.WalkTarget);
        s.I32(u.BuildingSiteId);
        s.Positions(u.Path);
        s.I32(u.ExploreIndex);
        s.Bool(u.SightFrom.HasValue);
        if (u.SightFrom is { } sight)
        {
            s.I32(sight.Planet);
            s.Pos(sight.Tile);
        }

        s.Pos(u.PathGoal);
        s.I32(u.PathStop);
        s.I32(u.StuckTicks);
        s.I32(u.FailedPlans);
        s.Pos(u.LastPlanPos);
    }

    private Unit ReadUnit(StateReader r)
    {
        ContentDb c = World.Content;
        Unit u = new Unit
        {
            Id = r.I32(),
            Planet = this,
            Name = r.Text(),
            IsLeader = r.Bool(),
            Robot = c.Robots[r.Text()],
        };
        r.Strings(u.Modules);
        u.OwnerPlayer = r.I32();
        u.Pos = r.Pos();
        u.Speed = r.I32();
        u.Carry = r.Inv(c);
        u.Job = new Job((JobKind)r.I32(), r.Str(), r.I32(), r.I32(), r.Str(), r.I32());
        u.Phase = (UnitPhase)r.I32();
        u.TargetSub = r.Pos();
        u.TargetTile = r.Pos();
        u.TargetEntity = r.I32();
        u.SourceEntity = r.I32();
        u.WorkTicks = r.I32();
        u.WaitTicks = r.I32();
        u.Boarded = r.Bool();
        u.Status = r.Text();
        u.MoveIntent = r.Pos();
        u.MiningTile = r.MaybePos();
        u.MiningResource = r.Str();
        u.MiningProgress = r.I32();
        u.WalkTarget = r.MaybePos();
        u.BuildingSiteId = r.I32();
        r.Positions(u.Path);
        u.ExploreIndex = r.I32();
        u.SightFrom = r.Bool() ? (r.I32(), r.Pos()) : null;
        u.PathGoal = r.Pos();
        u.PathStop = r.I32();
        u.StuckTicks = r.I32();
        u.FailedPlans = r.I32();
        u.LastPlanPos = r.Pos();

        return u;
    }

    // ---------------------------------------------------------------- trains

    private static void WriteTrain(StateWriter s, Train t)
    {
        s.I32(t.Id);
        s.I32(t.Vehicles.Count);
        foreach (Vehicle v in t.Vehicles)
        {
            s.Str(v.Def.Id);
            s.Inv(v.Cargo);
        }

        s.Positions(t.Tiles);
        s.I32(t.Progress);
        s.I32(t.Speed);
        s.Positions(t.Path);
        s.I32(t.PathIndex);
        s.I32(t.Schedule.Count);
        foreach (ScheduleStop stop in t.Schedule)
        {
            s.I32(stop.StopId);
            s.I32((int)stop.Action);
        }

        s.I32(t.StopIndex);
        s.I32((int)t.State);
        s.I32(t.StopTicks);
        s.I32(t.IdleTicks);
        s.I32(t.RetryTicks);
        s.I64(t.FuelJ);
        s.I32(t.Water);
        s.Str(t.Name);
    }

    private Train ReadTrain(StateReader r)
    {
        ContentDb c = World.Content;
        Train t = new Train { Id = r.I32(), Planet = this };
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            t.Vehicles.Add(new Vehicle { Def = c.Vehicles[r.Text()], Cargo = r.Inv(c) });
        }

        r.Positions(t.Tiles);
        t.Progress = r.I32();
        t.Speed = r.I32();
        r.Positions(t.Path);
        t.PathIndex = r.I32();
        for (int i = 0, n = r.I32(); i < n; i++)
        {
            t.Schedule.Add(new ScheduleStop(r.I32(), (StopAction)r.I32()));
        }

        t.StopIndex = r.I32();
        t.State = (TrainState)r.I32();
        t.StopTicks = r.I32();
        t.IdleTicks = r.I32();
        t.RetryTicks = r.I32();
        t.FuelJ = r.I64();
        t.Water = r.I32();
        t.Name = r.Text();

        return t;
    }
}
