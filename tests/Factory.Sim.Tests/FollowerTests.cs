namespace Factory.Sim.Tests;

public class FollowerTests
{
    [Test]
    public async Task Gatherer_MinesAndDeliversToLander()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Ore("iron", l.X + 8, l.Y, 3, 3, 1000);
        int before = tw.Lander.Inv.Count("iron-ore");
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Gather, Resource: "iron")));
        tw.RunUntil(() => tw.Lander.Inv.Count("iron-ore") >= before + 25, 60 * 60, "a batch of iron ore");
    }

    [Test]
    public async Task WoodGathering_ClearsTrees()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Ore("tree", l.X + 6, l.Y, 1, 1, 3);
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Gather, Resource: "tree")));
        tw.RunUntil(() => tw.P.ResourceAt(new Int2(l.X + 6, l.Y)) is null, 60 * 20, "tree to be cut down");
        await Assert.That(tw.P.IsAreaFree(new Int2(l.X + 6, l.Y), 1, 1)).IsTrue();
    }

    [Test]
    public async Task Builders_ConstructGhostsFromLanderMaterials()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        int stone = tw.Lander.Inv.Count("stone");
        tw.Do(new PlaceBuilding("stone-furnace", new Int2(l.X + 6, l.Y), Dir.North));
        tw.Do(new PlaceBuilding("chest", new Int2(l.X - 4, l.Y), Dir.North));
        foreach (Unit f in tw.Followers)
        {
            tw.Equip(f, "construction-module");
            tw.Do(new AssignJob(f.Id, new Job(JobKind.Build)));
        }

        tw.RunUntil(() => tw.P.Sites.Count == 0, 60 * 60, "construction to finish");
        await Assert.That(tw.P.Buildings.Any(b => b.Def.Id == "stone-furnace")).IsTrue();
        await Assert.That(tw.P.Buildings.Any(b => b.Def.Id == "chest")).IsTrue();
        await Assert.That(tw.Lander.Inv.Count("stone")).IsEqualTo(stone - 5);
    }

    [Test]
    public async Task Builders_WaitWhenMaterialsAreMissing()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("stone", 1000);
        tw.Do(new PlaceBuilding("stone-furnace", new Int2(tw.Lander.Origin.X + 6, tw.Lander.Origin.Y), Dir.North));
        tw.Equip(tw.Followers[0], "construction-module");
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Build)));
        tw.Run(600);
        await Assert.That(tw.P.Sites).HasSingleItem();
        await Assert.That(tw.Followers[0].Status).IsEqualTo("Waiting for materials: Stone Furnace needs 5 Stone");
        tw.Lander.Inv.Add("stone", 5);
        tw.RunUntil(() => tw.P.Sites.Count == 0, 60 * 30, "furnace once stone arrives");
    }

    [Test]
    public async Task Builders_FetchFromCommanderPockets()
    {
        // Two furnaces out of the commander's own reach, one already part-supplied; the only stone
        // left is in the commander's pockets, so the builder has to come and take it.
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Lander.Inv.Remove("stone", 1000);
        tw.Lander.Inv.Add("stone", 2);
        tw.Do(new PlaceBuilding("stone-furnace", new Int2(l.X + 20, l.Y), Dir.North));
        tw.Do(new PlaceBuilding("stone-furnace", new Int2(l.X + 24, l.Y), Dir.North));
        tw.Equip(tw.Followers[0], "construction-module");
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Build)));
        tw.RunUntil(() => tw.P.Sites.Any(s => s.Delivered.Count("stone") == 2), 60 * 30, "first furnace part-supplied");
        tw.Run(60 * 5);
        await Assert.That(tw.Followers[0].Status).StartsWith("Waiting for materials");

        tw.Leader.Carry.Add("stone", 8);
        tw.RunUntil(() => tw.P.Sites.Count == 0, 60 * 60, "both furnaces built from the commander's stone");
        await Assert.That(tw.Leader.Carry.Count("stone")).IsEqualTo(0);
    }

    [Test]
    public async Task Deconstruction_RefundsMaterials()
    {
        TestWorld tw = new TestWorld();
        Building chest = tw.Build("chest", tw.Lander.Origin.X + 5, tw.Lander.Origin.Y);
        chest.Inv.Add("coal", 7);
        int wood = tw.Lander.Inv.Count("wood"), coal = tw.Lander.Inv.Count("coal");
        tw.Leader.Pos += new Int2(Fixed.One * 30, 0); // out of the commander's own build reach
        tw.Do(new DeconstructEntity(chest.Id));
        tw.Equip(tw.Followers[0], "construction-module");
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Build)));
        tw.RunUntil(() => (tw.W.Get<Building>(chest.Id) is null) && tw.Followers[0].Carry.IsEmpty, 60 * 30, "chest dismantled and stored");
        await Assert.That(tw.Lander.Inv.Count("wood")).IsEqualTo(wood + 4);
        await Assert.That(tw.Lander.Inv.Count("coal")).IsEqualTo(coal + 7);
    }

    [Test]
    public async Task Hauler_MovesFurnaceOutputToChest()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building furnace = tw.Build("stone-furnace", l.X + 6, l.Y);
        Building chest = tw.Build("chest", l.X + 12, l.Y);
        furnace.Out.Add("iron-plate", 30);
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Haul, FromId: furnace.Id, ToId: chest.Id)));
        tw.RunUntil(() => chest.Inv.Count("iron-plate") == 30, 60 * 30, "plates hauled");
    }

    [Test]
    public async Task Leader_DepositsOneItemByTheStack()
    {
        TestWorld tw = new TestWorld();
        Building engine = tw.Build("steam-engine", tw.Leader.Tile.X + 2, tw.Leader.Tile.Y);
        tw.Leader.Carry.Add("wood", 30);
        tw.Leader.Carry.Add("stone", 10);
        tw.Do(new DepositToBuilding(tw.Leader.Id, engine.Id, "wood", 5));
        await Assert.That(engine.Inv.Count("wood")).IsEqualTo(5);
        await Assert.That(tw.Leader.Carry.Count("wood")).IsEqualTo(25);
        await Assert.That(tw.Leader.Carry.Count("stone")).IsEqualTo(10);
        await Assert.That(tw.Try(new DepositToBuilding(tw.Leader.Id, engine.Id, "wood", 0))).IsNotNull();
    }

    [Test]
    public async Task Leader_TakesFromTheChosenPart()
    {
        TestWorld tw = new TestWorld();
        Building furnace = tw.Build("stone-furnace", tw.Leader.Tile.X + 2, tw.Leader.Tile.Y);
        furnace.Inv.Add("wood", 4);
        furnace.Out.Add("iron-plate", 7);
        tw.Do(new TakeFromBuilding(tw.Leader.Id, furnace.Id, "wood", 3, InventoryPart.Input));
        await Assert.That(tw.Leader.Carry.Count("wood")).IsEqualTo(3);
        tw.Do(new TakeFromBuilding(tw.Leader.Id, furnace.Id, Part: InventoryPart.Output));
        await Assert.That(tw.Leader.Carry.Count("iron-plate")).IsEqualTo(7);
        await Assert.That(furnace.Inv.Count("wood")).IsEqualTo(1);
    }

    [Test]
    public async Task Leader_HandMinesAndDeposits()
    {
        TestWorld tw = new TestWorld();
        Int2 tile = tw.Leader.Tile + new Int2(1, 0);
        tw.Ore("stone", tile.X, tile.Y, 1, 1, 100);
        tw.Do(new MineTile(tw.Leader.Id, tile));
        tw.Run(60 * 5);
        int mined = tw.Leader.Carry.Count("stone");
        await Assert.That(mined >= 6).IsTrue().Because($"mined {mined}");
        int before = tw.Lander.Inv.Count("stone");
        tw.Do(new DepositToBuilding(tw.Leader.Id, tw.Lander.Id));
        await Assert.That(tw.Lander.Inv.Count("stone") >= before + mined).IsTrue();
    }
}
