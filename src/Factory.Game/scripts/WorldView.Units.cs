using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>People and rolling stock: engineers in work suits, timber carts and iron locomotives.</summary>
public partial class WorldView
{
    private readonly Dictionary<int, Vector2> _lastUnitPos = [];
    private readonly Dictionary<int, Vector2> _unitFacing = [];

    private static Color JobColor(JobKind k) => k switch
    {
        JobKind.Gather => new Color(0.45f, 0.8f, 0.35f),
        JobKind.Build => new Color(0.95f, 0.75f, 0.2f),
        JobKind.Haul => new Color(0.3f, 0.6f, 0.95f),
        JobKind.OperateTrain => new Color(0.9f, 0.35f, 0.25f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };

    /// <summary>Facing follows actual movement, so a unit keeps looking where it last walked.</summary>
    private Vector2 Facing(Unit u, Vector2 tilePos)
    {
        if (_lastUnitPos.TryGetValue(u.Id, out Vector2 last))
        {
            Vector2 moved = Root.WrapDelta(last, tilePos);
            if (moved.LengthSquared() > 0.0004f)
            {
                _unitFacing[u.Id] = moved.Normalized();
            }
        }

        _lastUnitPos[u.Id] = tilePos;

        return _unitFacing.TryGetValue(u.Id, out Vector2 f) ? f : Vector2.Down;
    }

    private const float FeetOffset = 0.3f;

    /// <summary>
    /// The area a standing figure covers, in tiles relative to the unit's position: about one and a
    /// half tiles tall with the feet just below the position. Used for clicking and the selection brackets.
    /// </summary>
    public static readonly Rect2 FigureBounds = new Rect2(-0.45f, FeetOffset - 1.65f, 0.9f, 1.8f);

    private static readonly Color _ink = new Color(0.06f, 0.055f, 0.05f);

    private Vector2? WorkTile(Unit u, Vector2 tilePos, Vector2 face)
    {
        if (u.IsLeader && (u.MiningTile is { } mt))
        {
            Vector2 mc = new Vector2(mt.X + 0.5f, mt.Y + 0.5f);
            if (Root.WrapDelta(tilePos, mc).Length() <= Planet.LeaderMineReachTiles)
            {
                return mc;
            }
        }

        return u.Phase == UnitPhase.Work ? tilePos + face : null;
    }

    /// <summary>
    /// A colonist in a work suit, seen from high above at an angle: a big helmet on top, broad shoulders,
    /// a short foreshortened body and feet just peeking out below. The figure turns smoothly with its
    /// facing (visor, backpack, shoulders and feet all rotate around it) instead of switching to a side
    /// profile. Drawn in two passes, a dark silhouette slightly larger than the figure and then the
    /// coloured parts, so it reads against any ground.
    /// </summary>
    private void DrawUnit(Unit u)
    {
        Vector2 tilePos = GameRoot.SubToTile(u.Pos);
        Vector2 c = S(tilePos);
        Vector2 face = Facing(u, tilePos);
        Vector2? workTile = WorkTile(u, tilePos, face);
        if (workTile is { } aim)
        {
            Vector2 d = Root.WrapDelta(tilePos, aim);
            if (d.LengthSquared() > 0.01f)
            {
                face = d.Normalized();
            }
        }

        bool walking = (workTile is null) && ((u.Phase is UnitPhase.MoveTo or UnitPhase.Deliver or UnitPhase.Seek) || (u.IsLeader && (u.MoveIntent != Int2.Zero)));
        // The commander's pockets are not drawn as a held load; followers carry what they haul.
        bool carrying = (u.IsLeader == false) && (u.Carry.IsEmpty == false) && (workTile is null);
        bool away = face.Y < -0.35f;
        float k = T * 0.2f;
        float phase = (Time * 10f) + (u.Id * 1.7f);
        float stride = walking ? Mathf.Sin(phase) : 0;
        Vector2 feet = c + new Vector2(0, T * FeetOffset);
        Vector2 body = feet - new Vector2(0, walking ? Mathf.Abs(Mathf.Cos(phase)) * k * 0.25f : 0);

        // The ground is seen at an angle, so directions on it are squashed vertically on screen:
        // "forward" is the facing, "across" the shoulder line.
        Vector2 forward = new Vector2(face.X, face.Y * 0.5f) * k;
        Vector2 across = new Vector2(-face.Y, face.X * 0.5f) * k;
        float sideways = Mathf.Abs(face.X);

        Vector2 At(Vector2 origin, float x, float y) => origin + new Vector2(x * k, y * k);

        Color suit = u.IsLeader ? new Color(0.86f, 0.48f, 0.17f) : new Color(0.45f, 0.51f, 0.47f);
        Color legs = suit.Darkened(0.35f);
        Color boots = new Color(0.17f, 0.15f, 0.13f);
        Color gloves = new Color(0.24f, 0.21f, 0.19f);
        Color pack = u.IsLeader ? new Color(0.3f, 0.32f, 0.35f) : new Color(0.36f, 0.35f, 0.3f);
        Color helmet = u.IsLeader ? new Color(0.94f, 0.91f, 0.84f) : new Color(0.78f, 0.79f, 0.75f);

        // Short legs under the body; walking steps the feet back and forth along the facing.
        Vector2 hipA = At(body, 0, -2.0f) + (across * 0.7f);
        Vector2 hipB = At(body, 0, -2.0f) - (across * 0.7f);
        Vector2 footA = feet + (across * 0.8f) + (forward * stride * 0.9f) - new Vector2(0, Mathf.Max(0, stride) * k * 0.35f);
        Vector2 footB = feet - (across * 0.8f) - (forward * stride * 0.9f) - new Vector2(0, Mathf.Max(0, -stride) * k * 0.35f);

        // Arms hang at the shoulders and swing against the feet; B is the tool arm.
        Vector2 shoulderA = At(body, 0, -5.0f) + (across * 1.95f);
        Vector2 shoulderB = At(body, 0, -5.0f) - (across * 1.95f);
        Vector2 handA = shoulderA + new Vector2(0, k * 2.3f) - (forward * stride * 1.2f);
        Vector2 handB = shoulderB + new Vector2(0, k * 2.3f) + (forward * stride * 1.2f);
        Vector2 boxAt = At(body, 0, -3.4f) + (forward * 1.7f);
        if (carrying)
        {
            handA = boxAt + (across * 1.15f);
            handB = boxAt - (across * 1.15f);
        }

        float swing = 0;
        Vector2 strike = Vector2.Zero;
        if (workTile is { } wt)
        {
            // Raise the pick overhead, then bring it down toward the work tile.
            strike = c + (Root.WrapDelta(tilePos, wt) * T);
            swing = 0.5f + (0.5f * Mathf.Sin(Time * 9f));
            Vector2 down = (strike - shoulderB).Normalized();
            Vector2 up = new Vector2(-face.X * 0.4f, -1f).Normalized();
            handB = shoulderB + (up.Lerp(down, swing).Normalized() * k * 2.6f);
        }

        Vector2 torso = At(body, 0, -3.6f);
        Vector2 torsoSize = new Vector2(3.8f - (sideways * 0.7f), 3.3f) * k;
        Vector2 packAt = At(body, 0, -4.0f - (face.Y * 0.9f)) - new Vector2(face.X * k * 1.4f, 0);
        Vector2 packSize = new Vector2(2.9f - (sideways * 1.1f), 2.9f) * k;
        Vector2 head = At(body, face.X * 0.25f, -6.2f + (face.Y * 0.15f));

        DrawEllipse(feet, k * 2.3f, k * 0.85f, new Color(0, 0, 0, 0.3f));

        void Pass(bool outline)
        {
            float g = outline ? k * 0.38f : 0;
            Color F(Color col) => outline ? _ink : col;
            Vector2 grow = new Vector2(g * 2, g * 2);

            void Arm(Vector2 shoulder, Vector2 hand, bool tool)
            {
                Capsule(shoulder, hand, (k * 0.6f) + g, F(suit.Darkened(0.1f)));
                if (tool)
                {
                    Vector2 t = (hand - shoulder).Normalized();
                    Vector2 n = new Vector2(-t.Y, t.X);
                    Vector2 end = hand + (t * k * 2.2f);
                    Capsule(hand - (t * k * 0.3f), end, (k * 0.22f) + g, F(new Color(0.5f, 0.36f, 0.22f)));
                    Capsule(end - (n * k * 1.0f), end + (n * k * 0.8f), (k * 0.3f) + g, F(new Color(0.62f, 0.64f, 0.66f)));
                }

                DrawCircle(hand, (k * 0.62f) + g, F(gloves));
            }

            void Box()
            {
                Color cargo = Hex(Root.Content.Item(u.Carry.Items.First().Key).Color);
                Vector2 size = new Vector2(2.5f - (sideways * 0.6f), 1.9f) * k;
                RoundBox(boxAt, size + grow, (k * 0.25f) + g, F(cargo.Darkened(0.1f)));
                if (outline == false)
                {
                    DrawRect(new Rect2(boxAt - (size / 2), new Vector2(size.X, k * 0.45f)), cargo.Lightened(0.2f));
                }
            }

            // Behind the body: the backpack when facing the camera, the far arm, the load when facing away.
            if (away == false)
            {
                RoundBox(packAt, packSize + grow, (k * 0.6f) + g, F(pack));
            }

            if (carrying && away)
            {
                Box();
            }

            bool armABehind = shoulderA.Y < shoulderB.Y - (k * 0.3f);
            bool armBBehind = shoulderB.Y < shoulderA.Y - (k * 0.3f);
            if (armABehind || (carrying && away))
            {
                Arm(shoulderA, handA, false);
            }

            if ((armBBehind || (carrying && away)) && (workTile is null))
            {
                Arm(shoulderB, handB, false);
            }

            // Feet and short legs, farther one first.
            bool aFirst = footA.Y <= footB.Y;
            foreach ((Vector2 hip, Vector2 foot) in aFirst ? new[] { (hipA, footA), (hipB, footB) } : new[] { (hipB, footB), (hipA, footA) })
            {
                Capsule(hip, foot, (k * 0.62f) + g, F(legs));
                Capsule(foot - (forward * 0.2f), foot + (forward * 0.45f), (k * 0.55f) + g, F(boots));
            }

            RoundBox(torso, torsoSize + grow, (k * 1.1f) + g, F(suit));
            if (outline == false)
            {
                // Belt, plus a shoulder band in the job colour (a darker band for the commander).
                DrawRect(new Rect2(torso + new Vector2(-torsoSize.X / 2, (torsoSize.Y / 2) - (k * 0.95f)), new Vector2(torsoSize.X, k * 0.45f)), suit.Darkened(0.5f));
                Color band = u.IsLeader ? suit.Darkened(0.28f) : JobColor(u.Job.Kind);
                DrawRect(new Rect2(torso + new Vector2((-torsoSize.X / 2) + (k * 0.35f), (-torsoSize.Y / 2) + (k * 0.3f)), new Vector2(torsoSize.X - (k * 0.7f), k * 0.5f)), band);
                if (away == false)
                {
                    // Pack straps over the chest.
                    Vector2 strapTop = torso + new Vector2(0, (-torsoSize.Y / 2) + (k * 0.3f));
                    DrawLine(strapTop + (across * 0.95f), strapTop + (across * 0.95f) + new Vector2(0, k * 2.3f), pack.Darkened(0.3f), k * 0.35f);
                    DrawLine(strapTop - (across * 0.95f), strapTop - (across * 0.95f) + new Vector2(0, k * 2.3f), pack.Darkened(0.3f), k * 0.35f);
                }
            }

            // In front of the body: the backpack when facing away, the load, the near arms.
            if (away)
            {
                RoundBox(packAt, packSize + grow, (k * 0.6f) + g, F(pack));
                if (outline == false)
                {
                    DrawRect(new Rect2(packAt + new Vector2(-packSize.X / 2, -packSize.Y * 0.3f), new Vector2(packSize.X, k * 0.3f)), pack.Darkened(0.35f));
                    DrawCircle(packAt + new Vector2(0, packSize.Y * 0.15f), k * 0.45f, pack.Lightened(0.25f));
                }
            }

            if (carrying && (away == false))
            {
                Box();
            }

            if ((armABehind == false) && ((carrying && away) == false))
            {
                Arm(shoulderA, handA, false);
            }

            if (workTile is not null)
            {
                Arm(shoulderB, handB, true);
            }
            else if ((armBBehind == false) && ((carrying && away) == false))
            {
                Arm(shoulderB, handB, false);
            }

            // The helmet sits on top and is the biggest single shape, as it is from above.
            DrawCircle(head, (k * 1.75f) + g, F(helmet));
            if (outline)
            {
                return;
            }

            DrawCircle(head + new Vector2(0, k * 0.3f), k * 1.55f, helmet.Darkened(0.1f));
            DrawCircle(head - new Vector2(k * 0.1f, k * 0.25f), k * 1.5f, helmet);
            if (away == false)
            {
                // The visor slides around the helmet with the facing.
                Vector2 visor = head + new Vector2(face.X * k * 0.85f, (face.Y * k * 0.45f) + (k * 0.4f));
                Vector2 size = new Vector2(2.5f - (sideways * 1.2f), 1.2f) * k;
                RoundBox(visor, size, k * 0.5f, new Color(0.1f, 0.19f, 0.27f));
                DrawLine(visor + new Vector2(-size.X * 0.32f, -k * 0.15f), visor + new Vector2(-size.X * 0.05f, -k * 0.15f), new Color(0.55f, 0.8f, 1f, 0.7f), k * 0.22f);
            }
            else
            {
                DrawLine(head - new Vector2(0, k * 1.55f), head + new Vector2(0, k * 0.8f), helmet.Darkened(0.18f), k * 0.4f);
            }

            DrawCircle(head + new Vector2(-k * 0.6f, -k * 0.8f), k * 0.32f, A(Colors.White, 0.55f));
            if (u.IsLeader)
            {
                // The commander's helmet lamp.
                DrawCircle(head + new Vector2(face.X * k * 0.6f, -k * 1.3f), k * 0.32f, Hdr(new Color(1f, 0.8f, 0.4f), 1.4f));
            }
        }

        Pass(true);
        Pass(false);

        if ((workTile is not null) && (swing > 0.93f))
        {
            DrawCircle(strike + new Vector2(Mathf.Sin(Time * 31f) * T * 0.15f, -T * 0.1f), T * 0.045f, Hdr(new Color(1f, 0.85f, 0.5f), 1.2f));
        }

        if (Root.Zoom > 0.6f)
        {
            Color label = u.IsLeader ? new Color(1f, 0.85f, 0.6f) : new Color(0.92f, 0.92f, 0.88f, 0.9f);
            Vector2 at = feet + new Vector2(-T * 2, (k * 1.2f) + 11);
            DrawStringOutline(_font, at, u.Name, HorizontalAlignment.Center, T * 4, 11, 4, new Color(0, 0, 0, 0.7f));
            DrawString(_font, at, u.Name, HorizontalAlignment.Center, T * 4, 11, label);
        }
    }

    private void Capsule(Vector2 a, Vector2 b, float r, Color col)
    {
        DrawLine(a, b, col, r * 2);
        DrawCircle(a, r, col);
        DrawCircle(b, r, col);
    }

    private void RoundBox(Vector2 center, Vector2 size, float r, Color col)
    {
        r = Mathf.Min(r, Mathf.Min(size.X, size.Y) / 2);
        Vector2 h = size / 2;
        DrawRect(new Rect2(center - h + new Vector2(r, 0), new Vector2(size.X - (2 * r), size.Y)), col);
        DrawRect(new Rect2(center - h + new Vector2(0, r), new Vector2(size.X, size.Y - (2 * r))), col);
        DrawCircle(center + new Vector2(-h.X + r, -h.Y + r), r, col);
        DrawCircle(center + new Vector2(h.X - r, -h.Y + r), r, col);
        DrawCircle(center + new Vector2(-h.X + r, h.Y - r), r, col);
        DrawCircle(center + new Vector2(h.X - r, h.Y - r), r, col);
    }

    private void DrawEllipse(Vector2 center, float rx, float ry, Color col)
    {
        Vector2[] points = new Vector2[20];
        for (int i = 0; i < points.Length; i++)
        {
            float a = Mathf.Tau * i / points.Length;
            points[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }

        DrawColoredPolygon(points, col);
    }

    private void DrawTrain(Train t)
    {
        for (int i = t.Vehicles.Count - 1; i >= 0; i--)
        {
            Vehicle v = t.Vehicles[i];
            Vector2 c = S(GameRoot.SubToTile(P.VehicleSub(t, i)));
            Int2 ahead = i > 0 ? t.Tiles[i - 1] : (t.Tiles.Count > 1 ? t.Tiles[0] + (t.Tiles[0] - t.Tiles[1]) : t.Tiles[0] + new Int2(1, 0));
            Int2 dir = P.Torus.Delta(t.Tiles[i], P.Torus.Wrap(ahead));
            Vector2 fwd = (dir.X == 0) && (dir.Y == 0) ? Vector2.Right : new Vector2(dir.X, dir.Y).Normalized();
            bool horiz = Mathf.Abs(fwd.X) > 0.5f;
            Vector2 size = horiz ? new Vector2(T * 0.94f, T * 0.66f) : new Vector2(T * 0.66f, T * 0.94f);
            Rect2 r = new Rect2(c - (size / 2), size);
            DropShadow(r, 0.4f);
            switch (v.Def.Kind)
            {
                case VehicleKind.Cart:
                    DrawMineCart(v, r);
                    break;
                case VehicleKind.HandCart:
                    DrawHandCart(r, fwd, t.Speed > 0);
                    break;
                default:
                    DrawSteamLoco(t, r, fwd);
                    break;
            }
        }

        if ((Root.Zoom > 0.6f) && (t.State is not (TrainState.Moving or TrainState.AtStop)))
        {
            Vector2 head = S(GameRoot.SubToTile(P.VehicleSub(t, 0)));
            DrawString(_font, head + new Vector2(-T, -T * 0.65f), StateText(t.State), HorizontalAlignment.Center, T * 2, 11, new Color(1f, 0.75f, 0.4f));
        }
    }

    private void DrawMineCart(Vehicle v, Rect2 r)
    {
        DrawRect(r, _darkIron);
        DrawRect(r.Grow(-T * 0.05f), _iron.Lerp(_rust, 0.4f));
        Rect2 bed = r.Grow(-T * 0.13f);
        DrawRect(bed, _darkIron.Lightened(0.05f));
        float fill = v.Cargo.Slots > 0 ? (float)v.Cargo.UsedSlots / v.Cargo.Slots : 0;
        string? top = v.Cargo.Items.FirstOrDefault().Key;
        if ((top is not null) && (fill > 0))
        {
            Color cargo = Hex(Root.Content.Item(top).Color);
            Vector2 c = bed.GetCenter();
            float heap = Mathf.Min(bed.Size.X, bed.Size.Y) * (0.25f + (0.25f * fill));
            DrawCircle(c, heap, cargo.Darkened(0.25f));
            DrawCircle(c - new Vector2(heap * 0.25f, heap * 0.25f), heap * 0.55f, cargo);
        }
    }

    private void DrawHandCart(Rect2 r, Vector2 fwd, bool moving)
    {
        DrawRect(r, _timber.Darkened(0.45f));
        Rect2 deck = r.Grow(-T * 0.05f);
        DrawRect(deck, _timber);
        for (int k = 1; k < 4; k++)
        {
            float f = k / 4f;
            Vector2 a = deck.Position + (Mathf.Abs(fwd.X) > 0.5f ? new Vector2(deck.Size.X * f, 0) : new Vector2(0, deck.Size.Y * f));
            Vector2 b = a + (Mathf.Abs(fwd.X) > 0.5f ? new Vector2(0, deck.Size.Y) : new Vector2(deck.Size.X, 0));
            DrawLine(a, b, _timber.Darkened(0.35f), 1);
        }

        Vector2 c = r.GetCenter();
        float pump = moving ? Mathf.Sin(Time * 8f) * 0.6f : 0.3f;
        Vector2 side = new Vector2(-fwd.Y, fwd.X);
        DrawLine(c - (side * T * 0.25f) + (fwd * T * pump * 0.2f), c + (side * T * 0.25f) - (fwd * T * pump * 0.2f), _darkIron, Mathf.Max(1.5f, T * 0.07f));
        DrawCircle(c, T * 0.06f, _iron.Lightened(0.2f));
    }

    private void DrawSteamLoco(Train t, Rect2 r, Vector2 fwd)
    {
        DrawRect(r, _darkIron);
        DrawRect(r.Grow(-T * 0.05f), new Color(0.22f, 0.22f, 0.23f));
        Vector2 c = r.GetCenter();
        Vector2 side = new Vector2(-fwd.Y, fwd.X);
        Vector2 half = new Vector2(Mathf.Abs(fwd.X), Mathf.Abs(fwd.Y)) * (Mathf.Max(r.Size.X, r.Size.Y) * 0.5f);
        DrawLine(c - (fwd * half.Length() * 0.8f) + (side * T * 0.26f), c + (fwd * half.Length() * 0.8f) + (side * T * 0.26f), new Color(0.6f, 0.15f, 0.12f), Mathf.Max(1, T * 0.05f));
        DrawLine(c - (fwd * half.Length() * 0.8f) - (side * T * 0.26f), c + (fwd * half.Length() * 0.8f) - (side * T * 0.26f), new Color(0.6f, 0.15f, 0.12f), Mathf.Max(1, T * 0.05f));
        DrawCircle(c + (fwd * T * 0.12f), T * 0.2f, _iron.Darkened(0.15f));
        Vector2 stack = c + (fwd * T * 0.3f);
        DrawCircle(stack, T * 0.09f, _darkIron.Darkened(0.4f));
        DrawRect(new Rect2(c - (fwd * T * 0.3f) - new Vector2(T * 0.18f, T * 0.18f), new Vector2(T * 0.36f, T * 0.36f)), new Color(0.3f, 0.12f, 0.1f));
        DrawCircle(c + (fwd * T * 0.44f), T * 0.07f, Hdr(new Color(1f, 0.9f, 0.6f), t.Speed > 0 ? 1.3f : 0.9f));
        if (t.Speed > 0)
        {
            Puffs(stack, new Color(0.8f, 0.8f, 0.82f), 1.4f, t.Id);
        }
    }
}
