namespace Factory.Sim.Tests;

public class CollisionTests
{
    /// <summary>A clear spot well away from the lander, with the commander standing on a tile centre.</summary>
    private static (TestWorld Tw, Int2 Tile) Clearing()
    {
        TestWorld tw = new TestWorld();
        Int2 tile = tw.Lander.Origin + new Int2(-20, 12);
        tw.Leader.Pos = Fixed.TileCenter(tile);

        return (tw, tile);
    }

    private static void Walk(TestWorld tw, int dx, int dy, int ticks)
    {
        tw.Do(new SetMoveIntent(tw.Leader.Id, dx, dy));
        tw.Run(ticks);
        tw.Do(new SetMoveIntent(tw.Leader.Id, 0, 0));
    }

    [Test]
    public async Task Commander_StopsAtABuilding()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        Building chest = tw.Build("chest", tile.X + 3, tile.Y);
        Walk(tw, 1, 0, 120);
        int chestLeft = (chest.Origin.X * Fixed.One) + 154;
        await Assert.That(tw.Leader.Pos.X).IsEqualTo(chestLeft - Planet.PersonHalf);
        await Assert.That(tw.Leader.Pos.Y).IsEqualTo(Fixed.TileCenter(tile).Y);
    }

    [Test]
    public async Task Commander_SlidesAlongAWall()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        for (int i = -6; i <= 6; i++)
        {
            tw.Build("chest", tile.X + 2, tile.Y + i);
        }

        int startY = tw.Leader.Pos.Y;
        Walk(tw, 1, 1, 60);
        await Assert.That(tw.Leader.Pos.X).IsEqualTo(((tile.X + 2) * Fixed.One) + 154 - Planet.PersonHalf);
        await Assert.That(tw.Leader.Pos.Y > startY + Fixed.One).IsTrue().Because("diagonal movement keeps going along the wall");
    }

    [Test]
    public async Task Commander_CannotWalkIntoWater()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        tw.Water(tile.X, tile.Y - 4, 1, 1);
        tw.Water(tile.X - 2, tile.Y - 3, 5, 1);
        Walk(tw, 0, -1, 120);
        await Assert.That(tw.Leader.Pos.Y).IsEqualTo(((tile.Y - 2) * Fixed.One) + Planet.PersonHalf);
    }

    [Test]
    public async Task Commander_SqueezesThroughAOneTileGapButNotBetweenTouchingBuildings()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        // A gap one tile wide between two crates is walkable...
        tw.Build("chest", tile.X - 1, tile.Y - 3);
        tw.Build("chest", tile.X + 1, tile.Y - 3);
        Walk(tw, 0, -1, 120);
        await Assert.That(tw.Leader.Pos.Y < ((tile.Y - 3) * Fixed.One) - Fixed.One).IsTrue().Because("walked through the gap");

        // ...but two crates side by side are a wall.
        (TestWorld tw2, Int2 tile2) = Clearing();
        tw2.Build("chest", tile2.X, tile2.Y - 3);
        tw2.Build("chest", tile2.X + 1, tile2.Y - 3);
        tw2.Leader.Pos += new Int2(Fixed.Half, 0);
        Walk(tw2, 0, -1, 120);
        await Assert.That(tw2.Leader.Pos.Y).IsEqualTo(((tile2.Y - 2) * Fixed.One) - 154 + Planet.PersonHalf);
    }

    [Test]
    public async Task Commander_WalksOutOfABuildingFinishedOnTopOfThem()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        tw.Build("burner-smelter", tile.X, tile.Y);
        Walk(tw, -1, 0, 120);
        await Assert.That(tw.P.IsBlockedForPerson(tw.Leader.Pos)).IsFalse();
        await Assert.That(tw.Leader.Pos.X < (tile.X * Fixed.One) - Fixed.One).IsTrue();
    }

    [Test]
    public async Task Commander_WalksThroughGhostsRailAndOre()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        tw.Do(new PlaceBuilding("burner-smelter", new Int2(tile.X + 2, tile.Y - 1), Dir.North));
        tw.Rail(tile.X + 5, tile.Y - 3, 6, Dir.South);
        tw.Ore("siderite", tile.X + 7, tile.Y - 2, 3, 3);
        Walk(tw, 1, 0, 150);
        await Assert.That(tw.Leader.Pos.X > (tile.X + 10) * Fixed.One).IsTrue();
    }

    [Test]
    public async Task Commander_BumpsIntoTreeTrunks()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        Int2 tree = new Int2(tile.X + 3, tile.Y);
        tw.P.SetResource(tree, TestWorld.Content.Resources["xylo"], 10);
        Int2 trunk = (tree * Fixed.One) + Planet.TreeTrunkOffset(tree);
        tw.Leader.Pos = new Int2(tw.Leader.Pos.X, trunk.Y);
        Walk(tw, 1, 0, 120);
        await Assert.That(tw.Leader.Pos.X).IsEqualTo(trunk.X - Planet.TrunkHalf - Planet.PersonHalf);
    }

    [Test]
    public async Task Commander_StillReachesTreesToChopThem()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        Int2 tree = new Int2(tile.X + 6, tile.Y + 1);
        tw.P.SetResource(tree, TestWorld.Content.Resources["xylo"], 3);
        tw.Do(new MineTile(tw.Leader.Id, tree));
        tw.RunUntil(() => tw.Leader.Carry.Count("biomass") == 3, 60 * 30, "the tree chopped down");
    }

    [Test]
    public async Task FinishedBuilding_PushesSomeoneStandingOnItOut()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        tw.Build("chest", tile.X, tile.Y);
        tw.P.PushOutOfSolids();
        await Assert.That(tw.P.IsBlockedForPerson(tw.Leader.Pos)).IsFalse();
        await Assert.That(tw.Leader.Tile).IsNotEqualTo(tile);
    }

    [Test]
    public async Task FinishedBuilding_ThatWallsSomeoneIn_MovesThemOut()
    {
        (TestWorld tw, Int2 tile) = Clearing();
        tw.Water(tile.X - 1, tile.Y + 1, 3, 1);
        tw.Water(tile.X - 1, tile.Y, 1, 1);
        tw.Water(tile.X + 1, tile.Y, 1, 1);
        tw.Build("chest", tile.X, tile.Y - 1);
        tw.P.PushOutOfSolids();
        await Assert.That(tw.Leader.Tile).IsNotEqualTo(tile).Because("the commander was sealed in a one-tile pocket");
        await Assert.That(tw.P.IsBlockedForPerson(tw.Leader.Pos)).IsFalse();
    }
}
