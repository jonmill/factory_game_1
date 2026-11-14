using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Electric and ship technology art: chamfered white hulls, dark glass, and emissive accent trim that
/// blooms while the machine is working.
/// </summary>
public partial class WorldView
{
    private static readonly Color _hullLight = new(0.86f, 0.89f, 0.93f);
    private static readonly Color _hullShade = new(0.6f, 0.65f, 0.72f);
    private static readonly Color _glass = new(0.08f, 0.12f, 0.18f);

    private void DrawHighTech(Building b, Rect2 r, bool working)
    {
        Color accent = Hex(b.Def.Color);
        switch (b.Def.Kind)
        {
            case BuildingKind.Lander:
                DrawLander(r, accent);
                break;
            case BuildingKind.Assembler:
                DrawFabricator(b, r, accent, working);
                break;
            case BuildingKind.Beacon:
                DrawBeacon(r, accent, working);
                break;
            case BuildingKind.Lab:
                DrawLab(b, r, accent, working);
                break;
            case BuildingKind.Drill:
                DrawElectricDrill(b, r, accent, working);
                break;
            case BuildingKind.Sorter:
                DrawSorterArm(b, r, accent);
                break;
            default:
                Hull(r.Grow(-T * 0.06f), accent, working);
                break;
        }
    }

    private static Vector2[] Chamfered(Rect2 r, float cut)
    {
        Vector2 p = r.Position;
        Vector2 e = r.End;

        return
        [
            new Vector2(p.X + cut, p.Y), new Vector2(e.X - cut, p.Y), new Vector2(e.X, p.Y + cut), new Vector2(e.X, e.Y - cut),
            new Vector2(e.X - cut, e.Y), new Vector2(p.X + cut, e.Y), new Vector2(p.X, e.Y - cut), new Vector2(p.X, p.Y + cut),
        ];
    }

    /// <summary>Glow strength for accent trim: blooming while working, a calm unlit tone otherwise.</summary>
    private static Color Trim(Color accent, bool active, float boost = 1f) => active ? Hdr(accent, 1.35f * boost) : accent.Darkened(0.25f);

    /// <summary>A clean white hull with a lit top edge, panel seams and accent trim.</summary>
    private void Hull(Rect2 r, Color accent, bool active)
    {
        float cut = Mathf.Min(r.Size.X, r.Size.Y) * 0.18f;
        Vector2 off = new Vector2(T * 0.1f, T * 0.14f);
        DrawColoredPolygon(Chamfered(new Rect2(r.Position + off, r.Size), cut), new Color(0, 0, 0, 0.3f));
        DrawColoredPolygon(Chamfered(r, cut), _hullShade);
        Rect2 top = new Rect2(r.Position, new Vector2(r.Size.X, r.Size.Y * 0.92f)).Grow(-T * 0.02f);
        DrawColoredPolygon(Chamfered(top, cut * 0.9f), _hullLight);
        DrawLine(r.Position + new Vector2(cut, T * 0.03f), new Vector2(r.End.X - cut, r.Position.Y + (T * 0.03f)), Colors.White, Mathf.Max(1, T * 0.03f));
        Vector2[] outline = Chamfered(r, cut);
        DrawPolyline([.. outline, outline[0]], _hullShade.Darkened(0.35f), Mathf.Max(1, T * 0.03f), true);
        DrawLine(new Vector2(r.Position.X + (r.Size.X * 0.5f), r.Position.Y + cut), new Vector2(r.Position.X + (r.Size.X * 0.5f), r.End.Y - cut), _hullShade.Lightened(0.1f), 1);
        Rect2 strip = new Rect2(new Vector2(r.Position.X + cut, r.End.Y - (T * 0.16f)), new Vector2(r.Size.X - (cut * 2), T * 0.05f));
        DrawRect(strip, Trim(accent, active));
    }

    private void GlassDisc(Vector2 c, float radius)
    {
        DrawCircle(c, radius, _glass);
        DrawArc(c, radius, 0, Mathf.Tau, 32, _hullShade.Darkened(0.2f), Mathf.Max(1, T * 0.04f));
        DrawArc(c, radius * 0.8f, Mathf.Pi * 1.05f, Mathf.Pi * 1.45f, 10, new Color(1, 1, 1, 0.25f), Mathf.Max(1, T * 0.04f));
    }

    private void DrawLander(Rect2 r, Color accent)
    {
        Vector2 c = r.GetCenter();
        foreach (Vector2 leg in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
        {
            Vector2 foot = c + (leg * T * 1.35f);
            DrawLine(c + (leg * T * 0.8f), foot, _hullShade.Darkened(0.2f), T * 0.12f);
            DrawCircle(foot + new Vector2(T * 0.06f, T * 0.08f), T * 0.16f, new Color(0, 0, 0, 0.3f));
            DrawCircle(foot, T * 0.14f, _hullShade);
        }

        Hull(r.Grow(-T * 0.18f), accent, true);
        GlassDisc(c, T * 0.72f);
        float pulse = 0.5f + (0.5f * Mathf.Sin(Time * 2f));
        DrawArc(c, T * 0.55f, Time * 0.8f, (Time * 0.8f) + (Mathf.Pi * 1.3f), 32, Trim(accent, true), T * 0.06f);
        DrawCircle(c, T * 0.2f, Hdr(accent.Lightened(0.4f), 1.2f + (pulse * 0.4f)));
        for (int i = 0; i < 4; i++)
        {
            float a = (i * Mathf.Pi / 2) + (Mathf.Pi / 4);
            Vector2 lamp = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * T * 1.08f);
            DrawCircle(lamp, T * 0.06f, ((int)(Time * 1.5f) % 4) == i ? Hdr(new Color(1f, 0.35f, 0.3f), 1.6f) : new Color(0.5f, 0.2f, 0.18f));
        }
    }

    private void DrawFabricator(Building b, Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter() - new Vector2(0, T * 0.08f);
        GlassDisc(c, T * 0.95f);
        float spin = working ? Time * 1.6f : 0.3f;
        DrawArc(c, T * 0.75f, spin, spin + (Mathf.Pi * 1.4f), 40, Trim(accent, working), T * 0.08f);
        DrawArc(c, T * 0.5f, -spin * 1.6f, (-spin * 1.6f) + Mathf.Pi, 32, Trim(accent, working, 0.8f), T * 0.06f);
        DrawProgressRing(b, c, T * 0.32f, accent);
        if ((b.Recipe is { } recipe) && (Root.Zoom > 0.6f))
        {
            DrawString(_font, r.Position + new Vector2(T * 0.2f, r.Size.Y - (T * 0.28f)), recipe.Name, HorizontalAlignment.Left, r.Size.X - (T * 0.4f), 11, new Color(0.15f, 0.2f, 0.28f));
        }
    }

    private void DrawBeacon(Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter();
        GlassDisc(c, T * 0.8f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * Mathf.Tau / 3) - (Mathf.Pi / 2);
            DrawLine(c, c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * T * 0.7f), _hullLight, Mathf.Max(1, T * 0.06f));
        }

        DrawCircle(c, T * 0.22f, Trim(accent, working, 1.2f));
        if (working)
        {
            for (int i = 0; i < 3; i++)
            {
                float ph = Mathf.PosMod((Time * 0.5f) + (i / 3f), 1f);
                DrawArc(c, T * (1.3f + (ph * 3.5f)), 0, Mathf.Tau, 64, Hdr(A(accent, 1 - ph), 1.25f), T * 0.05f);
            }
        }
    }

    private void DrawLab(Building b, Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter() - new Vector2(0, T * 0.06f);
        GlassDisc(c, T * 0.6f);
        float pulse = 0.5f + (0.5f * Mathf.Sin((Time * 3f) + b.Id));
        DrawCircle(c, T * 0.28f, working ? Hdr(accent, 1.1f + (pulse * 0.5f)) : accent.Darkened(0.5f));
        if (working)
        {
            DrawArc(c, T * 0.45f, Time * 2, (Time * 2) + 1.5f, 16, Hdr(Colors.White, 1.25f), Mathf.Max(1, T * 0.04f));
        }

        DrawProgressRing(b, c, T * 0.52f, accent);
    }

    private void DrawElectricDrill(Building b, Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter();
        GlassDisc(c, T * 0.55f);
        float spin = working ? Time * 3f : b.Id;
        DrawArc(c, T * 0.4f, spin, spin + (Mathf.Pi * 1.5f), 24, Trim(accent, working), T * 0.08f);
        Vector2 v = new Vector2(b.Rot.Vec().X, b.Rot.Vec().Y);
        DrawArrow(c + (v * T * 0.8f), b.Rot, T * 0.15f, Trim(accent, working));
    }

    private void DrawSorterArm(Building b, Rect2 r, Color accent)
    {
        Vector2 c = r.GetCenter();
        DrawCircle(c + new Vector2(T * 0.06f, T * 0.08f), T * 0.26f, new Color(0, 0, 0, 0.3f));
        DrawCircle(c, T * 0.24f, _hullShade);
        DrawCircle(c, T * 0.18f, _hullLight);
        float swing = b.Def.Period > 0 ? b.Progress / (b.Def.Period * 100f) : 0;
        Vector2 v = new Vector2(b.Rot.Vec().X, b.Rot.Vec().Y);
        Vector2 hand = c + (v * T * Mathf.Lerp(-0.42f, 0.42f, swing));
        DrawLine(c, hand, _hullLight, T * 0.11f);
        DrawLine(c, hand, _hullShade, Mathf.Max(1, T * 0.03f));
        DrawCircle(hand, T * 0.09f, Trim(accent, b.Status == MachineStatus.Working, 1.1f));
        DrawCircle(c, T * 0.06f, Trim(accent, true, 0.9f));
    }

    private void DrawProgressRing(Building b, Vector2 c, float radius, Color accent)
    {
        long need = b.Def.Kind == BuildingKind.Lab
            ? (Root.World.Research.Current is { } id ? Root.Content.Techs[id].UnitTicks * 100L : 0)
            : (b.Recipe?.Ticks ?? 0) * 100L;
        if ((need <= 0) || (b.Crafting == false))
        {
            return;
        }

        float f = Mathf.Clamp(b.Progress / (float)need, 0, 1);
        DrawArc(c, radius, -Mathf.Pi / 2, (-Mathf.Pi / 2) + (Mathf.Tau * f), 32, Hdr(accent.Lightened(0.3f), 1.2f), Mathf.Max(1.5f, T * 0.05f));
    }
}
