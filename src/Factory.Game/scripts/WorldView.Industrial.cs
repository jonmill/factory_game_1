using Factory.Sim;
using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game;

/// <summary>
/// Steam, coal and timber era art: weathered iron, stone and planks, rivets, rust and soot. Nothing here
/// glows except open fire.
/// </summary>
public partial class WorldView
{
    private static readonly Color _iron = new(0.36f, 0.35f, 0.33f);
    private static readonly Color _darkIron = new(0.17f, 0.16f, 0.15f);
    private static readonly Color _rust = new(0.45f, 0.27f, 0.15f);
    private static readonly Color _timber = new(0.47f, 0.34f, 0.2f);

    private readonly Dictionary<PowerNetwork, (long Key, List<(Building From, Building To)> Edges)> _wireTrees = [];
    private static readonly Color _stone = new(0.55f, 0.5f, 0.43f);
    private static readonly Color _fire = new(1f, 0.55f, 0.18f);

    private void DrawBuilding(Building b)
    {
        Rect2 r = RectOf(b.Origin, b.W, b.H);
        if (OnScreen(r) == false)
        {
            return;
        }

        bool working = b.Status == MachineStatus.Working;
        if (Baked(b) is { } sprite)
        {
            DrawBaked(b, sprite, working);

            return;
        }

        if (b.Def.Look == VisualStyle.HighTech)
        {
            DrawHighTech(b, r, working);

            return;
        }

        if ((b.Def.AgeId == "combustion") && DrawCombustion(b, r, working))
        {
            return;
        }

        switch (b.Def.Kind)
        {
            case BuildingKind.Furnace:
                DrawStoneFurnace(b, r, working);
                break;
            case BuildingKind.Drill:
                DrawBurnerDrill(b, r, working);
                break;
            case BuildingKind.SteamEngine:
                DrawSteamEngine(b, r, working);
                break;
            case BuildingKind.Chest:
                DrawCrate(b, r);
                break;
            case BuildingKind.RailStop:
                DrawRailStop(b, r);
                break;
            case BuildingKind.Pole:
                DrawWoodPole(b, r);
                break;
            case BuildingKind.Battery:
                DrawLeadAcidBank(b, r);
                break;
            case BuildingKind.WaterTower:
                DrawWaterTower(r);
                break;
            default:
                IronBox(r.Grow(-T * 0.06f), _iron, b.Id);
                break;
        }
    }

    /// <summary>A riveted, weathered iron housing with a bevelled edge and a few rust patches.</summary>
    private void IronBox(Rect2 r, Color tint, int seed)
    {
        DropShadow(r);
        DrawRect(r, _darkIron);
        Rect2 body = r.Grow(-Mathf.Max(1.5f, T * 0.05f));
        DrawRect(body, tint);
        DrawLine(body.Position, body.Position + new Vector2(body.Size.X, 0), tint.Lightened(0.22f), Mathf.Max(1, T * 0.04f));
        DrawLine(body.Position, body.Position + new Vector2(0, body.Size.Y), tint.Lightened(0.12f), Mathf.Max(1, T * 0.04f));
        DrawLine(body.End, body.End - new Vector2(body.Size.X, 0), tint.Darkened(0.35f), Mathf.Max(1, T * 0.05f));
        DrawLine(body.End, body.End - new Vector2(0, body.Size.Y), tint.Darkened(0.3f), Mathf.Max(1, T * 0.05f));

        for (int k = 0; k < 3; k++)
        {
            uint h = Hash(seed, k, 21);
            Vector2 at = body.Position + new Vector2(body.Size.X * (0.15f + ((h & 63) / 90f)), body.Size.Y * (0.15f + (((h >> 6) & 63) / 90f)));
            DrawCircle(at, T * (0.06f + (((h >> 12) & 3) * 0.03f)), A(_rust, 0.45f));
        }

        float inset = T * 0.12f;
        foreach (Vector2 corner in new[] { body.Position, body.Position + new Vector2(body.Size.X, 0), body.End, body.Position + new Vector2(0, body.Size.Y) })
        {
            Vector2 toward = (body.GetCenter() - corner).Normalized() * inset * 1.4f;
            DrawCircle(corner + toward, Mathf.Max(1, T * 0.035f), tint.Lightened(0.35f));
        }
    }

    /// <summary>Rising soot or steam puffs above a point.</summary>
    private void Puffs(Vector2 at, Color color, float rate, int seed)
    {
        for (int i = 0; i < 4; i++)
        {
            float ph = Mathf.PosMod((Time * rate) + (i / 4f) + (seed * 0.137f), 1f);
            Vector2 drift = new Vector2((T * 0.35f * ph) + (Mathf.Sin((ph * 6f) + seed) * T * 0.08f), -T * 1.1f * ph);
            DrawCircle(at + drift, T * (0.1f + (ph * 0.28f)), A(color, 0.45f * (1 - ph)));
        }
    }

    private void FireGlow(Vector2 c, float radius, bool lit, int seed)
    {
        if (lit == false)
        {
            DrawCircle(c, radius, new Color(0.08f, 0.06f, 0.05f));
            DrawCircle(c, radius * 0.5f, new Color(0.2f, 0.1f, 0.06f));

            return;
        }

        float flicker = 0.85f + (0.15f * Mathf.Sin((Time * 17f) + seed)) + (0.1f * Mathf.Sin((Time * 7.3f) + (seed * 2)));
        DrawCircle(c, radius, new Color(0.25f, 0.08f, 0.03f));
        DrawCircle(c, radius * 0.8f, Hdr(_fire.Darkened(0.2f), 1.05f * flicker));
        DrawCircle(c + new Vector2(0, radius * 0.15f), radius * 0.45f, Hdr(new Color(1f, 0.85f, 0.45f), 1.3f * flicker));
    }

    private void DrawStoneFurnace(Building b, Rect2 r, bool working)
    {
        Rect2 body = r.Grow(-T * 0.08f);
        DropShadow(body);
        DrawRect(body, _stone.Darkened(0.45f));
        int rows = 4;
        float rowH = body.Size.Y / rows;
        for (int row = 0; row < rows; row++)
        {
            float offset = row % 2 * 0.5f;
            int blocks = 3;
            float blockW = body.Size.X / blocks;
            for (int k = -1; k < blocks; k++)
            {
                float bx = body.Position.X + ((k + offset) * blockW);
                Rect2 block = new Rect2(new Vector2(Mathf.Max(bx, body.Position.X), body.Position.Y + (row * rowH)), new Vector2(blockW, rowH));
                block = block.Intersection(body).Grow(-Mathf.Max(0.8f, T * 0.025f));
                uint h = Hash(b.Id, (row * 8) + k, 31);
                DrawRect(block, _stone.Darkened(((h & 7) / 7f * 0.22f) + (row * 0.03f)));
                DrawLine(block.Position, block.Position + new Vector2(block.Size.X, 0), _stone.Lightened(0.15f), 1);
            }
        }

        Vector2 mouth = body.GetCenter() + new Vector2(0, body.Size.Y * 0.12f);
        DrawRect(new Rect2(mouth - new Vector2(T * 0.38f, T * 0.3f), new Vector2(T * 0.76f, T * 0.52f)), _darkIron);
        FireGlow(mouth, T * 0.24f, working, b.Id);
        DrawRect(new Rect2(body.Position + new Vector2(body.Size.X * 0.62f, T * 0.04f), new Vector2(T * 0.32f, T * 0.32f)), _darkIron);
        if (working)
        {
            Puffs(body.Position + new Vector2((body.Size.X * 0.62f) + (T * 0.16f), T * 0.1f), new Color(0.25f, 0.24f, 0.23f), 0.5f, b.Id);
        }
    }

    private void DrawBurnerDrill(Building b, Rect2 r, bool working)
    {
        IronBox(r.Grow(-T * 0.06f), _iron.Lerp(_rust, 0.25f), b.Id);
        Vector2 c = r.GetCenter();
        float spin = working ? Time * 4f : b.Id;
        DrawCircle(c, T * 0.52f, _darkIron);
        for (int i = 0; i < 3; i++)
        {
            float a = spin + (i * Mathf.Tau / 3);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(c, c + (dir * T * 0.48f), _iron.Lightened(0.15f), T * 0.13f);
        }

        DrawCircle(c, T * 0.14f, _iron.Darkened(0.2f));
        Vector2 v = new Vector2(b.Rot.Vec().X, b.Rot.Vec().Y);
        Vector2 chute = c + (v * T * 0.82f);
        DrawRect(new Rect2(chute - new Vector2(T * 0.2f, T * 0.2f), new Vector2(T * 0.4f, T * 0.4f)), _darkIron);
        DrawArrow(chute, b.Rot, T * 0.14f, new Color(0.85f, 0.7f, 0.25f));
        Vector2 firebox = r.Position + new Vector2(T * 0.38f, r.Size.Y - (T * 0.38f));
        FireGlow(firebox, T * 0.13f, working, b.Id);
        if (working)
        {
            Puffs(r.Position + new Vector2(r.Size.X - (T * 0.35f), T * 0.3f), new Color(0.22f, 0.21f, 0.2f), 0.6f, b.Id);
        }
    }

    private void DrawSteamEngine(Building b, Rect2 r, bool working)
    {
        Rect2 body = r.Grow(-T * 0.06f);
        DropShadow(body);
        DrawRect(body, _darkIron);

        // Boiler drum along the top, banded and riveted.
        Rect2 drum = new Rect2(body.Position + new Vector2(T * 0.08f, T * 0.1f), new Vector2(body.Size.X - (T * 0.16f), body.Size.Y * 0.45f));
        DrawRect(drum, _iron.Darkened(0.1f));
        DrawRect(new Rect2(drum.Position, new Vector2(drum.Size.X, drum.Size.Y * 0.35f)), _iron.Lightened(0.12f));
        for (int k = 1; k < 4; k++)
        {
            float x = drum.Position.X + (drum.Size.X * k / 4f);
            DrawLine(new Vector2(x, drum.Position.Y), new Vector2(x, drum.End.Y), _darkIron, Mathf.Max(1, T * 0.04f));
        }

        // Firebox, flywheel and piston below.
        FireGlow(body.Position + new Vector2(T * 0.4f, body.Size.Y * 0.75f), T * 0.17f, working, b.Id);
        Vector2 wheel = body.Position + new Vector2(body.Size.X * 0.7f, body.Size.Y * 0.74f);
        DrawCircle(wheel, T * 0.36f, _darkIron.Lightened(0.08f));
        DrawArc(wheel, T * 0.32f, 0, Mathf.Tau, 20, _iron.Lightened(0.1f), Mathf.Max(1, T * 0.06f));
        float spin = working ? Time * 6f : 0.4f;
        for (int i = 0; i < 4; i++)
        {
            float a = spin + (i * Mathf.Pi / 2);
            DrawLine(wheel, wheel + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * T * 0.3f), _iron, Mathf.Max(1, T * 0.04f));
        }

        Vector2 crank = wheel + (new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * T * 0.22f);
        DrawLine(body.Position + new Vector2(body.Size.X * 0.3f, body.Size.Y * 0.74f), crank, _iron.Lightened(0.25f), Mathf.Max(1.5f, T * 0.07f));
        if (working)
        {
            Puffs(drum.Position + new Vector2(drum.Size.X * 0.2f, 0), new Color(0.85f, 0.85f, 0.85f), 0.9f, b.Id);
        }
    }

    /// <summary>A plank crate with a darker frame, steel corner brackets and a fill gauge along its foot.</summary>
    private void DrawCrate(Building b, Rect2 r)
    {
        Rect2 body = r.Grow(-T * 0.1f);
        DropShadow(body, 0.3f);
        DrawRect(body, _timber.Darkened(0.5f));
        float frame = T * 0.09f;
        Rect2 face = body.Grow(-frame);
        int planks = 3;
        for (int k = 0; k < planks; k++)
        {
            Rect2 plank = new Rect2(face.Position + new Vector2(0, face.Size.Y * k / planks), new Vector2(face.Size.X, face.Size.Y / planks)).Grow(-Mathf.Max(0.6f, T * 0.015f));
            Color wood = _timber.Darkened((Hash(b.Id, k, 41) & 7) / 7f * 0.16f);
            DrawRect(plank, wood);
            DrawLine(plank.Position, plank.Position + new Vector2(plank.Size.X, 0), wood.Lightened(0.18f), Mathf.Max(1, T * 0.02f));
            if (T > 30)
            {
                // Grain: two faint streaks per plank.
                for (int g = 0; g < 2; g++)
                {
                    float gy = plank.Position.Y + (plank.Size.Y * (0.35f + (g * 0.3f)));
                    float gx = plank.Position.X + (plank.Size.X * ((Hash(b.Id, k + (g * 5), 43) & 15) / 40f));
                    DrawLine(new Vector2(gx, gy), new Vector2(gx + (plank.Size.X * 0.45f), gy), wood.Darkened(0.15f), 1);
                }
            }
        }

        // The diagonal brace and the frame's lit top edge.
        DrawLine(face.Position, face.End, _timber.Darkened(0.4f), Mathf.Max(1.5f, T * 0.08f));
        DrawLine(face.Position + new Vector2(0, -T * 0.02f), face.End + new Vector2(0, -T * 0.02f), _timber.Lightened(0.12f), Mathf.Max(1, T * 0.02f));
        DrawLine(body.Position, body.Position + new Vector2(body.Size.X, 0), _timber.Lightened(0.05f), Mathf.Max(1, T * 0.03f));
        DrawRect(body, _darkIron, false, Mathf.Max(1, T * 0.04f));

        // Steel corner brackets with a rivet.
        float c = T * 0.17f;
        foreach (Vector2 corner in new[] { body.Position, body.Position + new Vector2(body.Size.X - c, 0), body.Position + new Vector2(0, body.Size.Y - c), body.End - new Vector2(c, c) })
        {
            DrawRect(new Rect2(corner, new Vector2(c, c)), new Color(0.42f, 0.44f, 0.46f));
            DrawRect(new Rect2(corner, new Vector2(c, c)), _darkIron, false, Mathf.Max(1, T * 0.02f));
            DrawCircle(corner + new Vector2(c / 2, c / 2), Mathf.Max(0.8f, T * 0.025f), new Color(0.7f, 0.72f, 0.74f));
        }

        float fill = b.Inv.Slots > 0 ? (float)b.Inv.UsedSlots / b.Inv.Slots : 0;
        if (fill > 0)
        {
            Rect2 gauge = new Rect2(body.Position + new Vector2(0, body.Size.Y + (T * 0.04f)), new Vector2(body.Size.X, T * 0.06f));
            DrawRect(gauge, new Color(0, 0, 0, 0.35f));
            DrawRect(gauge with { Size = new Vector2(gauge.Size.X * fill, gauge.Size.Y) }, fill >= 1 ? new Color(0.9f, 0.45f, 0.3f, 0.9f) : new Color(0.8f, 0.7f, 0.35f, 0.85f));
        }
    }

    private void DrawRailStop(Building b, Rect2 r)
    {
        Rect2 body = r.Grow(-T * 0.08f);
        DropShadow(body, 0.3f);
        DrawRect(body, _timber.Darkened(0.2f));
        for (int k = 1; k < 4; k++)
        {
            float y = body.Position.Y + (body.Size.Y * k / 4f);
            DrawLine(new Vector2(body.Position.X, y), new Vector2(body.End.X, y), _timber.Darkened(0.45f), 1);
        }

        DrawRect(body, _darkIron, false, Mathf.Max(1, T * 0.05f));
        DrawArrow(body.GetCenter(), b.Rot, T * 0.24f, new Color(0.92f, 0.78f, 0.3f));
        Vector2 post = body.Position + new Vector2(body.Size.X * 0.85f, body.Size.Y * 0.15f);
        DrawLine(post, post - new Vector2(0, T * 0.55f), _darkIron, Mathf.Max(1, T * 0.06f));
        DrawRect(new Rect2(post - new Vector2(T * 0.02f, T * 0.6f), new Vector2(T * 0.32f, T * 0.2f)), new Color(0.75f, 0.2f, 0.15f));
        if (Root.Zoom > 0.6f)
        {
            DrawString(_font, body.Position + new Vector2(0, -T * 0.12f), b.Name, HorizontalAlignment.Left, -1, 12, new Color(1f, 0.92f, 0.75f));
        }
    }

    private void DrawWoodPole(Building b, Rect2 r)
    {
        Vector2 c = r.GetCenter();
        DrawLine(c + new Vector2(T * 0.08f, T * 0.32f), c + new Vector2(T * 0.4f, T * 0.15f), new Color(0, 0, 0, 0.3f), T * 0.12f);
        DrawCircle(c + new Vector2(0, T * 0.32f), T * 0.1f, _timber.Darkened(0.4f));
        DrawLine(c + new Vector2(0, T * 0.32f), c - new Vector2(0, T * 0.22f), _timber, T * 0.11f);
        DrawLine(c - new Vector2(T * 0.24f, T * 0.2f), c + new Vector2(T * 0.24f, -T * 0.2f), _timber.Darkened(0.15f), T * 0.08f);
        bool live = (b.NetworkId != 0) && P.Networks.TryGetValue(b.NetworkId, out PowerNetwork? n) && ((n.GenCapacityJ + n.StoredJ) > 0);
        Color insulator = live ? new Color(0.75f, 0.85f, 0.75f) : new Color(0.55f, 0.55f, 0.5f);
        DrawCircle(c + new Vector2(-T * 0.2f, -T * 0.24f), T * 0.05f, insulator);
        DrawCircle(c + new Vector2(T * 0.2f, -T * 0.24f), T * 0.05f, insulator);
    }

    private void DrawLeadAcidBank(Building b, Rect2 r)
    {
        IronBox(r.Grow(-T * 0.06f), new Color(0.3f, 0.33f, 0.28f), b.Id);
        Rect2 inner = r.Grow(-T * 0.28f);
        int cells = 3;
        for (int k = 0; k < cells; k++)
        {
            Rect2 cell = new Rect2(inner.Position + new Vector2(inner.Size.X * k / cells, 0), new Vector2(inner.Size.X / cells, inner.Size.Y)).Grow(-T * 0.05f);
            DrawRect(cell, new Color(0.18f, 0.2f, 0.17f));
            DrawCircle(cell.Position + new Vector2(cell.Size.X * 0.3f, T * 0.14f), T * 0.06f, new Color(0.65f, 0.2f, 0.15f));
            DrawCircle(cell.Position + new Vector2(cell.Size.X * 0.7f, T * 0.14f), T * 0.06f, new Color(0.12f, 0.12f, 0.12f));
        }

        float frac = b.Def.CapacityKj > 0 ? b.StoredJ / (b.Def.CapacityKj * 1000f) : 0;
        Rect2 gauge = new Rect2(inner.Position + new Vector2(0, inner.Size.Y + (T * 0.06f)), new Vector2(inner.Size.X, T * 0.1f));
        DrawRect(gauge, _darkIron);
        DrawRect(new Rect2(gauge.Position, new Vector2(gauge.Size.X * frac, gauge.Size.Y)), new Color(0.55f, 0.75f, 0.3f));
    }

    private void DrawWaterTower(Rect2 r)
    {
        Vector2 c = r.GetCenter();
        DrawCircle(c + new Vector2(T * 0.12f, T * 0.16f), T * 0.44f, new Color(0, 0, 0, 0.3f));
        foreach (Vector2 leg in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
        {
            DrawCircle(c + (leg * T * 0.32f), T * 0.07f, _darkIron);
        }

        DrawCircle(c, T * 0.4f, _iron.Lerp(_rust, 0.35f));
        DrawArc(c, T * 0.4f, 0, Mathf.Tau, 24, _darkIron, Mathf.Max(1, T * 0.06f));
        DrawCircle(c, T * 0.26f, new Color(0.2f, 0.38f, 0.45f));
        DrawCircle(c + new Vector2(-T * 0.08f, -T * 0.08f), T * 0.08f, new Color(0.45f, 0.62f, 0.68f));
    }

    private void DrawRails(int x0, int y0, int x1, int y1)
    {
        Color ballast = new Color(0.33f, 0.31f, 0.28f, 0.85f);
        Color tie = new Color(0.32f, 0.22f, 0.14f);
        Color steel = new Color(0.62f, 0.62f, 0.6f);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Int2 t = P.Torus.Wrap(new Int2(x, y));
                if (P.IsRail(t) == false)
                {
                    continue;
                }

                Vector2 c = Center(t);
                List<SimDir> links = Dirs.All.Where(d => P.IsRail(t + d.Vec())).ToList();
                if (links.Count == 0)
                {
                    DrawRect(new Rect2(c - new Vector2(T * 0.4f, T * 0.4f), new Vector2(T * 0.8f, T * 0.8f)), ballast);
                    continue;
                }

                foreach (SimDir d in links)
                {
                    Vector2 v = new Vector2(d.Vec().X, d.Vec().Y);
                    Vector2 perp = new Vector2(-v.Y, v.X);
                    DrawLine(c, c + (v * T * 0.5f), ballast, T * 0.8f);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector2 tp = c + (v * T * (0.1f + (0.25f * k)));
                        DrawLine(tp - (perp * T * 0.34f), tp + (perp * T * 0.34f), tie, T * 0.11f);
                    }
                }

                foreach (SimDir d in links)
                {
                    Vector2 v = new Vector2(d.Vec().X, d.Vec().Y);
                    Vector2 perp = new Vector2(-v.Y, v.X);
                    foreach (float side in new[] { -0.18f, 0.18f })
                    {
                        Vector2 a = c + (perp * T * side);
                        DrawLine(a, a + (v * T * 0.5f), steel.Darkened(0.35f), Mathf.Max(1.5f, T * 0.075f));
                        DrawLine(a, a + (v * T * 0.5f), steel, Mathf.Max(1, T * 0.035f));
                    }
                }
            }
        }
    }

    /// <summary>Copper wires between poles, sagging slightly between their crossbars.</summary>
    /// <summary>
    /// Copper wires between poles. A network's poles are joined by its shortest spanning tree, the way a player
    /// strings wire from each new pole to the nearest one, rather than every pole to every pole in reach,
    /// which turns a dense base into a web. The tree is cached until the network's poles change.
    /// </summary>
    private void DrawPowerWires()
    {
        Color copper = new Color(0.72f, 0.45f, 0.25f);
        Color shine = new Color(0.95f, 0.7f, 0.45f);
        foreach (PowerNetwork net in P.Networks.Values)
        {
            foreach ((Building from, Building to) in WireTree(net))
            {
                Vector2 a = WireAnchor(from);
                Vector2 b = a + (Root.WrapDelta(new Vector2(from.Origin.X, from.Origin.Y), new Vector2(to.Origin.X, to.Origin.Y)) * T);
                if (OnScreen(new Rect2(a, Vector2.Zero).Expand(b)) == false)
                {
                    continue;
                }

                // A sagging catenary: deepest in the middle, deeper for longer spans.
                float sag = (a.DistanceTo(b) * 0.07f) + (T * 0.05f);
                Vector2[] wire = new Vector2[9];
                for (int k = 0; k < wire.Length; k++)
                {
                    float f = k / (float)(wire.Length - 1);
                    wire[k] = a.Lerp(b, f) + new Vector2(0, sag * 4 * f * (1 - f));
                }

                Vector2 shadow = new Vector2(T * 0.25f, T * 0.45f);
                DrawPolyline([.. wire.Select(w => w + shadow)], new Color(0, 0, 0, 0.16f), Mathf.Max(1, T * 0.03f), true);
                DrawPolyline(wire, copper.Darkened(0.25f), Mathf.Max(1.2f, T * 0.045f), true);
                DrawPolyline(wire, A(shine, 0.55f), Mathf.Max(0.6f, T * 0.015f), true);
            }
        }
    }

    /// <summary>Where a pole's wires attach: the baked model's insulators, or just above the flat pole's centre.</summary>
    private Vector2 WireAnchor(Building pole) => Baked(pole) is { Anchor: { } a } ? S(pole.Origin) + (a * T) : Center(pole.Origin) - new Vector2(0, T * 0.24f);

    /// <summary>The network's wires: a minimum spanning tree over poles within wire reach of each other (Prim's).</summary>
    private List<(Building From, Building To)> WireTree(PowerNetwork net)
    {
        List<Building> poles = net.Poles;
        long key = poles.Count;
        foreach (Building pole in poles)
        {
            key = (key * 31) + pole.Id;
        }

        if (_wireTrees.TryGetValue(net, out (long Key, List<(Building From, Building To)> Edges) cached) && (cached.Key == key))
        {
            return cached.Edges;
        }

        if (_wireTrees.Count > 64)
        {
            _wireTrees.Clear();
        }

        List<(Building From, Building To)> edges = [];
        int n = poles.Count;
        bool[] inTree = new bool[n];
        long[] best = new long[n];
        int[] parent = new int[n];
        System.Array.Fill(best, long.MaxValue);
        System.Array.Fill(parent, -1);
        for (int root = 0; root < n; root++)
        {
            if (inTree[root])
            {
                continue;
            }

            best[root] = 0;
            while (true)
            {
                int pick = -1;
                for (int i = 0; i < n; i++)
                {
                    if ((inTree[i] == false) && (best[i] != long.MaxValue) && ((pick < 0) || (best[i] < best[pick])))
                    {
                        pick = i;
                    }
                }

                if (pick < 0)
                {
                    break;
                }

                inTree[pick] = true;
                if (parent[pick] >= 0)
                {
                    edges.Add((poles[parent[pick]], poles[pick]));
                }

                for (int j = 0; j < n; j++)
                {
                    int reach = System.Math.Max(poles[pick].Def.WireReach, poles[j].Def.WireReach);
                    if (inTree[j] || (P.Torus.Chebyshev(poles[pick].Origin, poles[j].Origin) > reach))
                    {
                        continue;
                    }

                    long d = P.Torus.DistSq(poles[pick].Origin, poles[j].Origin);
                    if (d < best[j])
                    {
                        best[j] = d;
                        parent[j] = pick;
                    }
                }
            }
        }

        _wireTrees[net] = (key, edges);

        return edges;
    }
}
