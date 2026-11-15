using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>Interaction overlays: construction ghosts, status signs, haul routes, selection and placement previews.</summary>
public partial class WorldView
{
    private static readonly Color _blueprint = new(0.45f, 0.75f, 1f);
    private static readonly Color _warning = new(0.95f, 0.75f, 0.15f);
    private static readonly Color _alarm = new(0.92f, 0.25f, 0.2f);
    private static readonly Color _highlight = new(1f, 0.92f, 0.55f);

    private void DrawSite(ConstructionSite s)
    {
        float progress = s.WorkNeeded > 0 ? s.WorkDone / (float)s.WorkNeeded : 0;
        if (s.Kind == SiteKind.BuildTrain)
        {
            foreach (Int2 t in s.TrainTiles)
            {
                Rect2 tr = new Rect2(S(t), new Vector2(T, T)).Grow(-T * 0.12f);
                DrawRect(tr, A(_blueprint, 0.22f));
                DrawRect(tr, A(_blueprint, 0.7f), false, Mathf.Max(1, T * 0.04f));
            }

            return;
        }

        Rect2 r = RectOf(s.Origin, s.W, s.H).Grow(-T * 0.06f);
        if (s.Kind == SiteKind.Deconstruct)
        {
            DrawRect(r, A(_alarm, 0.18f));
            DrawRect(r, A(_alarm, 0.85f), false, Mathf.Max(1.5f, T * 0.05f));
            DrawLine(r.Position, r.End, A(_alarm, 0.85f), Mathf.Max(1.5f, T * 0.05f));
            DrawLine(r.Position + new Vector2(r.Size.X, 0), r.Position + new Vector2(0, r.Size.Y), A(_alarm, 0.85f), Mathf.Max(1.5f, T * 0.05f));

            return;
        }

        DrawRect(r, A(_blueprint, 0.2f));
        float pulse = 0.65f + (0.15f * Mathf.Sin((Time * 3) + s.Id));
        DrawRect(r, A(_blueprint, pulse), false, Mathf.Max(1, T * 0.04f));
        float matFrac = s.Cost.Length == 0 ? 1 : s.Cost.Sum(c => System.Math.Min(c.Count, s.Delivered.Count(c.Item))) / (float)s.Cost.Sum(c => c.Count);
        Rect2 bars = new Rect2(r.Position + new Vector2(T * 0.08f, r.Size.Y - (T * 0.24f)), new Vector2(r.Size.X - (T * 0.16f), T * 0.07f));
        DrawRect(bars, new Color(0, 0, 0, 0.4f));
        DrawRect(new Rect2(bars.Position, new Vector2(bars.Size.X * matFrac, bars.Size.Y)), new Color(0.85f, 0.7f, 0.3f));
        Rect2 work = bars with { Position = bars.Position + new Vector2(0, T * 0.1f) };
        DrawRect(work, new Color(0, 0, 0, 0.4f));
        DrawRect(new Rect2(work.Position, new Vector2(work.Size.X * progress, work.Size.Y)), _blueprint);
        if (s.DefId is "rail-stop" or "burner-drill" or "sorter" or "electric-drill")
        {
            DrawArrow(r.GetCenter(), s.Rot, T * 0.2f, A(_blueprint, 0.8f));
        }
    }

    /// <summary>A small warning sign on machines that are stuck: red for missing power or fuel, amber for flow problems.</summary>
    private void DrawStatus(Building b)
    {
        Color? c = b.Status switch
        {
            MachineStatus.NoPower or MachineStatus.NoFuel or MachineStatus.NoWater => _alarm,
            MachineStatus.NoInput or MachineStatus.OutputFull or MachineStatus.NoRecipe or MachineStatus.NoResearch or MachineStatus.NoResource or MachineStatus.NotReady => _warning,
            _ => null,
        };
        if ((c is null) || (b.Def.Kind == BuildingKind.Lander))
        {
            return;
        }

        Rect2 r = RectOf(b.Origin, b.W, b.H);
        if (OnScreen(r) == false)
        {
            return;
        }

        float a = 0.75f + (0.25f * Mathf.Sin(Time * 4f));
        Vector2 p = r.Position + new Vector2(r.Size.X - (T * 0.25f), T * 0.25f);
        float s = T * 0.2f;
        Vector2[] sign = [p + new Vector2(0, -s), p + new Vector2(s, s * 0.8f), p + new Vector2(-s, s * 0.8f)];
        DrawColoredPolygon(sign, new Color(0.1f, 0.08f, 0.05f, 0.85f * a));
        DrawColoredPolygon([p + new Vector2(0, -s * 0.72f), p + new Vector2(s * 0.75f, s * 0.62f), p + new Vector2(-s * 0.75f, s * 0.62f)], A(c.Value, a));
        DrawLine(p + new Vector2(0, -s * 0.3f), p + new Vector2(0, s * 0.15f), new Color(0.1f, 0.08f, 0.05f), Mathf.Max(1, T * 0.04f));
        DrawCircle(p + new Vector2(0, s * 0.38f), Mathf.Max(0.8f, T * 0.025f), new Color(0.1f, 0.08f, 0.05f));
    }

    private void DrawRoutes()
    {
        Color route = new Color(0.95f, 0.8f, 0.4f);
        foreach (Unit u in P.Units)
        {
            if (u.Job.Kind != JobKind.Haul)
            {
                continue;
            }

            Building? a = Root.World.Get<Building>(u.Job.FromId);
            Building? b = Root.World.Get<Building>(u.Job.ToId);
            if ((a is null) || (b is null))
            {
                continue;
            }

            bool sel = Root.SelectedId == u.Id;
            Vector2 at = GameRoot.SubToTile(P.SubTorus.Wrap(a.CenterSub));
            Vector2 pa = S(at);
            Vector2 pb = pa + (Root.WrapDelta(at, GameRoot.SubToTile(P.SubTorus.Wrap(b.CenterSub))) * T);
            DrawDashedLine(pa, pb, A(route, sel ? 0.9f : 0.22f), sel ? 2.5f : 1.5f, 8);
        }

        if (Root.World.Get<Train>(Root.SelectedId) is { } t)
        {
            foreach (ScheduleStop st in t.Schedule)
            {
                if (Root.World.Get<Building>(st.StopId) is { } stop)
                {
                    DrawArc(Center(stop.Origin), T * 0.8f, 0, Mathf.Tau, 24, A(new Color(1f, 0.45f, 0.7f), 0.9f), 2);
                }
            }
        }
    }

    /// <summary>The tile the commander is hand-mining: warm corner brackets and a ring filling toward the next item.</summary>
    private void DrawMiningTarget()
    {
        Unit leader = Root.Leader;
        if ((leader.MiningTile is not { } t) || (P.ResourceAt(t) is not { } res))
        {
            return;
        }

        Color warm = new Color(1f, 0.78f, 0.35f);
        Rect2 r = new Rect2(S(t), new Vector2(T, T));
        Brackets(r, A(warm, 0.9f));
        int need = P.HandMiningTicks(leader, res);
        float f = Mathf.Clamp(leader.MiningProgress / (float)need, 0, 1);
        if (f > 0)
        {
            Vector2 c = r.GetCenter();
            DrawArc(c, T * 0.42f, 0, Mathf.Tau, 24, new Color(0, 0, 0, 0.35f), Mathf.Max(2, T * 0.07f));
            DrawArc(c, T * 0.42f, -Mathf.Pi / 2, (-Mathf.Pi / 2) + (Mathf.Tau * f), 24, warm, Mathf.Max(2, T * 0.07f));
        }
    }

    /// <summary>Corner brackets around the selection.</summary>
    private void Brackets(Rect2 r, Color c)
    {
        float len = Mathf.Min(r.Size.X, r.Size.Y) * 0.3f;
        float w = Mathf.Max(1.5f, T * 0.06f);
        Vector2[] corners = [r.Position, r.Position + new Vector2(r.Size.X, 0), r.End, r.Position + new Vector2(0, r.Size.Y)];
        Vector2[] dx = [Vector2.Right, Vector2.Left, Vector2.Left, Vector2.Right];
        Vector2[] dy = [Vector2.Down, Vector2.Down, Vector2.Up, Vector2.Up];
        for (int i = 0; i < 4; i++)
        {
            DrawLine(corners[i], corners[i] + (dx[i] * len), c, w);
            DrawLine(corners[i], corners[i] + (dy[i] * len), c, w);
        }
    }

    private void DrawSelection()
    {
        World w = Root.World;
        if (w.Get<Building>(Root.SelectedId) is { } b)
        {
            Brackets(RectOf(b.Origin, b.W, b.H).Grow(T * 0.08f), _highlight);
        }
        else if ((w.Get<ConstructionSite>(Root.SelectedId) is { } s) && (s.Kind != SiteKind.BuildTrain))
        {
            Brackets(RectOf(s.Origin, s.W, s.H).Grow(T * 0.08f), _highlight);
        }
        else if (w.Get<Unit>(Root.SelectedId) is { } u)
        {
            Brackets(new Rect2(S(GameRoot.SubToTile(u.Pos)) + (BoundsFor(u).Position * T), BoundsFor(u).Size * T), _highlight);
        }
        else if (w.Get<Train>(Root.SelectedId) is { } t)
        {
            foreach (Int2 tile in t.Tiles)
            {
                Brackets(new Rect2(S(tile), new Vector2(T, T)), _highlight);
            }
        }
    }

    private void DrawPreview()
    {
        Color good = new Color(0.4f, 0.9f, 0.5f);
        Color bad = new Color(0.95f, 0.3f, 0.3f);
        switch (Root.Tool)
        {
            case ToolKind.Place when Root.PlaceDefId is not null:
                {
                    BuildingDef def = Root.Content.Buildings[Root.PlaceDefId];
                    Int2 origin = def.Kind == BuildingKind.Rail ? Root.HoverTile : Root.PlacementOrigin(def);
                    (int w, int h) = Planet.SizeOf(def, Root.PlaceRot);
                    bool ok = P.CanPlace(def, origin, Root.PlaceRot) is null;
                    Rect2 r = RectOf(origin, w, h);
                    DrawRect(r, A(ok ? good : bad, 0.22f));
                    DrawRect(r, A(ok ? good : bad, 0.85f), false, 2);
                    if (def.Kind is BuildingKind.Drill or BuildingKind.RailStop or BuildingKind.Sorter)
                    {
                        DrawArrow(r.GetCenter(), Root.PlaceRot, T * 0.3f, A(ok ? good : bad, 0.9f));
                    }

                    if ((def.Kind == BuildingKind.Drill) && (def.MiningRadius > 0))
                    {
                        // The ore it reaches beyond its edges.
                        float mr = def.MiningRadius;
                        DrawRect(new Rect2(S(origin) - (new Vector2(mr, mr) * T), new Vector2(w + (2 * mr), h + (2 * mr)) * T), A(new Color(0.5f, 0.85f, 1f), 0.6f), false, 1);
                    }

                    if (def.Kind == BuildingKind.Pole)
                    {
                        float sr = def.SupplyRadius;
                        DrawRect(new Rect2(S(origin) - (new Vector2(sr, sr) * T), new Vector2((2 * sr) + 1, (2 * sr) + 1) * T), A(new Color(0.95f, 0.8f, 0.45f), 0.1f));
                    }

                    break;
                }
            case ToolKind.PlaceTrain:
                {
                    List<Int2>? tiles = P.LayoutTrain(Root.HoverTile, Root.PlaceRot, Root.TrainPreset.Length);
                    if (tiles is null)
                    {
                        DrawRect(new Rect2(S(Root.HoverTile), new Vector2(T, T)), A(bad, 0.4f));
                        break;
                    }

                    for (int i = 0; i < tiles.Count; i++)
                    {
                        DrawRect(new Rect2(S(tiles[i]), new Vector2(T, T)).Grow(-T * 0.1f), A(i == 0 ? Colors.White : good, 0.45f));
                    }

                    DrawArrow(Center(tiles[0]), Root.PlaceRot, T * 0.3f, A(good, 0.9f));
                    break;
                }
            case ToolKind.Deconstruct:
                DrawRect(new Rect2(S(Root.HoverTile), new Vector2(T, T)), A(bad, 0.35f));
                break;
            case ToolKind.PickHaulFrom or ToolKind.PickHaulTo or ToolKind.PickTrain or ToolKind.PickStop:
                Brackets(new Rect2(S(Root.HoverTile), new Vector2(T, T)), _highlight);
                break;
        }
    }
}
