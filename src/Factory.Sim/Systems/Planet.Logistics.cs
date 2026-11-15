namespace Factory.Sim;

/// <summary>
/// Logistics stations. Each station is set to supply or demand a few items. A supplying station's
/// drones fly its stock to stations on the same planet that demand it, nearest first, and an orbital
/// station's vessels also fly to orbital stations on other planets. Items in flight are kept on the
/// shipment until they land, so nothing is lost or counted twice.
/// </summary>
public sealed partial class Planet
{
    /// <summary>How often a station looks for work, in ticks.</summary>
    private const int StationPeriod = 30;

    /// <summary>Drone speed in tiles per second.</summary>
    private const int DroneTilesPerSecond = 8;

    /// <summary>A vessel's time between planets: a fixed part plus a part per tenth of an orbit apart, in seconds.</summary>
    private const int VesselBaseSeconds = 40;

    private const int VesselSecondsPerOrbit = 5;

    private void UpdateStation(Building b)
    {
        if (EnergyFactor(b) == 0)
        {
            return;
        }

        b.Status = Shipments.Any(s => s.FromId == b.Id) ? MachineStatus.Working : MachineStatus.Idle;
        if (((World.Tick + b.Id) % StationPeriod) != 0)
        {
            return;
        }

        foreach (StationSlot slot in b.StationSlots.Where(s => s.Mode == StationMode.Supply))
        {
            Dispatch(b, slot.Item);
        }
    }

    /// <summary>Drones or vessels free at a station.</summary>
    /// <param name="station">The station.</param>
    /// <param name="vessels">True for vessels, false for drones.</param>
    /// <returns>How many are home.</returns>
    public int FreeCarriers(Building station, bool vessels) =>
        (vessels ? station.Def.Vessels : station.Def.Drones) - Shipments.Count(s => (s.FromId == station.Id) && (s.Vessel == vessels));

    private void Dispatch(Building from, string item)
    {
        // Drones first, to stations on this planet; then vessels to orbital stations elsewhere.
        foreach (Building to in DemandersOf(item, this).OrderBy(d => SubTorus.DistSq(from.CenterSub, d.CenterSub)).ThenBy(d => d.Id))
        {
            if ((to == from) || (FreeCarriers(from, false) <= 0) || (from.Inv.Count(item) <= 0))
            {
                continue;
            }

            int n = System.Math.Min(System.Math.Min(from.Def.DroneLoad, from.Inv.Count(item)), Wanted(to, item));
            if (n > 0)
            {
                long tiles = Fixed.ISqrt(SubTorus.DistSq(from.CenterSub, to.CenterSub)) / Fixed.One;
                Send(from, to, item, n, false, System.Math.Max(World.TicksPerSecond, tiles * World.TicksPerSecond / DroneTilesPerSecond));
            }
        }

        if (from.Def.Kind != BuildingKind.OrbitalStation)
        {
            return;
        }

        foreach (Planet p in World.Planets.Where(p => p != this))
        {
            foreach (Building to in DemandersOf(item, p).Where(d => d.Def.Kind == BuildingKind.OrbitalStation).OrderBy(d => d.Id))
            {
                if ((FreeCarriers(from, true) <= 0) || (from.Inv.Count(item) <= 0))
                {
                    return;
                }

                int n = System.Math.Min(System.Math.Min(from.Def.VesselLoad, from.Inv.Count(item)), Wanted(to, item));
                if (n > 0)
                {
                    int orbits = System.Math.Abs(Def.Orbit - p.Def.Orbit);
                    long ticks = (VesselBaseSeconds + (orbits * VesselSecondsPerOrbit)) * (long)World.TicksPerSecond;
                    Send(from, to, item, n, true, HasSpaceElevator ? ticks / 2 : ticks);
                }
            }
        }
    }

    private static IEnumerable<Building> DemandersOf(string item, Planet p) =>
        p.Buildings.Where(b => b.Def.IsStation && b.StationSlots.Any(s => (s.Item == item) && (s.Mode == StationMode.Demand)));

    /// <summary>How many more of an item a demanding station wants, after what is already on its way.</summary>
    /// <param name="station">The demanding station.</param>
    /// <param name="item">Item id.</param>
    /// <returns>The shortfall, 0 if it is full.</returns>
    public int Wanted(Building station, string item)
    {
        int coming = World.Planets.Sum(p => p.Shipments.Where(s => (s.ToId == station.Id) && (s.Item == item)).Sum(s => s.Count));

        return System.Math.Max(0, System.Math.Min(station.Def.ItemCap - station.Inv.Count(item), station.Inv.SpaceFor(item)) - coming);
    }

    private void Send(Building from, Building to, string item, int n, bool vessel, long ticks)
    {
        from.Inv.Remove(item, n);
        Shipments.Add(new Shipment
        {
            FromId = from.Id,
            ToId = to.Id,
            ToPlanet = to.Planet.Id,
            Vessel = vessel,
            Item = item,
            Count = n,
            ArriveTick = World.Tick + ticks,
            BackTick = World.Tick + (2 * ticks),
        });
    }

    /// <summary>Lands shipments that have arrived and frees carriers that are home.</summary>
    private void UpdateShipments()
    {
        foreach (Shipment s in Shipments.Where(s => (s.Count > 0) && (s.ArriveTick <= World.Tick)).ToList())
        {
            // The receiving station may have been dismantled: then the goods go back to the sender, or
            // into any storage on the sender's planet; failing that they stay aboard until there is one.
            Building? into = World.Get<Building>(s.ToId) is { Def.IsStation: true } to ? to
                : World.Get<Building>(s.FromId) is { Def.IsStation: true } back ? back
                : Buildings.Where(b => b.Def.IsStorage).OrderBy(b => b.Id).FirstOrDefault();
            if (into is not null)
            {
                into.Inv.AddUnchecked(s.Item, s.Count);
                s.Count = 0;
            }
        }

        Shipments.RemoveAll(s => (s.Count == 0) && (s.BackTick <= World.Tick));
    }
}
