using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>Per-frame terrain overlays. Ground, ore and trees are cached by <see cref="TerrainLayer"/>.</summary>
public partial class WorldView
{
    /// <summary>
    /// Moving glints on open water: each water tile owns one short highlight that fades in and out on its
    /// own phase, so lakes shimmer without any texture animation.
    /// </summary>
    private void DrawWaterGlints(int x0, int y0, int x1, int y1)
    {
        if (Root.Zoom < 0.55f)
        {
            return;
        }

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Int2 t = P.Torus.Wrap(new Int2(x, y));
                if (P.TerrainAt(t) != Terrain.Water)
                {
                    continue;
                }

                uint h = Hash(t.X, t.Y, 91);
                float phase = (Time * (0.4f + ((h & 7) * 0.05f))) + ((h >> 3) & 63);
                float a = Mathf.Max(0, Mathf.Sin(phase)) * 0.22f;
                if (a < 0.02f)
                {
                    continue;
                }

                Vector2 at = S(new Vector2(x + (((h >> 9) & 15) / 16f), y + (((h >> 13) & 15) / 16f)));
                float len = T * (0.18f + (((h >> 17) & 3) * 0.06f)) * (0.6f + (0.4f * Mathf.Sin(phase)));
                Vector2 drift = new Vector2(Mathf.Sin(phase * 0.5f) * T * 0.08f, 0);
                DrawLine(at + drift - new Vector2(len / 2, 0), at + drift + new Vector2(len / 2, 0), new Color(0.75f, 0.9f, 1f, a), Mathf.Max(1, T * 0.035f));
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
}
