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
        // Ground, ore and trees come from TerrainLayer, drawn just before this node.
        (int x0, int y0, int x1, int y1) = VisibleTiles(GetViewportRect().Size);
        DrawWaterGlints(x0, y0, x1, y1);
        DrawGrid(x0, y0, x1, y1);
        DrawRails(x0, y0, x1, y1);
        foreach (ConstructionSite s in P.Sites)
        {
            DrawSite(s);
        }

        // Baked buildings' shadows go down first, so they fall on the ground but never over another building.
        foreach (Building b in P.Buildings)
        {
            DrawBakedShadow(b);
        }

        // Belts lie flat under everything else; the rest go back to front, so taller models overlap the row behind.
        foreach (Building b in P.Buildings.Where(b => b.Def.Kind == BuildingKind.Belt))
        {
            DrawBuilding(b);
        }

        foreach (Building b in P.Buildings.Where(b => b.Def.Kind != BuildingKind.Belt).OrderBy(b => b.Origin.Y + b.H))
        {
            DrawBuilding(b);
        }

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

    private void DrawBakedShadow(Building b)
    {
        if ((Baked(b) is { Shadow: { } shadow } sprite) && OnScreen(RectOf(b.Origin, b.W, b.H)))
        {
            DrawTextureRect(shadow, new Rect2(S(b.Origin) + (sprite.Offset * T), sprite.Size * T), false);
        }
    }

    /// <summary>Draws a baked model: its frame (animated while working), the glow layer brightened while working, and smoke.</summary>
    private void DrawBaked(Building b, Art.BakedSprite sprite, bool working)
    {
        Rect2 rect = new Rect2(S(b.Origin) + (sprite.Offset * T), sprite.Size * T);
        int frame = (working && (sprite.Frames.Length > 1)) ? (int)((Time * sprite.Fps) + b.Id) % sprite.Frames.Length : 0;
        DrawTextureRect(sprite.Frames[frame], rect, false);
        if (working && (sprite.Glow.Length > 0))
        {
            float k = 1.45f + (0.12f * Mathf.Sin((Time * 5f) + b.Id));
            DrawTextureRect(sprite.Glow[frame], rect, false, new Color(k, k, k, 0.9f));
        }

        if (working && (sprite.Smoke is { } smoke))
        {
            Puffs(S(b.Origin) + (smoke * T), new Color(0.42f, 0.4f, 0.38f), 0.7f, b.Id);
        }
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
