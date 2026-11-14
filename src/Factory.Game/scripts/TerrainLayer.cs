using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The planet surface: the painted ground texture plus ore, beds and trees. Ore and trees are recorded
/// once into <see cref="TerrainChunk"/> canvas items and then only moved and scaled with the camera, so
/// zooming out costs a few hundred node transforms instead of tens of thousands of draw calls a frame.
/// A chunk re-records only when the resources on its tiles change.
/// </summary>
public partial class TerrainLayer : Node2D
{
    /// <summary>Chunk edge length in tiles.</summary>
    public const int ChunkTiles = 16;

    /// <summary>Seconds between rescans for slow changes (crystal clusters shrinking as ore is mined).</summary>
    private const double RescanSeconds = 0.5;

    private readonly Dictionary<(int Cx, int Cy, int Ox, int Oy), TerrainChunk> _copies = [];
    private ulong[] _signatures = [];
    private int _chunksX;
    private int _chunksY;
    private int _scannedVersion = -1;
    private double _sinceScan;
    private Planet? _planet;

    private ImageTexture? _groundTex;
    private ulong _groundSignature;
    private int _groundVersion = -1;

    /// <summary>The game root that owns the camera and the simulation being drawn.</summary>
    public GameRoot Root { get; set; } = null!;

    private Planet P => Root.Planet;

    /// <inheritdoc />
    public override void _Ready() => TextureFilter = TextureFilterEnum.LinearWithMipmaps;

    /// <summary>Positions the visible chunk copies for this frame and re-records any that changed.</summary>
    /// <param name="delta">Seconds since the last frame.</param>
    public void Refresh(double delta)
    {
        if (ReferenceEquals(_planet, P) == false)
        {
            Reset();
        }

        _sinceScan += delta;
        if ((P.TerrainVersion != _scannedVersion) || (_sinceScan >= RescanSeconds))
        {
            Rescan();
        }

        Place();
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.2f, 0.17f, 0.12f));
        DrawGround(size);
    }

    private void Reset()
    {
        foreach (TerrainChunk c in _copies.Values)
        {
            c.QueueFree();
        }

        _copies.Clear();
        _planet = P;
        _chunksX = (P.Width + ChunkTiles - 1) / ChunkTiles;
        _chunksY = (P.Height + ChunkTiles - 1) / ChunkTiles;
        _signatures = new ulong[_chunksX * _chunksY];
        _scannedVersion = -1;
        _groundTex = null;
        _groundVersion = -1;
    }

    /// <summary>Recomputes each chunk's content signature and re-records the copies of chunks that changed.</summary>
    private void Rescan()
    {
        _scannedVersion = P.TerrainVersion;
        _sinceScan = 0;
        for (int cy = 0; cy < _chunksY; cy++)
        {
            for (int cx = 0; cx < _chunksX; cx++)
            {
                ulong sig = Signature(cx, cy);
                int i = (cy * _chunksX) + cx;
                if (sig == _signatures[i])
                {
                    continue;
                }

                _signatures[i] = sig;
                foreach (KeyValuePair<(int Cx, int Cy, int Ox, int Oy), TerrainChunk> kv in _copies)
                {
                    if ((kv.Key.Cx == cx) && (kv.Key.Cy == cy))
                    {
                        kv.Value.QueueRedraw();
                    }
                }
            }
        }
    }

    /// <summary>What a chunk would draw: each tile's resource and crystal cluster size.</summary>
    private ulong Signature(int cx, int cy)
    {
        ulong h = 1469598103934665603UL;
        for (int y = cy * ChunkTiles; y < System.Math.Min(P.Height, (cy + 1) * ChunkTiles); y++)
        {
            for (int x = cx * ChunkTiles; x < System.Math.Min(P.Width, (cx + 1) * ChunkTiles); x++)
            {
                Int2 t = new Int2(x, y);
                ResourceDef? res = P.ResourceAt(t);
                ulong v = res is null ? 0UL : (ulong)(TerrainChunk.Crystals(P.AmountAt(t)) + 1 + (res.Id.GetHashCode() * 4L));
                h = (h ^ v) * 1099511628211UL;
            }
        }

        return h;
    }

    /// <summary>
    /// Shows every wrapped copy of every chunk that overlaps the screen (a wide window can see the same
    /// part of the planet twice) and hides the rest.
    /// </summary>
    private void Place()
    {
        Vector2 size = GetViewportRect().Size;
        float t = GameRoot.TilePx * Root.Zoom;
        Rect2 screen = new Rect2(Vector2.Zero, size);
        Vector2 chunkPx = new Vector2(ChunkTiles, ChunkTiles) * t;
        Vector2 planetPx = new Vector2(P.Width, P.Height) * t;
        // Screen position of the planet's (0, 0) corner for the copy nearest the camera.
        Vector2 origin = (size / 2) - (Root.Cam * t);

        // Only touch Visible when it changes: a CanvasItem that becomes visible re-records its drawing.
        HashSet<TerrainChunk> shown = [];
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                Vector2 copyOrigin = origin + new Vector2(ox * planetPx.X, oy * planetPx.Y);
                if (new Rect2(copyOrigin, planetPx).Grow(t).Intersects(screen) == false)
                {
                    continue;
                }

                for (int cy = 0; cy < _chunksY; cy++)
                {
                    for (int cx = 0; cx < _chunksX; cx++)
                    {
                        Vector2 pos = copyOrigin + new Vector2(cx * chunkPx.X, cy * chunkPx.Y);
                        // Grow by a tile: tree canopies and bed blobs spill slightly past the chunk edge.
                        if (new Rect2(pos, chunkPx).Grow(t).Intersects(screen) == false)
                        {
                            continue;
                        }

                        TerrainChunk chunk = Copy(cx, cy, ox, oy);
                        chunk.Position = pos;
                        chunk.Scale = new Vector2(Root.Zoom, Root.Zoom);
                        shown.Add(chunk);
                    }
                }
            }
        }

        foreach (TerrainChunk c in _copies.Values)
        {
            bool visible = shown.Contains(c);
            if (c.Visible != visible)
            {
                c.Visible = visible;
            }
        }
    }

    private TerrainChunk Copy(int cx, int cy, int ox, int oy)
    {
        if (_copies.TryGetValue((cx, cy, ox, oy), out TerrainChunk? chunk))
        {
            return chunk;
        }

        chunk = new TerrainChunk { Planet = P, Origin = new Int2(cx * ChunkTiles, cy * ChunkTiles), Size = ChunkTiles };
        _copies[(cx, cy, ox, oy)] = chunk;
        AddChild(chunk);

        return chunk;
    }

    private void DrawGround(Vector2 size)
    {
        // Repaint only when land and water actually changed; resource edits bump the version far more often.
        if ((_groundTex is null) || (_groundVersion != P.TerrainVersion))
        {
            ulong signature = GroundPainter.WaterSignature(P);
            if ((_groundTex is null) || (signature != _groundSignature))
            {
                Image img = GroundPainter.Paint(P);
                if (_groundTex is null)
                {
                    _groundTex = ImageTexture.CreateFromImage(img);
                }
                else
                {
                    _groundTex.Update(img);
                }

                _groundSignature = signature;
            }

            _groundVersion = P.TerrainVersion;
        }

        float t = GameRoot.TilePx * Root.Zoom;
        Vector2 origin = (size / 2) - (Root.Cam * t);
        Vector2 planetPx = new Vector2(P.Width, P.Height) * t;
        Rect2 screen = new Rect2(Vector2.Zero, size);
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                Rect2 r = new Rect2(origin + new Vector2(i * planetPx.X, j * planetPx.Y), planetPx);
                if (r.Intersects(screen))
                {
                    DrawTextureRect(_groundTex!, r, false);
                }
            }
        }
    }
}
