namespace Factory.Sim;

/// <summary>
/// Rail logistics. Track is tile based: orthogonally adjacent rail tiles connect. A train is a chain of
/// vehicles each occupying one tile, moving tile-to-tile with integer progress. Trains may reverse
/// (push) when the only route is behind them, so simple point-to-point lines work.
/// </summary>
public sealed partial class Planet
{
    private const int StopTransferPerTick = 5;
    private const int WaterTowerRange = 3;
    private const int LocoFuelTarget = 20;

    /// <summary>Interpolated sub-unit position of vehicle i (for rendering and operators).</summary>
    public Int2 VehicleSub(Train t, int i)
    {
        Int2 here = Fixed.TileCenter(t.Tiles[i]);
        Int2? ahead = i == 0 ? (t.Progress > 0 ? t.NextTile : null) : t.Tiles[i - 1];
        if ((ahead is null) || (t.Progress == 0))
        {
            return here;
        }

        Int2 d = Torus.Delta(t.Tiles[i], ahead.Value);

        return SubTorus.Wrap(here + new Int2(d.X * t.Progress, d.Y * t.Progress));
    }

    /// <summary>Mass the locomotive must pull: every non-locomotive vehicle plus its cargo.</summary>
    public static long PulledMass(Train t)
    {
        Vehicle? loco = t.Locomotive;
        long m = 0;
        foreach (Vehicle v in t.Vehicles)
        {
            if (v != loco)
            {
                m += v.Def.Mass + v.Cargo.Mass();
            }
        }

        return m;
    }

    /// <summary>True if a follower assigned to operate this train is aboard.</summary>
    public bool HasOperator(Train t) =>
        Units.Any(u => (u.Job.Kind == JobKind.OperateTrain) && (u.Job.TrainId == t.Id) && u.Boarded);

    /// <summary>Why this train can't move right now, or null if it can.</summary>
    public TrainState? TractionProblem(Train t)
    {
        Vehicle? loco = t.Locomotive;
        if (loco is null)
        {
            return TrainState.NoOperator;
        }

        if (PulledMass(t) > loco.Def.MaxPull)
        {
            return TrainState.Overloaded;
        }

        switch (loco.Def.Kind)
        {
            case VehicleKind.HandCart:
                return HasOperator(t) ? null : TrainState.NoOperator;
            case VehicleKind.Steam:
                if (t.Water <= 0)
                {
                    return TrainState.NoWater;
                }

                if ((t.FuelJ <= 0) && (loco.Cargo.Items.Any(kv => World.Content.IsFuel(kv.Key)) == false))
                {
                    return TrainState.NoFuel;
                }

                return null;
        }

        return null;
    }

    internal List<Int2>? FindRailPath(Int2 start, Int2 goal, Int2? forbidFirst)
    {
        if (start == goal)
        {
            return [start];
        }

        Dictionary<Int2, Int2> came = new Dictionary<Int2, Int2> { [start] = start };
        Queue<Int2> q = new Queue<Int2>();
        q.Enqueue(start);
        while ((q.Count > 0) && (came.Count < 50_000))
        {
            Int2 cur = q.Dequeue();
            foreach (Dir d in Dirs.All)
            {
                Int2 n = Torus.Wrap(cur + d.Vec());
                if ((IsRail(n) == false) || came.ContainsKey(n))
                {
                    continue;
                }

                if ((cur == start) && (forbidFirst == n))
                {
                    continue;
                }

                came[n] = cur;
                if (n == goal)
                {
                    List<Int2> path = new List<Int2> { n };
                    while (path[^1] != start)
                    {
                        path.Add(came[path[^1]]);
                    }

                    path.Reverse();

                    return path;
                }

                q.Enqueue(n);
            }
        }

        return null;
    }

    private void UpdateTrains()
    {
        Dictionary<Int2, int> occ = new Dictionary<Int2, int>();
        foreach (Train t in Trains)
        {
            foreach (Int2 tile in t.Tiles)
            {
                occ[tile] = t.Id;
            }

            if ((t.Progress > 0) && (t.NextTile is { } n))
            {
                occ[n] = t.Id;
            }
        }

        foreach (Train t in Trains.ToArray())
        {
            UpdateTrain(t, occ);
        }
    }

    private void UpdateTrain(Train t, Dictionary<Int2, int> occ)
    {
        Vehicle? loco = t.Locomotive;
        if ((t.Schedule.Count == 0) || (loco is null))
        {
            t.State = TrainState.NoSchedule;
            t.Speed = 0;
            t.Progress = 0;
            t.Path.Clear();

            return;
        }

        if (t.StopIndex >= t.Schedule.Count)
        {
            t.StopIndex = 0;
        }

        if (t.State == TrainState.AtStop)
        {
            HandleStop(t);

            return;
        }

        if (t.Path.Count == 0)
        {
            if (t.RetryTicks > 0)
            {
                t.RetryTicks--;

                return;
            }

            Building? stop = World.Get<Building>(t.Schedule[t.StopIndex].StopId);
            if (stop is null)
            {
                t.StopIndex = (t.StopIndex + 1) % t.Schedule.Count;
                t.RetryTicks = 30;
                t.State = TrainState.NoPath;

                return;
            }

            Int2 goal = stop.FrontTile();
            if (t.Tiles[0] == goal)
            {
                ArriveAtStop(t);

                return;
            }

            Int2? behind = t.Tiles.Count > 1 ? t.Tiles[1] : null;
            List<Int2>? path = FindRailPath(t.Tiles[0], goal, behind);
            if ((path is null) && (t.Tiles.Count > 1))
            {
                // Try backing up: flip the consist so the old tail becomes the front.
                t.Tiles.Reverse();
                t.Vehicles.Reverse();
                path = FindRailPath(t.Tiles[0], goal, t.Tiles[1]);
                if (path is null)
                {
                    t.Tiles.Reverse();
                    t.Vehicles.Reverse();
                }
            }

            if (path is null)
            {
                t.State = TrainState.NoPath;
                t.RetryTicks = 120;

                return;
            }

            t.Path.AddRange(path);
            t.PathIndex = 0;
            t.State = TrainState.Moving;
        }

        if (TractionProblem(t) is { } problem)
        {
            t.State = problem;
            t.Speed = 0;

            return;
        }

        Int2 next = t.Path[t.PathIndex + 1];
        if (IsRail(next) == false)
        {
            t.Path.Clear();
            t.Progress = 0;
            t.Speed = 0;
            t.State = TrainState.Pathing;

            return;
        }

        if (occ.TryGetValue(next, out int other) && (other != t.Id))
        {
            t.Speed = 0;
            t.State = TrainState.Blocked;

            return;
        }

        occ[next] = t.Id;
        t.State = TrainState.Moving;

        VehicleDef def = loco.Def;
        long pulled = PulledMass(t);
        int top = (int)System.Math.Max(8, def.MaxSpeed * ((2L * def.MaxPull) - pulled) / (2L * def.MaxPull));
        int accel = (int)System.Math.Max(1, def.Accel * def.MaxPull / (def.MaxPull + pulled));
        int decel = System.Math.Max(2, def.Accel * 2);
        long remaining = ((long)(t.Path.Count - 1 - t.PathIndex) * Fixed.One) - t.Progress;
        long brake = (long)t.Speed * t.Speed / (2 * decel);
        if (brake + t.Speed >= remaining)
        {
            t.Speed = System.Math.Max(8, t.Speed - decel);
        }
        else
        {
            t.Speed = System.Math.Min(top, t.Speed + accel);
        }

        t.Speed = (int)System.Math.Min(t.Speed, remaining);

        if (def.Kind == VehicleKind.Steam)
        {
            long need = PerTickJ(def.PowerKw) * System.Math.Max(t.Speed, def.MaxSpeed / 4) / def.MaxSpeed;
            while (t.FuelJ < need)
            {
                string? fuel = loco.Cargo.Items.FirstOrDefault(kv => World.Content.IsFuel(kv.Key)).Key;
                if (fuel is null)
                {
                    break;
                }

                loco.Cargo.Remove(fuel, 1);
                World.Stats.Record(ItemFlow.Fuel, fuel, 1, loco.Def.Id, "", Id);
                t.FuelJ += World.Content.Item(fuel).FuelKj * 1000L;
            }

            t.FuelJ = System.Math.Max(0, t.FuelJ - need);
            t.Water = System.Math.Max(0, t.Water - 1);
        }

        t.Progress += t.Speed;
        while (t.Progress >= Fixed.One)
        {
            t.Progress -= Fixed.One;
            t.PathIndex++;
            t.Tiles.Insert(0, t.Path[t.PathIndex]);
            t.Tiles.RemoveAt(t.Tiles.Count - 1);
            if (t.PathIndex >= t.Path.Count - 1)
            {
                ArriveAtStop(t);

                return;
            }

            Int2 n2 = t.Path[t.PathIndex + 1];
            if ((IsRail(n2) == false) || (occ.TryGetValue(n2, out int o2) && (o2 != t.Id)))
            {
                t.Progress = 0;
                t.Speed = 0;

                return;
            }
        }
    }

    private static void ArriveAtStop(Train t)
    {
        t.Progress = 0;
        t.Speed = 0;
        t.Path.Clear();
        t.PathIndex = 0;
        t.State = TrainState.AtStop;
        t.StopTicks = 0;
        t.IdleTicks = 0;
    }

    private void HandleStop(Train t)
    {
        ScheduleStop entry = t.Schedule[t.StopIndex];
        Building? stop = World.Get<Building>(entry.StopId);
        if (stop is null)
        {
            Depart(t);

            return;
        }

        t.StopTicks++;
        ContentDb content = World.Content;
        Vehicle loco = t.Locomotive!;

        if (loco.Def.Kind == VehicleKind.Steam)
        {
            bool tower = Buildings.Any(b => (b.Def.Kind == BuildingKind.WaterTower) && (Torus.Chebyshev(b.Origin, stop.Origin) <= WaterTowerRange));
            if (tower)
            {
                t.Water = loco.Def.WaterCapacity;
            }

            int have = loco.Cargo.Items.Where(kv => content.IsFuel(kv.Key)).Sum(kv => kv.Value);
            foreach ((string? item, int n) in stop.Inv.Items.ToList())
            {
                if ((have >= LocoFuelTarget) || (content.IsFuel(item) == false))
                {
                    continue;
                }

                have += stop.Inv.MoveTo(loco.Cargo, item, System.Math.Min(n, LocoFuelTarget - have));
            }
        }

        int moved = 0;
        foreach (Vehicle v in t.Vehicles)
        {
            if (v.Def.Kind != VehicleKind.Cart)
            {
                continue;
            }

            int budget = StopTransferPerTick;
            if (entry.Action == StopAction.Load)
            {
                foreach ((string? item, int n) in stop.Inv.Items.ToList())
                {
                    if (budget <= 0)
                    {
                        break;
                    }

                    int m = stop.Inv.MoveTo(v.Cargo, item, System.Math.Min(n, budget));
                    budget -= m;
                    moved += m;
                }
            }
            else if (entry.Action == StopAction.Unload)
            {
                foreach ((string? item, int n) in v.Cargo.Items.ToList())
                {
                    if (budget <= 0)
                    {
                        break;
                    }

                    int m = v.Cargo.MoveTo(stop.Inv, item, System.Math.Min(n, budget));
                    budget -= m;
                    moved += m;
                }
            }
        }

        t.IdleTicks = moved > 0 ? 0 : t.IdleTicks + 1;

        bool done = entry.Action == StopAction.Pass ? t.StopTicks >= 30 : (t.StopTicks >= 60) && (t.IdleTicks >= 90);
        if (done)
        {
            Depart(t);
        }
    }

    private static void Depart(Train t)
    {
        t.StopIndex = (t.StopIndex + 1) % System.Math.Max(1, t.Schedule.Count);
        t.State = TrainState.Pathing;
        t.Path.Clear();
    }

    /// <summary>Finds rail tiles for a consist with its front at head facing dir. Null if there isn't enough track.</summary>
    public List<Int2>? LayoutTrain(Int2 head, Dir facing, int count)
    {
        head = Torus.Wrap(head);
        if (IsRail(head) == false)
        {
            return null;
        }

        List<Int2> tiles = new List<Int2> { head };
        Int2 lastStep = facing.Opposite().Vec();
        while (tiles.Count < count)
        {
            Int2 cur = tiles[^1];
            Int2? pick = null;
            Int2 straight = Torus.Wrap(cur + lastStep);
            if (IsRail(straight) && (tiles.Contains(straight) == false))
            {
                pick = straight;
            }
            else
            {
                foreach (Dir d in Dirs.All)
                {
                    Int2 n = Torus.Wrap(cur + d.Vec());
                    if ((tiles.Count == 1) && (d.Vec() == facing.Vec()))
                    {
                        continue; // don't put cars in front of the loco
                    }

                    if (IsRail(n) && (tiles.Contains(n) == false))
                    {
                        pick = n;
                        break;
                    }
                }
            }

            if (pick is null)
            {
                return null;
            }

            lastStep = Torus.Delta(cur, pick.Value);
            tiles.Add(pick.Value);
        }

        return tiles;
    }

    /// <summary>True if a train, or a train under construction, occupies the tile.</summary>
    public bool TrainOccupies(Int2 tile) =>
        Trains.Any(t => t.Tiles.Contains(tile)) || Sites.Any(s => (s.Kind == SiteKind.BuildTrain) && s.TrainTiles.Contains(tile));
}
