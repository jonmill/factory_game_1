using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Stellar Age art: obsidian hulls with gold trim and contained violet plasma, so the last age reads as a
/// step beyond the white Clean Age hulls. Also the orbital collector's skyhook.
/// </summary>
public partial class WorldView
{
    private static readonly Color _obsidian = new(0.13f, 0.11f, 0.18f);
    private static readonly Color _obsidianLight = new(0.22f, 0.19f, 0.3f);
    private static readonly Color _gold = new(0.95f, 0.78f, 0.38f);

    /// <summary>Draws a Stellar Age building; false if it has no special art.</summary>
    private bool DrawStellar(Building b, Rect2 r, Color accent, bool working)
    {
        switch (b.Def.Kind)
        {
            case BuildingKind.Furnace:
                StellarHull(r.Grow(-T * 0.06f), working);
                PlasmaCore(r.GetCenter(), T * 0.55f, accent, working, b.Id);
                return true;
            case BuildingKind.Drill:
                StellarHull(r.Grow(-T * 0.06f), working);
                PlasmaCore(r.GetCenter(), T * 0.7f, accent, working, b.Id);
                // Three cutting heads that sweep while it works.
                for (int i = 0; i < 3; i++)
                {
                    float a = (working ? Time * 1.5f : 0) + (i * Mathf.Tau / 3f);
                    Vector2 head = r.GetCenter() + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * T * 1.05f);
                    DrawCircle(head, T * 0.16f, _gold.Darkened(0.2f));
                    DrawCircle(head, T * 0.08f, Trim(accent, working, 1.4f));
                }

                return true;
            case BuildingKind.SteamEngine:
                DrawFusionReactor(b, r, accent, working);
                return true;
            case BuildingKind.SpaceElevator:
                DrawSpaceElevator(b, r, accent, working);
                return true;
            case BuildingKind.Terraformer:
                StellarHull(r.Grow(-T * 0.06f), working);
                GlassDisc(r.GetCenter(), T * 1.3f);
                DrawCircle(r.GetCenter(), T * 1.1f, new Color(0.12f, 0.3f, 0.36f));
                // Weather bands swirling over a globe.
                for (int i = 0; i < 3; i++)
                {
                    float ph = (working ? Time * (0.4f + (i * 0.15f)) : 0) + (i * 2f);
                    DrawArc(r.GetCenter(), T * (0.45f + (i * 0.25f)), ph, ph + 2.2f, 18, A(accent.Lightened(0.3f), 0.7f), Mathf.Max(1, T * 0.07f));
                }

                DrawCircle(r.GetCenter(), T * 0.18f, Trim(accent, working, 1.2f));
                return true;
            default:
                return false;
        }
    }

    /// <summary>A dark chamfered hull with a gold rim that warms while the machine runs.</summary>
    private void StellarHull(Rect2 r, bool active)
    {
        float cut = Mathf.Min(r.Size.X, r.Size.Y) * 0.2f;
        DrawColoredPolygon(Chamfered(new Rect2(r.Position + new Vector2(T * 0.1f, T * 0.14f), r.Size), cut), new Color(0, 0, 0, 0.35f));
        DrawColoredPolygon(Chamfered(r, cut), _obsidian);
        DrawColoredPolygon(Chamfered(r.Grow(-T * 0.12f), cut * 0.8f), _obsidianLight);
        Vector2[] rim = Chamfered(r, cut);
        DrawPolyline([.. rim, rim[0]], active ? Hdr(_gold, 1.2f) : _gold.Darkened(0.35f), Mathf.Max(1, T * 0.05f), true);
    }

    /// <summary>A contained plasma ball: a glass ring with a pulsing violet core.</summary>
    private void PlasmaCore(Vector2 c, float radius, Color accent, bool active, int seed)
    {
        DrawCircle(c, radius, _glass);
        float pulse = active ? 0.85f + (0.15f * Mathf.Sin((Time * 5f) + seed)) : 0.45f;
        DrawCircle(c, radius * 0.72f * pulse, A(accent, active ? 0.35f : 0.15f));
        DrawCircle(c, radius * 0.42f * pulse, active ? Hdr(accent.Lightened(0.2f), 1.8f) : accent.Darkened(0.4f));
        DrawArc(c, radius, 0, Mathf.Tau, 32, _gold.Darkened(0.2f), Mathf.Max(1, T * 0.05f));
    }

    private void DrawFusionReactor(Building b, Rect2 r, Color accent, bool working)
    {
        StellarHull(r.Grow(-T * 0.06f), working);
        Vector2 c = r.GetCenter();
        // A torus of magnets around the plasma, with the field lines turning.
        DrawArc(c, T * 1.35f, 0, Mathf.Tau, 48, _gold.Darkened(0.15f), T * 0.3f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12f;
            DrawCircle(c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * T * 1.35f), T * 0.1f, _obsidian);
        }

        float spin = working ? Time * 2.5f : 0;
        for (int i = 0; i < 4; i++)
        {
            float a = spin + (i * Mathf.Pi / 2f);
            DrawArc(c, T * 0.95f, a, a + 1.1f, 12, A(Hdr(accent, 1.4f), working ? 0.9f : 0.3f), Mathf.Max(1, T * 0.06f));
        }

        PlasmaCore(c, T * 0.7f, accent, working, b.Id);
    }

    private void DrawSpaceElevator(Building b, Rect2 r, Color accent, bool working)
    {
        StellarHull(r.Grow(-T * 0.08f), working);
        Vector2 c = r.GetCenter();
        // The anchor: stepped rings, a climber riding the cable, and the cable rising off the top of the view.
        for (int i = 0; i < 3; i++)
        {
            DrawCircle(c, T * (1.7f - (i * 0.45f)), i % 2 == 0 ? _obsidianLight : _obsidian);
            DrawArc(c, T * (1.7f - (i * 0.45f)), 0, Mathf.Tau, 40, _gold.Darkened(0.25f), Mathf.Max(1, T * 0.04f));
        }

        Vector2 top = c - new Vector2(0, T * 12f);
        DrawLine(c, top, A(_gold, 0.8f), Mathf.Max(1.5f, T * 0.08f));
        DrawLine(c, top, A(Hdr(accent, 1.3f), working ? 0.5f : 0.15f), Mathf.Max(1, T * 0.2f));
        if (working)
        {
            float ph = Mathf.PosMod((Time * 0.25f) + (b.Id * 0.1f), 1f);
            Vector2 climber = c.Lerp(top, ph);
            DrawRect(new Rect2(climber - new Vector2(T * 0.22f, T * 0.15f), new Vector2(T * 0.44f, T * 0.3f)), _gold);
            DrawCircle(climber, T * 0.07f, Hdr(accent, 1.8f));
        }

        PlasmaCore(c, T * 0.4f, accent, working, b.Id);
    }

    /// <summary>The orbital collector: a turntable with a skyhook tether rising toward the gas giant or the belt.</summary>
    private void DrawCollector(Building b, Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter();
        GlassDisc(c, T * 0.9f);
        float spin = (working ? Time * 0.8f : 0.3f) + (b.Id * 0.7f);
        for (int i = 0; i < 4; i++)
        {
            float a = spin + (i * Mathf.Pi / 2f);
            DrawLine(c, c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * T * 0.8f), _hullLight, Mathf.Max(1, T * 0.08f));
        }

        Vector2 top = c - new Vector2(T * 0.6f, T * 8f);
        DrawLine(c, top, A(accent, working ? 0.85f : 0.35f), Mathf.Max(1, T * 0.05f));
        DrawCircle(c, T * 0.2f, Trim(accent, working, 1.2f));
    }
}
