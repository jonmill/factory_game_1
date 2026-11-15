using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Combustion-age art: painted steel plate in safety yellow and oxide red, hazard stripes, pipework,
/// pressure gauges and oil stains. Cleaner than the salvaged iron of the Salvage Age, still greasy.
/// </summary>
public partial class WorldView
{
    private static readonly Color _paint = new(0.72f, 0.58f, 0.2f);
    private static readonly Color _oxide = new(0.52f, 0.2f, 0.14f);
    private static readonly Color _steel = new(0.46f, 0.48f, 0.5f);
    private static readonly Color _oil = new(0.06f, 0.05f, 0.04f);

    /// <summary>Draws a Combustion-age building, if this is one; false lets the caller draw it the Salvage way.</summary>
    private bool DrawCombustion(Building b, Rect2 r, bool working)
    {
        switch (b.Def.Kind)
        {
            case BuildingKind.Assembler:
                DrawRefinery(b, r, working);
                return true;
            case BuildingKind.SteamEngine when b.Def.NeedsWater == false:
                DrawGenerator(b, r, working);
                return true;
            case BuildingKind.Pole:
                DrawPylon(b, r);
                return true;
            case BuildingKind.LaunchPad:
                DrawLaunchPad(b, r);
                return true;
            case BuildingKind.Ferry:
                DrawFerry(b, r);
                return true;
            default:
                return false;
        }
    }

    /// <summary>A painted steel housing with a hazard-striped edge and an oil stain underneath.</summary>
    private void SteelBox(Rect2 r, Color paint, int seed, bool stripes = true)
    {
        uint h = Hash(seed, 3, 77);
        DrawCircle(r.GetCenter() + new Vector2(T * (((h & 7) / 7f) - 0.5f), r.Size.Y * 0.3f), r.Size.X * 0.32f, A(_oil, 0.35f));
        DropShadow(r);
        DrawRect(r, _darkIron);
        Rect2 body = r.Grow(-Mathf.Max(1.5f, T * 0.05f));
        DrawRect(body, paint);
        DrawRect(new Rect2(body.Position, new Vector2(body.Size.X, body.Size.Y * 0.18f)), paint.Lightened(0.15f));
        if (stripes)
        {
            float band = T * 0.14f;
            Rect2 strip = new Rect2(body.Position.X, body.End.Y - band, body.Size.X, band);
            DrawRect(strip, new Color(0.1f, 0.1f, 0.1f));
            for (float x = strip.Position.X; x < strip.End.X; x += band * 2)
            {
                DrawColoredPolygon(
                [
                    new Vector2(x, strip.End.Y), new Vector2(Mathf.Min(x + band, strip.End.X), strip.Position.Y),
                    new Vector2(Mathf.Min(x + (band * 2), strip.End.X), strip.Position.Y), new Vector2(Mathf.Min(x + band, strip.End.X), strip.End.Y),
                ], new Color(0.9f, 0.72f, 0.15f));
            }
        }

        // Grime streaks down from the top edge.
        for (int k = 0; k < 3; k++)
        {
            uint g = Hash(seed, k, 91);
            float x = body.Position.X + (body.Size.X * (0.1f + ((g & 63) / 80f)));
            DrawLine(new Vector2(x, body.Position.Y), new Vector2(x, body.Position.Y + (body.Size.Y * (0.2f + (((g >> 6) & 15) / 40f)))), A(_oil, 0.25f), Mathf.Max(1, T * 0.05f));
        }
    }

    private void Gauge(Vector2 c, float radius, float value)
    {
        DrawCircle(c, radius, new Color(0.88f, 0.86f, 0.8f));
        DrawArc(c, radius, 0, Mathf.Tau, 16, _darkIron, Mathf.Max(1, radius * 0.25f));
        float a = Mathf.Pi * (0.75f + (1.5f * Mathf.Clamp(value, 0, 1)));
        DrawLine(c, c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.8f), new Color(0.75f, 0.12f, 0.1f), Mathf.Max(1, radius * 0.18f));
    }

    private void DrawRefinery(Building b, Rect2 r, bool working)
    {
        Rect2 body = r.Grow(-T * 0.08f);
        SteelBox(body, _oxide, b.Id);

        // Two cracking columns and a squat tank, joined by pipes.
        Vector2 colA = body.Position + new Vector2(body.Size.X * 0.24f, body.Size.Y * 0.42f);
        Vector2 colB = body.Position + new Vector2(body.Size.X * 0.48f, body.Size.Y * 0.36f);
        Vector2 tank = body.Position + new Vector2(body.Size.X * 0.76f, body.Size.Y * 0.56f);
        DrawLine(colA, colB, _steel.Darkened(0.2f), T * 0.12f);
        DrawLine(colB, tank, _steel.Darkened(0.2f), T * 0.12f);
        foreach ((Vector2 c, float rad) in new[] { (colA, T * 0.34f), (colB, T * 0.42f) })
        {
            DrawCircle(c, rad, _steel.Darkened(0.15f));
            DrawCircle(c, rad * 0.8f, _steel);
            DrawArc(c, rad * 0.55f, 0, Mathf.Tau, 16, _steel.Darkened(0.3f), Mathf.Max(1, T * 0.04f));
            DrawCircle(c - new Vector2(rad * 0.25f, rad * 0.25f), rad * 0.2f, _steel.Lightened(0.3f));
        }

        DrawCircle(tank, T * 0.5f, _paint.Darkened(0.1f));
        DrawArc(tank, T * 0.5f, 0, Mathf.Tau, 20, _darkIron, Mathf.Max(1, T * 0.05f));
        Gauge(tank, T * 0.18f, working ? 0.6f + (0.1f * Mathf.Sin(Time * 3f)) : 0.1f);
        if (working)
        {
            FireGlow(colB - new Vector2(0, T * 0.55f), T * 0.1f, true, b.Id);
            Puffs(colA - new Vector2(0, T * 0.3f), new Color(0.2f, 0.18f, 0.16f), 0.5f, b.Id);
        }
    }

    private void DrawGenerator(Building b, Rect2 r, bool working)
    {
        Rect2 body = r.Grow(-T * 0.06f);
        SteelBox(body, _paint, b.Id);

        // Engine block with cylinder heads, an exhaust stack and a radiator grille.
        Rect2 block = new Rect2(body.Position + new Vector2(T * 0.14f, T * 0.24f), new Vector2(body.Size.X * 0.55f, body.Size.Y * 0.5f));
        DrawRect(block, _steel.Darkened(0.25f));
        for (int k = 0; k < 3; k++)
        {
            float shake = working ? Mathf.Sin((Time * 30f) + (k * 2.1f)) * T * 0.02f : 0;
            Rect2 head = new Rect2(block.Position + new Vector2((block.Size.X * k / 3f) + (T * 0.04f), (T * 0.06f) + shake), new Vector2((block.Size.X / 3f) - (T * 0.08f), block.Size.Y * 0.4f));
            DrawRect(head, _steel.Lightened(0.08f));
        }

        Rect2 grille = new Rect2(body.Position + new Vector2(body.Size.X * 0.68f, T * 0.24f), new Vector2(body.Size.X * 0.24f, body.Size.Y * 0.5f));
        DrawRect(grille, _darkIron);
        for (int k = 1; k < 5; k++)
        {
            float y = grille.Position.Y + (grille.Size.Y * k / 5f);
            DrawLine(new Vector2(grille.Position.X, y), new Vector2(grille.End.X, y), _steel.Darkened(0.1f), 1);
        }

        Vector2 stack = body.Position + new Vector2(body.Size.X * 0.82f, T * 0.14f);
        DrawCircle(stack, T * 0.12f, _darkIron);
        if (working)
        {
            Puffs(stack, new Color(0.3f, 0.28f, 0.26f), 1.2f, b.Id);
        }

        Gauge(body.Position + new Vector2(T * 0.3f, body.Size.Y - (T * 0.36f)), T * 0.13f, working ? 0.75f : 0.05f);
    }

    private void DrawPylon(Building b, Rect2 r)
    {
        Vector2 c = r.GetCenter();
        DrawLine(c + new Vector2(T * 0.1f, T * 0.35f), c + new Vector2(T * 0.55f, T * 0.15f), new Color(0, 0, 0, 0.3f), T * 0.16f);
        Vector2 top = c - new Vector2(0, T * 0.38f);
        foreach (float sx in new[] { -1f, 1f })
        {
            DrawLine(c + new Vector2(sx * T * 0.3f, T * 0.36f), top + new Vector2(sx * T * 0.06f, 0), _steel, Mathf.Max(1, T * 0.06f));
        }

        for (int k = 1; k < 4; k++)
        {
            float f = k / 4f;
            float y = Mathf.Lerp(c.Y + (T * 0.36f), top.Y, f);
            float half = Mathf.Lerp(T * 0.3f, T * 0.06f, f);
            DrawLine(new Vector2(c.X - half, y), new Vector2(c.X + half, y), _steel.Darkened(0.15f), Mathf.Max(1, T * 0.03f));
        }

        DrawLine(top - new Vector2(T * 0.3f, 0), top + new Vector2(T * 0.3f, 0), _steel.Darkened(0.1f), Mathf.Max(1, T * 0.06f));
        bool live = (b.NetworkId != 0) && P.Networks.TryGetValue(b.NetworkId, out PowerNetwork? n) && ((n.GenCapacityJ + n.StoredJ) > 0);
        Color insulator = live ? new Color(0.6f, 0.8f, 0.9f) : new Color(0.5f, 0.5f, 0.5f);
        DrawCircle(top - new Vector2(T * 0.27f, 0), T * 0.05f, insulator);
        DrawCircle(top + new Vector2(T * 0.27f, 0), T * 0.05f, insulator);
    }

    private void DrawLaunchPad(Building b, Rect2 r)
    {
        Rect2 body = r.Grow(-T * 0.06f);
        DropShadow(body, 0.3f);
        DrawRect(body, new Color(0.34f, 0.33f, 0.31f));
        DrawRect(body, _darkIron, false, Mathf.Max(1, T * 0.06f));
        Vector2 c = body.GetCenter();
        DrawCircle(c, body.Size.X * 0.4f, new Color(0.2f, 0.19f, 0.18f));
        DrawArc(c, body.Size.X * 0.4f, 0, Mathf.Tau, 32, new Color(0.9f, 0.72f, 0.15f), Mathf.Max(1, T * 0.08f));
        DrawCircle(c, body.Size.X * 0.28f, A(_oil, 0.6f));

        // Scorch marks and a service gantry on one side.
        Rect2 gantry = new Rect2(body.Position + new Vector2(T * 0.08f, T * 0.1f), new Vector2(T * 0.3f, body.Size.Y - (T * 0.2f)));
        DrawRect(gantry, _oxide);
        for (int k = 1; k < 6; k++)
        {
            float y = gantry.Position.Y + (gantry.Size.Y * k / 6f);
            DrawLine(new Vector2(gantry.Position.X, y), new Vector2(gantry.End.X, y), _oxide.Darkened(0.4f), 1);
        }

        if ((b.Def.Vessel is { } vessel) && (b.Inv.Count(vessel) > 0))
        {
            DrawRocket(c, T * 1.1f);
        }

        float fuel = b.Def.LaunchFuelList.Length == 0 ? 0 : b.Def.LaunchFuelList.Min(f => Mathf.Min(1f, b.Inv.Count(f.Item) / (float)f.Count));
        Rect2 gauge = new Rect2(body.Position + new Vector2(body.Size.X * 0.2f, body.Size.Y + (T * 0.05f)), new Vector2(body.Size.X * 0.6f, T * 0.08f));
        DrawRect(gauge, _darkIron);
        DrawRect(new Rect2(gauge.Position, new Vector2(gauge.Size.X * fuel, gauge.Size.Y)), new Color(0.9f, 0.6f, 0.2f));
    }

    /// <summary>A stubby white ferry rocket seen from above: a nose cone, four fins and a red band.</summary>
    private void DrawRocket(Vector2 c, float length)
    {
        float w = length * 0.32f;
        foreach (Vector2 fin in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            DrawLine(c, c + (fin * w * 0.95f), _steel.Darkened(0.2f), w * 0.3f);
        }

        DrawCircle(c, w * 0.62f, new Color(0.88f, 0.88f, 0.9f));
        DrawArc(c, w * 0.62f, 0, Mathf.Tau, 20, new Color(0.7f, 0.2f, 0.15f), w * 0.14f);
        DrawCircle(c, w * 0.3f, new Color(0.95f, 0.95f, 0.97f));
        DrawCircle(c - new Vector2(w * 0.1f, w * 0.1f), w * 0.1f, new Color(1f, 1f, 1f));
    }

    private void DrawFerry(Building b, Rect2 r)
    {
        Vector2 c = r.GetCenter();
        DrawCircle(c, r.Size.X * 0.5f, A(_oil, 0.35f));
        foreach (Vector2 leg in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            DrawLine(c, c + (leg * r.Size.X * 0.42f), _darkIron, Mathf.Max(1.5f, T * 0.08f));
            DrawCircle(c + (leg * r.Size.X * 0.42f), T * 0.12f, _steel.Darkened(0.2f));
        }

        DrawRocket(c, r.Size.X * 0.9f);
        float fill = b.Inv.Slots > 0 ? (float)b.Inv.UsedSlots / b.Inv.Slots : 0;
        if (fill > 0)
        {
            DrawRect(new Rect2(r.Position + new Vector2(r.Size.X * 0.2f, r.Size.Y + (T * 0.02f)), new Vector2(r.Size.X * 0.6f * fill, T * 0.06f)), new Color(0.8f, 0.7f, 0.35f, 0.8f));
        }
    }
}
