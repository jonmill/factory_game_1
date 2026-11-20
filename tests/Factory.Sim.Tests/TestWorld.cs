namespace Factory.Sim.Tests;

/// <summary>Helpers for building deterministic test scenarios without going through followers.</summary>
public sealed class TestWorld
{
    public static readonly ContentDb Content = ContentDb.LoadDefault();

    public World W { get; }
    public Planet P => W.Planets[0];
    public Building Lander => W.Get<Building>(P.LanderId)!;
    public Unit Leader => P.Units.First(u => u.IsLeader);
    public List<Unit> Followers => P.Units.Where(u => u.IsLeader == false).ToList();

    /// <summary>
    /// The supplies the lander used to start with. A real game now lands empty; scenario tests that are
    /// about something else (crafting, building, trains) start with these so they don't have to mine first.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> Supplies = new Dictionary<string, int>
    {
        ["biomass"] = 80, ["regolith"] = 40, ["siderite-plate"] = 120, ["cupral-bar"] = 40, ["drive-cog"] = 30,
        ["sintered-brick"] = 40, ["cupral-filament"] = 30, ["charge-cell"] = 10, ["compacted-carbon"] = 20,
    };

    /// <param name="empty">A flat, empty map for hand-built scenarios (false: the real generated map).</param>
    /// <param name="seed">World seed.</param>
    /// <param name="stocked">Put <see cref="Supplies"/> in the lander (default: for scenario maps only).</param>
    public TestWorld(bool empty = true, ulong seed = 1, bool? stocked = null)
    {
        W = new World(Content, new WorldSettings { Seed = seed, EmptyPlanet = empty, PlanetWidth = 96, PlanetHeight = 96 });
        if (stocked ?? empty)
        {
            foreach ((string item, int n) in Supplies)
            {
                Lander.Inv.AddUnchecked(item, n);
            }
        }
    }

    /// <summary>A test world with railways unlocked, for tests that exercise trains without researching them.</summary>
    public static TestWorld WithTrains()
    {
        TestWorld tw = new TestWorld();
        tw.Unlock(Content.Techs["rail-logistics"].Unlocks);

        return tw;
    }

    /// <summary>Places a finished building instantly (skips construction).</summary>
    public Building Build(string id, int x, int y, Dir rot = Dir.North)
    {
        BuildingDef def = Content.Buildings[id];
        Building b = P.CreateBuilding(def, new Int2(x, y), rot);

        return b;
    }

    public void Water(int x, int y, int w = 1, int h = 1)
    {
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                P.SetTerrain(new Int2(x + i, y + j), Terrain.Water);
            }
        }
    }

    public void Ore(string resource, int x, int y, int w, int h, int amount = 500)
    {
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                P.SetResource(new Int2(x + i, y + j), Content.Resources[resource], amount);
            }
        }
    }

    public void Rail(int x, int y, int length, Dir along = Dir.East)
    {
        Int2 v = along.Vec();
        for (int i = 0; i < length; i++)
        {
            P.SetRail(new Int2(x + (v.X * i), y + (v.Y * i)), true);
        }
    }

    /// <summary>Submit a command and apply it immediately; asserts it was accepted.</summary>
    public void Do(Command c)
    {
        W.Submit(c);
        W.Step();
        (Command _, string? err) = W.LastCommandResults.Single();
        if (err is not null)
        {
            Assert.Fail($"Command {c.GetType().Name} rejected: {err}");
        }
    }

    public string? Try(Command c)
    {
        W.Submit(c);
        W.Step();

        return W.LastCommandResults.Single().Error;
    }

    public void Run(int ticks) => W.Run(ticks);

    /// <summary>Runs until the predicate holds, failing if it takes longer than maxTicks.</summary>
    public void RunUntil(Func<bool> done, int maxTicks, string what)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (done())
            {
                return;
            }

            W.Step();
        }

        Assert.Fail($"Timed out after {maxTicks} ticks waiting for: {what}");
    }

    /// <summary>Installs a module straight into a robot (skips crafting it and the commander's reach).</summary>
    public void Equip(Unit u, string module)
    {
        u.Modules.Add(module);
        P.RefreshRobot(u);
    }

    public void Unlock(params string[] ids)
    {
        foreach (string id in ids)
        {
            W.Research.Unlocked.Add(id);
        }
    }
}
