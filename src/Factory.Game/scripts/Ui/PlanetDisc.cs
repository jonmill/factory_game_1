using Factory.Sim;
using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// A small drawn picture of a body in the system: a lit sphere banded with its biome colours, cloud
/// bands for a gas giant, or a scatter of rocks for a belt.
/// </summary>
public partial class PlanetDisc : Control
{
    /// <summary>The body to draw.</summary>
    public PlanetDef Def { get; set; } = null!;

    /// <summary>Content, for biome colours.</summary>
    public ContentDb Content { get; set; } = null!;

    /// <inheritdoc />
    public override void _Draw()
    {
        Vector2 c = Size / 2;
        float r = (Mathf.Min(Size.X, Size.Y) / 2) - 2;
        Color body = Color.FromString(Def.Color, Colors.Gray);
        if (Def.Kind == PlanetKind.Belt)
        {
            uint h = GroundPainter.Hash(Def.Id.Length, Def.Orbit, 5);
            for (int i = 0; i < 26; i++)
            {
                h = GroundPainter.Hash((int)h, i, 9);
                float a = h % 628 / 100f;
                float d = r * (0.55f + ((h >> 10) % 40 / 100f));
                Vector2 p = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.45f) * d);
                DrawCircle(p, 1.5f + ((h >> 16) % 3), body.Darkened((h >> 20) % 4 * 0.1f));
            }

            return;
        }

        DrawCircle(c, r + 3, new Color(body, 0.12f));
        DrawCircle(c, r, body);
        if (Def.Kind == PlanetKind.GasGiant)
        {
            for (int i = -3; i <= 3; i++)
            {
                float y = c.Y + (i * r * 0.26f);
                float half = Mathf.Sqrt(Mathf.Max(0, (r * r) - ((y - c.Y) * (y - c.Y))));
                DrawLine(new Vector2(c.X - half, y), new Vector2(c.X + half, y), i % 2 == 0 ? body.Darkened(0.18f) : body.Lightened(0.12f), r * 0.14f);
            }
        }
        else
        {
            // Patches of each biome's ground colour.
            for (int i = 0; i < Def.Biomes.Length; i++)
            {
                if (Content.Biomes.TryGetValue(Def.Biomes[i].Biome, out BiomeDef? b) == false)
                {
                    continue;
                }

                Color g = Color.FromString(b.Ground[0], body);
                float a = (i * 2.2f) + 0.6f;
                Vector2 p = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.45f);
                DrawCircle(p, r * 0.42f, new Color(g, 0.85f));
            }
        }

        // Shade the right-hand limb and add a highlight on the left so it reads as a lit sphere.
        DrawArc(c, r * 0.84f, -1.3f, 1.3f, 24, new Color(0, 0, 0, 0.32f), r * 0.32f, true);
        DrawCircle(c - new Vector2(r * 0.35f, r * 0.35f), r * 0.28f, new Color(1, 1, 1, 0.08f));

        DrawArc(c, r, 0, Mathf.Tau, 48, body.Lightened(0.3f), 1.5f, true);
    }
}
