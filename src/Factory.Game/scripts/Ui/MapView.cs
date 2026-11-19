using Factory.Sim;
using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// A top-down map of the viewed planet at one pixel per tile: ground, water, deposits, buildings, rails
/// and robots, black where nothing has been explored, with a frame for what the screen shows. The
/// minimap is a small one following the camera (click it to open the map); the map window is a big one
/// you drag and zoom. The
/// planet image is filled from a byte buffer in one go and repainted a few times a second, and drawing
/// just scales it (wrapped round the planet), so both stay cheap on big maps.
/// </summary>
public partial class MapView : Control
{
    private static readonly Color _fog = new(0.02f, 0.025f, 0.035f);
    private static readonly Color _ground = new(0.3f, 0.26f, 0.19f);
    private static readonly Color _water = new(0.1f, 0.22f, 0.38f);
    private static readonly Color _lava = new(0.55f, 0.18f, 0.06f);
    private static readonly Color _tree = new(0.16f, 0.33f, 0.17f);
    private static readonly Color _building = new(0.72f, 0.8f, 0.88f);
    private static readonly Color _rail = new(0.55f, 0.52f, 0.48f);

    /// <summary>The game root that owns the camera and the simulation.</summary>
    public GameRoot Root { get; set; } = null!;

    /// <summary>Tiles shown across the control.</summary>
    public float TilesAcross { get; set; } = 72;

    /// <summary>Follow the camera (the minimap) rather than keep a centre of its own (the map window).</summary>
    public bool FollowCamera { get; set; } = true;

    /// <summary>Drag to pan and wheel to zoom, with a readout of what's under the mouse (the map window).</summary>
    public bool Interactive { get; set; }

    /// <summary>The tile at the middle of the control when not following the camera.</summary>
    public Vector2 Center { get; set; }

    /// <summary>Seconds between repaints of the planet image.</summary>
    public double RepaintSeconds { get; set; } = 0.25;

    /// <summary>Raised on a left click when not interactive (the minimap opens the map window).</summary>
    public event System.Action? Clicked;

    private ImageTexture? _texture;
    private byte[] _pixels = [];
    private Planet? _planet;
    private double _sinceRepaint = double.MaxValue;
    private bool _dragging;
    private Vector2? _mouse;

    private Planet P => Root.Planet;

    private Vector2 Mid => FollowCamera ? Root.Cam : Center;

    private float K => Size.X / Mathf.Max(1, TilesAcross);

    /// <inheritdoc />
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    /// <summary>Repaints straight away (the map window, when it opens).</summary>
    public void Refresh() => _sinceRepaint = double.MaxValue;

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (IsVisibleInTree() == false)
        {
            return;
        }

        _sinceRepaint += delta;
        if ((_sinceRepaint >= RepaintSeconds) || (ReferenceEquals(_planet, P) == false))
        {
            _sinceRepaint = 0;
            Repaint();
        }

        QueueRedraw();
    }

    private void Repaint()
    {
        int w = P.Width, h = P.Height;
        if ((_planet != P) || (_pixels.Length != w * h * 3))
        {
            _planet = P;
            _pixels = new byte[w * h * 3];
            _texture = null;
        }

        int i = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = TileColor(new Int2(x, y));
                _pixels[i++] = (byte)(c.R * 255);
                _pixels[i++] = (byte)(c.G * 255);
                _pixels[i++] = (byte)(c.B * 255);
            }
        }

        Image img = Image.CreateFromData(w, h, false, Image.Format.Rgb8, _pixels);
        if (_texture is null)
        {
            _texture = ImageTexture.CreateFromImage(img);
        }
        else
        {
            _texture.Update(img);
        }
    }

    private Color TileColor(Int2 t)
    {
        if (P.IsExplored(t) == false)
        {
            return _fog;
        }

        if (P.BuildingAt(t) is { } b)
        {
            return b.Def.Kind is BuildingKind.Belt or BuildingKind.Pole ? _building.Darkened(0.35f) : _building;
        }

        if (P.IsRail(t))
        {
            return _rail;
        }

        if (P.ResourceAt(t) is { } res)
        {
            return res.IsTree ? _tree : Color.FromHtml(res.Color).Darkened(0.15f);
        }

        return P.TerrainAt(t) switch
        {
            Terrain.Water => _water,
            Terrain.Lava => _lava,
            _ => _ground,
        };
    }

    /// <summary>Where a tile position falls in the control, taking the nearest way round the planet.</summary>
    private Vector2 ToLocal(Vector2 tile)
    {
        Vector2 d = new Vector2(Wrap(tile.X - Mid.X, P.Width), Wrap(tile.Y - Mid.Y, P.Height));

        return (Size / 2) + (d * K);
    }

    private static float Wrap(float d, int size) => Mathf.PosMod(d + (size / 2f), size) - (size / 2f);

    /// <summary>The tile under a point of the control.</summary>
    private Int2 TileAt(Vector2 local)
    {
        Vector2 t = Mid + ((local - (Size / 2)) / K);

        return P.Torus.Wrap(new Int2(Mathf.FloorToInt(t.X), Mathf.FloorToInt(t.Y)));
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Rect2 r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, _fog);
        float k = K;
        if (_texture is not null)
        {
            // The planet wraps: draw the copies around the one in the middle that the view can reach.
            Vector2 planet = new Vector2(P.Width, P.Height) * k;
            Vector2 origin = (Size / 2) - (Mid * k);
            int reachX = Mathf.CeilToInt(Size.X / planet.X) + 1, reachY = Mathf.CeilToInt(Size.Y / planet.Y) + 1;
            for (int j = -reachY; j <= reachY; j++)
            {
                for (int i = -reachX; i <= reachX; i++)
                {
                    Rect2 copy = new Rect2(origin + new Vector2(i * planet.X, j * planet.Y), planet);
                    if (copy.Intersects(r))
                    {
                        DrawTextureRect(_texture, copy, false);
                    }
                }
            }
        }

        foreach (Unit u in P.Units)
        {
            Vector2 at = ToLocal(new Vector2(u.Pos.X / (float)Fixed.One, u.Pos.Y / (float)Fixed.One));
            if (r.HasPoint(at))
            {
                float dot = Mathf.Clamp(k * (u.IsLeader ? 2.2f : 1.4f), 2, u.IsLeader ? 6 : 4);
                DrawCircle(at, dot, u.IsLeader ? HudStyle.Orange : HudStyle.Header);
            }
        }

        // What the screen shows.
        Vector2 view = Root.GetViewportRect().Size / (GameRoot.TilePx * Root.Zoom);
        Rect2 frame = new Rect2(ToLocal(Root.Cam) - (view * k / 2), view * k);
        DrawRect(frame.Intersection(r), new Color(1, 1, 1, 0.7f), false, 1);

        if (Interactive && (_mouse is { } m) && r.HasPoint(m))
        {
            string what = Describe(TileAt(m));
            Font font = GetThemeDefaultFont();
            const int Size13 = 13;
            Vector2 textSize = font.GetStringSize(what, HorizontalAlignment.Left, -1, Size13);
            Rect2 box = new Rect2(new Vector2(6, Size.Y - textSize.Y - 12), textSize + new Vector2(12, 6));
            DrawRect(box, HudStyle.WindowBg with { A = 0.9f });
            DrawString(font, box.Position + new Vector2(6, textSize.Y - 1), what, HorizontalAlignment.Left, -1, Size13, HudStyle.Text);
        }

        DrawRect(r, HudStyle.Line, false, 1);
    }

    /// <summary>What a tile holds, as far as anyone knows.</summary>
    private string Describe(Int2 t)
    {
        if (P.IsExplored(t) == false)
        {
            return $"Unexplored  ({t.X}, {t.Y})";
        }

        if (P.BuildingAt(t) is { } b)
        {
            return b.Name;
        }

        if (P.ResourceAt(t) is { } res)
        {
            return res.IsTree ? res.Name : $"{res.Name}  ·  {P.AmountAt(t)} left";
        }

        return P.TerrainAt(t) switch
        {
            Terrain.Water => "Water",
            Terrain.Lava => "Lava",
            _ => P.IsRail(t) ? "Rail" : P.BiomeAt(t)?.Name ?? "Ground",
        };
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (Interactive == false)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Clicked?.Invoke();
                AcceptEvent();
            }

            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                _dragging = mb.Pressed;
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                {
                    // Zoom about the point under the mouse, so it stays put.
                    Vector2 before = Center + ((wheel.Position - (Size / 2)) / K);
                    float f = wheel.ButtonIndex == MouseButton.WheelUp ? 1 / 1.2f : 1.2f;
                    TilesAcross = Mathf.Clamp(TilesAcross * f, 24, Mathf.Max(P.Width, P.Height) * 1.5f);
                    Center = before - ((wheel.Position - (Size / 2)) / K);
                    AcceptEvent();
                    break;
                }
            case InputEventMouseMotion motion:
                _mouse = motion.Position;
                if (_dragging)
                {
                    Center -= motion.Relative / K;
                }

                AcceptEvent();
                break;
        }

        Center = new Vector2(Mathf.PosMod(Center.X, P.Width), Mathf.PosMod(Center.Y, P.Height));
    }

    /// <inheritdoc />
    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _mouse = null;
            _dragging = false;
        }
    }
}
