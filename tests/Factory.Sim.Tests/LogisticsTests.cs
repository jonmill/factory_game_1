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
    public async Task CrateLimit_CapsWhatACrateTakes_AndLifts()
    {
        TestWorld tw = new TestWorld();
        Building crate = tw.Build("chest", tw.Lander.Origin.X + 6, tw.Lander.Origin.Y);
        int stack = TestWorld.Content.Stack("siderite-plate");
        tw.Do(new SetCrateLimit(crate.Id, 1));
        await Assert.That(tw.P.AcceptAmount(crate, "siderite-plate")).IsEqualTo(stack);
        crate.Inv.AddUnchecked("siderite-plate", stack);
        await Assert.That(tw.P.AcceptAmount(crate, "drive-cog")).IsEqualTo(0);
        tw.Do(new SetCrateLimit(crate.Id, 0));
        await Assert.That(crate.Inv.Slots).IsEqualTo(crate.Def.Slots);
        tw.Do(new SetCrateLimit(crate.Id, 0, "siderite-plate"));
        await Assert.That(tw.P.AcceptAmount(crate, "biomass")).IsEqualTo(0);
        await Assert.That(tw.P.AcceptAmount(crate, "siderite-plate")).IsGreaterThan(0);
        World loaded = WorldState.Restore(tw.W.Content, tw.W.Settings, WorldState.Capture(tw.W));
        await Assert.That(loaded.Planets[0].Buildings.First(b => b.Id == crate.Id).Inv.Filter).IsEqualTo("siderite-plate");
        await Assert.That(tw.Try(new SetCrateLimit(tw.Lander.Id, 1))).IsNotNull();
        await Assert.That(tw.Try(new SetCrateLimit(crate.Id, crate.Def.Slots + 1))).IsNotNull();
    }

    [Test]
    public async Task BeltLine_FeedsItsFabricatorsFromSourceCrates()
    {
        // The bot's belt line, built as laid out, with solar panels off to the west: what is in the source
        // crates comes out of the output crate as torque actuators (three inputs: one through a long arm),
        // with no helper involved.
        TestWorld tw = new TestWorld();
        tw.Unlock("torque-actuator");
        Int2 o = tw.Lander.Origin + new Int2(14, 6);
        Factory.Playtest.Bots.BeltLine line = new Factory.Playtest.Bots.BeltLine("torque-actuator", o, ["cupral-filament", "drive-cog", "siderite-plate"], 2, 3);
        foreach ((string id, Int2 at, Dir rot) in line.Parts("assembler", "sorter", "long-sorter", "power-pole"))
        {
            Building b = tw.P.CreateBuilding(TestWorld.Content.Buildings[id], at, rot);
            if (b.Def.Kind == BuildingKind.Assembler)
            {
                tw.Do(new SetRecipe(b.Id, "torque-actuator"));
            }
        }

        foreach (int px in new[] { -3, -8 })
        {
            tw.Build("power-pole", o.X + px, o.Y + 3);
            foreach (Int2 d in new[] { new Int2(-2, 0), new Int2(0, 0), new Int2(-2, 4), new Int2(0, 4) })
            {
                tw.Build("solar-panel", o.X + px + d.X, o.Y + d.Y);
            }
        }

        // Far more filament than the rest, as a player tends to load it: on one mixed belt the spare
        // filament would jam the cogs and plates out; on its own belt it just backs up.
        tw.P.BuildingAt(line.SourceCrate(0))!.Inv.AddUnchecked("cupral-filament", 200);
        tw.P.BuildingAt(line.SourceCrate(1))!.Inv.AddUnchecked("drive-cog", 25);
        tw.P.BuildingAt(line.SourceCrate(2))!.Inv.AddUnchecked("siderite-plate", 25);
        tw.Run(60 * 180);
        Building output = tw.P.BuildingAt(line.OutputCrate)!;
        // A few items stay on the belts past the last arm that wants them: the line's working stock.
        await Assert.That(output.Inv.Count("torque-actuator")).IsGreaterThanOrEqualTo(20);
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
    public async Task LogisticsStation_DemandStopsAtTheSlotLimit()
    {
        TestWorld tw = new TestWorld();
        Building supply = PoweredStation(tw.P, "logistics-station", 10, 10);
        Building demand = PoweredStation(tw.P, "logistics-station", 30, 10);
        tw.Do(new SetStationSlot(supply.Id, 0, "regolith", StationMode.Supply));
        tw.Do(new SetStationSlot(demand.Id, 0, "regolith", StationMode.Demand, 300));
        supply.Inv.AddUnchecked("regolith", 1400);
        tw.Run(60 * 120);
        await Assert.That(demand.Inv.Count("regolith")).IsEqualTo(300);
        await Assert.That(supply.Inv.Count("regolith")).IsEqualTo(1100);
        await Assert.That(tw.Try(new SetStationSlot(demand.Id, 0, "regolith", StationMode.Demand, -1))).IsNotNull();
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
