using Factory.Playtest.Bots;

namespace Factory.Sim.Tests;

/// <summary>
/// The world snapshot used for saves: a restored world must be the same world, not just one with the same
/// checksum. A bot plays a real game; at a few points the world is snapshotted and restored, then the
/// original and the copy are run side by side on the same commands, and must stay identical tick for tick
/// and snapshot to the same bytes at the end.
/// </summary>
public class WorldStateTests
{
    private const int Minute = 60 * 60;

    [Test]
    public async Task Snapshot_RestoresAWorldThatPlaysOnIdentically()
    {
        World a = new World(TestWorld.Content, new WorldSettings { Seed = 1 });
        GoalBot bot = new GoalBot(Goal.Parse("settlers"));
        Run(a, bot, null, 40 * Minute);

        for (int round = 0; round < 3; round++)
        {
            byte[] snap = WorldState.Capture(a);
            World b = WorldState.Restore(TestWorld.Content, a.Settings, snap);
            await Assert.That(b.Tick).IsEqualTo(a.Tick);
            await Assert.That(b.Checksum()).IsEqualTo(a.Checksum()).Because($"round {round}: the restored world hashes the same");
            await Assert.That(Unzip(WorldState.Capture(b))).IsEquivalentTo(Unzip(snap)).Because($"round {round}: every saved value reads back as written");

            // Side by side: the bot drives the original, and every command it sends goes to the copy too.
            long mismatchAt = Run(a, bot, b, 15 * Minute);
            await Assert.That(mismatchAt).IsEqualTo(-1L).Because($"round {round}: the copy went its own way at tick {mismatchAt}");
            await Assert.That(Unzip(WorldState.Capture(b))).IsEquivalentTo(Unzip(WorldState.Capture(a))).Because($"round {round}: the whole state matches, not just the hash");
        }

        await Assert.That(a.Planets[0].Buildings.Count).IsGreaterThan(20).Because("the test covers a working base, not an empty map");
    }

    [Test]
    public async Task Snapshot_IsSmallAndFast()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 2 });
        Run(w, new GoalBot(Goal.Parse("settlers")), null, 20 * Minute);
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        byte[] snap = WorldState.Capture(w);
        World back = WorldState.Restore(TestWorld.Content, w.Settings, snap);
        watch.Stop();

        await Assert.That(back.Checksum()).IsEqualTo(w.Checksum());
        await Assert.That(snap.Length).IsLessThan(1_000_000).Because($"a snapshot is {snap.Length:N0} bytes");
        await Assert.That(watch.ElapsedMilliseconds).IsLessThan(2000L).Because($"save and load took {watch.ElapsedMilliseconds} ms");
    }

    [Test]
    public async Task Snapshot_RefusesDamagedData()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 3 });
        byte[] snap = WorldState.Capture(w);

        await Assert.That(() => WorldState.Restore(TestWorld.Content, w.Settings, snap[..(snap.Length / 2)])).Throws<InvalidDataException>();
        await Assert.That(() => WorldState.Restore(TestWorld.Content, w.Settings, [1, 2, 3, 4])).Throws<InvalidDataException>();
    }

    /// <summary>
    /// Every field of the classes a snapshot covers, sorted into saved and rebuilt. When this fails, the sim
    /// has gained (or lost) a field: write and read it in Save/*.State.cs (and bump WorldState.Format), or
    /// list it as rebuilt if it is derived, then update the lists here.
    /// </summary>
    [Test]
    public async Task Snapshot_CoversEveryField()
    {
        Dictionary<Type, string> expected = new()
        {
            [typeof(World)] = "Content Settings Tick Rng Planets Research Events HelpersBuilt SettlersCalled Stats _nextId _entities _pending _commandSeq CommandLog LastCommandResults",
            [typeof(Planet)] = "World Id Name Def Width Height Torus SubTorus _terrain _biome _resource _amount _rail _occupant Buildings Sites Units Trains Shipments LanderId TerrainVersion PowerDirty _veinCredit _explored _fogVersions _fogChunksX ExploredTiles FogVersion _hoverWalk Networks Stats",
            [typeof(Building)] = "Id Planet Def Origin Rot Inv Out RecipeId Crafting Progress FuelJ StoredJ Status Satisfaction NetworkId BurningFuel Name CraftQueue CraftReserve CraftingIntermediate HomePadId HomePlanetId TargetPlanetId Lane StationSlots Modules BoostPct LastOutputJ",
            [typeof(Unit)] = "Id Planet Name IsLeader Robot Modules OwnerPlayer Pos Speed Carry Job Phase TargetSub TargetTile TargetEntity SourceEntity WorkTicks WaitTicks Boarded Status MoveIntent MiningTile MiningResource MiningProgress WalkTarget BuildingSiteId Path ExploreIndex SightFrom PathGoal PathStop StuckTicks FailedPlans LastPlanPos",
            [typeof(Train)] = "Id Planet Vehicles Tiles Progress Speed Path PathIndex Schedule StopIndex State StopTicks IdleTicks RetryTicks FuelJ Water Name",
            [typeof(ConstructionSite)] = "Id Planet Kind DefId Origin Rot W H Delivered Cost WorkNeeded WorkDone ClaimedBy TargetId Vehicles TrainTiles",
            [typeof(Shipment)] = "FromId ToId ToPlanet Vessel Item Count ArriveTick BackTick",
            [typeof(Vehicle)] = "Def Cargo",
            [typeof(BeltItem)] = "Item Pos",
            [typeof(Inventory)] = "_items _content Slots",
            [typeof(ResearchState)] = "Unlocked Completed Current Progress Levels",
            [typeof(ProductionStats)] = "_byFlow _items _series _actorSeries _buildings _techs _terraform Tick",
        };
        List<string> problems = [];
        foreach ((Type type, string names) in expected)
        {
            HashSet<string> actual = [];
            for (Type? t = type; (t is not null) && (t != typeof(object)); t = t.BaseType)
            {
                foreach (System.Reflection.FieldInfo f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    actual.Add(f.Name.StartsWith('<') ? f.Name[1..f.Name.IndexOf('>')] : f.Name);
                }
            }

            HashSet<string> listed = [.. names.Split(' ')];
            problems.AddRange(actual.Except(listed).Select(n => $"{type.Name}.{n} is new: save it or mark it rebuilt"));
            problems.AddRange(listed.Except(actual).Select(n => $"{type.Name}.{n} is gone: take it out of the save"));
        }

        await Assert.That(problems).IsEmpty().Because(string.Join("; ", problems));
    }

    /// <summary>
    /// Runs the bot on <paramref name="a"/> for some ticks; if <paramref name="b"/> is given, feeds it the
    /// same commands and steps it in lockstep.
    /// </summary>
    /// <returns>The first tick the two worlds' checksums differ, or -1.</returns>
    private static long Run(World a, GoalBot bot, World? b, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            bot.Tick(a);
            int logged = a.CommandLog.Count;
            a.Step();
            a.Events.Clear();
            if (b is null)
            {
                continue;
            }

            for (int c = logged; c < a.CommandLog.Count; c++)
            {
                b.SubmitAt(a.CommandLog[c].Tick, a.CommandLog[c].Command);
            }

            b.Step();
            b.Events.Clear();
            // Every ten seconds, compare everything, not just the hash: a value the hash doesn't cover
            // (a robot's timer, say) shows up here before it has a chance to even out again.
            if (((i % 600) == 0) && ((a.Checksum() != b.Checksum()) || (Unzip(WorldState.Capture(a)).AsSpan().SequenceEqual(Unzip(WorldState.Capture(b))) == false)))
            {
                return a.Tick;
            }
        }

        return (b is not null) && (a.Checksum() != b.Checksum()) ? a.Tick : -1;
    }

    private static byte[] Unzip(byte[] gz)
    {
        using MemoryStream raw = new MemoryStream(gz);
        using System.IO.Compression.GZipStream z = new System.IO.Compression.GZipStream(raw, System.IO.Compression.CompressionMode.Decompress);
        using MemoryStream o = new MemoryStream();
        z.CopyTo(o);

        return o.ToArray();
    }
}
