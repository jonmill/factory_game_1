using Factory.Sim;
using Godot;

namespace Factory.Game;

public partial class WorldView
{
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

        Vector2 origin = S(Vector2.Zero);
        Vector2 planetPx = new Vector2(P.Width, P.Height) * T;
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

    /// <summary>Darker stony beds under ore and leaf litter under trees, drawn as soft blobs so edges look natural.</summary>
    private void DrawTileBeds(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Int2 t = P.Torus.Wrap(new Int2(x, y));
                ResourceDef? res = P.ResourceAt(t);
                if (res is null)
                {
                    continue;
                }

                Vector2 c = Center(t);
                uint h = Hash(t.X, t.Y, 5);
                // Trees get darker leaf litter; ore gets a faint wash of its own colour, as if the
                // crystals were lighting the ground around them.
                Color bed = res.IsTree
                    ? new Color(0.14f, 0.16f, 0.08f, 0.22f)
                    : A(Hex(res.Color).Lerp(new Color(0.3f, 0.26f, 0.2f), 0.35f), 0.13f);
                for (int k = 0; k < 2; k++)
                {
                    Vector2 jitter = new Vector2(((h >> (k * 4)) & 15) / 15f, ((h >> ((k * 4) + 8)) & 15) / 15f) - new Vector2(0.5f, 0.5f);
                    DrawCircle(c + (jitter * T * 0.3f), T * (0.55f + (0.08f * k)), bed);
                }
            }
        }
    }

    /// <summary>A faint placement grid, only while the player is placing something.</summary>
    private void DrawGrid(int x0, int y0, int x1, int y1)
    {
        if ((Root.Tool is not (ToolKind.Place or ToolKind.PlaceTrain)) || (Root.Zoom < 0.5f))
        {
            return;
        }

        Color line = new Color(0, 0, 0, 0.12f);
        for (int x = x0; x <= x1; x++)
        {
            DrawLine(S(new Vector2(x, y0)), S(new Vector2(x, y1 + 1)), line, 1f);
        }

        for (int y = y0; y <= y1; y++)
        {
            DrawLine(S(new Vector2(x0, y)), S(new Vector2(x1 + 1, y)), line, 1f);
        }
    }

    /// <summary>
    /// Ore as faceted, glossy crystals: a lit face, a shaded face and a bright tip that catches the
    /// light now and then. Richer tiles grow bigger clusters.
    /// </summary>
    private void DrawOre(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Int2 t = P.Torus.Wrap(new Int2(x, y));
                ResourceDef? res = P.ResourceAt(t);
                if ((res is null) || res.IsTree)
                {
                    continue;
                }

                Vector2 p = S(t);
                Color baseColor = Hex(res.Color);
                int crystals = 1 + System.Math.Min(2, P.AmountAt(t) / 350);
                for (int k = 0; k < crystals; k++)
                {
                    uint hk = Hash((t.X * 7) + k, (t.Y * 13) - k, 9);
                    Vector2 cp = p + new Vector2(T * (0.22f + ((hk & 15) / 28f)), T * (0.42f + (((hk >> 4) & 15) / 32f)));
                    float s = T * (0.13f + (((hk >> 8) & 3) * 0.035f));
                    float lean = ((((hk >> 10) & 7) / 7f) - 0.5f) * 0.5f;
                    DrawCrystal(cp, s, lean, baseColor, (hk >> 13) & 63);
                }
            }
        }
    }

    private void DrawCrystal(Vector2 foot, float s, float lean, Color color, uint phase)
    {
        Vector2 tip = foot + new Vector2(lean * s, -s * 2.1f);
        Vector2 left = foot + new Vector2(-s * 0.7f, -s * 0.35f);
        Vector2 right = foot + new Vector2(s * 0.7f, -s * 0.35f);
        Vector2 shoulderL = foot + new Vector2((-s * 0.55f) + (lean * s * 0.6f), -s * 1.35f);
        Vector2 shoulderR = foot + new Vector2((s * 0.55f) + (lean * s * 0.6f), -s * 1.35f);

        DrawColoredPolygon([foot + new Vector2(s * 0.2f, s * 0.15f), right + new Vector2(s * 0.5f, s * 0.2f), tip + new Vector2(s * 0.9f, s * 1.3f)], new Color(0, 0, 0, 0.3f));
        DrawColoredPolygon([foot, left, shoulderL, tip], color.Lightened(0.15f));
        DrawColoredPolygon([foot, tip, shoulderR, right], color.Darkened(0.35f));
        DrawLine(foot, tip, color.Lightened(0.45f), Mathf.Max(1f, s * 0.12f));

        float glint = Mathf.Max(0, Mathf.Sin((Time * 1.3f) + phase) - 0.85f) / 0.15f;
        DrawCircle(tip + new Vector2(0, s * 0.35f), s * 0.18f, Hdr(color.Lightened(0.6f), 1f + (glint * 0.8f)));
    }

    /// <summary>Trees as clumps of foliage with a shadow, drawn after buildings so canopies overhang them slightly.</summary>
    private void DrawTrees(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Int2 t = P.Torus.Wrap(new Int2(x, y));
                ResourceDef? res = P.ResourceAt(t);
                if ((res is null) || (res.IsTree == false))
                {
                    continue;
                }

                uint h = Hash(t.X, t.Y, 13);
                Vector2 c = S(t) + new Vector2(T * (0.35f + ((h & 7) / 24f)), T * (0.3f + (((h >> 3) & 7) / 24f)));
                float r = T * (0.38f + (((h >> 6) & 3) * 0.04f));
                Color leaf = Hex(res.Color).Lerp(new Color(0.25f, 0.32f, 0.12f), 0.55f);

                DrawCircle(c + new Vector2(r * 0.35f, r * 0.45f), r * 1.05f, new Color(0, 0, 0, 0.3f));
                DrawCircle(c + new Vector2(0, T * 0.08f), T * 0.06f, new Color(0.25f, 0.18f, 0.1f));
                for (int k = 0; k < 4; k++)
                {
                    float a = (k * Mathf.Tau / 4) + ((h >> 9) & 7);
                    DrawCircle(c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.42f), r * 0.62f, leaf.Darkened(0.3f));
                }

                DrawCircle(c, r * 0.7f, leaf.Darkened(0.12f));
                DrawCircle(c + new Vector2(-r * 0.22f, -r * 0.25f), r * 0.42f, leaf);
                DrawCircle(c + new Vector2(-r * 0.3f, -r * 0.34f), r * 0.18f, leaf.Lightened(0.18f));
            }
        }
    }
}
