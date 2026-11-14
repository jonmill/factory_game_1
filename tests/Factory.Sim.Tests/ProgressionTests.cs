
namespace Factory.Sim.Tests;

public class ProgressionTests
{
    [Test]
    public async Task ColonyBeacon_SummonsANewFollower()
    {
        TestWorld tw = new TestWorld();
        tw.Water(9, 10, 1, 2);
        Building engine = tw.Build("steam-engine", 10, 10);
        engine.Inv.Add("coal", 10);
        engine.Inv.AddUnchecked("coal", 200);
        tw.Build("power-pole", 12, 10);
        tw.Unlock("colony-beacon", "summon-colonist");
        Building beacon = tw.Build("colony-beacon", 13, 9);
        beacon.Inv.AddUnchecked("basic-circuit", 20);
        beacon.Inv.AddUnchecked("battery-cell", 10);
        beacon.Inv.AddUnchecked("iron-gear", 20);
        int before = tw.Followers.Count;
        tw.RunUntil(() => tw.Followers.Count == before + 1, 1800 + 120, "new follower");
        await Assert.That(tw.W.FollowersSummoned).IsEqualTo(1);
        await Assert.That(tw.W.Events.Any(e => e.Message.Contains("answered"))).IsTrue();
    }
}
