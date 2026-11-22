using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>Robots and rolling stock: the commander, its helper bots, timber carts and iron locomotives.</summary>
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
    /// The area the commander covers, in tiles relative to its position: about one and a half tiles
    /// tall with the feet just below the position. Used for clicking and the selection brackets.
    /// </summary>
    public static readonly Rect2 FigureBounds = new Rect2(-0.45f, FeetOffset - 1.75f, 0.9f, 1.9f);

    /// <summary>The area a helper bot covers: a squat tracked robot, smaller than the commander.</summary>
    public static readonly Rect2 HelperBounds = new Rect2(-0.42f, -0.6f, 0.84f, 0.95f);

    /// <summary>The clickable area of a robot, in tiles relative to its position.</summary>
    /// <param name="u">The robot.</param>
    /// <returns>Its bounds.</returns>
    public static Rect2 BoundsFor(Unit u) => u.IsLeader ? FigureBounds : HelperBounds;

    private static readonly Color _ink = new Color(0.06f, 0.055f, 0.05f);
    private static readonly Color _gunmetal = new Color(0.25f, 0.27f, 0.3f);
    private static readonly Color _beam = new Color(1f, 0.45f, 0.25f);

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

    private void DrawUnit(Unit u)
    {
        Vector2 tilePos = GameRoot.SubToTile(u.Pos);
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
        Vector2? strike = workTile is { } wt ? S(tilePos) + (Root.WrapDelta(tilePos, wt) * T) : null;
        Vector2 labelAt;
        if (P.IsHovering(u))
        {
            DrawHoverGlow(u, S(tilePos));
        }

        if (u.IsLeader)
        {
            labelAt = DrawCommander(u, S(tilePos), face, walking, strike);
        }
        else
        {
            labelAt = DrawHelper(u, S(tilePos), face, walking, strike);
        }

        if (Root.Zoom > 0.6f)
        {
            Color label = u.IsLeader ? new Color(1f, 0.85f, 0.6f) : new Color(0.85f, 0.95f, 0.95f, 0.9f);
            Vector2 at = labelAt + new Vector2(-T * 2, 11);
            DrawStringOutline(_font, at, u.Name, HorizontalAlignment.Center, T * 4, 11, 4, new Color(0, 0, 0, 0.7f));
            DrawString(_font, at, u.Name, HorizontalAlignment.Center, T * 4, 11, label);
        }
    }

    /// <summary>
    /// The commander: a walking robot in orange livery, seen from high above at an angle. A squared-off
    /// sensor head with a glowing visor and an antenna sits on broad shoulders, a reactor pack rides on
    /// its back and it walks on two short jointed legs. It mines with a cutting beam from its right arm.
    /// The figure turns smoothly with its facing instead of switching to a side profile, and is drawn in
    /// two passes (a dark silhouette, then the coloured parts) so it reads against any ground.
    /// </summary>
    /// <returns>Where to put its name label.</returns>
    private Vector2 DrawCommander(Unit u, Vector2 c, Vector2 face, bool walking, Vector2? strike)
    {
        bool away = face.Y < -0.35f;
        float k = T * 0.2f;
        float phase = (Time * 10f) + (u.Id * 1.7f);
        float stride = walking ? Mathf.Sin(phase) : 0;
        Vector2 feet = c + new Vector2(0, T * FeetOffset);
        Vector2 body = feet - new Vector2(0, walking ? Mathf.Abs(Mathf.Cos(phase)) * k * 0.2f : 0);

        // The ground is seen at an angle, so directions on it are squashed vertically on screen:
        // "forward" is the facing, "across" the shoulder line.
        Vector2 forward = new Vector2(face.X, face.Y * 0.5f) * k;
        Vector2 across = new Vector2(-face.Y, face.X * 0.5f) * k;
        float sideways = Mathf.Abs(face.X);

        Vector2 At(Vector2 origin, float x, float y) => origin + new Vector2(x * k, y * k);

        Color shell = new Color(0.9f, 0.5f, 0.15f);
        Color plate = new Color(0.9f, 0.89f, 0.84f);
        Color joint = _gunmetal;
        Color glow = new Color(0.45f, 0.9f, 1f);

        // Short legs under the body; walking steps the feet back and forth along the facing.
        Vector2 hipA = At(body, 0, -2.0f) + (across * 0.75f);
        Vector2 hipB = At(body, 0, -2.0f) - (across * 0.75f);
        Vector2 footA = feet + (across * 0.85f) + (forward * stride * 0.9f) - new Vector2(0, Mathf.Max(0, stride) * k * 0.35f);
        Vector2 footB = feet - (across * 0.85f) - (forward * stride * 0.9f) - new Vector2(0, Mathf.Max(0, -stride) * k * 0.35f);

        // Arms hang at the shoulders and swing against the feet; B is the beam arm.
        Vector2 shoulderA = At(body, 0, -5.0f) + (across * 2.0f);
        Vector2 shoulderB = At(body, 0, -5.0f) - (across * 2.0f);
        Vector2 handA = shoulderA + new Vector2(0, k * 2.3f) - (forward * stride * 1.1f);
        Vector2 handB = shoulderB + new Vector2(0, k * 2.3f) + (forward * stride * 1.1f);
        if (strike is { } st)
        {
            // Raise the beam arm and point it at the work.
            handB = shoulderB + ((st - shoulderB).Normalized() * k * 2.5f);
        }

        Vector2 torso = At(body, 0, -3.6f);
        Vector2 torsoSize = new Vector2(3.9f - (sideways * 0.7f), 3.2f) * k;
        Vector2 packAt = At(body, 0, -4.1f - (face.Y * 0.9f)) - new Vector2(face.X * k * 1.4f, 0);
        Vector2 packSize = new Vector2(2.8f - (sideways * 1.0f), 2.7f) * k;
        Vector2 head = At(body, face.X * 0.25f, -6.4f + (face.Y * 0.15f));
        Vector2 headSize = new Vector2(3.2f - (sideways * 0.6f), 2.6f) * k;

        DrawEllipse(feet, k * 1.8f, k * 0.6f, new Color(0, 0, 0, 0.18f));

        void Pass(bool outline)
        {
            float g = outline ? k * 0.38f : 0;
            Color F(Color col) => outline ? _ink : col;
            Vector2 grow = new Vector2(g * 2, g * 2);

            void Arm(Vector2 shoulder, Vector2 hand, bool beam)
            {
                Capsule(shoulder, hand, (k * 0.45f) + g, F(joint));
                Vector2 t = (hand - shoulder).Normalized();
                Vector2 n = new Vector2(-t.Y, t.X);
                if (beam)
                {
                    // The cutter: a stubby emitter barrel on the forearm.
                    Capsule(hand - (t * k * 0.4f), hand + (t * k * 1.1f), (k * 0.42f) + g, F(plate.Darkened(0.2f)));
                }
                else
                {
                    // A two-pronged gripper.
                    Capsule(hand, hand + (t * k * 0.7f) + (n * k * 0.35f), (k * 0.2f) + g, F(joint.Lightened(0.15f)));
                    Capsule(hand, hand + (t * k * 0.7f) - (n * k * 0.35f), (k * 0.2f) + g, F(joint.Lightened(0.15f)));
                }

                DrawCircle(shoulder, (k * 0.85f) + g, F(shell.Darkened(0.12f)));
            }

            // Behind the body: the reactor pack when facing the camera, and whichever arm is farther.
            if (away == false)
            {
                RoundBox(packAt, packSize + grow, (k * 0.5f) + g, F(joint));
            }

            bool armABehind = shoulderA.Y < shoulderB.Y - (k * 0.3f);
            bool armBBehind = shoulderB.Y < shoulderA.Y - (k * 0.3f);
            if (armABehind)
            {
                Arm(shoulderA, handA, false);
            }

            if (armBBehind && (strike is null))
            {
                Arm(shoulderB, handB, false);
            }

            // Jointed legs and flat foot pads, farther one first.
            bool aFirst = footA.Y <= footB.Y;
            foreach ((Vector2 hip, Vector2 foot) in aFirst ? new[] { (hipA, footA), (hipB, footB) } : new[] { (hipB, footB), (hipA, footA) })
            {
                Capsule(hip, foot, (k * 0.5f) + g, F(joint));
                DrawCircle(hip.Lerp(foot, 0.5f), (k * 0.55f) + g, F(joint.Lightened(0.12f)));
                Capsule(foot - (forward * 0.3f), foot + (forward * 0.5f), (k * 0.6f) + g, F(shell.Darkened(0.35f)));
            }

            RoundBox(torso, torsoSize + grow, (k * 0.8f) + g, F(shell));
            if (outline == false)
            {
                // A pale chest plate with the core light, or a seam down the back.
                DrawRect(new Rect2(torso + new Vector2(-torsoSize.X / 2, (torsoSize.Y / 2) - (k * 0.8f)), new Vector2(torsoSize.X, k * 0.4f)), shell.Darkened(0.45f));
                if (away == false)
                {
                    Vector2 chest = torso + new Vector2(face.X * k * 0.5f, -k * 0.25f);
                    RoundBox(chest, new Vector2(torsoSize.X * 0.55f, k * 1.6f), k * 0.4f, plate);
                    float pulse = 0.8f + (0.25f * Mathf.Sin(Time * 3f));
                    DrawCircle(chest, k * 0.42f, Hdr(glow, 1.3f * pulse));
                }
            }

            // In front of the body: the reactor pack when facing away, with its glowing vents.
            if (away)
            {
                RoundBox(packAt, packSize + grow, (k * 0.5f) + g, F(joint));
                if (outline == false)
                {
                    DrawRect(new Rect2(packAt + new Vector2(-packSize.X / 2, -packSize.Y * 0.35f), new Vector2(packSize.X, k * 0.3f)), joint.Darkened(0.35f));
                    DrawCircle(packAt + new Vector2(-packSize.X * 0.22f, packSize.Y * 0.18f), k * 0.4f, Hdr(new Color(1f, 0.6f, 0.25f), 1.2f));
                    DrawCircle(packAt + new Vector2(packSize.X * 0.22f, packSize.Y * 0.18f), k * 0.4f, Hdr(new Color(1f, 0.6f, 0.25f), 1.2f));
                }
            }

            if (armABehind == false)
            {
                Arm(shoulderA, handA, false);
            }

            if (strike is not null)
            {
                Arm(shoulderB, handB, true);
            }
            else if (armBBehind == false)
            {
                Arm(shoulderB, handB, false);
            }

            // The sensor head: a squared-off block with an antenna, biggest from above.
            Vector2 tip = head + new Vector2(-face.Y * k * 0.9f, -k * 2.9f);
            DrawLine(head + new Vector2(-face.Y * k * 0.9f, -k * 0.8f), tip, F(joint), (k * 0.22f) + (g * 2));
            // Sensor pods on either side of the head.
            DrawCircle(head + (across * 1.7f) + new Vector2(0, k * 0.2f), (k * 0.6f) + g, F(joint));
            DrawCircle(head - (across * 1.7f) + new Vector2(0, k * 0.2f), (k * 0.6f) + g, F(joint));
            RoundBox(head, headSize + grow, (k * 0.55f) + g, F(plate));
            if (outline)
            {
                DrawCircle(tip, (k * 0.32f) + g, _ink);

                return;
            }

            bool blink = ((int)((Time + (u.Id * 0.37f)) * 1.3f) % 3) == 0;
            DrawCircle(tip, k * 0.32f, blink ? Hdr(new Color(1f, 0.35f, 0.25f), 1.6f) : new Color(0.55f, 0.2f, 0.16f));
            RoundBox(head - new Vector2(0, k * 0.65f), new Vector2(headSize.X, headSize.Y * 0.4f), k * 0.5f, shell);
            if (away == false)
            {
                // The visor slides around the head with the facing.
                Vector2 visor = head + new Vector2(face.X * k * 0.75f, (face.Y * k * 0.35f) + (k * 0.45f));
                Vector2 size = new Vector2(2.4f - (sideways * 1.1f), 0.85f) * k;
                RoundBox(visor, size + new Vector2(k * 0.3f, k * 0.3f), k * 0.5f, new Color(0.08f, 0.1f, 0.13f));
                RoundBox(visor, size, k * 0.4f, Hdr(glow, 1.15f));
            }
            else
            {
                DrawLine(head - new Vector2(0, k * 1.2f), head + new Vector2(0, k * 1.1f), plate.Darkened(0.2f), k * 0.3f);
            }
        }

        Pass(true);
        Pass(false);

        if (strike is { } at)
        {
            DrawBeam(handB + ((handB - shoulderB).Normalized() * k * 1.1f), at, k);
        }

        if (Root.World.Get<ConstructionSite>(u.BuildingSiteId) is { } site)
        {
            // The commander builds by itself with a construction beam from its antenna.
            Vector2 to = c + (Root.WrapDelta(GameRoot.SubToTile(u.Pos), GameRoot.SubToTile(site.CenterSub)) * T);
            Vector2 from = head + new Vector2(-face.Y * k * 0.9f, -k * 2.9f);
            float pulse = 0.6f + (0.4f * Mathf.Sin(Time * 12f));
            DrawLine(from, to, Hdr(new Color(0.45f, 0.9f, 1f, 0.35f * pulse), 1.1f), k * 0.5f);
            DrawLine(from, to, Hdr(new Color(0.75f, 0.97f, 1f, 0.8f), 1.3f), Mathf.Max(1, k * 0.15f));
            DrawCircle(to, k * 0.5f * pulse, Hdr(new Color(0.6f, 0.95f, 1f), 1.3f));
        }

        return feet + new Vector2(0, k * 1.2f);
    }

    /// <summary>
    /// A helper bot: a squat tracked robot with a flat cargo deck and a domed sensor head whose eye shows
    /// its job colour. Smaller and plainer than the commander. Haulers carry a crate on the deck, and at
    /// work a short arm reaches out to the job with a welding spark.
    /// </summary>
    /// <returns>Where to put its name label.</returns>
    private Vector2 DrawHelper(Unit u, Vector2 c, Vector2 face, bool moving, Vector2? strike)
    {
        float k = T * 0.2f;
        bool away = face.Y < -0.2f;
        bool carrying = (u.Carry.IsEmpty == false) && (strike is null);
        float hum = moving ? Mathf.Sin((Time * 30f) + u.Id) * k * 0.06f : 0;
        Vector2 ground = c + new Vector2(0, T * 0.18f);
        Vector2 forward = new Vector2(face.X, face.Y * 0.5f) * k;
        Vector2 across = new Vector2(-face.Y, face.X * 0.5f) * k;
        // Later models get a warmer livery.
        Color shell = u.Robot.Id == "helper-mk1" ? new Color(0.52f, 0.6f, 0.58f) : new Color(0.72f, 0.62f, 0.42f);
        Color job = JobColor(u.Job.Kind);
        Vector2 lower = ground - new Vector2(0, (k * 0.9f) + hum);
        Vector2 deck = ground - new Vector2(0, (k * 1.6f) + hum);
        Vector2 head = deck + (forward * 0.75f) - new Vector2(0, k * 0.75f);
        Vector2 cargoAt = deck - (forward * 0.55f) - new Vector2(0, k * 0.75f);
        Vector2 shoulder = deck + (forward * 0.4f) + (across * 1.0f) - new Vector2(0, k * 0.2f);
        Vector2 hand = strike is { } st ? shoulder + ((st - shoulder).Normalized() * k * 2.0f) : shoulder + (forward * 0.6f);
        float tread = moving ? (Time * 2.5f) + (u.Id * 0.3f) : 0;

        DrawEllipse(ground, k * 1.8f, k * 0.8f, new Color(0, 0, 0, 0.18f));

        void Pass(bool outline)
        {
            float g = outline ? k * 0.34f : 0;
            Color F(Color col) => outline ? _ink : col;

            // Tracks, farther one first.
            Vector2[] sides = [across * 1.3f, across * -1.3f];
            if (sides[0].Y > sides[1].Y)
            {
                (sides[0], sides[1]) = (sides[1], sides[0]);
            }

            foreach (Vector2 side in sides)
            {
                Vector2 mid = ground + side - new Vector2(0, k * 0.45f);
                Capsule(mid - (forward * 1.5f), mid + (forward * 1.5f), (k * 0.6f) + g, F(new Color(0.15f, 0.15f, 0.16f)));
                if (outline == false)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float f = (((i / 4f) + tread) % 1f * 3f) - 1.5f;
                        DrawCircle(mid + (forward * f), k * 0.17f, new Color(0.32f, 0.32f, 0.33f));
                    }
                }
            }

            Slab(lower, forward * 1.35f, across * 1.0f, g, F(_gunmetal));
            Slab(deck, forward * 1.3f, across * 1.05f, g, F(shell));
            if (outline == false)
            {
                // A job-coloured stripe across the back of the deck.
                Vector2 back = deck - (forward * 1.0f);
                DrawLine(back + (across * 0.95f), back - (across * 0.95f), job.Darkened(0.1f), k * 0.4f);
            }

            void Cargo()
            {
                if (carrying == false)
                {
                    return;
                }

                Color col = Hex(Root.Content.Item(u.Carry.Items.First().Key).Color);
                Vector2 size = new Vector2(1.9f, 1.4f) * k;
                RoundBox(cargoAt, size + new Vector2(g * 2, g * 2), (k * 0.2f) + g, F(col.Darkened(0.1f)));
                if (outline == false)
                {
                    DrawRect(new Rect2(cargoAt - (size / 2), new Vector2(size.X, k * 0.35f)), col.Lightened(0.2f));
                }
            }

            void Head()
            {
                DrawCircle(head, (k * 0.95f) + g, F(shell.Lightened(0.25f)));
                if (outline)
                {
                    return;
                }

                DrawCircle(head + new Vector2(0, k * 0.2f), k * 0.8f, shell.Lightened(0.12f));
                if (away == false)
                {
                    Vector2 eye = head + new Vector2(face.X * k * 0.45f, (face.Y * k * 0.2f) + (k * 0.15f));
                    DrawCircle(eye, k * 0.48f, new Color(0.07f, 0.08f, 0.1f));
                    DrawCircle(eye, k * 0.3f, Hdr(job.Lightened(0.2f), 1.35f));
                }

                DrawCircle(head + new Vector2(-k * 0.35f, -k * 0.45f), k * 0.2f, A(Colors.White, 0.5f));
            }

            // Facing away, the head is farther than the deck's load; facing the camera, nearer.
            if (away)
            {
                Head();
                Cargo();
            }
            else
            {
                Cargo();
                Head();
            }

            Capsule(shoulder, hand, (k * 0.28f) + g, F(_gunmetal.Lightened(0.15f)));
            DrawCircle(shoulder, (k * 0.4f) + g, F(shell.Darkened(0.15f)));
        }

        Pass(true);
        Pass(false);

        if (strike is not null)
        {
            Vector2 tipAt = hand + ((hand - shoulder).Normalized() * k * 0.3f);
            if (Mathf.Sin((Time * 23f) + u.Id) > 0.2f)
            {
                DrawCircle(tipAt + new Vector2(Mathf.Sin(Time * 31f) * k * 0.4f, 0), k * 0.25f, Hdr(new Color(1f, 0.9f, 0.6f), 1.4f));
            }
        }

        return ground + new Vector2(0, k * 1.1f);
    }

    /// <summary>A flat plate lying on the ground plane, turned with the facing, with a little thickness.</summary>
    private void Slab(Vector2 center, Vector2 halfForward, Vector2 halfAcross, float grow, Color col)
    {
        Vector2 f = halfForward + (halfForward.Normalized() * grow);
        Vector2 a = halfAcross + (halfAcross.Normalized() * grow);
        Vector2 depth = new Vector2(0, (T * 0.05f) + grow);
        Vector2[] top = [center + f + a, center + f - a, center - f - a, center - f + a];
        Vector2[] side = top.Select(p => p + depth).ToArray();
        DrawColoredPolygon(Hull([.. top, .. side]), col.Darkened(0.3f));
        DrawColoredPolygon(top, col);
    }

    /// <summary>The convex hull of a few points, wound in order, for drawing a solid outline.</summary>
    private static Vector2[] Hull(Vector2[] pts)
    {
        Vector2[] sorted = pts.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        static float Cross(Vector2 o, Vector2 a, Vector2 b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));
        List<Vector2> hull = [];
        foreach (bool lowerPass in new[] { true, false })
        {
            int startCount = hull.Count;
            IEnumerable<Vector2> order = lowerPass ? sorted : sorted.Reverse();
            foreach (Vector2 p in order)
            {
                while ((hull.Count >= startCount + 2) && (Cross(hull[^2], hull[^1], p) <= 0))
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(p);
            }

            hull.RemoveAt(hull.Count - 1);
        }

        return [.. hull];
    }

    /// <summary>The commander's cutting beam, flickering, with sparks where it meets the work.</summary>
    private void DrawBeam(Vector2 from, Vector2 to, float k)
    {
        float flicker = 0.75f + (0.25f * Mathf.Sin(Time * 40f));
        DrawLine(from, to, Hdr(_beam, 0.9f * flicker) with { A = 0.45f }, k * 0.7f);
        DrawLine(from, to, Hdr(new Color(1f, 0.85f, 0.6f), 1.5f * flicker), k * 0.25f);
        DrawCircle(from, k * 0.35f, Hdr(new Color(1f, 0.85f, 0.6f), 1.5f));
        for (int i = 0; i < 3; i++)
        {
            float a = (Time * 13f) + (i * 2.1f);
            float r = ((Time * 3f) + i) % 1f;
            Vector2 spark = to + (new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * T * 0.12f * r);
            DrawCircle(spark, T * 0.03f, Hdr(new Color(1f, 0.8f, 0.45f), 1.3f));
        }

        DrawCircle(to, T * 0.06f, Hdr(new Color(1f, 0.9f, 0.7f), 1.4f * flicker));
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

    /// <summary>Units' and trains' shadows, as white shapes in the shared shadow layer, cast with the sun.</summary>
    private void DrawMovingShadows()
    {
        Vector2 lean = Art.SpriteBaker.ShadowLean * T;
        foreach (Unit u in P.Units)
        {
            Vector2 feet = S(GameRoot.SubToTile(u.Pos)) + new Vector2(0, T * 0.18f);
            float height = u.Robot is null ? 0.55f : 0.35f;
            for (int i = 0; i <= 4; i++)
            {
                float f = i / 4f;
                DrawEllipse(feet + (lean * height * f), T * (0.17f - (f * 0.05f)), T * 0.09f, Colors.White);
            }
        }

        foreach (Train t in P.Trains)
        {
            for (int i = 0; i < t.Vehicles.Count; i++)
            {
                Vector2 c = S(GameRoot.SubToTile(P.VehicleSub(t, i)));
                Vector2 half = new Vector2(T * 0.42f, T * 0.42f);
                for (int s = 0; s <= 3; s++)
                {
                    DrawRect(new Rect2(c - half + (lean * 0.45f * s / 3f), half * 2), Colors.White);
                }
            }
        }

        // Wires: the same sagging line, each point's shadow cast from its height above the ground.
        foreach (PowerNetwork net in P.Networks.Values)
        {
            foreach ((Building from, Building to) in WireTree(net))
            {
                Vector2 a = S(from.Origin), b = a + (Root.WrapDelta(new Vector2(from.Origin.X, from.Origin.Y), new Vector2(to.Origin.X, to.Origin.Y)) * T);
                float h0 = Baked(from) is { Anchor: { } aa } ? -aa.Y + 0.5f : 0.4f;
                float h1 = Baked(to) is { Anchor: { } ab } ? -ab.Y + 0.5f : 0.4f;
                Vector2[] line = new Vector2[9];
                for (int k = 0; k < line.Length; k++)
                {
                    float f = k / (float)(line.Length - 1);
                    float h = Mathf.Lerp(h0, h1, f) / Art.SpriteBaker.Rise(1) * (1 - (0.5f * 4 * f * (1 - f) * 0.25f));
                    line[k] = a.Lerp(b, f) + new Vector2(T * 0.5f, T * 0.5f) + (lean * h);
                }

                if (OnScreen(new Rect2(line[0], Vector2.Zero).Expand(line[^1])))
                {
                    DrawPolyline(line, Colors.White, Mathf.Max(1, T * 0.035f), true);
                }
            }
        }
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
