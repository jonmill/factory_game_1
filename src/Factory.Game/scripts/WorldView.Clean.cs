using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Clean Age art: the high-tech white hulls, with more glass and green-cyan light. Solar panels tilt
/// blue cells at the sun, wind turbines spin with the planet's wind, and the payload launcher is a rail
/// gun on a turret that glows as it charges.
/// </summary>
public partial class WorldView
{
    private static readonly Color _cell = new(0.1f, 0.24f, 0.42f);

    /// <summary>
    /// A solar panel on a frame: a 4 × 4 grid of cells, each a little lighter at its top and split by fine
    /// fingers, silver busbars between them, and a band of sky reflection drifting slowly across the glass.
    /// </summary>
    private void DrawSolarPanel(Building b, Rect2 r, Color accent, bool working)
    {
        Rect2 frame = r.Grow(-T * 0.1f);
        DrawRect(new Rect2(frame.Position + new Vector2(T * 0.08f, T * 0.12f), frame.Size), new Color(0, 0, 0, 0.3f));
        DrawRect(frame, _hullShade.Darkened(0.1f));
        DrawLine(frame.Position, frame.Position + new Vector2(frame.Size.X, 0), _hullLight, Mathf.Max(1, T * 0.03f));
        DrawLine(frame.Position, frame.Position + new Vector2(0, frame.Size.Y), _hullLight.Darkened(0.1f), Mathf.Max(1, T * 0.03f));
        Rect2 face = frame.Grow(-T * 0.08f);
        DrawRect(face, _cell.Darkened(0.4f));

        int n = 4;
        Vector2 cellSize = face.Size / n;
        float gap = Mathf.Max(1, T * 0.03f);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                Rect2 cell = new Rect2(face.Position + new Vector2(cellSize.X * i, cellSize.Y * j), cellSize).Grow(-gap / 2);
                Vector2 p0 = cell.Position, p1 = p0 + new Vector2(cell.Size.X, 0), p2 = cell.End, p3 = p0 + new Vector2(0, cell.Size.Y);
                Color hi = _cell.Lightened(0.12f), lo = _cell.Darkened(0.12f);
                DrawPolygon([p0, p1, p2, p3], [hi, hi.Lerp(lo, 0.5f), lo, hi.Lerp(lo, 0.5f)]);
                if (T >= 40)
                {
                    DrawLine(p0 + new Vector2(cell.Size.X / 2, 0), p3 + new Vector2(cell.Size.X / 2, 0), A(_hullLight, 0.25f), 1);
                }
            }
        }

        // The sky's reflection: a soft diagonal band that slides across over a minute or so.
        float sweep = Mathf.PosMod((Time * 0.03f) + (b.Id * 0.137f), 1.6f) - 0.3f;
        float alpha = working ? 0.16f : 0.07f;
        Vector2 dA = face.Position + new Vector2(face.Size.X * sweep, 0);
        float w = face.Size.X * 0.22f;
        Vector2[] band = [dA, dA + new Vector2(w, 0), dA + new Vector2(w - (face.Size.X * 0.5f), face.Size.Y), dA + new Vector2(-face.Size.X * 0.5f, face.Size.Y)];
        Godot.Collections.Array<Vector2[]> inside = Geometry2D.IntersectPolygons(band, new[] { face.Position, face.Position + new Vector2(face.Size.X, 0), face.End, face.Position + new Vector2(0, face.Size.Y) });
        Vector2[] clipped = inside.Count > 0 ? inside[0] : [];
        if (clipped.Length >= 3)
        {
            DrawColoredPolygon(clipped, new Color(0.85f, 0.95f, 1f, alpha));
        }

        DrawRect(new Rect2(new Vector2(face.Position.X, face.End.Y - (T * 0.08f)), new Vector2(face.Size.X, T * 0.08f)), Trim(accent, working));
    }

    private void DrawWindTurbine(Building b, Rect2 r, Color accent, bool working)
    {
        Vector2 c = r.GetCenter();
        DrawCircle(c + new Vector2(T * 0.1f, T * 0.14f), T * 0.55f, new Color(0, 0, 0, 0.3f));
        DrawCircle(c, T * 0.5f, _hullShade);
        DrawCircle(c, T * 0.4f, _hullLight);

        // Three long blades; they turn faster in a thicker atmosphere.
        float speed = working ? 0.6f + (P.WindPct / 50f) : 0f;
        float spin = (Time * speed) + (b.Id * 1.7f);
        for (int i = 0; i < 3; i++)
        {
            float a = spin + (i * Mathf.Tau / 3f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 side = new Vector2(-dir.Y, dir.X);
            Vector2 tip = c + (dir * T * 0.98f);
            Vector2[] blade = [c + (side * T * 0.09f), tip + (side * T * 0.03f), tip - (side * T * 0.03f), c - (side * T * 0.06f)];
            DrawColoredPolygon(blade, _hullLight);
            DrawPolyline([.. blade, blade[0]], _hullShade.Darkened(0.3f), Mathf.Max(1, T * 0.02f), true);
            DrawLine(c + (dir * T * 0.7f), tip - (dir * T * 0.04f), Trim(accent, working, 0.8f), Mathf.Max(1, T * 0.04f));
        }

        DrawCircle(c, T * 0.16f, _glass);
        DrawCircle(c, T * 0.07f, Trim(accent, working, 1.2f));
    }

    private void DrawPayloadLauncher(Building b, Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter();

        // A turret ring, then a twin-rail barrel pointing at the target planet's side of the sky.
        GlassDisc(c, T * 0.95f);
        DrawArc(c, T * 0.8f, 0, Mathf.Tau, 32, Trim(accent, working, 0.7f), Mathf.Max(1, T * 0.05f));
        float aim = (b.TargetPlanetId < 0 ? -0.5f : -0.9f + (b.TargetPlanetId * 0.55f)) * Mathf.Pi;
        Vector2 dir = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim));
        Vector2 side = new Vector2(-dir.Y, dir.X);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 from = c + (side * T * 0.16f * s);
            DrawLine(from, from + (dir * T * 1.3f), _hullLight, T * 0.12f);
            DrawLine(from + (dir * T * 0.3f), from + (dir * T * 1.25f), Trim(accent, working), Mathf.Max(1, T * 0.04f));
        }

        // The charge pulses down the rails while a payload is being prepared.
        if (working)
        {
            float ph = Mathf.PosMod((Time * 1.3f) + (b.Id * 0.37f), 1f);
            Vector2 at = c + (dir * T * (0.2f + (ph * 1.1f)));
            DrawCircle(at, T * (0.12f + (ph * 0.06f)), Hdr(accent.Lightened(0.3f), 1.6f));
        }

        DrawCircle(c, T * 0.28f, _hullShade);
        DrawCircle(c, T * 0.16f, Trim(accent, working, 1.1f));
    }

    /// <summary>A soft cyan glow and ring under a robot with a hover module, which floats over water.</summary>
    private void DrawHoverGlow(Unit u, Vector2 at)
    {
        float bob = Mathf.Sin((Time * 3f) + u.Id) * 0.08f;
        Color glow = new Color(0.35f, 0.95f, 0.9f);
        DrawCircle(at + new Vector2(0, T * 0.22f), T * (0.3f + (bob * 0.3f)), A(glow, 0.18f));
        DrawArc(at + new Vector2(0, T * 0.22f), T * (0.24f + bob), 0, Mathf.Tau, 20, A(Hdr(glow, 1.3f), 0.7f), Mathf.Max(1, T * 0.03f));
    }
}
