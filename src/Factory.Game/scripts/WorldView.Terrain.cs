using Godot;

namespace Factory.Game;

/// <summary>Per-frame terrain overlays. Ground, ore and trees are cached by <see cref="TerrainLayer"/>.</summary>
public partial class WorldView
{
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
