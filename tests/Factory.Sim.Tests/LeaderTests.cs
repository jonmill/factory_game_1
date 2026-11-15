namespace Factory.Sim.Tests;

/// <summary>The commander's own hand-mining and pockets.</summary>
public class LeaderTests
{
    [Test]
    public async Task Leader_WalksToDistantOreAndMinesIt()
    {
        TestWorld tw = new TestWorld();
        Int2 tile = tw.Leader.Tile + new Int2(12, 0);
        tw.Ore("siderite", tile.X, tile.Y, 1, 1, 100);

        tw.Do(new MineTile(tw.Leader.Id, tile));
        tw.RunUntil(() => tw.Leader.Carry.Count("siderite-ore") >= 3, 60 * 20, "leader mined the distant ore");

        long reach = (long)Planet.LeaderMineReachTiles * Fixed.One;
        await Assert.That(tw.P.SubTorus.DistSq(tw.Leader.Pos, Fixed.TileCenter(tile)) <= reach * reach).IsTrue();
    }

    [Test]
    public async Task Leader_MovesOnThroughTheDepositWhenATileRunsOut()
    {
        TestWorld tw = new TestWorld();
        Int2 tile = tw.Leader.Tile + new Int2(2, 0);
        tw.Ore("regolith", tile.X, tile.Y, 3, 1, 2);

        tw.Do(new MineTile(tw.Leader.Id, tile));
        tw.RunUntil(() => tw.Leader.MiningTile is null, 60 * 30, "deposit mined out");

        await Assert.That(tw.Leader.Carry.Count("regolith")).IsEqualTo(6);
        await Assert.That(tw.Leader.Status).IsEqualTo("Deposit exhausted");
    }

    [Test]
    public async Task Leader_WalkingStopsMining()
    {
        TestWorld tw = new TestWorld();
        Int2 tile = tw.Leader.Tile + new Int2(1, 0);
        tw.Ore("regolith", tile.X, tile.Y, 1, 1, 100);
        tw.Do(new MineTile(tw.Leader.Id, tile));
        tw.Run(30);

        tw.Do(new SetMoveIntent(tw.Leader.Id, 0, 1));

        await Assert.That(tw.Leader.MiningTile).IsNull();
    }

    [Test]
    public async Task Leader_PocketsSupplyNearbyConstruction()
    {
        TestWorld tw = new TestWorld();
        Int2 at = tw.Leader.Tile + new Int2(3, 3);
        tw.Leader.Carry.Add("biomass", 10);
        tw.Do(new PlaceBuilding("chest", at, Dir.North));
        ConstructionSite site = tw.P.Sites.Single();

        await Assert.That(site.MaterialsComplete).IsTrue();
        await Assert.That(tw.Leader.Carry.Count("biomass")).IsEqualTo(6);
    }

    [Test]
    public async Task Leader_PocketsFeedTheLandersCraftQueue()
    {
        TestWorld tw = new TestWorld();
        await Assert.That(tw.P.InLeaderReach(tw.Leader, tw.Lander.Origin)).IsTrue();
        tw.Lander.Inv.Remove("siderite-plate", tw.Lander.Inv.Count("siderite-plate"));
        int gears = tw.Lander.Inv.Count("drive-cog") + tw.Lander.Out.Count("drive-cog");
        tw.Leader.Carry.Add("siderite-plate", 4);

        tw.Do(new QueueCraft(tw.Lander.Id, "drive-cog", 2));
        tw.RunUntil(() => tw.Lander.CraftQueue.Count == 0, 60 * 10, "gears crafted from pocket plates");

        await Assert.That(tw.Leader.Carry.Count("siderite-plate")).IsEqualTo(0);
        await Assert.That(tw.Lander.Inv.Count("drive-cog") + tw.Lander.Out.Count("drive-cog")).IsEqualTo(gears + 2);
    }

    [Test]
    public async Task Leader_PocketsHoldTwentyStacks()
    {
        TestWorld tw = new TestWorld();

        await Assert.That(tw.Leader.Carry.Slots).IsEqualTo(20);
    }
}
