using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The fog over unexplored ground, drawn above the world and below the HUD. The planet is split into
/// <see cref="Planet.FogChunk"/>-tile chunks, each a small texture with one pixel per tile (and a pixel
/// of padding from its neighbours, so filtering blends across chunk edges without seams). A chunk is
/// repainted only when its fog, or a neighbour's, changed, and fully explored chunks aren't drawn at
/// all, so the cost follows what is being explored, not the size of the map.
/// </summary>
public partial class FogLayer : Node2D
{
    /// <summary>Seconds between checks for newly explored ground.</summary>
    private const double RefreshSeconds = 0.1;

    private static readonly Color _fog = new(0.025f, 0.03f, 0.045f);

    /// <summary>The game root that owns the camera and the simulation being drawn.</summary>
    public GameRoot Root { get; set; } = null!;

    private Planet P => Root.Planet;

    private Planet? _planet;
    private int _chunksX, _chunksY;
    private ImageTexture?[] _textures = [];
    private long[] _signatures = [];
    private bool[] _clear = [];
    private int _fogVersion = -1;
    private double _sinceRefresh;

    /// <inheritdoc />
    public override void _Ready() => TextureFilter = TextureFilterEnum.Linear;

    /// <summary>Repaints chunks whose fog changed and redraws.</summary>
    /// <param name="delta">Seconds since the last frame.</param>
    public void Refresh(double delta)
    {
        if (ReferenceEquals(_planet, P) == false)
        {
            Reset();
        }

        _sinceRefresh += delta;
        if ((_fogVersion != P.FogVersion) && (_sinceRefresh >= RefreshSeconds))
        {
            _sinceRefresh = 0;
            _fogVersion = P.FogVersion;
            Repaint();
        }

        QueueRedraw();
    }

    private void Reset()
    {
        _planet = P;
        _chunksX = (P.Width + Planet.FogChunk - 1) / Planet.FogChunk;
        _chunksY = (P.Height + Planet.FogChunk - 1) / Planet.FogChunk;
        _textures = new ImageTexture?[_chunksX * _chunksY];
        _signatures = new long[_chunksX * _chunksY];
        System.Array.Fill(_signatures, -1);
        _clear = new bool[_chunksX * _chunksY];
        _fogVersion = -1;
        _sinceRefresh = RefreshSeconds;
    }

    /// <summary>A chunk's own fog version plus its neighbours', which its padding and soft edge read from.</summary>
    private long Signature(int cx, int cy)
    {
        long s = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = (((cx + dx) % _chunksX) + _chunksX) % _chunksX;
                int y = (((cy + dy) % _chunksY) + _chunksY) % _chunksY;
                s = (s * 1_000_003) + P.FogChunkVersion(x, y);
            }
        }

        return s;
    }

    private void Repaint()
    {
        for (int cy = 0; cy < _chunksY; cy++)
        {
            for (int cx = 0; cx < _chunksX; cx++)
            {
                int i = (cy * _chunksX) + cx;
                long sig = Signature(cx, cy);
                if (sig != _signatures[i])
                {
                    _signatures[i] = sig;
                    Paint(cx, cy, i);
                }
            }
        }
    }

    /// <summary>
    /// Paints one chunk: each pixel's fog is the share of unexplored tiles in the 3x3 block round it, so the
    /// edge of the explored area fades over a couple of tiles instead of stepping tile by tile.
    /// </summary>
    private void Paint(int cx, int cy, int i)
    {
        int x0 = cx * Planet.FogChunk, y0 = cy * Planet.FogChunk;
        int w = System.Math.Min(Planet.FogChunk, P.Width - x0), h = System.Math.Min(Planet.FogChunk, P.Height - y0);
        Image img = Image.CreateEmpty(w + 2, h + 2, false, Image.Format.Rgba8);
        bool clear = true;
        for (int py = 0; py < h + 2; py++)
        {
            for (int px = 0; px < w + 2; px++)
            {
                Int2 t = new Int2(x0 + px - 1, y0 + py - 1);
                int hidden = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (P.IsExplored(t + new Int2(dx, dy)) == false)
                        {
                            hidden++;
                        }
                    }
                }

                float a = hidden / 9f;
                a = a * a * (3 - (2 * a)); // smoothstep: soft edge, solid fog
                if (a > 0)
                {
                    clear = false;
                }

                img.SetPixel(px, py, new Color(_fog, a));
            }
        }

        _clear[i] = clear;
        if (clear)
        {
            _textures[i] = null;

            return;
        }

        if ((_textures[i] is { } tex) && (tex.GetWidth() == img.GetWidth()) && (tex.GetHeight() == img.GetHeight()))
        {
            tex.Update(img);
        }
        else
        {
            _textures[i] = ImageTexture.CreateFromImage(img);
        }
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        float t = GameRoot.TilePx * Root.Zoom;
        Vector2 origin = (size / 2) - (Root.Cam * t);
        Vector2 planetPx = new Vector2(P.Width, P.Height) * t;
        Rect2 screen = new Rect2(Vector2.Zero, size);
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                Vector2 copy = origin + new Vector2(i * planetPx.X, j * planetPx.Y);
                if (new Rect2(copy, planetPx).Intersects(screen) == false)
                {
                    continue;
                }

                for (int cy = 0; cy < _chunksY; cy++)
                {
                    for (int cx = 0; cx < _chunksX; cx++)
                    {
                        int k = (cy * _chunksX) + cx;
                        if (_clear[k] || (_textures[k] is not { } tex))
                        {
                            continue;
                        }

                        int w = tex.GetWidth() - 2, h = tex.GetHeight() - 2;
                        Rect2 r = new Rect2(copy + (new Vector2(cx, cy) * Planet.FogChunk * t), new Vector2(w, h) * t);
                        if (r.Intersects(screen))
                        {
                            // Skip the padding: it is only there so filtering blends into the next chunk.
                            DrawTextureRectRegion(tex, r, new Rect2(1, 1, w, h));
                        }
                    }
                }
            }
        }
    }
}
