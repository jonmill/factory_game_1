namespace Factory.Sim;

/// <summary>
/// Travel between planets: a launch pad flies its ferry rocket, with the commander, chosen helper bots and
/// the pad's cargo, to another planet, where it lands as a Landed Ferry. A landed ferry can always fly home.
/// </summary>
public sealed partial class Planet
{
    private const int LandingSearch = 48;

    /// <summary>
    /// Where a ferry of the given size can land: the clear spot nearest the middle of the planet (next to the
    /// lander on the start planet), with a free tile all around it so nobody is walled in.
    /// </summary>
    /// <param name="w">Width in tiles.</param>
    /// <param name="h">Height in tiles.</param>
    /// <returns>The top-left tile, or null if the planet has nowhere to land.</returns>
    public Int2? FindLandingSite(int w, int h)
    {
        Int2 mid = new Int2((Width / 2) - (w / 2), (Height / 2) - (h / 2));
        for (int r = 0; r <= LandingSearch; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if ((System.Math.Abs(dx) != r) && (System.Math.Abs(dy) != r))
                    {
                        continue;
                    }

                    Int2 o = Torus.Wrap(mid + new Int2(dx, dy));
                    if (IsAreaFree(o + new Int2(-1, -1), w + 2, h + 2) && (IsPocket(o + new Int2(-1, -1)) == false))
                    {
                        return o;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>The ferry rocket a landed ferry is, so it can go back on its pad.</summary>
    internal string? VesselOf(BuildingDef ferry) =>
        World.Content.BuildingList.FirstOrDefault(b => (b.Kind == BuildingKind.LaunchPad) && (b.LandsAs == ferry.Id))?.Vessel;

    /// <summary>Flies a launch pad's ferry to another planet with the leader, passengers and all the pad's cargo.</summary>
    internal Building Launch(Building pad, Planet to, Unit leader, IReadOnlyList<Unit> passengers)
    {
        ContentDb c = World.Content;
        pad.Inv.Remove(pad.Def.Vessel!, 1);
        World.Stats.Record(ItemFlow.Launched, pad.Def.Vessel!, 1);
        foreach (ItemAmount f in pad.Def.LaunchFuelList)
        {
            pad.Inv.Remove(f.Item, f.Count);
            World.Stats.Record(ItemFlow.Fuel, f.Item, f.Count);
        }

        Int2 site = to.FindLandingSite(c.Buildings[pad.Def.LandsAs!].W, c.Buildings[pad.Def.LandsAs!].H)!.Value;
        Building ferry = to.CreateBuilding(c.Buildings[pad.Def.LandsAs!], site, Dir.North);
        ferry.HomePadId = pad.Id;
        ferry.HomePlanetId = Id;
        foreach ((string item, int n) in pad.Inv.Items.ToList())
        {
            pad.Inv.Remove(item, n);
            ferry.Inv.AddUnchecked(item, n);
        }

        foreach (Unit u in passengers.Prepend(leader))
        {
            Depart(u);
            to.Arrive(u, ferry);
        }

        World.Emit($"{leader.Name} launched for {to.Name}", Id, pad.Origin);
        World.Emit($"{leader.Name} landed on {to.Name}", to.Id, ferry.Origin);

        return ferry;
    }

    /// <summary>
    /// Flies a landed ferry home: back onto its launch pad, or to the start planet's lander if the pad is
    /// gone. Its cargo, the leader and every one of the leader's helper bots on this planet go with it.
    /// </summary>
    internal void FlyHome(Building ferry, Unit leader)
    {
        (Planet home, Building store) = HomeOf(ferry);
        if (VesselOf(ferry.Def) is { } vessel)
        {
            store.Inv.AddUnchecked(vessel, 1);
            World.Stats.Record(ItemFlow.Refunded, vessel, 1);
        }

        foreach ((string item, int n) in ferry.Inv.Items.ToList())
        {
            ferry.Inv.Remove(item, n);
            store.Inv.AddUnchecked(item, n);
        }

        List<Unit> going = Units.Where(u => (u == leader) || ((u.IsLeader == false) && (u.OwnerPlayer == leader.OwnerPlayer))).ToList();
        RemoveBuilding(ferry);
        foreach (Unit u in going)
        {
            Depart(u);
            home.Arrive(u, store);
        }

        World.Emit($"{leader.Name} flew home to {home.Name}", home.Id, store.Origin);
    }

    /// <summary>Where a landed ferry goes home to: its launch pad if it still stands, else the start planet's lander.</summary>
    /// <param name="ferry">A landed ferry.</param>
    /// <returns>The planet and the building it will land on.</returns>
    public (Planet Home, Building Store) HomeOf(Building ferry)
    {
        if ((World.Get<Building>(ferry.HomePadId) is { Def.Kind: BuildingKind.LaunchPad } pad) && (pad.Planet.Id == ferry.HomePlanetId))
        {
            return (pad.Planet, pad);
        }

        Planet start = World.Planets.First(p => p.Def.Start);

        return (start, start.Buildings.First(b => b.Def.Kind == BuildingKind.Lander));
    }

    private void Depart(Unit u)
    {
        ReleaseClaims(u);
        Units.Remove(u);
    }

    private void Arrive(Unit u, Building at)
    {
        u.Planet = this;
        Units.Add(u);
        Int2 centre = Fixed.ToTile(SubTorus.Wrap(at.CenterSub));
        u.Pos = Fixed.TileCenter(NearestOpenTile(centre) ?? Torus.Wrap(at.Origin + new Int2(-1, -1)));
        u.Path.Clear();
        u.PathStop = -1;
        u.FailedPlans = 0;
        u.StuckTicks = 0;
        u.WalkTarget = null;
        u.MoveIntent = Int2.Zero;
        u.MiningTile = null;
        u.MiningResource = null;
        u.MiningProgress = 0;
        u.BuildingSiteId = 0;
        if (u.IsLeader == false)
        {
            SetJob(u, Job.Idle);
        }
    }
}
