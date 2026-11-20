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

    /// <summary>Furthest the camera zooms out.</summary>
    public const float MinZoom = 0.45f;

    /// <summary>Closest the camera zooms in.</summary>
    public const float MaxZoom = 2.5f;

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
    public Unit Leader => World.Planets.SelectMany(p => p.Units).First(u => u.IsLeader && (u.OwnerPlayer == LocalPlayer));

    /// <summary>True if the commander is on the viewed planet and within reach of a tile on it.</summary>
    /// <param name="tile">A tile on the viewed planet.</param>
    /// <returns>Whether the commander can reach it.</returns>
    public bool InReach(Int2 tile) => (Leader.Planet == Planet) && Planet.InLeaderReach(Leader, tile);
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

    /// <summary>A tile to treat as hovered instead of the mouse's (FACTORY_HOVER, for screenshots).</summary>
    private Int2? _hoverOverride;
    /// <summary>Recent notifications, oldest first; only the last few are kept.</summary>
    public List<Message> Messages { get; } = [];
    /// <summary>Real time elapsed since start, in seconds, used for animation and message fading.</summary>
    public double Clock { get; set; }

    private TerrainLayer _terrain = null!;
    private WorldView _view = null!;
    private FogLayer _fog = null!;
    private Hud _hud = null!;
    private double _acc;
    private Int2 _lastIntent;
    private Vector2 _panIntent;
    private Int2? _lastDragTile;
    private bool _mining;
    private Int2? _sweepSent;

    /// <inheritdoc />
    public override void _Ready()
    {
        FitWindowToScreen();
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
        _fog = new FogLayer { Root = this };
        AddChild(_fog);
        _hud = new Hud { Root = this };
        AddChild(_hud);
        Notify($"Platform: {PlatformServices.Name}. Press H for controls, G for the guide.", Colors.LightSkyBlue);
    }

    /// <summary>
    /// Opens the window at about 80% of the screen. The project's 1600×900 is in physical pixels, which is
    /// a small window on a high-DPI display; the canvas_items stretch mode then scales the game to fit.
    /// Screenshot runs keep the fixed size so shots stay comparable.
    /// </summary>
    private static void FitWindowToScreen()
    {
        if ((System.Environment.GetEnvironmentVariable("FACTORY_SCREENSHOT") is not null) || (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed))
        {
            return;
        }

        Rect2I usable = DisplayServer.ScreenGetUsableRect();
        Vector2I size = new Vector2I((int)(usable.Size.X * 0.8f), (int)(usable.Size.Y * 0.8f));
        // Keep the design aspect so the first view matches what the game was laid out for.
        size = new Vector2I(System.Math.Min(size.X, size.Y * 16 / 9), System.Math.Min(size.Y, size.X * 9 / 16));
        if ((size.X <= DisplayServer.WindowGetSize().X) || (size.Y <= 0))
        {
            return;
        }

        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + ((usable.Size - size) / 2));
        GD.Print($"Window {size.X}x{size.Y} on a {usable.Size.X}x{usable.Size.Y} screen (scale {DisplayServer.ScreenGetScale()})");
    }

    /// <summary>Starts a new game from a seed, or reconstructs one by playing back a replay, and resets the view.</summary>
    /// <param name="seed">World seed used when no replay is given.</param>
    /// <param name="replay">Optional replay to restore instead of generating a fresh world.</param>
    public void NewGame(ulong seed, Replay? replay = null)
    {
        Adopt(replay is null ? new World(Content, new WorldSettings { Seed = seed }) : replay.Play(Content));
        Notify("Landing complete. Your helper bots await orders.", Colors.Cyan);
    }

    /// <summary>Makes <paramref name="world"/> the running game and resets the view to the commander.</summary>
    /// <param name="world">A new or freshly loaded world.</param>
    private void Adopt(World world)
    {
        World = world;
        PlanetId = 0;
        _leaderPlanet = Leader.Planet.Id;
        ViewPlanet(_leaderPlanet);
        SelectedId = 0;
        SetTool(ToolKind.Select);
        Messages.Clear();
        _acc = 0;
        _hud?.OnWorldReplaced();
    }

    /// <summary>The planet the commander was on last frame, to follow it when it flies.</summary>
    private int _leaderPlanet;

    /// <summary>
    /// Shows another planet. On the commander's own planet the camera follows it; elsewhere it's a remote
    /// view centred on the lander (or the middle of the map) that the movement keys pan.
    /// </summary>
    /// <param name="id">The planet to show.</param>
    public void ViewPlanet(int id)
    {
        PlanetId = System.Math.Clamp(id, 0, World.Planets.Count - 1);
        Int2 focus = Leader.Planet == Planet ? Leader.Pos
            : World.Get<Building>(Planet.LanderId) is { } lander ? lander.CenterSub
            : Fixed.TileCenter(new Int2(Planet.Width / 2, Planet.Height / 2));
        Cam = new Vector2(focus.X / (float)Fixed.One, focus.Y / (float)Fixed.One);
        SelectedId = 0;
        SetTool(ToolKind.Select);
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
        if (IsLoading)
        {
            // The old game stays on screen, frozen, under the loading overlay until the new one is ready.
            StepLoading();
            TakeScreenshotWhenDue();

            return;
        }

        TickAutosave(delta);
        HoverTile = _hoverOverride ?? ScreenToTile(GetViewport().GetMousePosition());
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

        // The commander flew to another planet: the camera goes with it.
        if (Leader.Planet.Id != _leaderPlanet)
        {
            _leaderPlanet = Leader.Planet.Id;
            ViewPlanet(_leaderPlanet);
        }

        DebugHooks();
        foreach (SimEvent e in World.Events)
        {
            Notify(e.Message, Colors.Aquamarine);
        }

        World.Events.Clear();

        // Camera follows the leader across the wrap; looking at another planet, the keys pan it instead.
        if (Leader.Planet == Planet)
        {
            Vector2 lt = SubToTile(Leader.Pos);
            Cam += WrapDelta(Cam, lt) * Mathf.Min(1f, (float)delta * 6f);
        }
        else
        {
            Cam += _panIntent * (float)delta * 30f / Zoom;
        }

        Cam = new Vector2(Mathf.PosMod(Cam.X, Planet.Width), Mathf.PosMod(Cam.Y, Planet.Height));

        if ((Tool == ToolKind.Place) && (PlaceDefId is "rail" or "conveyor-belt") && Input.IsMouseButtonPressed(MouseButton.Left) && (_lastDragTile is { } last))
        {
            DragRail(last, HoverTile);
        }

        _terrain.Refresh(delta);
        _fog.Refresh(delta);

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
        _panIntent = new Vector2(dx, dy);
        if (Leader.Planet != Planet)
        {
            intent = Int2.Zero; // remote view: the keys pan the camera, the commander stays put
        }

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
            Zoom = Mathf.Min(MaxZoom, Zoom * 1.15f);
        }

        if (Input.IsActionJustPressed("zoom_out"))
        {
            Zoom = Mathf.Max(MinZoom, Zoom / 1.15f);
        }
    }

    private void DragRail(Int2 from, Int2 to)
    {
        Int2 d = Planet.Torus.Delta(from, to);
        Int2 cur = from;
        Int2 prev = from;
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
            if (PlaceDefId == "conveyor-belt")
            {
                // A dragged belt runs the way the mouse moved.
                Int2 step = Planet.Torus.Delta(prev, cur);
                SimDir dir = step.X > 0 ? SimDir.East : step.X < 0 ? SimDir.West : step.Y > 0 ? SimDir.South : SimDir.North;
                if (Planet.CanPlace(Content.Buildings["conveyor-belt"], cur, dir) is null)
                {
                    Send(new PlaceBuilding("conveyor-belt", cur, dir));
                }

                prev = cur;
                continue;
            }

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

    /// <summary>
    /// Zoom is handled before the GUI sees the event, so the wheel works over HUD panels too, and it
    /// accepts the mouse wheel (with precise trackpad deltas) as well as trackpad pinch and two-finger
    /// scroll gestures.
    /// </summary>
    /// <param name="event">The input event.</param>
    public override void _Input(InputEvent @event)
    {
        float steps = @event switch
        {
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } mb => mb.Factor > 0 ? mb.Factor : 1,
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } mb => -(mb.Factor > 0 ? mb.Factor : 1),
            InputEventPanGesture pan => -pan.Delta.Y,
            InputEventMagnifyGesture mag => Mathf.Log(mag.Factor) / Mathf.Log(1.1f),
            _ => 0,
        };
        if ((steps == 0) || IsLoading)
        {
            return;
        }

        // Over a scrolling HUD panel (the help), the wheel scrolls it instead of zooming the map.
        for (Node? n = GetViewport().GuiGetHoveredControl(); n is not null; n = n.GetParent())
        {
            if (n is ScrollContainer or Ui.MapView)
            {
                return;
            }
        }

        Zoom = Mathf.Clamp(Zoom * Mathf.Pow(1.1f, steps), MinZoom, MaxZoom);
        GetViewport().SetInputAsHandled();
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsLoading)
        {
            return;
        }

        if (@event is InputEventMouseButton mb)
        {
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
            // Esc steps back one level: the tool, then a window or build category, then the selection,
            // and with nothing left to close it opens the game menu.
            if (Tool != ToolKind.Select)
            {
                SetTool(ToolKind.Select);
            }
            else if (_hud.BackOut() == false)
            {
                if (SelectedId != 0)
                {
                    SelectedId = 0;
                }
                else
                {
                    _hud.Toggle(HudWindow.Menu);
                }
            }
        }
        else if (k.IsAction("pipette"))
        {
            Pipette();
        }
        else if (k.IsAction("inventory"))
        {
            _hud.Toggle(HudWindow.Inventory);
        }
        else if (k.IsAction("replicator"))
        {
            _hud.Toggle(HudWindow.Replicator);
        }
        else if (k.IsAction("map"))
        {
            _hud.Toggle(HudWindow.Map);
        }
        else if (k.IsAction("system"))
        {
            _hud.Toggle(HudWindow.System);
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
        else if (k.IsAction("guide"))
        {
            _hud.ToggleHints();
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
            _hud.BuildKey((int)(k.Keycode - Key.Key1));
        }
        else if (k.Keycode == Key.Key0)
        {
            _hud.BuildKey(9);
        }
    }

    /// <summary>
    /// Q: picks the building type under the cursor as the thing to place, facing the same way, or clears
    /// the current tool when there is nothing to pick.
    /// </summary>
    private void Pipette()
    {
        BuildingDef? def = Planet.BuildingAt(HoverTile) is { } b ? b.Def : Planet.IsRail(HoverTile) ? Content.Buildings["rail"] : null;
        if ((def is null) || (def.Buildable == false) || (World.Research.Unlocked.Contains(def.Id) == false))
        {
            SetTool(ToolKind.Select);

            return;
        }

        SetTool(ToolKind.Place, def.Id);
        if (Planet.BuildingAt(HoverTile) is { } picked)
        {
            PlaceRot = picked.Rot;
        }
    }

    /// <summary>The entity under the mouse, or 0 when there is none or the mouse is over the HUD.</summary>
    public int HoverEntityId => GetViewport().GuiGetHoveredControl() is null ? PickEntity(HoverTile) : 0;

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
                    if (def.Kind == BuildingKind.Belt)
                    {
                        _lastDragTile = tile; // hold and drag to lay a run of belt
                    }

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

    private bool Mineable(Int2 tile) => (Planet.KnownResourceAt(tile) is not null) && (Planet.OccupantAt(tile) == 0);

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

    /// <summary>The nearest lander or crate within the commander's reach, optionally only ones matching a filter.</summary>
    /// <param name="filter">Extra condition a storage building must meet.</param>
    /// <returns>The storage building, or null if none is in reach.</returns>
    public Building? NearestStorage(System.Func<Building, bool>? filter = null)
    {
        Unit leader = Leader;

        return Planet.Buildings
            .Where(b => b.Def.IsDepot && InReach(b.Origin) && ((filter is null) || filter(b)))
            .OrderBy(b => Planet.SubTorus.DistSq(leader.Pos, b.CenterSub))
            .ThenBy(b => b.Id)
            .FirstOrDefault();
    }

    /// <summary>Empties the leader's pockets into the nearest lander or crate within reach.</summary>
    public void UnloadPockets()
    {
        Unit leader = Leader;
        if (leader.Carry.IsEmpty)
        {
            Notify("Your pockets are empty", Colors.Khaki);

            return;
        }

        Building? store = NearestStorage();
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
        foreach (Unit u in Planet.Units.OrderByDescending(unit => unit.Pos.Y))
        {
            if (WorldView.BoundsFor(u).HasPoint(WrapDelta(SubToTile(u.Pos), mouse)))
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
    private bool _uiHooksDone;

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
            // The replay may end on a planet with no lander (the commander flew elsewhere): then the camera stays on the commander.
            if (World.Get<Building>(Planet.LanderId) is { } lander)
            {
                Cam = new Vector2(lander.Origin.X + 1.5f, lander.Origin.Y + 3f);
            }

            if (float.TryParse(System.Environment.GetEnvironmentVariable("FACTORY_ZOOM"), System.Globalization.CultureInfo.InvariantCulture, out float zoom))
            {
                Zoom = zoom;
            }

            _hud.CloseWindows();
        }

        string? demo = System.Environment.GetEnvironmentVariable("FACTORY_DEMO");
        if ((_demoDone == false) && (demo is "1" or "2" or "3" or "4" or "5" or "6"))
        {
            _demoDone = true;
            Int2 l = World.Get<Building>(Planet.LanderId)!.Origin;
            List<Unit> f = Planet.Units.Where(u => u.IsLeader == false).ToList();
            // The helpers gather; the commander builds everything near the lander by itself.
            Send(new AssignJob(f[0].Id, new Job(JobKind.Gather, Resource: "xylo")));
            Send(new AssignJob(f[1].Id, new Job(JobKind.Gather, Resource: "regolith")));
            Send(new PlaceBuilding("burner-smelter", new Int2(l.X + 5, l.Y), SimDir.North));
            Send(new PlaceBuilding("burner-smelter", new Int2(l.X + 5, l.Y + 3), SimDir.North));
            Send(new PlaceBuilding("chest", new Int2(l.X - 3, l.Y), SimDir.North));
            Send(new PlaceBuilding("power-pole", new Int2(l.X - 3, l.Y + 3), SimDir.North));
            Send(new PlaceBuilding("lab", new Int2(l.X - 6, l.Y + 2), SimDir.North));
            for (int i = -6; i <= 8; i++)
            {
                Send(new PlaceBuilding("rail", new Int2(l.X + i, l.Y + 6), SimDir.North));
            }

            Send(new PlaceBuilding("rail-stop", new Int2(l.X + 7, l.Y + 5), SimDir.South));
            if (demo is "2" or "3" or "4" or "5" or "6")
            {
                // Dev only: skip ahead to the late game (unlock everything, stock the lander) to show the
                // robot bay, terraformers and uplink. Not a replayable game.
                foreach (TechDef tech in Content.TechList)
                {
                    World.Research.Unlocked.UnionWith(tech.Unlocks);
                }

                Building lander = World.Get<Building>(Planet.LanderId)!;
                foreach (string item in new[] { "logic-board", "siderite-plate", "drive-cog", "sintered-brick", "cupral-bar", "ballast-plate", "charge-cell" })
                {
                    lander.Inv.AddUnchecked(item, 200);
                }

                lander.Inv.AddUnchecked("servo-module", 1);
                Send(new PlaceBuilding("atmo-processor", new Int2(l.X - 4, l.Y - 5), SimDir.North));
                Send(new PlaceBuilding("hydro-seeder", new Int2(l.X, l.Y - 5), SimDir.North));
                Send(new PlaceBuilding("bio-incubator", new Int2(l.X + 4, l.Y - 5), SimDir.North));
                Send(new PlaceBuilding("settler-uplink", new Int2(l.X + 9, l.Y - 2), SimDir.North));
                Send(new PlaceBuilding("robot-bay", new Int2(l.X - 9, l.Y - 3), SimDir.North));
                if (demo is "3" or "4" or "5")
                {
                    // The Combustion Age set: refinery, generator, pylons and a launch pad.
                    lander.Inv.AddUnchecked("crude-kerogen", 100);
                    Send(new PlaceBuilding("kerogen-refinery", new Int2(l.X - 4, l.Y + 9), SimDir.North));
                    Send(new PlaceBuilding("kerogen-generator", new Int2(l.X, l.Y + 9), SimDir.North));
                    Send(new PlaceBuilding("lattice-pylon", new Int2(l.X + 3, l.Y + 10), SimDir.North));
                    Send(new PlaceBuilding("launch-pad", new Int2(l.X + 5, l.Y + 9), SimDir.North));
                }

                if (demo == "6")
                {
                    // The Stellar Age set: fusion, the space elevator, a climate engine, a collector and plasma machines.
                    foreach (string item in new[] { "titanium-plate", "osmiridium-ingot", "promethium-lattice", "photonic-wafer", "polymer-resin", "cupral-filament" })
                    {
                        lander.Inv.AddUnchecked(item, 200);
                    }

                    Send(new PlaceBuilding("fusion-reactor", new Int2(l.X - 11, l.Y - 12), SimDir.North));
                    Send(new PlaceBuilding("space-elevator", new Int2(l.X - 6, l.Y - 13), SimDir.North));
                    Send(new PlaceBuilding("climate-engine", new Int2(l.X, l.Y - 12), SimDir.North));
                    Send(new PlaceBuilding("orbital-collector", new Int2(l.X + 5, l.Y - 11), SimDir.North));
                    Send(new PlaceBuilding("plasma-smelter", new Int2(l.X + 9, l.Y - 10), SimDir.North));
                    Send(new PlaceBuilding("plasma-drill", new Int2(l.X + 12, l.Y - 11), SimDir.North));
                    for (int x = l.X - 12; x <= l.X + 14; x += 6)
                    {
                        Send(new PlaceBuilding("power-pole", new Int2(x, l.Y - 7), SimDir.North));
                    }
                }

                if (demo == "5")
                {
                    // The Clean Age set: solar panels, a wind turbine, a payload launcher and a hover module.
                    foreach (string item in new[] { "photonic-wafer", "polymer-resin", "cupral-filament" })
                    {
                        lander.Inv.AddUnchecked(item, 100);
                    }

                    Send(new PlaceBuilding("solar-panel", new Int2(l.X - 9, l.Y + 9), SimDir.North));
                    Send(new PlaceBuilding("solar-panel", new Int2(l.X - 7, l.Y + 9), SimDir.North));
                    Send(new PlaceBuilding("wind-turbine", new Int2(l.X - 9, l.Y + 12), SimDir.North));
                    Send(new PlaceBuilding("payload-launcher", new Int2(l.X - 5, l.Y + 12), SimDir.North));
                    // Belts carrying plates from a crate along a sorter line, and two logistics stations.
                    lander.Inv.AddUnchecked("charge-cell", 50);
                    Send(new PlaceBuilding("chest", new Int2(l.X - 12, l.Y + 16), SimDir.North));
                    Send(new PlaceBuilding("sorter", new Int2(l.X - 11, l.Y + 16), SimDir.East));
                    for (int x = l.X - 10; x <= l.X - 3; x++)
                    {
                        Send(new PlaceBuilding("conveyor-belt", new Int2(x, l.Y + 16), SimDir.East));
                    }

                    Send(new PlaceBuilding("power-pole", new Int2(l.X - 6, l.Y + 15), SimDir.North));
                    Send(new PlaceBuilding("logistics-station", new Int2(l.X - 14, l.Y), SimDir.North));
                    Send(new PlaceBuilding("power-pole", new Int2(l.X - 11, l.Y), SimDir.North));
                    Send(new PlaceBuilding("solar-panel", new Int2(l.X - 14, l.Y + 3), SimDir.North));
                    Send(new PlaceBuilding("logistics-station", new Int2(l.X + 13, l.Y + 1), SimDir.North));
                    Send(new PlaceBuilding("power-pole", new Int2(l.X + 12, l.Y + 1), SimDir.North));
                    Send(new PlaceBuilding("solar-panel", new Int2(l.X + 13, l.Y + 4), SimDir.North));
                    Leader.Carry.AddUnchecked("hover-module", 1);
                    Send(new InstallModule(Leader.Id, Leader.Id, "hover-module"));
                }
            }

            World.Run(60 * 60);
            if (demo == "6")
            {
                foreach (Int2 stop in new[] { new Int2(l.X - 6, l.Y - 7), new Int2(l.X + 6, l.Y - 7) })
                {
                    Send(new WalkTo(Leader.Id, stop));
                    World.Run(60 * 90);
                }

                if (Planet.Buildings.FirstOrDefault(b => b.Def.Id == "fusion-reactor") is { } reactor)
                {
                    reactor.Inv.AddUnchecked("fusion-cell", 10);
                }

                if (Planet.Buildings.FirstOrDefault(b => b.Def.Id == "orbital-collector") is { } collector)
                {
                    Send(new SetRecipe(collector.Id, "collect-helium-3"));
                }

                if (Planet.Buildings.FirstOrDefault(b => b.Def.Id == "climate-engine") is { } climate)
                {
                    Send(new SetRecipe(climate.Id, "climate-atmosphere"));
                    climate.Inv.AddUnchecked("catalyst-pack", 4);
                    climate.Inv.AddUnchecked("helium-3", 4);
                }

                Send(new WalkTo(Leader.Id, new Int2(l.X - 2, l.Y - 8)));
                World.Run(60 * 10);
            }

            if (demo == "5")
            {
                // Walk the commander past the Clean Age ghosts so it builds them.
                foreach (Int2 stop in new[] { new Int2(l.X - 6, l.Y + 13), new Int2(l.X - 6, l.Y + 18), new Int2(l.X - 11, l.Y + 3), new Int2(l.X + 11, l.Y + 5) })
                {
                    Send(new WalkTo(Leader.Id, stop));
                    World.Run(60 * 60);
                }
            }

            Send(new PlaceTrain(new Int2(l.X + 2, l.Y + 6), SimDir.East, ["hand-cart", "mine-cart"]));
            Send(new QueueCraft(Planet.LanderId, "drive-cog", 10));
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

            if ((demo is "3" or "4" or "5") && (Planet.Buildings.FirstOrDefault(b => b.Def.Kind == BuildingKind.LaunchPad) is { } pad))
            {
                pad.Inv.AddUnchecked("ferry-rocket", 1);
                pad.Inv.AddUnchecked("kerogen-fuel", 20);
                if (Planet.Buildings.FirstOrDefault(b => b.Def.Id == "kerogen-generator") is { } gen)
                {
                    gen.Inv.AddUnchecked("kerogen-fuel", 5);
                }

                if (Planet.Buildings.FirstOrDefault(b => b.Def.Id == "kerogen-refinery") is { } refinery)
                {
                    Send(new SetRecipe(refinery.Id, "kerogen-fuel"));
                }

                List<Building> stations = Planet.Buildings.Where(b => b.Def.Kind == BuildingKind.LogisticsStation).OrderBy(b => b.Id).ToList();
                if ((demo == "5") && (stations.Count == 2))
                {
                    stations[0].Inv.AddUnchecked("siderite-plate", 300);
                    Send(new SetStationSlot(stations[0].Id, 0, "siderite-plate", StationMode.Supply));
                    Send(new SetStationSlot(stations[1].Id, 0, "siderite-plate", StationMode.Demand));
                }

                if ((demo == "5") && (Planet.Buildings.FirstOrDefault(b => b.Def.Id == "chest" && b.Origin.Y == l.Y + 16) is { } crate))
                {
                    crate.Inv.AddUnchecked("cupral-bar", 40);
                }

                if ((demo == "5") && (Planet.Buildings.FirstOrDefault(b => b.Def.Kind == BuildingKind.PayloadLauncher) is { } launcher))
                {
                    launcher.Inv.AddUnchecked("catalyst-pack", 5);
                    Send(new SetLaunchTarget(launcher.Id, World.Planets.First(p => p.Def.Id == "rime").Id));
                }

                World.Run(60 * 5);
                if (demo == "4")
                {
                    Send(new WalkTo(Leader.Id, pad.Origin + new Int2(-1, 1)));
                    World.Run(60 * 20);
                    pad.Inv.AddUnchecked("siderite-plate", 50);
                    Send(new LaunchFerry(Leader.Id, pad.Id, World.Planets.First(p => p.Def.Id == "rime").Id, [.. Planet.Units.Where(u => u.IsLeader == false).Select(u => u.Id)]));
                    World.Run(60);
                }
            }

            SelectedId = Planet.Trains.FirstOrDefault()?.Id ?? Planet.LanderId;
            _hud.CloseWindows();
            Zoom = 1.4f;
            if (demo == "6")
            {
                SelectedId = 0;
                Zoom = 0.9f;
            }

            if ((demo == "5") && (Planet.Buildings.FirstOrDefault(b => b.Def.IsStation) is { } station))
            {
                SelectedId = station.Id;
                Zoom = 0.9f;
            }
        }

        // FACTORY_WINDOW=inventory|replicator|tech|help|victory|system|map and FACTORY_CATEGORY=<1-7> open HUD panels for screenshots;
        // FACTORY_SELECT=<building id> selects the nearest building of that type to the commander; "helper" selects a helper bot.
        if ((_uiHooksDone == false) && (Clock > 0.5))
        {
            _uiHooksDone = true;
            string? selectDef = System.Environment.GetEnvironmentVariable("FACTORY_SELECT");
            if ((selectDef == "helper") && (Planet.Units.FirstOrDefault(u => u.IsLeader == false) is { } helper))
            {
                SelectedId = helper.Id;
            }
            else if (Planet.Buildings.Where(b => b.Def.Id == selectDef).OrderBy(b => Planet.SubTorus.DistSq(Leader.Pos, b.CenterSub)).FirstOrDefault() is { } pick)
            {
                SelectedId = pick.Id;
            }

            // FACTORY_PLANET=<planet id> views another planet (remote view).
            if (System.Environment.GetEnvironmentVariable("FACTORY_PLANET") is { } planetId &&
                (World.Planets.FindIndex(p => p.Def.Id == planetId) is int index and >= 0))
            {
                ViewPlanet(index);
            }

            // FACTORY_WINDOW=save|load open the save and load windows.
            if (System.Enum.TryParse(System.Environment.GetEnvironmentVariable("FACTORY_WINDOW"), true, out HudWindow window))
            {
                _hud.Toggle(window);
            }

            // FACTORY_HOVER=<resource id> points the hover at the nearest explored tile of that resource.
            if (System.Environment.GetEnvironmentVariable("FACTORY_HOVER") is { } hoverRes)
            {
                Int2 at = Fixed.ToTile(Leader.Pos);
                _hoverOverride = Enumerable.Range(0, Planet.Width * Planet.Height).Select(i => new Int2(i % Planet.Width, i / Planet.Width))
                    .Where(t => Planet.KnownResourceAt(t)?.Id == hoverRes).OrderBy(t => Planet.Torus.DistSq(at, t)).Cast<Int2?>().FirstOrDefault();
            }

            // FACTORY_QUEUE=<recipe>:<count> queues crafts on the lander (screenshots of the replicator queue).
            if ((System.Environment.GetEnvironmentVariable("FACTORY_QUEUE") is { } queue) && (queue.Split(':') is [string recipe, string n]) &&
                int.TryParse(n, out int count))
            {
                Send(new QueueCraft(Planet.LanderId, recipe, count));
            }

            if (System.Environment.GetEnvironmentVariable("FACTORY_JOBMENU") == "1")
            {
                _hud.DebugOpenJobMenu();
            }

            if (int.TryParse(System.Environment.GetEnvironmentVariable("FACTORY_CATEGORY"), out int category))
            {
                _hud.BuildKey(category - 1);
            }

            SaveLoadHooks();
        }

        TakeScreenshotWhenDue();
    }

    /// <summary>FACTORY_SCREENSHOT=/path.png: saves a frame after about 3 s and quits.</summary>
    private void TakeScreenshotWhenDue()
    {
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
        Bind("cancel", Key.Escape);
        Bind("pipette", Key.Q);
        Bind("deconstruct", Key.X);
        Bind("unload", Key.F);
        Bind("inventory", Key.E, Key.I, Key.Tab);
        Bind("replicator", Key.C);
        Bind("pause", Key.Space);
        Bind("speed_up", Key.Equal, Key.KpAdd);
        Bind("speed_down", Key.Minus, Key.KpSubtract);
        Bind("research", Key.T);
        Bind("map", Key.M);
        Bind("system", Key.P);
        Bind("help", Key.H);
        Bind("guide", Key.G);
        Bind("zoom_in", Key.Pageup);
        Bind("zoom_out", Key.Pagedown);
        Bind("quicksave", Key.F5);
        Bind("quickload", Key.F9);
    }
}
