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

    private void DrawUnit(Unit u)
    {
        Vector2 tilePos = GameRoot.SubToTile(u.Pos);
        Vector2 c = S(tilePos);
        Vector2 face = Facing(u, tilePos);
        Vector2 side = new Vector2(-face.Y, face.X);
        bool walking = (u.Phase is UnitPhase.MoveTo or UnitPhase.Deliver or UnitPhase.Seek) || (u.IsLeader && (u.MoveIntent != Int2.Zero));
        float bob = walking ? Mathf.Sin((Time * 14f) + u.Id) * T * 0.025f : 0;

        Color suit = u.IsLeader ? new Color(0.78f, 0.45f, 0.18f) : new Color(0.42f, 0.47f, 0.42f);
        Color pack = suit.Darkened(0.45f);
        float s = T * 0.25f;

        DrawCircle(c + new Vector2(T * 0.08f, T * 0.14f), s * 1.25f, new Color(0, 0, 0, 0.35f));
        if (walking)
        {
            float stride = Mathf.Sin((Time * 14f) + u.Id) * s * 0.5f;
            DrawCircle(c + (side * s * 0.45f) + (face * stride), s * 0.32f, new Color(0.15f, 0.13f, 0.12f));
            DrawCircle(c - (side * s * 0.45f) - (face * stride), s * 0.32f, new Color(0.15f, 0.13f, 0.12f));
        }

        Vector2 body = c + new Vector2(0, bob);
        DrawCircle(body - (face * s * 0.55f), s * 0.62f, pack);
        if (u.Carry.IsEmpty == false)
        {
            Color cargo = Hex(Root.Content.Item(u.Carry.Items.First().Key).Color);
            DrawRect(new Rect2(body - (face * s * 0.75f) - new Vector2(s * 0.42f, s * 0.42f), new Vector2(s * 0.84f, s * 0.84f)), cargo.Darkened(0.15f));
        }

        DrawCircle(body, s * 0.85f, suit);
        DrawCircle(body + (side * s * 0.8f), s * 0.3f, suit.Darkened(0.15f));
        DrawCircle(body - (side * s * 0.8f), s * 0.3f, suit.Darkened(0.15f));
        if (u.IsLeader == false)
        {
            DrawCircle(body + (side * s * 0.8f), s * 0.18f, JobColor(u.Job.Kind));
        }

        // Helmet seen from above with a dark visor band toward the facing direction.
        Vector2 head = body + (face * s * 0.25f);
        Color helmet = u.IsLeader ? new Color(0.9f, 0.82f, 0.6f) : new Color(0.78f, 0.78f, 0.74f);
        DrawCircle(head, s * 0.5f, helmet.Darkened(0.25f));
        DrawCircle(head - (face * s * 0.06f), s * 0.42f, helmet);
        float facing = face.Angle();
        DrawArc(head, s * 0.36f, facing - 0.9f, facing + 0.9f, 10, new Color(0.16f, 0.2f, 0.24f), s * 0.22f);
        DrawCircle(head - (face * s * 0.18f) - (side * s * 0.14f), s * 0.09f, A(Colors.White, 0.45f));

        Vector2? workTile = u.IsLeader ? (u.MiningTile is { } mt ? new Vector2(mt.X + 0.5f, mt.Y + 0.5f) : null) : null;
        if ((workTile is null) && (u.Phase == UnitPhase.Work))
        {
            workTile = tilePos + face;
        }

        if (workTile is { } wt)
        {
            Vector2 target = c + (Root.WrapDelta(tilePos, wt) * T);
            float swing = Mathf.Abs(Mathf.Sin(Time * 9f));
            Vector2 toolTip = body + (side * s * 0.8f) + ((target - body).Normalized() * s * (0.6f + (swing * 0.6f)));
            DrawLine(body + (side * s * 0.8f), toolTip, new Color(0.55f, 0.5f, 0.45f), Mathf.Max(1.5f, T * 0.05f));
            if (swing > 0.9f)
            {
                DrawCircle(target + new Vector2(Mathf.Sin(Time * 31f) * T * 0.15f, -T * 0.1f), T * 0.04f, Hdr(new Color(1f, 0.85f, 0.5f), 1.2f));
            }
        }

        if (Root.Zoom > 0.6f)
        {
            Color label = u.IsLeader ? new Color(1f, 0.85f, 0.6f) : new Color(0.92f, 0.92f, 0.88f, 0.9f);
            DrawString(_font, c + new Vector2(-T * 2, T * 0.65f), u.Name, HorizontalAlignment.Center, T * 4, 11, label);
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
