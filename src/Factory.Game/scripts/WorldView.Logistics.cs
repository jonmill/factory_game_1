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
    private static readonly Color _beltBed = new(0.17f, 0.19f, 0.22f);

    private void DrawBelt(Building b, Rect2 r, Color accent)
    {
        bool across = b.Rot is SimDir.North or SimDir.South;
        Rect2 bed = across ? new Rect2(r.Position + new Vector2(T * 0.1f, 0), new Vector2(T * 0.8f, T)) : new Rect2(r.Position + new Vector2(0, T * 0.1f), new Vector2(T, T * 0.8f));
        DrawRect(bed, _beltBed);
        Vector2 dir = new Vector2(b.Rot.Vec().X, b.Rot.Vec().Y);
        Vector2 side = new Vector2(-dir.Y, dir.X);
        Vector2 c = r.GetCenter();

        // Side rails in the belt's colour.
        DrawLine(c + (side * T * 0.42f) - (dir * T * 0.5f), c + (side * T * 0.42f) + (dir * T * 0.5f), accent.Darkened(0.2f), Mathf.Max(1, T * 0.06f));
        DrawLine(c - (side * T * 0.42f) - (dir * T * 0.5f), c - (side * T * 0.42f) + (dir * T * 0.5f), accent.Darkened(0.2f), Mathf.Max(1, T * 0.06f));

        // Chevrons slide along with the belt.
        float phase = Mathf.PosMod(Time * b.Def.Speed * 60f / Fixed.One, 0.5f);
        for (int i = 0; i < 2; i++)
        {
            float along = -0.5f + phase + (i * 0.5f);
            Vector2 tip = c + (dir * T * (along + 0.1f));
            DrawPolyline([tip - (dir * T * 0.14f) + (side * T * 0.22f), tip, tip - (dir * T * 0.14f) - (side * T * 0.22f)], A(Colors.White, 0.12f), Mathf.Max(1, T * 0.05f));
        }

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
