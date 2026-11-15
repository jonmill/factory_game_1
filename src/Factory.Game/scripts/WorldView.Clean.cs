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

    private void DrawSolarPanel(Building b, Rect2 r, Color accent, bool working)
    {
        Rect2 frame = r.Grow(-T * 0.1f);
        DrawRect(new Rect2(frame.Position + new Vector2(T * 0.08f, T * 0.12f), frame.Size), new Color(0, 0, 0, 0.3f));
        DrawRect(frame, _hullShade);
        Rect2 face = frame.Grow(-T * 0.08f);
        DrawRect(face, _cell);

        // A 4 × 4 grid of cells with thin silver busbars; the sheen moves a little with the light.
        int n = 4;
        Vector2 cellSize = face.Size / n;
        for (int i = 1; i < n; i++)
        {
            float x = face.Position.X + (cellSize.X * i);
            float y = face.Position.Y + (cellSize.Y * i);
            DrawLine(new Vector2(x, face.Position.Y), new Vector2(x, face.End.Y), _hullLight.Darkened(0.2f), Mathf.Max(1, T * 0.03f));
            DrawLine(new Vector2(face.Position.X, y), new Vector2(face.End.X, y), _hullLight.Darkened(0.2f), Mathf.Max(1, T * 0.03f));
        }

        float sheen = working ? 0.22f + (Mathf.Sin((Time * 0.4f) + b.Id) * 0.06f) : 0.08f;
        Vector2[] glint =
        [
            face.Position + new Vector2(face.Size.X * 0.15f, 0), face.Position + new Vector2(face.Size.X * 0.45f, 0),
            face.Position + new Vector2(0, face.Size.Y * 0.45f), face.Position + new Vector2(0, face.Size.Y * 0.15f),
        ];
        DrawColoredPolygon(glint, new Color(1, 1, 1, sheen));
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
