namespace Factory.Sim.Tests;

public class PathTests
{
    /// <summary>Steps the world, failing if anyone walks into something solid once they're clear of it.</summary>
    private static void RunWithoutOverlaps(TestWorld tw, int ticks, Func<bool>? until = null)
    {
        HashSet<int> clear = [];
        for (int i = 0; i < ticks; i++)
        {
            if (until?.Invoke() == true)
            {
                return;
            }

            tw.W.Step();
            foreach (Unit u in tw.P.Units.Where(u => u.Boarded == false))
            {
                bool blocked = tw.P.IsBlockedForPerson(u.Pos);
                if (blocked && clear.Contains(u.Id))
                {
                    Assert.Fail($"{u.Name} walked into something solid at tick {tw.W.Tick} ({u.Pos}, {u.Status})");
                }

                if (blocked == false)
                {
                    clear.Add(u.Id);
                }
            }
        }

        if (until is not null)
        {
            Assert.Fail($"Timed out after {ticks} ticks");
        }
    }

    [Test]
    public async Task Hauler_WalksAroundAWall()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building from = tw.Build("chest", l.X - 12, l.Y + 10);
        Building to = tw.Build("chest", l.X - 2, l.Y + 10);
        for (int y = -6; y <= 6; y++)
        {
            tw.Build("chest", l.X - 7, l.Y + 10 + y);
        }

        from.Inv.Add("stone", 40);
        Unit f = tw.Followers[0];
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: from.Id, ToId: to.Id)));
        RunWithoutOverlaps(tw, 60 * 120, () => to.Inv.Count("stone") == 40);
        await Assert.That(to.Inv.Count("stone")).IsEqualTo(40);
    }

    [Test]
    public async Task Gatherer_ReachesTreesInsideAForest()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                tw.P.SetResource(new Int2(l.X + 12 + x, l.Y + y), TestWorld.Content.Resources["tree"], 4);
            }
        }

        int wood = tw.Lander.Inv.Count("wood");
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Gather, Resource: "tree")));
        RunWithoutOverlaps(tw, 60 * 240, () => tw.Lander.Inv.Count("wood") >= wood + 40);
        await Assert.That(tw.Lander.Inv.Count("wood") >= wood + 40).IsTrue();
    }

    [Test]
    public async Task Walker_ReroutesWhenSomethingIsBuiltInTheWay()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building from = tw.Build("chest", l.X - 14, l.Y + 12);
        Building to = tw.Build("chest", l.X - 2, l.Y + 12);
        from.Inv.Add("stone", 40);
        Unit f = tw.Followers[0];
        tw.Do(new AssignJob(f.Id, new Job(JobKind.Haul, FromId: from.Id, ToId: to.Id)));
        RunWithoutOverlaps(tw, 60 * 60, () => f.Carry.Count("stone") > 0);

        // Wall off the way back while the hauler is on its way.
        for (int y = -8; y <= 8; y++)
        {
            tw.Build("chest", l.X - 8, l.Y + 12 + y);
        }

        RunWithoutOverlaps(tw, 60 * 120, () => to.Inv.Count("stone") == 40);
        await Assert.That(to.Inv.Count("stone")).IsEqualTo(40);
    }

    [Test]
    public async Task Path_GoesAroundWaterAndWrapsAroundThePlanet()
    {
        TestWorld tw = new TestWorld();
        tw.Water(10, 0, 1, 90);
        List<Int2> path = tw.P.FindPath(Fixed.TileCenter(new Int2(8, 40)), Fixed.TileCenter(new Int2(12, 40)), 100);
        await Assert.That(path.Count > 0).IsTrue();
        await Assert.That(path.All(t => tw.P.IsWalkable(t))).IsTrue();
        await Assert.That(path.Any(t => (t.Y >= 90) || (t.Y < 2))).IsTrue().Because("the only way past is around the seam");
    }
}
