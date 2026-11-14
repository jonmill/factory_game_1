using Factory.Game.Platform;
using Factory.Sim;
using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game;

/// <summary>
/// Owns the simulation and drives it at a fixed 60 ticks/s, converts input into Commands, and holds
/// view state (camera, current tool, selection). Rendering is in WorldView, UI in Hud.
/// The client NEVER mutates the World directly.
/// </summary>
public partial class GameRoot : Node2D
{
    /// <summary>Size of one tile in pixels at zoom 1.</summary>
    public const float TilePx = 32f;

    /// <summary>Loaded game content definitions.</summary>
    public ContentDb Content { get; private set; } = null!;
    /// <summary>The running simulation.</summary>
    public World World { get; private set; } = null!;
    /// <summary>The planet currently being viewed.</summary>
    public Planet Planet => World.Planets[PlanetId];
    /// <summary>Index of the planet currently being viewed.</summary>
    public int PlanetId { get; private set; }
    /// <summary>The player index this client controls.</summary>
    public static int LocalPlayer => 0;
    /// <summary>The local player's leader unit on the current planet.</summary>
    public Unit Leader => Planet.Units.First(u => u.IsLeader && (u.OwnerPlayer == LocalPlayer));
    /// <summary>Store/platform integration (Steam or offline).</summary>
    public IPlatformServices PlatformServices { get; private set; } = null!;

    // View state
    /// <summary>Camera centre, in tiles.</summary>
    public Vector2 Cam { get; set; }            // in tiles
    /// <summary>View zoom factor applied on top of <see cref="TilePx"/>.</summary>
    public float Zoom { get; set; } = 1f;
    /// <summary>Simulation speed multiplier.</summary>
    public int Speed { get; set; } = 1;
    /// <summary>Whether the simulation is paused.</summary>
    public bool Paused { get; set; }
    /// <summary>The active interaction tool.</summary>
    public ToolKind Tool { get; set; } = ToolKind.Select;
    /// <summary>Building definition being placed with the place tool, if any.</summary>
    public string? PlaceDefId { get; set; }
    /// <summary>Rotation applied to buildings and trains being placed.</summary>
    public SimDir PlaceRot { get; set; } = SimDir.North;
    /// <summary>Vehicle definition ids for the train being placed.</summary>
    public string[] TrainPreset { get; set; } = [];
    /// <summary>Id of the selected entity shown in the inspector, or 0 for none.</summary>
    public int SelectedId { get; set; }
    /// <summary>Follower being given a haul or train-operate order.</summary>
    public int PickUnitId { get; set; }          // follower being given a haul/operate order
    /// <summary>Source building chosen for a haul order while picking its destination.</summary>
    public int PickFromId { get; set; }
    /// <summary>Tile under the mouse cursor.</summary>
    public Int2 HoverTile { get; set; }
    /// <summary>Recent notifications, oldest first; only the last few are kept.</summary>
    public List<Message> Messages { get; } = [];
    /// <summary>Real time elapsed since start, in seconds, used for animation and message fading.</summary>
    public double Clock { get; set; }

    private TerrainLayer _terrain = null!;
    private WorldView _view = null!;
    private Hud _hud = null!;
    private double _acc;
    private Int2 _lastIntent;
    private Int2? _lastDragTile;
    private bool _mining;
    private Int2? _sweepSent;

    /// <inheritdoc />
    public override void _Ready()
    {
        RegisterInput();
        PlatformServices = Platform.PlatformServices.Create();
        Content = ContentDb.LoadDefault();
        // FACTORY_SEED pins the map, which keeps screenshots comparable between runs.
        string? seedVar = System.Environment.GetEnvironmentVariable("FACTORY_SEED");
        NewGame(ulong.TryParse(seedVar, out ulong pinned) ? pinned : (ulong)System.DateTime.Now.Ticks % 100000);

        // Bloom only what is deliberately brighter than white (high-tech trim, fire, glinting ore), and
        // keep it soft: the ground and the industrial era should read as matte.
        WorldEnvironment env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Canvas,
                GlowEnabled = true,
                GlowIntensity = 0.45f,
                GlowStrength = 0.9f,
                GlowBloom = 0f,
                GlowHdrThreshold = 1.15f,
                GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
            },
        };
        env.Environment.SetGlowLevel(1, 0.8f);
        env.Environment.SetGlowLevel(2, 0.6f);
        env.Environment.SetGlowLevel(3, 0f);
        AddChild(env);

        _terrain = new TerrainLayer { Root = this };
        AddChild(_terrain);
        _view = new WorldView { Root = this };
        AddChild(_view);
        _hud = new Hud { Root = this };
        AddChild(_hud);
        Notify($"Platform: {PlatformServices.Name}. Press H for help.", Colors.LightSkyBlue);
    }

    /// <summary>Starts a new game from a seed, or reconstructs one by playing back a replay, and resets the view.</summary>
    /// <param name="seed">World seed used when no replay is given.</param>
    /// <param name="replay">Optional replay to restore instead of generating a fresh world.</param>
    public void NewGame(ulong seed, Replay? replay = null)
    {
        World = replay is null ? new World(Content, new WorldSettings { Seed = seed }) : replay.Play(Content);
        PlanetId = 0;
        Int2 lp = Leader.Pos;
        Cam = new Vector2(lp.X / (float)Fixed.One, lp.Y / (float)Fixed.One);
        SelectedId = 0;
        SetTool(ToolKind.Select);
        Messages.Clear();
        Notify("Landing complete. Your three followers await orders.", Colors.Cyan);
    }

    /// <inheritdoc />
    public override void _ExitTree() => PlatformServices?.Dispose();

    // ------------------------------------------------------------------ helpers

    /// <summary>Posts a message to the HUD log.</summary>
    /// <param name="text">Message text.</param>
    /// <param name="color">Colour to draw it in.</param>
    public void Notify(string text, Color color)
    {
        Messages.Add(new Message(text, color, Clock));
        if (Messages.Count > 8)
        {
            Messages.RemoveAt(0);
        }
    }

    /// <summary>Submits a command to the simulation as the local player on the current planet.</summary>
    public void Send(Command c) => World.Submit(c with { Player = LocalPlayer, PlanetId = PlanetId });

    /// <summary>Switches the active tool and clears any in-progress drag.</summary>
    /// <param name="tool">The tool to activate.</param>
    /// <param name="defId">Building definition to place, for the place tool.</param>
    public void SetTool(ToolKind tool, string? defId = null)
    {
        Tool = tool;
        PlaceDefId = defId;
        _lastDragTile = null;
    }

    /// <summary>Converts a tile-space position to screen pixels, using the wrapped copy nearest the camera.</summary>
    public Vector2 TileToScreen(Vector2 tile)
    {
        Vector2 size = GetViewportRect().Size;
        Vector2 d = WrapDelta(Cam, tile);

        return (size / 2) + (d * TilePx * Zoom);
    }

    /// <summary>Shortest wrapped offset (in tiles) from a to b.</summary>
    public Vector2 WrapDelta(Vector2 a, Vector2 b)
    {
        float w = Planet.Width, h = Planet.Height;
        float dx = Mathf.PosMod(b.X - a.X + (w / 2), w) - (w / 2);
        float dy = Mathf.PosMod(b.Y - a.Y + (h / 2), h) - (h / 2);

        return new Vector2(dx, dy);
    }

    /// <summary>Converts a screen position to a fractional tile position wrapped onto the planet.</summary>
    public Vector2 ScreenToTileF(Vector2 screen)
    {
        Vector2 size = GetViewportRect().Size;
        Vector2 t = Cam + ((screen - (size / 2)) / (TilePx * Zoom));

        return new Vector2(Mathf.PosMod(t.X, Planet.Width), Mathf.PosMod(t.Y, Planet.Height));
    }

    /// <summary>Converts a screen position to the tile it falls in.</summary>
    public Int2 ScreenToTile(Vector2 screen)
    {
        Vector2 t = ScreenToTileF(screen);

        return new Int2((int)Mathf.Floor(t.X), (int)Mathf.Floor(t.Y));
    }

    /// <summary>Converts a fixed-point sub-tile position to fractional tile coordinates.</summary>
    public static Vector2 SubToTile(Int2 sub) => new(sub.X / (float)Fixed.One, sub.Y / (float)Fixed.One);

    /// <summary>Origin so the footprint is centered on the hovered tile.</summary>
    public Int2 PlacementOrigin(BuildingDef def)
    {
        (int w, int h) = Planet.SizeOf(def, PlaceRot);

        return new Int2(HoverTile.X - ((w - 1) / 2), HoverTile.Y - ((h - 1) / 2));
    }

    // ------------------------------------------------------------------ main loop

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        Clock += delta;
        PlatformServices.Update();
        HoverTile = ScreenToTile(GetViewport().GetMousePosition());
        SendLeaderIntent();
        HandleCameraKeys();
        SweepMining();

        if (Paused == false)
        {
            _acc += delta * Speed;
            int steps = 0;
            const double TickSeconds = 1.0 / World.TicksPerSecond;
            while ((_acc >= TickSeconds) && (steps < 8 * Speed))
            {
                World.Step();
                foreach ((Command? cmd, string? err) in World.LastCommandResults)
                {
                    if ((err is not null) && (cmd.Player == LocalPlayer) && (cmd is not SetMoveIntent))
                    {
                        Notify(err, Colors.OrangeRed);
                    }
                }

                _acc -= TickSeconds;
                steps++;
            }

            if (_acc > TickSeconds * 8 * Speed)
            {
                _acc = 0; // don't spiral if we fall behind
            }
        }

        DebugHooks();
        foreach (SimEvent e in World.Events)
        {
            Notify(e.Message, Colors.Aquamarine);
        }

        World.Events.Clear();

        // Camera follows the leader across the wrap.
        Vector2 lt = SubToTile(Leader.Pos);
        Cam += WrapDelta(Cam, lt) * Mathf.Min(1f, (float)delta * 6f);
        Cam = new Vector2(Mathf.PosMod(Cam.X, Planet.Width), Mathf.PosMod(Cam.Y, Planet.Height));

        if ((Tool == ToolKind.Place) && (PlaceDefId == "rail") && Input.IsMouseButtonPressed(MouseButton.Left) && (_lastDragTile is { } last))
        {
            DragRail(last, HoverTile);
        }

        _terrain.Refresh(delta);

        _view.QueueRedraw();
    }

    private void SendLeaderIntent()
    {
        int dx = 0, dy = 0;
        bool typing = GetViewport().GuiGetFocusOwner() is LineEdit;
        if (typing == false)
        {
            if (Input.IsActionPressed("move_left"))
            {
                dx--;
            }

            if (Input.IsActionPressed("move_right"))
            {
                dx++;
            }

            if (Input.IsActionPressed("move_up"))
            {
                dy--;
            }

            if (Input.IsActionPressed("move_down"))
            {
                dy++;
            }
        }

        Int2 intent = new Int2(dx, dy);
        if (intent != _lastIntent)
        {
            Send(new SetMoveIntent(Leader.Id, dx, dy));
            _lastIntent = intent;
        }
    }

    private void HandleCameraKeys()
    {
        if (Input.IsActionJustPressed("zoom_in"))
        {
            Zoom = Mathf.Min(2.5f, Zoom * 1.15f);
        }

        if (Input.IsActionJustPressed("zoom_out"))
        {
            Zoom = Mathf.Max(0.45f, Zoom / 1.15f);
        }
    }

    private void DragRail(Int2 from, Int2 to)
    {
        Int2 d = Planet.Torus.Delta(from, to);
        Int2 cur = from;
        while (d != Int2.Zero)
        {
            if (System.Math.Abs(d.X) >= System.Math.Abs(d.Y))
            {
                cur = Planet.Torus.Wrap(cur + new Int2(System.Math.Sign(d.X), 0));
            }
            else
            {
                cur = Planet.Torus.Wrap(cur + new Int2(0, System.Math.Sign(d.Y)));
            }

            d = Planet.Torus.Delta(cur, to);
            TryPlaceRail(cur);
        }

        _lastDragTile = to;
    }

    private void TryPlaceRail(Int2 tile)
    {
        if (Planet.CanPlace(Content.Buildings["rail"], tile, SimDir.North) is null)
        {
            Send(new PlaceBuilding("rail", tile, SimDir.North));
        }
    }

    // ------------------------------------------------------------------ input

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if ((mb.ButtonIndex == MouseButton.WheelUp) && mb.Pressed)
            {
                Zoom = Mathf.Min(2.5f, Zoom * 1.1f);

                return;
            }

            if ((mb.ButtonIndex == MouseButton.WheelDown) && mb.Pressed)
            {
                Zoom = Mathf.Max(0.45f, Zoom / 1.1f);

                return;
            }

            if ((mb.ButtonIndex == MouseButton.Left) && mb.Pressed)
            {
                LeftClick();

                return;
            }

            if ((mb.ButtonIndex == MouseButton.Left) && (mb.Pressed == false))
            {
                _lastDragTile = null;

                return;
            }

            if ((mb.ButtonIndex == MouseButton.Right) && mb.Pressed)
            {
                RightClick();

                return;
            }

            // Releasing the button keeps mining the last tile; it only ends the sweep.
            if ((mb.ButtonIndex == MouseButton.Right) && (mb.Pressed == false))
            {
                _mining = false;
            }

            return;
        }

        if (@event is not InputEventKey { Pressed: true, Echo: false } k)
        {
            return;
        }

        if (k.IsAction("rotate"))
        {
            PlaceRot = PlaceRot.CW();
        }
        else if (k.IsAction("cancel"))
        {
            if (Tool != ToolKind.Select)
            {
                SetTool(ToolKind.Select);
            }
            else
            {
                SelectedId = 0;
            }
        }
        else if (k.IsAction("unload"))
        {
            UnloadPockets();
        }
        else if (k.IsAction("deconstruct"))
        {
            SetTool(Tool == ToolKind.Deconstruct ? ToolKind.Select : ToolKind.Deconstruct);
        }
        else if (k.IsAction("pause"))
        {
            Paused = Paused == false;
        }
        else if (k.IsAction("speed_up"))
        {
            Speed = System.Math.Min(8, Speed * 2);
        }
        else if (k.IsAction("speed_down"))
        {
            Speed = System.Math.Max(1, Speed / 2);
        }
        else if (k.IsAction("research"))
        {
            _hud.ToggleResearch();
        }
        else if (k.IsAction("help"))
        {
            _hud.ToggleHelp();
        }
        else if (k.IsAction("quicksave"))
        {
            QuickSave();
        }
        else if (k.IsAction("quickload"))
        {
            QuickLoad();
        }
        else if ((k.Keycode >= Key.Key1) && (k.Keycode <= Key.Key9))
        {
            _hud.HotbarSelect((int)(k.Keycode - Key.Key1));
        }
        else if (k.Keycode == Key.Key0)
        {
            _hud.HotbarSelect(9);
        }
    }

    private void LeftClick()
    {
        Int2 tile = HoverTile;
        switch (Tool)
        {
            case ToolKind.Place when PlaceDefId is not null:
                {
                    BuildingDef def = Content.Buildings[PlaceDefId];
                    if (def.Kind == BuildingKind.Rail)
                    {
                        TryPlaceRail(tile);
                        _lastDragTile = tile;

                        return;
                    }

                    Int2 origin = PlacementOrigin(def);
                    string? err = Planet.CanPlace(def, origin, PlaceRot);
                    if (err is not null)
                    {
                        Notify(err, Colors.OrangeRed);

                        return;
                    }

                    Send(new PlaceBuilding(def.Id, origin, PlaceRot));

                    return;
                }
            case ToolKind.PlaceTrain:
                Send(new PlaceTrain(tile, PlaceRot, TrainPreset));

                return;
            case ToolKind.Deconstruct:
                {
                    if (Planet.Sites.FirstOrDefault(s => SiteCovers(s, tile)) is { } site)
                    {
                        Send(new CancelSite(site.Id));

                        return;
                    }

                    if (TrainAt(tile) is { } tr)
                    {
                        Send(new DeconstructEntity(tr.Id));

                        return;
                    }

                    if (Planet.BuildingAt(tile) is { } b)
                    {
                        Send(new DeconstructEntity(b.Id));

                        return;
                    }

                    if (Planet.IsRail(tile))
                    {
                        Send(new DeconstructRail(tile));
                    }

                    return;
                }
            case ToolKind.PickHaulFrom:
                if (Planet.BuildingAt(tile) is { } from)
                {
                    PickFromId = from.Id;
                    Tool = ToolKind.PickHaulTo;
                    Notify($"Haul from {from.Name}. Now click the destination.", Colors.Khaki);
                }

                return;
            case ToolKind.PickHaulTo:
                if (Planet.BuildingAt(tile) is { } to)
                {
                    Send(new AssignJob(PickUnitId, new Job(JobKind.Haul, FromId: PickFromId, ToId: to.Id)));
                    SetTool(ToolKind.Select);
                }

                return;
            case ToolKind.PickTrain:
                if (TrainAt(tile) is { } train)
                {
                    Send(new AssignJob(PickUnitId, new Job(JobKind.OperateTrain, TrainId: train.Id)));
                    SetTool(ToolKind.Select);
                }

                return;
            case ToolKind.PickStop:
                if ((Planet.BuildingAt(tile) is { Def.Kind: BuildingKind.RailStop } stop) && (World.Get<Train>(SelectedId) is { } st))
                {
                    ScheduleStop[] stops = st.Schedule.Append(new ScheduleStop(stop.Id, st.Schedule.Count % 2 == 0 ? StopAction.Load : StopAction.Unload)).ToArray();
                    Send(new SetSchedule(st.Id, stops));
                    Notify($"Added {stop.Name} to {st.Name}'s schedule", Colors.Khaki);
                }

                return;
            default:
                SelectedId = PickEntity(tile);

                return;
        }
    }

    private void RightClick()
    {
        if (Tool != ToolKind.Select)
        {
            SetTool(ToolKind.Select);

            return;
        }

        Int2 tile = HoverTile;
        if (Mineable(tile))
        {
            Send(new MineTile(Leader.Id, tile));
            _mining = true;
            _sweepSent = tile;
        }
        else if (Leader.MiningTile is not null)
        {
            Send(new MineTile(Leader.Id, null));
        }
    }

    private bool Mineable(Int2 tile) => (Planet.ResourceAt(tile) is not null) && (Planet.OccupantAt(tile) == 0);

    /// <summary>Holding right-click and sweeping over ore retargets mining to the tile under the cursor.</summary>
    private void SweepMining()
    {
        if ((_mining == false) || (Input.IsMouseButtonPressed(MouseButton.Right) == false))
        {
            _mining = false;

            return;
        }

        Int2 tile = HoverTile;
        if ((_sweepSent != tile) && (Leader.MiningTile != tile) && Mineable(tile))
        {
            Send(new MineTile(Leader.Id, tile));
            _sweepSent = tile;
        }
    }

    /// <summary>Empties the leader's pockets into the nearest lander or crate within reach.</summary>
    private void UnloadPockets()
    {
        Unit leader = Leader;
        if (leader.Carry.IsEmpty)
        {
            Notify("Your pockets are empty", Colors.Khaki);

            return;
        }

        Building? store = Planet.Buildings
            .Where(b => (b.Def.Kind is BuildingKind.Lander or BuildingKind.Chest) && Planet.InLeaderReach(leader, b.Origin))
            .OrderBy(b => Planet.SubTorus.DistSq(leader.Pos, b.CenterSub))
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        if (store is null)
        {
            Notify("No lander or crate within reach to unload into", Colors.OrangeRed);

            return;
        }

        Send(new DepositToBuilding(leader.Id, store.Id));
        Notify($"Unloaded pockets into {store.Name}", Colors.Khaki);
    }

    /// <summary>Whether a construction site's footprint (or planned train tiles) includes the given tile.</summary>
    public bool SiteCovers(ConstructionSite s, Int2 tile)
    {
        if (s.Kind == SiteKind.BuildTrain)
        {
            return s.TrainTiles.Contains(tile);
        }

        Int2 d = Planet.Torus.Delta(s.Origin, tile);

        return (d.X >= 0) && (d.Y >= 0) && (d.X < s.W) && (d.Y < s.H);
    }

    /// <summary>The train occupying the given tile, if any.</summary>
    public Train? TrainAt(Int2 tile) => Planet.Trains.FirstOrDefault(t => t.Tiles.Contains(tile));

    private int PickEntity(Int2 tile)
    {
        Vector2 mouse = ScreenToTileF(GetViewport().GetMousePosition());
        foreach (Unit u in Planet.Units)
        {
            if (WrapDelta(mouse, SubToTile(u.Pos)).Length() < 0.6f)
            {
                return u.Id;
            }
        }

        if (TrainAt(tile) is { } t)
        {
            return t.Id;
        }

        int occ = Planet.OccupantAt(tile);
        if (occ != 0)
        {
            return occ;
        }

        ConstructionSite? site = Planet.Sites.FirstOrDefault(s => SiteCovers(s, tile));

        return site?.Id ?? 0;
    }

    // ------------------------------------------------------------------ debug / automation hooks

    private bool _demoDone;

    /// <summary>
    /// FACTORY_DEMO=1 queues a small sample base and fast-forwards (all via normal Commands).
    /// FACTORY_SCREENSHOT=/path.png saves a frame after ~3s and quits (visual smoke test).
    /// </summary>
    private void DebugHooks()
    {
        // FACTORY_REPLAY=/path.replay.json opens a saved replay, e.g. a playtest bot's run or a softlock finding.
        string? replayPath = System.Environment.GetEnvironmentVariable("FACTORY_REPLAY");
        if ((_demoDone == false) && (replayPath is not null))
        {
            _demoDone = true;
            Replay replay = Replay.FromJson(System.IO.File.ReadAllText(replayPath));
            NewGame(replay.Seed, replay);
            Building lander = World.Get<Building>(Planet.LanderId)!;
            Cam = new Vector2(lander.Origin.X + 1.5f, lander.Origin.Y + 3f);
            if (float.TryParse(System.Environment.GetEnvironmentVariable("FACTORY_ZOOM"), System.Globalization.CultureInfo.InvariantCulture, out float zoom))
            {
                Zoom = zoom;
            }

            _hud.ToggleHelp();
        }

        if ((_demoDone == false) && (System.Environment.GetEnvironmentVariable("FACTORY_DEMO") == "1"))
        {
            _demoDone = true;
            Int2 l = World.Get<Building>(Planet.LanderId)!.Origin;
            List<Unit> f = Planet.Units.Where(u => u.IsLeader == false).ToList();
            Send(new AssignJob(f[0].Id, new Job(JobKind.Build)));
            Send(new AssignJob(f[1].Id, new Job(JobKind.Build)));
            Send(new AssignJob(f[2].Id, new Job(JobKind.Gather, Resource: "tree")));
            Send(new PlaceBuilding("stone-furnace", new Int2(l.X + 5, l.Y), SimDir.North));
            Send(new PlaceBuilding("stone-furnace", new Int2(l.X + 5, l.Y + 3), SimDir.North));
            Send(new PlaceBuilding("chest", new Int2(l.X - 3, l.Y), SimDir.North));
            Send(new PlaceBuilding("power-pole", new Int2(l.X - 3, l.Y + 3), SimDir.North));
            Send(new PlaceBuilding("lab", new Int2(l.X - 6, l.Y + 2), SimDir.North));
            for (int i = -6; i <= 8; i++)
            {
                Send(new PlaceBuilding("rail", new Int2(l.X + i, l.Y + 6), SimDir.North));
            }

            Send(new PlaceBuilding("rail-stop", new Int2(l.X + 7, l.Y + 5), SimDir.South));
            World.Run(60 * 60);
            Send(new PlaceTrain(new Int2(l.X + 2, l.Y + 6), SimDir.East, ["hand-cart", "mine-cart"]));
            Send(new QueueCraft(Planet.LanderId, "iron-gear", 10));
            World.Run(60 * 60);
            // Show the commander hand-mining the nearest ore.
            Int2 me = Leader.Tile;
            Int2? ore = null;
            for (int r = 1; (r < 30) && (ore is null); r++)
            {
                for (int d = -r; (d <= r) && (ore is null); d++)
                {
                    foreach (Int2 t in new[] { me + new Int2(d, -r), me + new Int2(d, r), me + new Int2(-r, d), me + new Int2(r, d) })
                    {
                        if ((ore is null) && (Planet.ResourceAt(Planet.Torus.Wrap(t)) is { IsTree: false }) && (Planet.OccupantAt(Planet.Torus.Wrap(t)) == 0))
                        {
                            ore = Planet.Torus.Wrap(t);
                        }
                    }
                }
            }

            if (ore is { } target)
            {
                Send(new MineTile(Leader.Id, target));
                World.Run(60 * 15);
            }

            SelectedId = Planet.Trains.FirstOrDefault()?.Id ?? Planet.LanderId;
            _hud.ToggleHelp();
            Zoom = 1.4f;
        }

        string? shot = System.Environment.GetEnvironmentVariable("FACTORY_SCREENSHOT");
        if ((shot is not null) && (Clock > 3.0))
        {
            GD.Print($"[shot] viewport={GetViewportRect().Size} zoom={Zoom} window={DisplayServer.WindowGetSize()} scale={DisplayServer.ScreenGetScale()}");
            // With HDR 2D the viewport holds linear colour; convert so the file matches what is on screen.
            Image frame = GetViewport().GetTexture().GetImage();
            if (GetViewport().UseHdr2D)
            {
                frame.Convert(Image.Format.Rgba8);
                frame.LinearToSrgb();
            }

            frame.SavePng(shot);
            GetTree().Quit();
        }
    }

    // ------------------------------------------------------------------ save / load (replay based)

    private const string SavePath = "user://quicksave.replay.json";

    private void QuickSave()
    {
        using Godot.FileAccess f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        f.StoreString(Replay.Capture(World).ToJson());
        Notify($"Saved ({ProjectSettings.GlobalizePath(SavePath)})", Colors.LightGreen);
    }

    private void QuickLoad()
    {
        if (Godot.FileAccess.FileExists(SavePath) == false)
        {
            Notify("No quicksave yet (F5)", Colors.OrangeRed);

            return;
        }

        Replay replay = Replay.FromJson(Godot.FileAccess.GetFileAsString(SavePath));
        NewGame(replay.Seed, replay);
        Notify(World.Checksum() == replay.FinalChecksum ? "Loaded quicksave" : "Loaded, but checksum mismatch (content changed?)", Colors.LightGreen);
    }

    // ------------------------------------------------------------------ input map

    private static void RegisterInput()
    {
        static void Bind(string action, params Key[] keys)
        {
            if (InputMap.HasAction(action) == false)
            {
                InputMap.AddAction(action);
            }

            foreach (Key key in keys)
            {
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
            }
        }

        Bind("move_up", Key.W, Key.Up);
        Bind("move_down", Key.S, Key.Down);
        Bind("move_left", Key.A, Key.Left);
        Bind("move_right", Key.D, Key.Right);
        Bind("rotate", Key.R);
        Bind("cancel", Key.Escape, Key.Q);
        Bind("deconstruct", Key.X);
        Bind("unload", Key.F);
        Bind("pause", Key.Space);
        Bind("speed_up", Key.Equal, Key.KpAdd);
        Bind("speed_down", Key.Minus, Key.KpSubtract);
        Bind("research", Key.T);
        Bind("help", Key.H);
        Bind("zoom_in", Key.Pageup);
        Bind("zoom_out", Key.Pagedown);
        Bind("quicksave", Key.F5);
        Bind("quickload", Key.F9);
    }
}
