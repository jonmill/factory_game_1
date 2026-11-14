namespace Factory.Sim.Tests;

public class TrainTests
{
    private static (TestWorld tw, Building a, Building b) Line(int length = 30)
    {
        TestWorld tw = new TestWorld();
        tw.Rail(10, 20, length);
        Building a = tw.Build("rail-stop", 10, 19, Dir.South);           // points at (10,20)
        Building b = tw.Build("rail-stop", 10 + length - 1, 19, Dir.South); // points at end

        return (tw, a, b);
    }

    [Test]
    public async Task HandCart_NeedsAnOperator()
    {
        (TestWorld? tw, Building? a, Building? b) = Line();
        Train t = tw.P.CreateTrain(["hand-cart", "mine-cart"], [new Int2(12, 20), new Int2(11, 20)]);
        tw.Do(new SetSchedule(t.Id, [new ScheduleStop(b.Id, StopAction.Unload), new ScheduleStop(a.Id, StopAction.Load)]));
        tw.Run(30);
        await Assert.That(t.State).IsEqualTo(TrainState.NoOperator);
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.OperateTrain, TrainId: t.Id)));
        tw.RunUntil(() => t.State == TrainState.Moving, 60 * 60, "train moving with operator");
    }

    [Test]
    public async Task HandCart_ShuttlesCargoAndReversesOnADeadEndLine()
    {
        (TestWorld? tw, Building? a, Building? b) = Line();
        a.Inv.Add("iron-ore", 200);
        Train t = tw.P.CreateTrain(["hand-cart", "mine-cart"], [new Int2(12, 20), new Int2(11, 20)]);
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.OperateTrain, TrainId: t.Id)));
        tw.Do(new SetSchedule(t.Id, [new ScheduleStop(a.Id, StopAction.Load), new ScheduleStop(b.Id, StopAction.Unload)]));
        tw.RunUntil(() => b.Inv.Count("iron-ore") > 0, 60 * 120, "first delivery");
        tw.RunUntil(() => b.Inv.Count("iron-ore") == 200, 60 * 300, "all ore delivered");
    }

    [Test]
    public async Task HandCart_WhenOverloaded_CannotMove()
    {
        (TestWorld? tw, Building? a, Building? b) = Line();
        Train t = tw.P.CreateTrain(["hand-cart", "mine-cart", "mine-cart"], [new Int2(13, 20), new Int2(12, 20), new Int2(11, 20)]);
        t.Vehicles[1].Cargo.Add("lead-ore", 400);
        t.Vehicles[2].Cargo.Add("lead-ore", 400);
        tw.Do(new AssignJob(tw.Followers[0].Id, new Job(JobKind.OperateTrain, TrainId: t.Id)));
        tw.Do(new SetSchedule(t.Id, [new ScheduleStop(b.Id, StopAction.Unload)]));
        tw.Run(60 * 20);
        await Assert.That(t.State).IsEqualTo(TrainState.Overloaded);
        await Assert.That(t.Tiles[0]).IsEqualTo(new Int2(13, 20));
    }

    [Test]
    public async Task SteamLocomotive_NeedsWaterAndRefuelsAtStops()
    {
        (TestWorld? tw, Building? a, Building? b) = Line(40);
        Train t = tw.P.CreateTrain(["steam-locomotive", "mine-cart"], [new Int2(12, 20), new Int2(11, 20)]);
        tw.Do(new SetSchedule(t.Id, [new ScheduleStop(b.Id, StopAction.Pass), new ScheduleStop(a.Id, StopAction.Pass)]));
        tw.Run(10);
        await Assert.That(t.State).IsEqualTo(TrainState.NoWater);

        // Water tower next to stop A and coal at the stop.
        tw.Water(8, 17, 1, 1);
        tw.Build("water-tower", 9, 17);
        a.Inv.Add("coal", 30);
        t.Water = 1; // just enough to crawl back? no: start at stop A instead
        tw.Do(new SetSchedule(t.Id, [new ScheduleStop(a.Id, StopAction.Pass), new ScheduleStop(b.Id, StopAction.Pass)]));
        t.Water = 0;
        // Put the train on stop A's tile so it refills there.
        tw.P.Trains.Remove(t);
        tw.W.Unregister(t);
        Train t2 = tw.P.CreateTrain(["steam-locomotive", "mine-cart"], [new Int2(10, 20), new Int2(11, 20)]);
        tw.Do(new SetSchedule(t2.Id, [new ScheduleStop(a.Id, StopAction.Pass), new ScheduleStop(b.Id, StopAction.Pass)]));
        tw.RunUntil(() => (t2.Tiles[0] == b.FrontTile()) && (t2.State == TrainState.AtStop), 60 * 60, "steam train reaches B");
        await Assert.That((t2.Locomotive!.Cargo.Count("coal") > 0) || (t2.FuelJ > 0)).IsTrue();
        await Assert.That(t2.Water < t2.Locomotive.Def.WaterCapacity).IsTrue();
    }

    [Test]
    public async Task Trains_DoNotDriveThroughEachOther()
    {
        (TestWorld? tw, Building? a, Building? b) = Line(30);
        Train t1 = tw.P.CreateTrain(["steam-locomotive"], [new Int2(20, 20)]);
        Train t2 = tw.P.CreateTrain(["steam-locomotive"], [new Int2(15, 20)]);
        t1.Water = t2.Water = 3000;
        t1.Locomotive!.Cargo.Add("coal", 10);
        t2.Locomotive!.Cargo.Add("coal", 10);
        tw.Do(new SetSchedule(t2.Id, [new ScheduleStop(b.Id, StopAction.Pass)])); // t2 must pass through t1's tile
        tw.Run(60 * 10);
        await Assert.That(t2.State).IsEqualTo(TrainState.Blocked);
        await Assert.That(t2.Tiles[0].X < 20).IsTrue();
    }

    [Test]
    public async Task Followers_AssembleATrainFromAGhost()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Rail(l.X - 5, l.Y + 6, 10);
        tw.Do(new PlaceTrain(new Int2(l.X, l.Y + 6), Dir.East, ["hand-cart", "mine-cart"]));
        foreach (Unit f in tw.Followers)
        {
            tw.Do(new AssignJob(f.Id, new Job(JobKind.Build)));
        }

        tw.RunUntil(() => tw.P.Trains.Count == 1, 60 * 60, "train assembled");
        Train t = tw.P.Trains[0];
        await Assert.That(t.Tiles[0]).IsEqualTo(new Int2(l.X, l.Y + 6));
        await Assert.That(t.Tiles[1]).IsEqualTo(new Int2(l.X - 1, l.Y + 6));
    }
}
