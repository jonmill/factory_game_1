using Factory.Sim;
using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// The minimap, top-right: a one-pixel-per-tile radar of the ground around the camera, with deposits,
/// buildings, rails and robots, black where nothing has been explored, and a frame showing what the
/// screen covers. It shows <see cref="RadarTiles"/> tiles across, centred on the camera, so it stays a
/// local radar however big the planet. Left-click sends the commander walking there.
/// </summary>
public partial class Minimap : Control
{
    /// <summary>Tiles shown across: a local radar round the camera, so its cost doesn't grow with the map.</summary>
    public const int RadarTiles = 72;

    /// <summary>Seconds between repaints of the radar image.</summary>
    private const double RepaintSeconds = 0.25;

    private static readonly Color _fog = new(0.02f, 0.025f, 0.035f);
    private static readonly Color _ground = new(0.3f, 0.26f, 0.19f);
    private static readonly Color _water = new(0.1f, 0.22f, 0.38f);
    private static readonly Color _lava = new(0.55f, 0.18f, 0.06f);
    private static readonly Color _tree = new(0.16f, 0.33f, 0.17f);
    private static readonly Color _building = new(0.72f, 0.8f, 0.88f);
    private static readonly Color _rail = new(0.55f, 0.52f, 0.48f);

    /// <summary>The game root that owns the camera and the simulation.</summary>
    public GameRoot Root { get; set; } = null!;

    private Image? _image;
    private ImageTexture? _texture;
    private double _sinceRepaint = RepaintSeconds;
    private Vector2I _origin; // top-left tile of the radar
    private int _span;        // tiles across

    private Planet P => Root.Planet;

    /// <inheritdoc />
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "Minimap  ·  click to walk the commander there";
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        _sinceRepaint += delta;
        if (_sinceRepaint >= RepaintSeconds)
        {
            _sinceRepaint = 0;
            Repaint();
        }

        QueueRedraw();
    }

    private void Repaint()
    {
        _span = System.Math.Min(RadarTiles, System.Math.Min(P.Width, P.Height));
        Vector2I cam = new Vector2I(Mathf.FloorToInt(Root.Cam.X), Mathf.FloorToInt(Root.Cam.Y));
        _origin = cam - new Vector2I(_span / 2, _span / 2);
        if ((_image is null) || (_image.GetWidth() != _span))
        {
            _image = Image.CreateEmpty(_span, _span, false, Image.Format.Rgb8);
            _texture = null;
        }

        for (int y = 0; y < _span; y++)
        {
            for (int x = 0; x < _span; x++)
            {
                _image.SetPixel(x, y, TileColor(new Int2(_origin.X + x, _origin.Y + y)));
            }
        }

        if (_texture is null)
        {
            _texture = ImageTexture.CreateFromImage(_image);
        }
        else
        {
            _texture.Update(_image);
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

    /// <summary>Where a tile falls on the minimap, in its local coordinates.</summary>
    private Vector2 ToLocal(Vector2 tile)
    {
        float k = Size.X / System.Math.Max(1, _span);
        Vector2 d = new Vector2(Mathf.PosMod(tile.X - _origin.X, P.Width), Mathf.PosMod(tile.Y - _origin.Y, P.Height));

        return d * k;
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Rect2 r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, _fog);
        if (_texture is not null)
        {
            DrawTextureRect(_texture, r, false);
        }

        float k = Size.X / System.Math.Max(1, _span);
        foreach (Unit u in P.Units)
        {
            Vector2 at = ToLocal(new Vector2(u.Pos.X / (float)Fixed.One, u.Pos.Y / (float)Fixed.One));
            if (r.HasPoint(at))
            {
                DrawCircle(at, Mathf.Max(1.5f, k * (u.IsLeader ? 2.2f : 1.4f)), u.IsLeader ? HudStyle.Orange : HudStyle.Header);
            }
        }

        // What the screen shows.
        Vector2 view = Root.GetViewportRect().Size / (GameRoot.TilePx * Root.Zoom);
        Vector2 topLeft = ToLocal(Root.Cam - (view / 2));
        Rect2 frame = new Rect2(topLeft, view * k);
        DrawRect(frame.Intersection(r), new Color(1, 1, 1, 0.7f), false, 1);
        DrawRect(r, HudStyle.Line, false, 1);
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if ((@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) && (Root.Leader.Planet == P) && (_span > 0))
        {
            float k = Size.X / _span;
            Int2 tile = P.Torus.Wrap(new Int2(_origin.X + (int)(mb.Position.X / k), _origin.Y + (int)(mb.Position.Y / k)));
            Root.Send(new WalkTo(Root.Leader.Id, tile));
            AcceptEvent();
        }
    }
}
