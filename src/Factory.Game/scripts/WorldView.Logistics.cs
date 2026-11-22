using Factory.Game.Ui;
using Factory.Sim;
using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game;

/// <summary>
/// Clean Age logistics art: belts with moving chevrons and the items riding them, and logistics
/// stations with a landing ring whose drone lights show how many are home.
/// </summary>
public partial class WorldView
{
    private static readonly Color _beltBed = new(0.15f, 0.135f, 0.12f);
    private static readonly float[] _railSides = [0.42f, -0.42f];

    private void DrawBelt(Building b, Rect2 r, Color accent)
    {
        bool across = b.Rot is SimDir.North or SimDir.South;
        Rect2 bed = across ? new Rect2(r.Position + new Vector2(T * 0.1f, 0), new Vector2(T * 0.8f, T)) : new Rect2(r.Position + new Vector2(0, T * 0.1f), new Vector2(T, T * 0.8f));
        // (Its shadow is drawn in the shared shadow layer: see DrawFlatShadow.)
        DrawRect(bed, _beltBed);
        Vector2 dir = new Vector2(b.Rot.Vec().X, b.Rot.Vec().Y);
        Vector2 side = new Vector2(-dir.Y, dir.X);
        Vector2 c = r.GetCenter();
        float phase = Mathf.PosMod(Time * b.Def.Speed * 60f / Fixed.One, 0.25f);

        // Rubber slats across the belt, sliding with it; every other one catches a little light.
        if (T >= 14)
        {
            for (int i = -2; i <= 2; i++)
            {
                float along = (i * 0.25f) + phase;
                if (Mathf.Abs(along) > 0.5f)
                {
                    continue;
                }

                Vector2 at = c + (dir * T * along);
                DrawLine(at + (side * T * 0.36f), at - (side * T * 0.36f), new Color(0, 0, 0, 0.28f), Mathf.Max(1, T * 0.04f));
                DrawLine(at + (side * T * 0.36f) + (dir * T * 0.04f), at - (side * T * 0.36f) + (dir * T * 0.04f), A(Colors.White, 0.05f), 1);
            }
        }

        // Steel side rails with a stripe of the belt's colour and a lit edge.
        Vector2 lit = -Art.SpriteBaker.ShadowLean.Normalized();
        foreach (float k in _railSides)
        {
            Vector2 a0 = c + (side * T * k) - (dir * T * 0.5f), a1 = c + (side * T * k) + (dir * T * 0.5f);
            DrawLine(a0, a1, _darkIron, Mathf.Max(1.5f, T * 0.09f));
            DrawLine(a0, a1, accent.Lerp(new Color(0.35f, 0.31f, 0.26f), 0.45f).Darkened(0.2f), Mathf.Max(1, T * 0.045f));

            // The rail's top edge catches the sun on the side facing it.
            float facing = Mathf.Clamp(side.Dot(lit) * Mathf.Sign(k), 0, 1);
            DrawLine(a0 + (side * T * 0.03f * Mathf.Sign(k)), a1 + (side * T * 0.03f * Mathf.Sign(k)), new Color(1f, 0.92f, 0.78f, 0.1f + (0.25f * facing)), 1);
        }

        // One faint chevron so the direction still reads at a glance.
        Vector2 tip = c + (dir * T * ((phase * 2) - 0.15f));
        DrawPolyline([tip - (dir * T * 0.12f) + (side * T * 0.18f), tip, tip - (dir * T * 0.12f) - (side * T * 0.18f)], A(Colors.White, 0.1f), Mathf.Max(1, T * 0.04f));

        // The items, from the back edge (0) to the front edge.
        foreach (BeltItem it in b.Lane)
        {
            Vector2 at = c + (dir * T * ((it.Pos / (float)Fixed.One) - 0.5f));
            float s = T * 0.42f;
            Icons.Item(this, new Rect2(at - new Vector2(s / 2, s / 2), new Vector2(s, s)), P.World.Content.Item(it.Item));
        }
    }

    private void DrawStation(Building b, Rect2 r, Color accent, bool working)
    {
        Hull(r.Grow(-T * 0.06f), accent, working);
        Vector2 c = r.GetCenter();
        float rad = Mathf.Min(r.Size.X, r.Size.Y) * 0.36f;
        GlassDisc(c, rad);
        DrawArc(c, rad * 0.75f, 0, Mathf.Tau, 40, Trim(accent, working, 0.8f), Mathf.Max(1, T * 0.05f));

        // One light per drone around the pad: lit while it's home.
        int drones = b.Def.Drones;
        int home = P.FreeCarriers(b, false);
        for (int i = 0; i < drones; i++)
        {
            float a = i * Mathf.Tau / drones;
            Vector2 at = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad * 0.9f);
            DrawCircle(at, T * 0.08f, i < home ? Trim(accent, true, 1.2f) : _hullShade.Darkened(0.3f));
        }

        if (b.Def.Kind == BuildingKind.OrbitalStation)
        {
            // A tall mast for the vessels, with a beacon.
            DrawCircle(c, rad * 0.35f, _hullLight);
            DrawCircle(c, rad * 0.2f, _glass);
            float pulse = working ? 0.6f + (0.4f * Mathf.Sin(Time * 3f)) : 0.3f;
            DrawCircle(c, rad * 0.1f, Hdr(accent, 1f + pulse));
        }
        else
        {
            DrawCircle(c, rad * 0.22f, Trim(accent, working, 1.1f));
        }
    }

    /// <summary>Drones and vessels in flight from this planet's stations, drawn as small glowing craft.</summary>
    private void DrawShipments()
    {
        foreach (Shipment s in P.Shipments)
        {
            if ((P.World.Get<Building>(s.FromId) is not { } from) || (P.World.Get<Building>(s.ToId) is not { } to))
            {
                continue;
            }

            long total = s.BackTick - s.ArriveTick;
            if (total <= 0)
            {
                continue;
            }

            // Out and back: 0 → 1 → 0 along the line between the two stations.
            long now = P.World.Tick;
            float t = s.Count > 0 ? 1f - ((s.ArriveTick - now) / (float)total) : (s.BackTick - now) / (float)total;
            t = Mathf.Clamp(t, 0, 1);
            Vector2 a = GameRoot.SubToTile(from.CenterSub);
            Vector2 at;
            if (s.Vessel || (to.Planet != P))
            {
                // A vessel climbs out of view.
                at = a + new Vector2(0, -t * 6f);
            }
            else
            {
                Vector2 bpos = GameRoot.SubToTile(to.CenterSub);
                at = a + (Root.WrapDelta(a, bpos) * t);
            }

            Vector2 p = S(at);
            Color glow = Hex(from.Def.Color);
            DrawCircle(p, T * 0.16f, A(glow, 0.25f));
            DrawCircle(p, T * 0.08f, Hdr(glow, 1.4f));
        }
    }
}
