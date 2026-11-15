namespace Factory.Sim.Tests;

public class LogisticsTests
{
    private static long Total(World w, string item)
    {
        long n = 0;
        foreach (Planet p in w.Planets)
        {
            n += p.Buildings.Sum(b => b.Inv.Count(item) + (ReferenceEquals(b.Inv, b.Out) ? 0 : b.Out.Count(item)));
            n += p.Shipments.Where(s => s.Item == item).Sum(s => s.Count);
            n += p.Units.Sum(u => u.Carry.Count(item));
        }

        return n;
    }

    /// <summary>A station with its own solar panel and pole, so it has power.</summary>
    private static Building PoweredStation(Planet p, string id, int x, int y)
    {
        ContentDb c = p.World.Content;
        Building s = p.CreateBuilding(c.Buildings[id], new Int2(x, y), Dir.North);
        int w = s.W;
        p.CreateBuilding(c.Buildings["power-pole"], new Int2(x + w, y), Dir.North);
        p.CreateBuilding(c.Buildings["solar-panel"], new Int2(x + w + 1, y), Dir.North);
        p.CreateBuilding(c.Buildings["solar-panel"], new Int2(x + w + 1, y + 2), Dir.North);
        p.CreateBuilding(c.Buildings["solar-panel"], new Int2(x + w + 3, y), Dir.North);
        p.CreateBuilding(c.Buildings["solar-panel"], new Int2(x + w + 3, y + 2), Dir.North);

        return s;
    }

    /// <summary>A pole and solar panels around a spot, powering sorters within the pole's reach.</summary>
    private static void Power(TestWorld tw, int x, int y)
    {
        tw.Build("power-pole", x + 2, y);
        tw.Build("solar-panel", x, y + 1);
        tw.Build("solar-panel", x + 3, y + 1);
    }

    [Test]
    public async Task Belt_CarriesItemsBetweenSorters()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock("conveyor-belt");
        Building from = tw.Build("chest", 10, 10);
        tw.Build("sorter", 11, 10, Dir.East);
        for (int x = 12; x <= 17; x++)
        {
            tw.Build("conveyor-belt", x, 10, Dir.East);
        }

        tw.Build("sorter", 18, 10, Dir.East);
        Building to = tw.Build("chest", 19, 10);
        Power(tw, 10, 12);
        Power(tw, 17, 12);
        long before = Total(tw.W, "siderite-plate");
        from.Inv.AddUnchecked("siderite-plate", 20);
        tw.Run(60 * 30);
        await Assert.That(to.Inv.Count("siderite-plate")).IsEqualTo(20);
        await Assert.That(Total(tw.W, "siderite-plate")).IsEqualTo(before + 20);
    }

    [Test]
    public async Task Belt_BacksUpWithoutLosingItems()
    {
        TestWorld tw = new TestWorld();
        Building from = tw.Build("chest", 10, 10);
        tw.Build("sorter", 11, 10, Dir.East);
        Building b1 = tw.Build("conveyor-belt", 12, 10, Dir.East);
        Building b2 = tw.Build("conveyor-belt", 13, 10, Dir.East);
        Power(tw, 10, 12);
        long before = Total(tw.W, "cupral-bar");
        from.Inv.AddUnchecked("cupral-bar", 30);
        tw.Run(60 * 30);
        await Assert.That(b1.Lane.Count + b2.Lane.Count).IsEqualTo(8);
        await Assert.That(b2.Inv.Count("cupral-bar")).IsEqualTo(b2.Lane.Count);
        await Assert.That(Total(tw.W, "cupral-bar")).IsEqualTo(before + 30);
        // Items keep their spacing.
        await Assert.That(b2.Lane.Zip(b2.Lane.Skip(1)).All(p => p.First.Pos - p.Second.Pos >= Planet.BeltGap)).IsTrue();
    }

    [Test]
    public async Task Belt_IsWalkable()
    {
        TestWorld tw = new TestWorld();
        tw.Build("conveyor-belt", 20, 20, Dir.East);
        await Assert.That(tw.P.IsWalkable(new Int2(20, 20))).IsTrue();
        await Assert.That(tw.P.IsBlockedForPerson(Fixed.TileCenter(new Int2(20, 20)))).IsFalse();
    }

    [Test]
    public async Task LogisticsStations_DroneSuppliesToDemand()
    {
        TestWorld tw = new TestWorld();
        Building supply = PoweredStation(tw.P, "logistics-station", 10, 10);
        Building demand = PoweredStation(tw.P, "logistics-station", 40, 30);
        tw.Do(new SetStationSlot(supply.Id, 0, "siderite-plate", StationMode.Supply));
        tw.Do(new SetStationSlot(demand.Id, 0, "siderite-plate", StationMode.Demand));
        long before = Total(tw.W, "siderite-plate");
        supply.Inv.AddUnchecked("siderite-plate", 120);
        tw.Run(60 * 30);
        await Assert.That(demand.Inv.Count("siderite-plate")).IsEqualTo(120);
        await Assert.That(Total(tw.W, "siderite-plate")).IsEqualTo(before + 120);
        tw.Run(60 * 30);
        await Assert.That(tw.P.Shipments.Count).IsEqualTo(0).Because("every drone is home");
    }

    [Test]
    public async Task LogisticsStation_DemandStopsAtItsCap()
    {
        TestWorld tw = new TestWorld();
        Building supply = PoweredStation(tw.P, "logistics-station", 10, 10);
        Building demand = PoweredStation(tw.P, "logistics-station", 30, 10);
        tw.Do(new SetStationSlot(supply.Id, 0, "regolith", StationMode.Supply));
        tw.Do(new SetStationSlot(demand.Id, 0, "regolith", StationMode.Demand));
        long before = Total(tw.W, "regolith");
        supply.Inv.AddUnchecked("regolith", 1400);
        tw.Run(60 * 120);
        await Assert.That(demand.Inv.Count("regolith")).IsEqualTo(demand.Def.ItemCap);
        await Assert.That(Total(tw.W, "regolith")).IsEqualTo(before + 1400);
    }

    [Test]
    public async Task OrbitalStations_ShipBetweenPlanets()
    {
        TestWorld tw = new TestWorld();
        Planet rime = tw.W.Planets.First(p => p.Def.Id == "rime");
        Building home = PoweredStation(tw.P, "orbital-station", 10, 10);
        Building away = PoweredStation(rime, "orbital-station", 20, 20);
        tw.Do(new SetStationSlot(home.Id, 0, "logic-board", StationMode.Supply));
        tw.Do(new SetStationSlot(away.Id, 0, "logic-board", StationMode.Demand));
        long before = Total(tw.W, "logic-board");
        home.Inv.AddUnchecked("logic-board", 300);
        tw.Run(60 * 5);
        await Assert.That(tw.P.Shipments.Any(s => s.Vessel)).IsTrue();
        await Assert.That(Total(tw.W, "logic-board")).IsEqualTo(before + 300);
        tw.Run(60 * 120);
        await Assert.That(away.Inv.Count("logic-board")).IsEqualTo(300);
    }

    [Test]
    public async Task Shipment_ToADismantledStation_GoesBack()
    {
        TestWorld tw = new TestWorld();
        Building supply = PoweredStation(tw.P, "logistics-station", 10, 10);
        Building demand = PoweredStation(tw.P, "logistics-station", 60, 60);
        tw.Do(new SetStationSlot(supply.Id, 0, "drive-cog", StationMode.Supply));
        tw.Do(new SetStationSlot(demand.Id, 0, "drive-cog", StationMode.Demand));
        supply.Inv.AddUnchecked("drive-cog", 25);
        tw.Run(60);
        await Assert.That(tw.P.Shipments.Sum(s => s.Count)).IsEqualTo(25);
        tw.P.RemoveBuilding(demand);
        tw.Run(60 * 30);
        await Assert.That(supply.Inv.Count("drive-cog")).IsEqualTo(25);
    }

    [Test]
    public async Task SetStationSlot_RejectsTheSameItemTwice()
    {
        TestWorld tw = new TestWorld();
        Building s = tw.Build("logistics-station", 10, 10);
        tw.Do(new SetStationSlot(s.Id, 0, "biomass", StationMode.Supply));
        await Assert.That(tw.Try(new SetStationSlot(s.Id, 1, "biomass", StationMode.Demand))).IsNotNull();
        await Assert.That(tw.Try(new SetStationSlot(s.Id, 9, "regolith", StationMode.Demand))).IsNotNull();
    }
}
