using Factory.Playtest.Bots;

namespace Factory.Sim.Tests;

/// <summary>
/// Recovery behaviours of the reference bot. Each one is a way the softlock experiment found the bot
/// stuck in a messy base that a sensible player would have untangled.
/// </summary>
public class GoalBotTests
{
    [Test]
    public async Task GoalBot_ClearsCraftQueueWhenNothingQueuedCanBeCrafted()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("copper-wire", 1000);
        tw.Lander.Inv.Remove("copper-plate", 1000);
        tw.Do(new QueueCraft(tw.Lander.Id, "basic-circuit", 5));
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-gear:200"));

        Think(tw, bot);

        await Assert.That(tw.Lander.CraftQueue).IsEmpty();
        await Assert.That(bot.LastPlan!.Problems.Any(p => p.Contains("stalled craft queue"))).IsTrue();
    }

    [Test]
    public async Task GoalBot_KeepsCraftQueueWhenAnEntryCanStillBeCrafted()
    {
        TestWorld tw = new TestWorld();
        tw.Lander.Inv.Remove("copper-wire", 1000);
        tw.Lander.Inv.Remove("copper-plate", 1000);
        tw.Do(new QueueCraft(tw.Lander.Id, "basic-circuit", 2));
        tw.Do(new QueueCraft(tw.Lander.Id, "iron-gear", 1));
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-gear:200"));

        Think(tw, bot);

        await Assert.That(tw.Lander.CraftQueue).Contains("basic-circuit");
    }

    [Test]
    public async Task GoalBot_FuelsFurnaceThatAlreadyHoldsEnoughOre()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building furnace = tw.Build("stone-furnace", l.X + 6, l.Y);
        furnace.Inv.AddUnchecked("iron-ore", 10);
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-plate:500"));

        Think(tw, bot);

        bool fuelHaul = bot.LastPlan!.Needs.Any(j => (j.Kind == JobKind.Haul) && (j.ToId == furnace.Id) && (j.Item is not null) && tw.W.Content.IsFuel(j.Item));
        await Assert.That(fuelHaul).IsTrue();
    }

    [Test]
    public async Task GoalBot_HaulsFuelFromTheStorageThatHoldsIt()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Lander.Inv.Remove("wood", 1000);
        tw.Lander.Inv.Remove("coal", 1000);
        Building chest = tw.Build("chest", l.X - 6, l.Y);
        chest.Inv.AddUnchecked("wood", 50);
        Building furnace = tw.Build("stone-furnace", l.X + 6, l.Y);
        furnace.Inv.AddUnchecked("iron-ore", 10);
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-plate:500"));

        Think(tw, bot);

        Job? fuelHaul = bot.LastPlan!.Needs.FirstOrDefault(j => (j.Kind == JobKind.Haul) && (j.ToId == furnace.Id) && (j.Item == "wood"));
        await Assert.That(fuelHaul).IsNotNull();
        await Assert.That(fuelHaul!.FromId).IsEqualTo(chest.Id);
    }

    [Test]
    public async Task GoalBot_FetchesCraftInputsFromOtherStorageBackToTheLander()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        tw.Lander.Inv.Remove("iron-plate", 1000);
        tw.Lander.Inv.Remove("iron-gear", 1000);
        Building chest = tw.Build("chest", l.X - 6, l.Y);
        chest.Inv.AddUnchecked("iron-plate", 60);
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-gear:20"));

        Think(tw, bot);

        bool consolidate = bot.LastPlan!.Needs.Any(j => (j.Kind == JobKind.Haul) && (j.FromId == chest.Id) && (j.ToId == tw.Lander.Id) && (j.Item == "iron-plate"));
        await Assert.That(consolidate).IsTrue();
    }

    [Test]
    public async Task GoalBot_LetsAFollowerFinishADeliveryInProgress()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building chest = tw.Build("chest", l.X - 10, l.Y);
        chest.Inv.AddUnchecked("stone", 30);
        Unit f = tw.Followers[0];
        Job unneeded = new Job(JobKind.Haul, FromId: chest.Id, ToId: tw.Lander.Id, Item: "stone");
        tw.Do(new AssignJob(f.Id, unneeded));
        tw.RunUntil(() => (f.Phase == UnitPhase.Deliver) && (f.Carry.IsEmpty == false), 60 * 30, "follower to pick up stone");
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-gear:200"));

        Think(tw, bot);

        await Assert.That(f.Job).IsEqualTo(unneeded);
    }

    [Test]
    public async Task GoalBot_ReassignsAFollowerOnAnUnneededJobBeforeItPicksAnythingUp()
    {
        TestWorld tw = new TestWorld();
        Int2 l = tw.Lander.Origin;
        Building chest = tw.Build("chest", l.X - 30, l.Y);
        chest.Inv.AddUnchecked("stone", 30);
        Unit f = tw.Followers[0];
        Job unneeded = new Job(JobKind.Haul, FromId: chest.Id, ToId: tw.Lander.Id, Item: "stone");
        tw.Do(new AssignJob(f.Id, unneeded));
        GoalBot bot = new GoalBot(Goal.Parse("item:iron-gear:200"));

        Think(tw, bot);

        await Assert.That(f.Job).IsNotEqualTo(unneeded);
    }

    /// <summary>Advances to the bot's next think tick, lets it plan, then applies the commands it sent.</summary>
    private static void Think(TestWorld tw, GoalBot bot)
    {
        while ((tw.W.Tick % bot.Params.ThinkInterval) != 0)
        {
            tw.W.Step();
        }

        bot.Tick(tw.W);
        tw.W.Step();
    }

    [Test]
    public async Task GoalBot_PlaysTheWholeGame_AndCallsTheSettlers()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 1 });
        GoalBot bot = new GoalBot(new SettlersGoal());
        for (int i = 0; (i < 60 * 60 * 120) && (w.SettlersCalled == false); i++)
        {
            bot.Tick(w);
            w.Step();
        }

        await Assert.That(w.SettlersCalled).IsTrue();
        await Assert.That(w.AllPlanetsReady).IsTrue();
    }

    [Test]
    public async Task GoalBot_FitsHelpersWithConstructionModules()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 2 });
        GoalBot bot = new GoalBot(new HelpersGoal(1));
        Planet p = w.Planets[0];
        for (int i = 0; (i < 60 * 60 * 60) && (w.HelpersBuilt == 0); i++)
        {
            bot.Tick(w);
            w.Step();
        }

        await Assert.That(w.HelpersBuilt).IsEqualTo(1);
        await Assert.That(p.Units.Count(u => (u.IsLeader == false) && p.CanTakeJob(u, JobKind.Build))).IsGreaterThanOrEqualTo(1);
    }
}
