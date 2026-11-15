namespace Factory.Sim;

/// <summary>
/// Root of all simulation state. Deterministic: given the same settings, content and command stream
/// it produces bit-identical state (see <see cref="Checksum"/>). The game client only reads state and
/// submits <see cref="Command"/>s; it never mutates anything directly.
/// </summary>
public sealed partial class World
{
    /// <summary>Simulation rate: the world advances 60 ticks per second of game time.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>Static game content: items, buildings, recipes, resources, technologies.</summary>
    public ContentDb Content { get; }
    /// <summary>Settings the world was created with.</summary>
    public WorldSettings Settings { get; }
    /// <summary>Number of ticks simulated so far.</summary>
    public long Tick { get; private set; }
    /// <summary>Deterministic random generator; all simulation randomness draws from it.</summary>
    public Rng Rng { get; }
    /// <summary>All planets, indexed by planet id.</summary>
    public List<Planet> Planets { get; } = [];
    /// <summary>Research is shared by the whole colony (all players in co-op).</summary>
    public ResearchState Research { get; } = new();
    /// <summary>Log of events reported to the player, oldest first.</summary>
    public List<SimEvent> Events { get; } = [];
    /// <summary>Number of helper bots assembled so far (not counting those that landed with the commander).</summary>
    public int HelpersBuilt { get; internal set; }

    /// <summary>True once the settlers have been called: the game is won.</summary>
    public bool SettlersCalled { get; internal set; }

    /// <summary>The planets the settlers are waiting on: every one must reach the readiness bar.</summary>
    public IEnumerable<Planet> SettlerTargets => Planets.Where(p => p.Def.SettlerTarget);

    /// <summary>The system-wide readiness meter: the average readiness of the settler target planets, 0 to 100.</summary>
    public int SystemReadiness => SettlerTargets.Any() ? SettlerTargets.Sum(p => p.Readiness) / SettlerTargets.Count() : 0;

    /// <summary>True once every settler target planet has reached the readiness bar.</summary>
    public bool AllPlanetsReady => SettlerTargets.All(p => p.IsReady);

    /// <summary>Extra module slots the commander has gained from research.</summary>
    public int CommanderBonusSlots => Research.Completed.Sum(id => Content.Techs.TryGetValue(id, out TechDef? t) ? t.CommanderSlots : 0);
    /// <summary>Cumulative item production and consumption counters.</summary>
    public ProductionStats Stats { get; } = new();

    private int _nextId = 1;
    private readonly Dictionary<int, Entity> _entities = new();
    private readonly List<ScheduledCommand> _pending = [];
    private int _commandSeq;

    /// <summary>Every command that has been applied, in order. Replaying this from the same seed reproduces the game.</summary>
    public List<ScheduledCommand> CommandLog { get; } = [];

    /// <summary>Creates a new world: generates the starting planet and places the lander and leaders.</summary>
    /// <param name="content">Game content to simulate with.</param>
    /// <param name="settings">Seed and size settings.</param>
    public World(ContentDb content, WorldSettings settings)
    {
        Content = content;
        Settings = settings;
        Rng = new Rng(settings.Seed);
        foreach (string u in content.Start.Unlocked)
        {
            Research.Unlocked.Add(u);
        }

        // The start planet comes first (id 0), then every other surface world in content order. Each planet
        // is generated from its own seeded generator, so adding a planet never reshuffles another.
        List<PlanetDef> surfaces = content.PlanetList.Where(d => d.Kind == PlanetKind.Surface).OrderBy(d => d.Start ? 0 : 1).ToList();
        if (surfaces.Count == 0)
        {
            surfaces.Add(new PlanetDef { Id = "home", Name = "Kepler's Rest", Start = true, SettlerTarget = true });
        }

        foreach (PlanetDef def in surfaces)
        {
            int size = def.Start ? 0 : (settings.OtherPlanetSize > 0 ? settings.OtherPlanetSize : def.Size);
            Planet planet = new Planet(this, Planets.Count, def, def.Start ? settings.PlanetWidth : size, def.Start ? settings.PlanetHeight : size);
            Planets.Add(planet);
            planet.InitStats();
            ulong planetSeed = (settings.Seed * 0x9E3779B97F4A7C15UL) ^ StateHasher.Of(def.Id);
            WorldGen.Generate(planet, new Rng(planetSeed), settings);
        }

        SetupStart(Planets[0]);
    }

    /// <summary>Planet with the given id.</summary>
    public Planet Planet(int id) => Planets[id];

    internal int NextId() => _nextId++;
    internal void Register(Entity e) => _entities[e.Id] = e;
    internal void Unregister(Entity e) => _entities.Remove(e.Id);
    /// <summary>Looks up an entity by id, or null if it does not exist or is not of type T.</summary>
    public T? Get<T>(int id) where T : Entity => _entities.TryGetValue(id, out Entity? e) ? e as T : null;

    /// <summary>Records an event for the player at the current tick.</summary>
    /// <param name="message">Text to show.</param>
    /// <param name="planetId">Planet the event relates to.</param>
    /// <param name="tile">Optional location of the event.</param>
    public void Emit(string message, int planetId = 0, Int2? tile = null) => Events.Add(new SimEvent(Tick, message, planetId, tile));

    /// <summary>Queue a command; it is applied at the start of the next <see cref="Step"/>.</summary>
    public void Submit(Command command)
    {
        _pending.Add(new ScheduledCommand(Tick, _commandSeq++, command));
    }

    /// <summary>Queue a command for a specific future tick (used by replays / lockstep networking).</summary>
    public void SubmitAt(long tick, Command command)
    {
        _pending.Add(new ScheduledCommand(tick, _commandSeq++, command));
    }

    /// <summary>Result of the last applied commands (for UI feedback). Cleared each step.</summary>
    public List<(Command Command, string? Error)> LastCommandResults { get; } = [];

    /// <summary>Advances one tick: applies due commands in deterministic order, then updates every planet.</summary>
    public void Step()
    {
        LastCommandResults.Clear();
        // Apply commands due this tick in deterministic order (player, then submission order).
        List<ScheduledCommand> due = _pending.Where(c => c.Tick <= Tick).OrderBy(c => c.Command.Player).ThenBy(c => c.Seq).ToList();
        foreach (ScheduledCommand? c in due)
        {
            _pending.Remove(c);
            string? err = c.Command.Validate(this);
            if (err is null)
            {
                c.Command.Apply(this);
            }

            LastCommandResults.Add((c.Command, err));
            CommandLog.Add(c with
            {
                Tick = Tick
            });
        }

        foreach (Planet p in Planets)
        {
            p.Update();
        }

        Tick++;
    }

    /// <summary>Advances the given number of ticks.</summary>
    public void Run(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Step();
        }
    }

    /// <summary>Hash of the full simulation state, used to verify determinism across runs, replays and peers.</summary>
    public ulong Checksum()
    {
        StateHasher h = new StateHasher();
        h.Add(Tick);
        h.Add((long)Rng.State);
        h.Add(Research.Current);
        foreach ((string? k, int v) in Research.Progress)
        {
            h.Add(k);
            h.Add(v);
        }

        foreach (string u in Research.Unlocked)
        {
            h.Add(u);
        }

        foreach ((string k, int v) in Research.Levels)
        {
            h.Add(k);
            h.Add(v);
        }

        h.Add(SettlersCalled);
        foreach (Planet p in Planets)
        {
            p.Hash(h);
        }

        Stats.Hash(h);

        return h.Value;
    }

    private void SetupStart(Planet planet)
    {
        Int2 center = new Int2(planet.Width / 2, planet.Height / 2);
        Building lander = planet.CreateBuilding(Content.Buildings["lander"], new Int2(center.X - 1, center.Y - 1), Dir.South);
        lander.Name = "Lander";
        foreach ((string? item, int n) in Content.Start.Lander)
        {
            lander.Inv.AddUnchecked(item, n);
            Stats.Record(ItemFlow.Start, item, n);
        }

        planet.LanderId = lander.Id;

        for (int p = 0; p < Settings.Players; p++)
        {
            Unit leader = planet.CreateUnit(p == 0 ? "Commander" : $"Commander {p + 1}", Content.Robots[Content.Start.CommanderRobot], isLeader: true, owner: p,
                Fixed.TileCenter(new Int2(center.X + p, center.Y + 3)));
            leader.Job = Job.Manual;
        }

        for (int i = 0; i < Content.Start.StartingFollowers; i++)
        {
            SpawnHelper(planet, owner: 0, Content.Robots[Content.Start.HelperRobot]);
        }
    }

    /// <summary>Creates a helper bot near the lander, or next to <paramref name="at"/> when given.</summary>
    internal Unit SpawnHelper(Planet planet, int owner, RobotDef robot, Building? at = null)
    {
        string[] names = Content.Start.FollowerNames;
        int index = planet.Units.Count(u => u.IsLeader == false);
        string name = index < names.Length ? names[index] : $"HB-{index + 1:00}";
        Building? lander = Get<Building>(planet.LanderId);
        Int2 basePos = lander is not null ? lander.CenterSub : Fixed.TileCenter(new Int2(planet.Width / 2, planet.Height / 2));
        Int2 pos = basePos + new Int2(((index % 3) - 1) * Fixed.One * 2, Fixed.One * 3);
        if (at is not null)
        {
            // Roll out of the bay's front, just past its bottom edge.
            pos = at.CenterSub + new Int2(0, (at.H * Fixed.Half) + Fixed.Half);
        }

        return planet.CreateUnit(name, robot, isLeader: false, owner, planet.SubTorus.Wrap(pos));
    }
}
