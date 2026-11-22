using Factory.Sim;
using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game;

/// <summary>
/// Draws everything on the planet that moves or changes often: rails and wires, buildings, trains, units and the
/// interaction overlays. The art direction follows the technology: the ground, people and the steam
/// and coal era are drawn grounded and weathered, while ore crystals and electric or ship technology
/// are clean and luminous. Only colours brighter than 1.0 bloom (see the glow set up in
/// <see cref="GameRoot"/>), so glow is reserved for high-tech trim, fire and glinting ore.
/// Everything is drawn at the camera's wrap-around position, so the planet tiles seamlessly.
/// </summary>
public partial class WorldView : Node2D
{
    /// <summary>The colour and strength of every shadow, applied once to the merged shadow layer.</summary>
    public static readonly Color ShadowTint = new(0.07f, 0.05f, 0.03f, 0.5f);

    /// <summary>The colour of the scuffed ground under buildings.</summary>
    private static readonly Color _decalTint = new(0.16f, 0.12f, 0.08f, 0.85f);

    /// <summary>Which layer of the world this view draws.</summary>
    public WorldLayer Layer { get; set; } = WorldLayer.Things;

    /// <summary>The game root that owns the camera, tools and the simulation being drawn.</summary>
    public GameRoot Root { get; set; } = null!;

    private Font _font = null!;

    private Planet P => Root.Planet;
    private float T => GameRoot.TilePx * Root.Zoom;
    private float Time => (float)Root.Clock;

    /// <inheritdoc />
    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    /// <summary>Parses a <c>#rrggbb</c> content colour.</summary>
    /// <param name="hex">Colour string from content data.</param>
    /// <returns>The colour.</returns>
    public static Color Hex(string hex) => Color.FromHtml(hex);

    /// <summary>Scales a colour's brightness past 1.0 so it blooms; alpha is unchanged.</summary>
    /// <param name="c">Base colour.</param>
    /// <param name="k">Brightness multiplier; values above roughly 1.15 start to glow.</param>
    /// <returns>The brightened colour.</returns>
    public static Color Hdr(Color c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);

    private static Color A(Color c, float a) => new(c.R, c.G, c.B, a);

    private Vector2 S(Vector2 tile) => Root.TileToScreen(tile);

    private Vector2 S(Int2 tile) => Root.TileToScreen(new Vector2(tile.X, tile.Y));

    private Vector2 Center(Int2 tile) => S(tile) + new Vector2(T / 2, T / 2);

    private Rect2 RectOf(Int2 origin, int w, int h) => new(S(origin), new Vector2(w * T, h * T));

    private static uint Hash(int x, int y, uint seed = 0) => GroundPainter.Hash(x, y, seed);

    /// <inheritdoc />
    public override void _Draw()
    {
        // Ground, ore and trees come from TerrainLayer, drawn just before the world layers.
        (int x0, int y0, int x1, int y1) = VisibleTiles(GetViewportRect().Size);
        switch (Layer)
        {
            case WorldLayer.Decals:
                foreach (Building b in P.Buildings)
                {
                    DrawBakedDecal(b);
                }

                return;
            case WorldLayer.Ground:
                DrawWaterGlints(x0, y0, x1, y1);
                DrawGrid(x0, y0, x1, y1);
                DrawRails(x0, y0, x1, y1);
                foreach (ConstructionSite s in P.Sites)
                {
                    DrawSite(s);
                }

                // Belts lie flat on the ground, under the shadows.
                foreach (Building b in P.Buildings.Where(b => b.Def.Kind == BuildingKind.Belt))
                {
                    DrawBuilding(b);
                }

                return;
            case WorldLayer.Shadows:
                foreach (Building b in P.Buildings)
                {
                    DrawBakedShadow(b);
                }

                DrawMovingShadows();

                return;
            case WorldLayer.Lights:
                foreach (Building b in P.Buildings)
                {
                    DrawBakedLight(b);
                }

                return;
        }

        // The rest go back to front, so taller models overlap the row behind.
        foreach (Building b in P.Buildings.Where(b => b.Def.Kind != BuildingKind.Belt).OrderBy(b => b.Origin.Y + b.H))
        {
            DrawBuilding(b);
        }

        foreach ((Vector2 at, Art.Emit kind, int seed) in _emitting)
        {
            Emit(at, kind, seed);
        }

        _emitting.Clear();
        DrawPowerWires();
        DrawShipments();
        DrawRoutes();
        foreach (Train t in P.Trains)
        {
            DrawTrain(t);
        }

        // Back to front, so nearer people overlap the ones behind them.
        foreach (Unit u in P.Units.OrderBy(unit => unit.Pos.Y))
        {
            DrawUnit(u);
        }

        foreach (Building b in P.Buildings)
        {
            DrawStatus(b);
        }

        DrawMiningTarget();
        DrawSelection();
        DrawPreview();
    }

    /// <summary>The building's baked 3D sprite, if its model has been baked.</summary>
    private Art.BakedSprite? Baked(Building b) => Root.Art.TryGet(b.Def.Id, b.Rot, out Art.BakedSprite sprite) ? sprite : null;

    /// <summary>The shadow mask, in white; the shadow layer tints and fades all of them together.</summary>
    private void DrawBakedShadow(Building b)
    {
        if ((Baked(b) is { } sprite) && (sprite.Shadows.Length > 0) && OnScreen(RectOf(b.Origin, b.W, b.H)))
        {
            int frame = sprite.FrameAt(Time, b.Id, b.Status == MachineStatus.Working);
            DrawTextureRect(sprite.Shadows[System.Math.Min(frame, sprite.Shadows.Length - 1)], new Rect2(S(b.Origin) + (sprite.ShadowOffset * T), sprite.ShadowSize * T), false);
        }
    }

    private void DrawBakedDecal(Building b)
    {
        if ((Baked(b) is { Decal: { } decal } sprite) && OnScreen(RectOf(b.Origin, b.W, b.H)))
        {
            DrawTextureRect(decal, new Rect2(S(b.Origin) + (sprite.DecalOffset * T), sprite.DecalSize * T), false, _decalTint);
        }
    }

    /// <summary>Draws a baked model: its frame (animated while working), the glow layer brightened while working, and smoke.</summary>
    private void DrawBaked(Building b, Art.BakedSprite sprite, bool working)
    {
        Rect2 rect = new Rect2(S(b.Origin) + (sprite.Offset * T), sprite.Size * T);
        int frame = sprite.FrameAt(Time, b.Id, working);
        DrawTextureRect(sprite.Frames[frame], rect, false);
        // A loaded launch pad shows its rocket standing on it.
        if ((b.Def.Kind == BuildingKind.LaunchPad) && (b.Def.Vessel is { } vessel) && (b.Inv.Count(vessel) > 0) && Root.Art.TryGet("rocket-on-pad", b.Rot, out Art.BakedSprite rocket))
        {
            DrawTextureRect(rocket.Frames[0], new Rect2(S(b.Origin) + (rocket.Offset * T), rocket.Size * T), false);
        }

        // What a crafting machine is making, as a small badge on top (like an alt view).
        if ((b.Recipe is { } recipe) && (b.Def.Kind is BuildingKind.Assembler or BuildingKind.Furnace) && (Root.Zoom > 0.6f) && (recipe.Outputs.Length > 0))
        {
            Vector2 at = S(b.Origin) + (new Vector2(b.W * 0.5f, b.H * 0.42f) * T);
            float size = T * Mathf.Min(0.75f, 0.3f * b.W);
            DrawCircle(at, size * 0.62f, new Color(0.05f, 0.07f, 0.1f, 0.72f));
            DrawArc(at, size * 0.62f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.25f), Mathf.Max(1, T * 0.03f));
            Ui.Icons.Item(this, new Rect2(at - new Vector2(size / 2, size / 2), new Vector2(size, size)), P.World.Content.Item(recipe.Outputs[0].Item));
        }

        if (working)
        {
            foreach ((Vector2 at, Art.Emit kind) in sprite.Emitters)
            {
                _emitting.Add((S(b.Origin) + (at * T), kind, b.Id));
            }
        }
    }

    /// <summary>Emitters gathered while drawing buildings, drawn after all of them so no building covers its smoke.</summary>
    private readonly List<(Vector2 At, Art.Emit Kind, int Seed)> _emitting = [];

    /// <summary>A soft round puff, white in the middle fading to nothing, for particles.</summary>
    private static Texture2D Puff => field ??= new GradientTexture2D
    {
        Width = 64,
        Height = 64,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(0.5f, 0f),
        Gradient = new Gradient { Offsets = [0f, 0.45f, 1f], Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0)] },
    };

    private void SoftDot(Vector2 at, float radius, Color color) => DrawTextureRect(Puff, new Rect2(at - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2)), false, color);

    /// <summary>
    /// Smoke, steam, sparks, dust or vapour from a working machine. Each particle has its own life, size and
    /// drift from a hash of its index, so a row of chimneys never puffs in step.
    /// </summary>
    private void Emit(Vector2 at, Art.Emit kind, int seed)
    {
        (int count, float rate) = kind switch { Art.Emit.Smoke => (7, 0.32f), Art.Emit.Steam => (6, 0.75f), Art.Emit.Sparks => (9, 1.6f), Art.Emit.Dust => (5, 0.6f), _ => (6, 0.28f) };
        for (int i = 0; i < count; i++)
        {
            uint h = Hash(seed, i, (uint)kind);
            float r1 = (h & 255) / 255f, r2 = ((h >> 8) & 255) / 255f, r3 = ((h >> 16) & 255) / 255f;
            float life = 0.7f + (r1 * 0.6f);
            float ph = Mathf.PosMod((Time * rate / life) + (i / (float)count) + (seed * 0.137f), 1f);
            float wobble = Mathf.Sin((ph * 6f) + (r2 * 9f)) * T * 0.07f;
            switch (kind)
            {
                case Art.Emit.Smoke:
                    SoftDot(at + new Vector2((T * (0.25f + (r2 * 0.3f)) * ph) + wobble, -T * (0.9f + (r3 * 0.5f)) * ph), T * (0.12f + (ph * (0.35f + (r3 * 0.2f)))), new Color(0.3f, 0.28f, 0.26f, 0.55f * (1 - ph) * Mathf.Min(1, ph * 8)));
                    break;
                case Art.Emit.Steam:
                    SoftDot(at + new Vector2((T * 0.2f * ph) + wobble, -T * (0.6f + (r3 * 0.4f)) * ph), T * (0.08f + (ph * 0.3f)), new Color(0.95f, 0.95f, 0.97f, 0.6f * (1 - ph) * Mathf.Min(1, ph * 6)));
                    break;
                case Art.Emit.Sparks:
                    float ang = (-Mathf.Pi / 2) + ((r2 - 0.5f) * 2.6f);
                    float speed = T * (0.25f + (r3 * 0.55f));
                    Vector2 p = at + (new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed * ph) + new Vector2(0, T * 0.9f * ph * ph);
                    DrawCircle(p, Mathf.Max(1, T * 0.022f * (1 - (ph * 0.5f))), Hdr(new Color(1f, 0.72f, 0.32f, 1 - ph), 2.4f));
                    break;
                case Art.Emit.Dust:
                    float side = r2 < 0.5f ? 1 : -1;
                    SoftDot(at + new Vector2(side * T * (0.3f + (r3 * 0.3f)) * ph, -T * 0.15f * ph), T * (0.1f + (ph * 0.25f)), new Color(0.5f, 0.42f, 0.32f, 0.4f * (1 - ph)));
                    break;
                default:
                    SoftDot(at + new Vector2((T * 0.5f * ph) + wobble, -T * (1.2f + (r3 * 0.6f)) * ph), T * (0.14f + (ph * 0.5f)), new Color(0.75f, 0.9f, 0.95f, 0.3f * (1 - ph) * Mathf.Min(1, ph * 5)));
                    break;
            }
        }
    }

    private readonly Dictionary<int, float> _lit = [];

    /// <summary>
    /// The light a working machine gives off, on the additive light layer. It fades up over a third of a
    /// second when the machine starts and down when it stops, and fire flickers.
    /// </summary>
    private void DrawBakedLight(Building b)
    {
        if ((Baked(b) is not { } sprite) || (sprite.Lights.Length == 0))
        {
            return;
        }

        float dt = Mathf.Clamp((float)GetProcessDeltaTime(), 0, 0.1f);
        bool working = b.Status == MachineStatus.Working;
        float lit = _lit.GetValueOrDefault(b.Id);
        lit = Mathf.MoveToward(lit, working ? 1 : 0, dt * 3f);
        _lit[b.Id] = lit;
        if ((lit <= 0.001f) || (OnScreen(RectOf(b.Origin, b.W, b.H)) == false))
        {
            return;
        }

        float k = lit;
        if (sprite.Flicker)
        {
            k *= 0.86f + (0.09f * Mathf.Sin((Time * 13f) + b.Id)) + (0.05f * Mathf.Sin((Time * 31f) + (b.Id * 3)));
        }

        int frame = sprite.FrameAt(Time, b.Id, working);
        Texture2D tex = sprite.Lights[System.Math.Min(frame, sprite.Lights.Length - 1)];
        DrawTextureRect(tex, new Rect2(S(b.Origin) + (sprite.LightOffset * T), sprite.LightSize * T), false, new Color(k, k, k, 1));
    }

    private (int X0, int Y0, int X1, int Y1) VisibleTiles(Vector2 size)
    {
        Vector2 half = size / 2 / T;

        return ((int)Mathf.Floor(Root.Cam.X - half.X) - 1, (int)Mathf.Floor(Root.Cam.Y - half.Y) - 1,
                (int)Mathf.Ceil(Root.Cam.X + half.X) + 1, (int)Mathf.Ceil(Root.Cam.Y + half.Y) + 1);
    }

    private bool OnScreen(Rect2 r) => r.Grow(T).Intersects(GetViewportRect());

    private void DrawArrow(Vector2 c, SimDir d, float s, Color col)
    {
        Vector2 v = new Vector2(d.Vec().X, d.Vec().Y);
        Vector2 p = new Vector2(-v.Y, v.X);
        DrawColoredPolygon([c + (v * s), c - (v * s * 0.6f) + (p * s * 0.7f), c - (v * s * 0.6f) - (p * s * 0.7f)], col);
    }

    /// <summary>Soft drop shadow cast toward the lower right, the light direction used for everything.</summary>
    private void DropShadow(Rect2 r, float strength = 0.35f)
    {
        Vector2 off = new Vector2(T * 0.12f, T * 0.16f);
        DrawRect(new Rect2(r.Position + off, r.Size), new Color(0, 0, 0, strength * 0.6f));
        DrawRect(new Rect2(r.Position + (off * 0.5f), r.Size), new Color(0, 0, 0, strength * 0.5f));
    }

    /// <summary>Player-facing text for a train's state, shared with the HUD.</summary>
    /// <param name="s">The train state.</param>
    /// <returns>A short label.</returns>
    public static string StateText(TrainState s) => s switch
    {
        TrainState.NoSchedule => "No schedule",
        TrainState.NoOperator => "Needs operator",
        TrainState.NoFuel => "No fuel",
        TrainState.NoWater => "No water",
        TrainState.Overloaded => "Too heavy!",
        TrainState.NoPath => "No route",
        TrainState.Blocked => "Blocked",
        TrainState.AtStop => "At stop",
        TrainState.Moving => "Moving",
        _ => s.ToString(),
    };
}

/// <summary>The layers the world is drawn in, bottom to top.</summary>
public enum WorldLayer
{
    /// <summary>Scuffed ground under buildings.</summary>
    Decals,

    /// <summary>Things lying flat on the ground: water glints, the grid, rails, construction sites and belts.</summary>
    Ground,

    /// <summary>Every shadow, as white masks merged into one group.</summary>
    Shadows,

    /// <summary>Buildings, wires, trains, units and the interface overlays.</summary>
    Things,

    /// <summary>The light working machines give off, drawn additively over everything.</summary>
    Lights,
}
