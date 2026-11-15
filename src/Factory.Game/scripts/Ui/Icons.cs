using Factory.Sim;
using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// Procedural icons for items, buildings, vehicles and tools, drawn into any rect on any canvas item.
/// Ores are faceted crystals like on the map, industrial buildings are iron and timber, and high-tech
/// buildings are white hulls with accent trim, so an icon reads the same as the thing in the world.
/// </summary>
public static class Icons
{
    private static readonly Color _iron = new(0.42f, 0.44f, 0.46f);
    private static readonly Color _darkIron = new(0.2f, 0.21f, 0.23f);
    private static readonly Color _timber = new(0.55f, 0.4f, 0.24f);
    private static readonly Color _hull = new(0.86f, 0.89f, 0.93f);
    private static readonly Color _hullShade = new(0.58f, 0.63f, 0.7f);
    private static readonly Color _glass = new(0.08f, 0.12f, 0.18f);
    private static readonly Color _fire = new(1f, 0.6f, 0.2f);

    /// <summary>Draws the icon for an item.</summary>
    /// <param name="ci">Canvas item to draw on.</param>
    /// <param name="r">Target rect.</param>
    /// <param name="item">Item definition.</param>
    public static void Item(CanvasItem ci, Rect2 r, ItemDef item)
    {
        Color c = WorldView.Hex(item.Color);
        Vector2 m = r.GetCenter();
        float s = Mathf.Min(r.Size.X, r.Size.Y);
        string id = item.Id;
        if (id == "wood")
        {
            Log(ci, m + new Vector2(-s * 0.05f, s * 0.12f), s, c);
            Log(ci, m + new Vector2(s * 0.05f, -s * 0.12f), s, c.Lightened(0.08f));
        }
        else if (id == "coal")
        {
            Lump(ci, m + new Vector2(-s * 0.14f, s * 0.08f), s * 0.2f, c);
            Lump(ci, m + new Vector2(s * 0.14f, s * 0.1f), s * 0.17f, c);
            Lump(ci, m + new Vector2(0, -s * 0.12f), s * 0.19f, c.Lightened(0.05f));
        }
        else if (id == "stone")
        {
            Lump(ci, m + new Vector2(-s * 0.12f, s * 0.1f), s * 0.2f, c.Darkened(0.1f));
            Lump(ci, m + new Vector2(s * 0.13f, -s * 0.06f), s * 0.18f, c);
        }
        else if (id.EndsWith("-ore", System.StringComparison.Ordinal))
        {
            Crystal(ci, m + new Vector2(-s * 0.16f, s * 0.3f), s * 0.16f, -0.25f, c);
            Crystal(ci, m + new Vector2(s * 0.16f, s * 0.32f), s * 0.14f, 0.3f, c.Darkened(0.08f));
            Crystal(ci, m + new Vector2(0, s * 0.34f), s * 0.2f, 0.05f, c.Lightened(0.05f));
        }
        else if (id.EndsWith("-plate", System.StringComparison.Ordinal))
        {
            for (int i = 2; i >= 0; i--)
            {
                Vector2 o = m + new Vector2(0, (i - 1) * s * 0.12f);
                Vector2[] p = [o + new Vector2(-s * 0.3f, 0), o + new Vector2(0, -s * 0.13f), o + new Vector2(s * 0.3f, 0), o + new Vector2(0, s * 0.13f)];
                ci.DrawColoredPolygon(p, i == 0 ? c.Lightened(0.15f) : c.Darkened(0.12f * i));
                ci.DrawPolyline([.. p, p[0]], c.Darkened(0.45f), 1, true);
            }
        }
        else if (id == "stone-brick")
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    float off = row % 2 * s * 0.14f;
                    Rect2 b = new Rect2(m + new Vector2((-s * 0.34f) + off + (col * s * 0.3f), (-s * 0.27f) + (row * s * 0.19f)), new Vector2(s * 0.27f, s * 0.16f));
                    ci.DrawRect(b, row == 0 ? c.Lightened(0.08f) : c.Darkened(0.06f * row));
                    ci.DrawRect(b, c.Darkened(0.45f), false, 1);
                }
            }
        }
        else if (id == "iron-gear")
        {
            Gear(ci, m, s * 0.34f, c);
        }
        else if (id == "copper-wire")
        {
            for (int i = 0; i < 4; i++)
            {
                ci.DrawArc(m + new Vector2(0, (i - 1.5f) * s * 0.1f), s * 0.26f, Mathf.Pi * 0.05f, Mathf.Pi * 0.95f, 16, c.Darkened(i * 0.06f), Mathf.Max(1.5f, s * 0.06f), true);
            }

            ci.DrawLine(m + new Vector2(s * 0.26f, -s * 0.15f), m + new Vector2(s * 0.36f, -s * 0.32f), c, Mathf.Max(1.5f, s * 0.05f), true);
        }
        else if (id == "basic-circuit")
        {
            Rect2 board = new Rect2(m - new Vector2(s * 0.34f, s * 0.26f), new Vector2(s * 0.68f, s * 0.52f));
            ci.DrawRect(board, new Color(0.08f, 0.35f, 0.2f));
            ci.DrawRect(board, c.Darkened(0.3f), false, 1);
            for (int i = 0; i < 3; i++)
            {
                float y = board.Position.Y + (board.Size.Y * (0.25f + (i * 0.25f)));
                ci.DrawLine(new Vector2(board.Position.X + 2, y), new Vector2(board.End.X - 2, y), A(c, 0.55f), 1);
            }

            ci.DrawRect(new Rect2(m - new Vector2(s * 0.12f, s * 0.1f), new Vector2(s * 0.24f, s * 0.2f)), new Color(0.12f, 0.14f, 0.16f));
            ci.DrawRect(new Rect2(m - new Vector2(s * 0.05f, s * 0.04f), new Vector2(s * 0.1f, s * 0.08f)), c);
        }
        else if (id == "battery-cell")
        {
            Rect2 cell = new Rect2(m - new Vector2(s * 0.17f, s * 0.28f), new Vector2(s * 0.34f, s * 0.6f));
            ci.DrawRect(cell, new Color(0.2f, 0.22f, 0.2f));
            ci.DrawRect(new Rect2(cell.Position + new Vector2(0, cell.Size.Y * 0.3f), new Vector2(cell.Size.X, cell.Size.Y * 0.4f)), c.Darkened(0.2f));
            ci.DrawRect(new Rect2(m + new Vector2(-s * 0.07f, -s * 0.36f), new Vector2(s * 0.14f, s * 0.08f)), _iron.Lightened(0.3f));
            ci.DrawRect(cell, c.Darkened(0.5f), false, 1);
        }
        else
        {
            ci.DrawRect(r.Grow(-s * 0.2f), c);
        }
    }

    /// <summary>Draws the icon for a building, in its era's style.</summary>
    /// <param name="ci">Canvas item to draw on.</param>
    /// <param name="r">Target rect.</param>
    /// <param name="def">Building definition.</param>
    public static void Building(CanvasItem ci, Rect2 r, BuildingDef def)
    {
        Color accent = WorldView.Hex(def.Color);
        Vector2 m = r.GetCenter();
        float s = Mathf.Min(r.Size.X, r.Size.Y);
        Rect2 body = new Rect2(m - new Vector2(s * 0.34f, s * 0.34f), new Vector2(s * 0.68f, s * 0.68f));
        if (def.Look == VisualStyle.HighTech)
        {
            Hull(ci, body, accent);
        }

        switch (def.Kind)
        {
            case BuildingKind.Lander:
                ci.DrawCircle(m, s * 0.2f, _glass);
                ci.DrawArc(m, s * 0.14f, 0, Mathf.Pi * 1.4f, 16, accent, Mathf.Max(1.5f, s * 0.05f), true);
                break;
            case BuildingKind.Assembler:
                ci.DrawCircle(m, s * 0.22f, _glass);
                Gear(ci, m, s * 0.15f, accent);
                break;
            case BuildingKind.Lab:
                ci.DrawCircle(m, s * 0.2f, _glass);
                ci.DrawCircle(m, s * 0.1f, accent);
                break;
            case BuildingKind.Beacon:
                ci.DrawCircle(m, s * 0.2f, _glass);
                ci.DrawArc(m, s * 0.28f, -2.4f, -0.7f, 12, accent, Mathf.Max(1.5f, s * 0.04f), true);
                ci.DrawArc(m, s * 0.38f, -2.4f, -0.7f, 12, A(accent, 0.6f), Mathf.Max(1.5f, s * 0.04f), true);
                ci.DrawCircle(m, s * 0.07f, accent);
                break;
            case BuildingKind.Sorter:
                ci.DrawCircle(m + new Vector2(-s * 0.15f, s * 0.15f), s * 0.12f, _hullShade);
                ci.DrawLine(m + new Vector2(-s * 0.15f, s * 0.15f), m + new Vector2(s * 0.18f, -s * 0.18f), _hull, s * 0.09f, true);
                ci.DrawCircle(m + new Vector2(s * 0.18f, -s * 0.18f), s * 0.08f, accent);
                break;
            case BuildingKind.Drill when def.Look == VisualStyle.HighTech:
                ci.DrawCircle(m, s * 0.18f, _glass);
                ci.DrawArc(m, s * 0.13f, 0, Mathf.Pi * 1.5f, 12, accent, Mathf.Max(1.5f, s * 0.05f), true);
                break;
            case BuildingKind.Drill:
                IronBox(ci, body);
                ci.DrawColoredPolygon([m + new Vector2(-s * 0.16f, -s * 0.14f), m + new Vector2(s * 0.16f, -s * 0.14f), m + new Vector2(0, s * 0.26f)], _iron.Lightened(0.25f));
                ci.DrawLine(m + new Vector2(-s * 0.1f, -s * 0.02f), m + new Vector2(s * 0.1f, -s * 0.02f), _darkIron, 1);
                ci.DrawLine(m + new Vector2(-s * 0.06f, s * 0.08f), m + new Vector2(s * 0.06f, s * 0.08f), _darkIron, 1);
                break;
            case BuildingKind.Furnace:
                Bricks(ci, body, new Color(0.55f, 0.5f, 0.44f));
                ci.DrawRect(new Rect2(m + new Vector2(-s * 0.12f, -s * 0.02f), new Vector2(s * 0.24f, s * 0.2f)), new Color(0.12f, 0.08f, 0.06f));
                ci.DrawCircle(m + new Vector2(0, s * 0.1f), s * 0.07f, _fire);
                break;
            case BuildingKind.SteamEngine:
                IronBox(ci, body);
                ci.DrawRect(new Rect2(m + new Vector2(-s * 0.26f, -s * 0.12f), new Vector2(s * 0.3f, s * 0.24f)), _iron.Lightened(0.1f));
                ci.DrawArc(m + new Vector2(s * 0.14f, 0), s * 0.15f, 0, Mathf.Tau, 16, _iron.Lightened(0.3f), Mathf.Max(1.5f, s * 0.05f), true);
                ci.DrawLine(m + new Vector2(s * 0.14f, 0), m + new Vector2(s * 0.24f, -s * 0.08f), _iron.Lightened(0.3f), 1.5f, true);
                break;
            case BuildingKind.Pole:
                ci.DrawLine(m + new Vector2(0, -s * 0.36f), m + new Vector2(0, s * 0.36f), _timber, s * 0.09f);
                ci.DrawLine(m + new Vector2(-s * 0.22f, -s * 0.26f), m + new Vector2(s * 0.22f, -s * 0.26f), _timber.Darkened(0.2f), s * 0.07f);
                ci.DrawCircle(m + new Vector2(-s * 0.2f, -s * 0.3f), s * 0.04f, accent);
                ci.DrawCircle(m + new Vector2(s * 0.2f, -s * 0.3f), s * 0.04f, accent);
                break;
            case BuildingKind.Battery:
                IronBox(ci, body);
                for (int i = 0; i < 3; i++)
                {
                    Rect2 cell = new Rect2(m + new Vector2((-s * 0.25f) + (i * s * 0.18f), -s * 0.18f), new Vector2(s * 0.13f, s * 0.36f));
                    ci.DrawRect(cell, new Color(0.2f, 0.22f, 0.2f));
                    ci.DrawRect(new Rect2(cell.Position + new Vector2(0, cell.Size.Y * 0.35f), new Vector2(cell.Size.X, cell.Size.Y * 0.3f)), accent.Darkened(0.2f));
                }

                break;
            case BuildingKind.Chest:
                ci.DrawRect(body, _timber.Darkened(0.35f));
                ci.DrawRect(body.Grow(-s * 0.04f), _timber);
                ci.DrawLine(body.Position, body.End, _timber.Darkened(0.35f), Mathf.Max(1.5f, s * 0.05f));
                ci.DrawLine(body.Position + new Vector2(body.Size.X, 0), body.Position + new Vector2(0, body.Size.Y), _timber.Darkened(0.35f), Mathf.Max(1.5f, s * 0.05f));
                break;
            case BuildingKind.Rail:
                for (int i = 0; i < 4; i++)
                {
                    float y = m.Y + ((i - 1.5f) * s * 0.18f);
                    ci.DrawLine(new Vector2(m.X - (s * 0.3f), y), new Vector2(m.X + (s * 0.3f), y), _timber, s * 0.08f);
                }

                ci.DrawLine(m + new Vector2(-s * 0.16f, -s * 0.4f), m + new Vector2(-s * 0.16f, s * 0.4f), _iron.Lightened(0.3f), s * 0.06f);
                ci.DrawLine(m + new Vector2(s * 0.16f, -s * 0.4f), m + new Vector2(s * 0.16f, s * 0.4f), _iron.Lightened(0.3f), s * 0.06f);
                break;
            case BuildingKind.RailStop:
                ci.DrawRect(new Rect2(m + new Vector2(-s * 0.3f, s * 0.12f), new Vector2(s * 0.6f, s * 0.18f)), _timber);
                ci.DrawLine(m + new Vector2(-s * 0.12f, s * 0.15f), m + new Vector2(-s * 0.12f, -s * 0.36f), _iron.Lightened(0.2f), s * 0.05f);
                ci.DrawColoredPolygon([m + new Vector2(-s * 0.1f, -s * 0.36f), m + new Vector2(s * 0.24f, -s * 0.27f), m + new Vector2(-s * 0.1f, -s * 0.18f)], new Color(0.9f, 0.25f, 0.2f));
                break;
            case BuildingKind.WaterTower:
                ci.DrawLine(m + new Vector2(-s * 0.18f, 0), m + new Vector2(-s * 0.24f, s * 0.36f), _timber.Darkened(0.2f), s * 0.05f);
                ci.DrawLine(m + new Vector2(s * 0.18f, 0), m + new Vector2(s * 0.24f, s * 0.36f), _timber.Darkened(0.2f), s * 0.05f);
                ci.DrawRect(new Rect2(m + new Vector2(-s * 0.24f, -s * 0.34f), new Vector2(s * 0.48f, s * 0.36f)), _timber);
                ci.DrawRect(new Rect2(m + new Vector2(-s * 0.2f, -s * 0.12f), new Vector2(s * 0.4f, s * 0.1f)), new Color(0.3f, 0.55f, 0.85f));
                break;
            default:
                if (def.Look != VisualStyle.HighTech)
                {
                    IronBox(ci, body);
                }

                break;
        }
    }

    /// <summary>Draws a vehicle icon: a locomotive, a hand cart or a mine cart.</summary>
    /// <param name="ci">Canvas item to draw on.</param>
    /// <param name="r">Target rect.</param>
    /// <param name="def">Vehicle definition.</param>
    public static void Vehicle(CanvasItem ci, Rect2 r, VehicleDef def)
    {
        Vector2 m = r.GetCenter();
        float s = Mathf.Min(r.Size.X, r.Size.Y);
        Rect2 body = new Rect2(m - new Vector2(s * 0.36f, s * 0.2f), new Vector2(s * 0.72f, s * 0.36f));
        ci.DrawCircle(m + new Vector2(-s * 0.2f, s * 0.2f), s * 0.09f, _darkIron);
        ci.DrawCircle(m + new Vector2(s * 0.2f, s * 0.2f), s * 0.09f, _darkIron);
        switch (def.Kind)
        {
            case VehicleKind.Steam:
                ci.DrawRect(body, new Color(0.22f, 0.22f, 0.23f));
                ci.DrawRect(new Rect2(body.Position + new Vector2(0, -s * 0.12f), new Vector2(s * 0.26f, s * 0.14f)), new Color(0.3f, 0.12f, 0.1f));
                ci.DrawRect(new Rect2(m + new Vector2(s * 0.14f, -s * 0.38f), new Vector2(s * 0.1f, s * 0.2f)), _darkIron);
                ci.DrawLine(new Vector2(body.Position.X, m.Y), new Vector2(body.End.X, m.Y), new Color(0.6f, 0.15f, 0.12f), Mathf.Max(1, s * 0.04f));
                break;
            case VehicleKind.HandCart:
                ci.DrawRect(body, _timber);
                ci.DrawLine(m + new Vector2(-s * 0.2f, -s * 0.34f), m + new Vector2(s * 0.2f, -s * 0.2f), _darkIron, Mathf.Max(1.5f, s * 0.06f));
                ci.DrawLine(m, m + new Vector2(0, -s * 0.27f), _darkIron, Mathf.Max(1.5f, s * 0.05f));
                break;
            default:
                ci.DrawRect(body, _iron.Darkened(0.2f));
                ci.DrawCircle(m + new Vector2(0, -s * 0.18f), s * 0.18f, new Color(0.42f, 0.5f, 0.6f));
                ci.DrawRect(body, _darkIron, false, 1);
                break;
        }
    }

    /// <summary>The dismantle tool: a red crossed-out frame.</summary>
    /// <param name="ci">Canvas item to draw on.</param>
    /// <param name="r">Target rect.</param>
    public static void Dismantle(CanvasItem ci, Rect2 r)
    {
        float s = Mathf.Min(r.Size.X, r.Size.Y);
        Rect2 b = new Rect2(r.GetCenter() - new Vector2(s * 0.3f, s * 0.3f), new Vector2(s * 0.6f, s * 0.6f));
        Color red = new Color(0.95f, 0.32f, 0.28f);
        ci.DrawRect(b, A(red, 0.15f));
        ci.DrawRect(b, red, false, Mathf.Max(1.5f, s * 0.05f));
        ci.DrawLine(b.Position, b.End, red, Mathf.Max(1.5f, s * 0.07f), true);
        ci.DrawLine(b.Position + new Vector2(b.Size.X, 0), b.Position + new Vector2(0, b.Size.Y), red, Mathf.Max(1.5f, s * 0.07f), true);
    }

    private static Color A(Color c, float a) => new(c.R, c.G, c.B, a);

    private static void Hull(CanvasItem ci, Rect2 r, Color accent)
    {
        float cut = r.Size.X * 0.2f;
        Vector2 p = r.Position;
        Vector2 e = r.End;
        Vector2[] oct =
        [
            new Vector2(p.X + cut, p.Y), new Vector2(e.X - cut, p.Y), new Vector2(e.X, p.Y + cut), new Vector2(e.X, e.Y - cut),
            new Vector2(e.X - cut, e.Y), new Vector2(p.X + cut, e.Y), new Vector2(p.X, e.Y - cut), new Vector2(p.X, p.Y + cut),
        ];
        ci.DrawColoredPolygon(oct, _hullShade);
        ci.DrawColoredPolygon(oct.Select(v => v + ((r.GetCenter() - v) * 0.06f) - new Vector2(0, r.Size.Y * 0.03f)).ToArray(), _hull);
        ci.DrawLine(new Vector2(p.X + cut, e.Y - (r.Size.Y * 0.12f)), new Vector2(e.X - cut, e.Y - (r.Size.Y * 0.12f)), accent, Mathf.Max(1.5f, r.Size.Y * 0.05f));
    }

    private static void IronBox(CanvasItem ci, Rect2 r)
    {
        ci.DrawRect(r, _darkIron);
        ci.DrawRect(r.Grow(-Mathf.Max(1, r.Size.X * 0.06f)), _iron);
        float rivet = Mathf.Max(1, r.Size.X * 0.04f);
        foreach (Vector2 c in new[] { r.Position, r.Position + new Vector2(r.Size.X, 0), r.End, r.Position + new Vector2(0, r.Size.Y) })
        {
            ci.DrawCircle(c + ((r.GetCenter() - c) * 0.15f), rivet, _darkIron);
        }
    }

    private static void Bricks(CanvasItem ci, Rect2 r, Color c)
    {
        ci.DrawRect(r, c.Darkened(0.4f));
        int rows = 4;
        float h = r.Size.Y / rows;
        for (int row = 0; row < rows; row++)
        {
            for (int col = -1; col < 3; col++)
            {
                float w = r.Size.X / 2;
                float x = r.Position.X + (col * w) + (row % 2 * w * 0.5f);
                Rect2 b = new Rect2(new Vector2(x + 1, r.Position.Y + (row * h) + 1), new Vector2(w - 2, h - 2)).Intersection(r);
                if (b.HasArea())
                {
                    ci.DrawRect(b, row == 0 ? c.Lightened(0.08f) : c);
                }
            }
        }
    }

    private static void Gear(CanvasItem ci, Vector2 m, float radius, Color c)
    {
        const int Teeth = 8;
        List<Vector2> pts = [];
        for (int i = 0; i < Teeth * 4; i++)
        {
            float a = i * Mathf.Tau / (Teeth * 4);
            float rr = (i % 4) is 0 or 1 ? radius : radius * 0.78f;
            pts.Add(m + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr));
        }

        ci.DrawColoredPolygon([.. pts], c);
        ci.DrawCircle(m, radius * 0.55f, c.Darkened(0.2f));
        ci.DrawCircle(m, radius * 0.25f, new Color(0.05f, 0.08f, 0.12f));
    }

    private static void Log(CanvasItem ci, Vector2 c, float s, Color col)
    {
        Rect2 body = new Rect2(c - new Vector2(s * 0.3f, s * 0.1f), new Vector2(s * 0.6f, s * 0.2f));
        ci.DrawRect(body, col.Darkened(0.15f));
        ci.DrawLine(body.Position + new Vector2(0, s * 0.06f), new Vector2(body.End.X, body.Position.Y + (s * 0.06f)), col.Lightened(0.15f), 1);
        ci.DrawCircle(new Vector2(body.End.X, c.Y), s * 0.1f, col.Lightened(0.25f));
        ci.DrawArc(new Vector2(body.End.X, c.Y), s * 0.05f, 0, Mathf.Tau, 10, col.Darkened(0.2f), 1);
    }

    private static void Lump(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawCircle(c, r, col.Darkened(0.2f));
        ci.DrawCircle(c - new Vector2(r * 0.2f, r * 0.2f), r * 0.7f, col);
        ci.DrawCircle(c - new Vector2(r * 0.4f, r * 0.4f), r * 0.2f, col.Lightened(0.3f));
    }

    private static void Crystal(CanvasItem ci, Vector2 foot, float s, float lean, Color color)
    {
        Vector2 tip = foot + new Vector2(lean * s, -s * 2.6f);
        Vector2 left = foot + new Vector2(-s * 0.7f, -s * 0.35f);
        Vector2 right = foot + new Vector2(s * 0.7f, -s * 0.35f);
        Vector2 shoulderL = foot + new Vector2((-s * 0.55f) + (lean * s * 0.6f), -s * 1.6f);
        Vector2 shoulderR = foot + new Vector2((s * 0.55f) + (lean * s * 0.6f), -s * 1.6f);
        ci.DrawColoredPolygon([foot, left, shoulderL, tip], color.Lightened(0.15f));
        ci.DrawColoredPolygon([foot, tip, shoulderR, right], color.Darkened(0.35f));
        ci.DrawLine(foot, tip, color.Lightened(0.45f), Mathf.Max(1f, s * 0.12f));
    }
}
