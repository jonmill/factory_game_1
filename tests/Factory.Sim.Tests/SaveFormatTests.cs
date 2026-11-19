using System.Text;
using System.Text.Json.Nodes;

namespace Factory.Sim.Tests;

public class SaveFormatTests
{
    private static readonly ulong _contentHash = TestWorld.Content.ComputeHash();

    /// <summary>A short game with commands on several ticks, so playback has something to apply.</summary>
    private static TestWorld PlayedWorld()
    {
        TestWorld tw = new TestWorld(empty: false, seed: 42);
        tw.W.Submit(new AssignJob(tw.Followers[0].Id, new Job(JobKind.Gather, Resource: "siderite")));
        tw.W.Submit(new SetMoveIntent(tw.Leader.Id, 1, 0));
        tw.Run(45);
        tw.W.Submit(new PlaceBuilding("chest", new Int2(tw.Lander.Origin.X + 5, tw.Lander.Origin.Y), Dir.North));
        tw.W.Submit(new QueueCraft(tw.Lander.Id, "drive-cog", 2));
        tw.Run(200);
        tw.W.Submit(new SetMoveIntent(tw.Leader.Id, 0, 1));
        tw.W.Submit(new AssignJob(tw.Followers[1].Id, new Job(JobKind.Gather, Resource: "regolith")));
        tw.Run(300);
        tw.W.Submit(new SetMoveIntent(tw.Leader.Id, 0, 0));
        tw.Run(100);

        return tw;
    }

    private static SaveMeta Meta() => new() { Name = "My base", SavedAt = new DateTimeOffset(2026, 11, 19, 14, 30, 5, TimeSpan.FromHours(-8)), Planet = "Kepler's Rest", PlanetId = 0 };

    private static Replay Header(string json) => Replay.ReadHeader(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Test]
    public async Task SaveFormat_CurrentVersionIsWrittenWithBuildAndLoads()
    {
        TestWorld tw = PlayedWorld();
        Replay replay = Replay.FromJson(Replay.Capture(tw.W, Meta()).ToJson());

        await Assert.That(replay.Version).IsEqualTo(2);
        await Assert.That(replay.Build).IsEqualTo(BuildInfo.Version);
        await Assert.That(string.IsNullOrEmpty(replay.Build)).IsFalse();
        SaveCheck check = replay.Check(_contentHash);
        await Assert.That(check.CanLoad).IsTrue();
        await Assert.That(check.Problem).IsNull();
        await Assert.That(check.Warning).IsNull();
        await Assert.That(replay.Play(TestWorld.Content).Checksum()).IsEqualTo(tw.W.Checksum());
    }

    [Test]
    public async Task SaveFormat_Version1FilesWithoutBuildOrMetaStillLoad()
    {
        TestWorld tw = PlayedWorld();
        // A format 1 file: what the game wrote before this format existed (no Build, no Meta).
        JsonObject v1 = JsonNode.Parse(Replay.Capture(tw.W, Meta()).ToJson())!.AsObject();
        v1.Remove("Build");
        v1.Remove("Meta");
        v1["Version"] = 1;
        string json = v1.ToJsonString();

        Replay header = Header(json);
        await Assert.That(header.Version).IsEqualTo(1);
        await Assert.That(header.Build).IsNull();
        await Assert.That(header.Meta).IsNull();
        await Assert.That(header.Check(_contentHash).CanLoad).IsTrue();

        Replay replay = Replay.FromJson(json);
        await Assert.That(replay.Commands.Count).IsEqualTo(tw.W.CommandLog.Count);
        World again = replay.BeginPlayback(TestWorld.Content).Finish();
        await Assert.That(again.Checksum()).IsEqualTo(replay.FinalChecksum);
        await Assert.That(again.Checksum()).IsEqualTo(tw.W.Checksum());
    }

    [Test]
    public async Task SaveFormat_UnsupportedVersionsAreRefusedWithAReason()
    {
        string json = Replay.Capture(new TestWorld().W).ToJson();
        JsonObject old = JsonNode.Parse(json)!.AsObject();
        old["Version"] = Replay.MinSupportedVersion - 1;
        JsonObject future = JsonNode.Parse(json)!.AsObject();
        future["Version"] = Replay.CurrentVersion + 1;
        // A future format may change commands in ways this build can't parse; the header still reads.
        future["Commands"] = new JsonArray(new JsonObject { ["Tick"] = 0, ["Seq"] = 0, ["Command"] = new JsonObject { ["$cmd"] = "warpDrive" } });

        SaveCheck tooOld = Header(old.ToJsonString()).Check(_contentHash);
        await Assert.That(tooOld.CanLoad).IsFalse();
        await Assert.That(tooOld.Problem).IsEqualTo($"This save is from an older version of the game (format {Replay.MinSupportedVersion - 1}) and can't be loaded.");

        SaveCheck tooNew = Header(future.ToJsonString()).Check(_contentHash);
        await Assert.That(tooNew.CanLoad).IsFalse();
        await Assert.That(tooNew.Problem!).Contains("newer version of the game");
        await Assert.That(tooNew.Problem!).Contains($"format {Replay.CurrentVersion + 1}");
    }

    [Test]
    public async Task SaveFormat_ChangedGameDataWarnsButStillLoads()
    {
        Replay replay = Header(Replay.Capture(new TestWorld().W).ToJson());

        SaveCheck check = replay.Check(_contentHash ^ 1);

        await Assert.That(check.CanLoad).IsTrue();
        await Assert.That(check.Warning!).Contains("game data has changed");
    }

    [Test]
    public async Task SaveFormat_DamagedFilesGiveAFriendlyError()
    {
        string json = Replay.Capture(PlayedWorld().W, Meta()).ToJson();
        string cut = json[..(json.Length - 40)];

        foreach (string bad in new[] { "", "not json at all", "[1,2,3]", "{\"Seed\":1}", cut })
        {
            InvalidDataException? e = Assert.Throws<InvalidDataException>(() => Replay.FromJson(bad));
            await Assert.That(e!.Message).Contains("isn't a readable save");
        }

        foreach (string bad in new[] { "", "not json at all", "{\"Seed\":1}" })
        {
            InvalidDataException? e = Assert.Throws<InvalidDataException>(() => Header(bad));
            await Assert.That(e!.Message).Contains("isn't a readable save");
        }

        // A save cut off in its command log still shows in the list: the header comes first.
        await Assert.That(Header(cut).Meta!.Name).IsEqualTo("My base");
    }

    [Test]
    public async Task SaveMeta_RoundTripsThroughTheHeaderWithoutTheCommands()
    {
        TestWorld tw = PlayedWorld();
        // Pad the command log past the header reader's first read, as a long game's would be.
        Replay big = Replay.Capture(tw.W, Meta());
        big.Commands.AddRange(Enumerable.Range(0, 3000).Select(i => new ScheduledCommand(tw.W.Tick + i, i, new SetMoveIntent(tw.Leader.Id, 1, 0))));
        string json = big.ToJson();
        await Assert.That(json.Length > 128 * 1024).IsTrue();

        Replay header = Header(json);

        await Assert.That(header.Commands.Count).IsEqualTo(0);
        await Assert.That(header.Version).IsEqualTo(Replay.CurrentVersion);
        await Assert.That(header.Build).IsEqualTo(BuildInfo.Version);
        await Assert.That(header.Meta!.Name).IsEqualTo("My base");
        await Assert.That(header.Meta.SavedAt).IsEqualTo(Meta().SavedAt);
        await Assert.That(header.Meta.SavedAt.Offset).IsEqualTo(TimeSpan.FromHours(-8));
        await Assert.That(header.Meta.Planet).IsEqualTo("Kepler's Rest");
        await Assert.That(header.FinalTick).IsEqualTo(tw.W.Tick);
        await Assert.That(header.FinalChecksum).IsEqualTo(tw.W.Checksum());
        await Assert.That(header.Seed).IsEqualTo(42UL);
        await Assert.That(Replay.FromJson(json).Meta!.Name).IsEqualTo("My base");
    }

    [Test]
    public async Task Playback_InBatchesMatchesPlayingInOneGo()
    {
        TestWorld tw = PlayedWorld();
        Replay replay = Replay.FromJson(Replay.Capture(tw.W).ToJson());

        ReplayPlayback playback = replay.BeginPlayback(TestWorld.Content);
        int batches = 0;
        double last = -1;
        while (playback.Done == false)
        {
            await Assert.That(playback.Progress > last).IsTrue();
            last = playback.Progress;
            playback.Step(7);
            batches++;
        }

        await Assert.That(batches).IsEqualTo((int)((tw.W.Tick + 6) / 7));
        await Assert.That(playback.Progress).IsEqualTo(1.0);
        await Assert.That(playback.World.Tick).IsEqualTo(tw.W.Tick);
        await Assert.That(playback.World.Checksum()).IsEqualTo(replay.Play(TestWorld.Content).Checksum());
        await Assert.That(playback.World.Checksum()).IsEqualTo(replay.FinalChecksum);
        await Assert.That(playback.World.CommandLog.Count).IsEqualTo(replay.Commands.Count);
    }
}
