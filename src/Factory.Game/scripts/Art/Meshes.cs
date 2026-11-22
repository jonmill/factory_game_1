using Godot;

namespace Factory.Game.Art;

/// <summary>
/// Chamfered primitives for the models. Every hard edge gets a small 45° bevel, which catches the sun as a
/// thin highlight, and the bevel faces are flagged in the vertex colour (green = 0) so the weathered
/// material wears them to bright metal. Meshes are cached by shape.
/// </summary>
public static class Meshes
{
    private static readonly Dictionary<string, ArrayMesh> _cache = [];
    private static readonly Color _face = Colors.White;
    private static readonly Color _edge = new(1, 0, 1);

    /// <summary>A box of <paramref name="size"/> centred on the origin, every edge chamfered by <paramref name="c"/>.</summary>
    /// <param name="size">Edge lengths.</param>
    /// <param name="c">Chamfer width.</param>
    /// <returns>The mesh.</returns>
    public static ArrayMesh Box(Vector3 size, float c)
    {
        Vector3 h = size / 2;
        c = Mathf.Clamp(c, 0.0005f, Mathf.Min(h.X, Mathf.Min(h.Y, h.Z)) * 0.45f);
        string key = $"box-{h.X:F4}-{h.Y:F4}-{h.Z:F4}-{c:F4}";
        if (_cache.TryGetValue(key, out ArrayMesh? cached))
        {
            return cached;
        }

        Vector3 A(int sx, int sy, int sz) => new(sx * h.X, sy * (h.Y - c), sz * (h.Z - c));
        Vector3 B(int sx, int sy, int sz) => new(sx * (h.X - c), sy * h.Y, sz * (h.Z - c));
        Vector3 C(int sx, int sy, int sz) => new(sx * (h.X - c), sy * (h.Y - c), sz * h.Z);
        int[] pm = [-1, 1];

        SurfaceTool st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (int s in pm)
        {
            Convex(st, [A(s, -1, -1), A(s, -1, 1), A(s, 1, -1), A(s, 1, 1)], Vector3.Zero, _face);
            Convex(st, [B(-1, s, -1), B(-1, s, 1), B(1, s, -1), B(1, s, 1)], Vector3.Zero, _face);
            Convex(st, [C(-1, -1, s), C(-1, 1, s), C(1, -1, s), C(1, 1, s)], Vector3.Zero, _face);
        }

        foreach (int a in pm)
        {
            foreach (int b in pm)
            {
                Convex(st, [A(a, b, -1), A(a, b, 1), B(a, b, -1), B(a, b, 1)], Vector3.Zero, _edge);
                Convex(st, [A(a, -1, b), A(a, 1, b), C(a, -1, b), C(a, 1, b)], Vector3.Zero, _edge);
                Convex(st, [B(-1, a, b), B(1, a, b), C(-1, a, b), C(1, a, b)], Vector3.Zero, _edge);
                foreach (int d in pm)
                {
                    Convex(st, [A(a, b, d), B(a, b, d), C(a, b, d)], Vector3.Zero, _edge);
                }
            }
        }

        ArrayMesh mesh = st.Commit();
        _cache[key] = mesh;

        return mesh;
    }

    /// <summary>
    /// An upright cylinder, cone or frustum from y = 0 to y = <paramref name="height"/>, with its rims
    /// chamfered. Sides are smooth-shaded; caps and chamfers are flat.
    /// </summary>
    /// <param name="r0">Bottom radius.</param>
    /// <param name="r1">Top radius.</param>
    /// <param name="height">Height.</param>
    /// <param name="sides">Facets round.</param>
    /// <param name="c">Chamfer width.</param>
    /// <returns>The mesh.</returns>
    public static ArrayMesh Cylinder(float r0, float r1, float height, int sides, float c)
    {
        c = Mathf.Clamp(c, 0, Mathf.Min(height * 0.3f, Mathf.Min(r0, r1 > 0.001f ? r1 : r0) * 0.4f));
        string key = $"cyl-{r0:F4}-{r1:F4}-{height:F4}-{sides}-{c:F4}";
        if (_cache.TryGetValue(key, out ArrayMesh? cached))
        {
            return cached;
        }

        bool cone = r1 < 0.002f;
        float yb = c, yt = cone ? height : height - c;
        float rb = r0, rt = cone ? 0 : r1;
        SurfaceTool st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Ring(float r, float y, int i)
        {
            float a = i * Mathf.Tau / sides;

            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        Vector3 inside = new Vector3(0, height / 2, 0);
        float slope = (rb - rt) / Mathf.Max(0.0001f, yt - yb);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;

            // The side, smooth: each vertex's normal points straight out, tipped up by the taper.
            Vector3 n0 = new Vector3(Mathf.Cos(i * Mathf.Tau / sides), slope, Mathf.Sin(i * Mathf.Tau / sides)).Normalized();
            Vector3 n1 = new Vector3(Mathf.Cos(j * Mathf.Tau / sides), slope, Mathf.Sin(j * Mathf.Tau / sides)).Normalized();
            Quad(st, Ring(rb, yb, i), Ring(rb, yb, j), Ring(rt, yt, j), Ring(rt, yt, i), n0, n1, n1, n0, inside);

            if (c > 0)
            {
                Convex(st, [Ring(rb - c, 0, i), Ring(rb - c, 0, j), Ring(rb, yb, j), Ring(rb, yb, i)], inside, _edge);
                if (cone == false)
                {
                    Convex(st, [Ring(rt, yt, i), Ring(rt, yt, j), Ring(rt - c, height, j), Ring(rt - c, height, i)], inside, _edge);
                }
            }
            else
            {
                Convex(st, [Ring(rb, 0, i), Ring(rb, 0, j), Ring(rb, yb, j), Ring(rb, yb, i)], inside, _face);
            }

            Convex(st, [new Vector3(0, 0, 0), Ring(rb - c, 0, i), Ring(rb - c, 0, j)], inside, _face);
            if (cone == false)
            {
                Convex(st, [new Vector3(0, height, 0), Ring(rt - c, height, i), Ring(rt - c, height, j)], inside, _face);
            }
        }

        ArrayMesh mesh = st.Commit();
        _cache[key] = mesh;

        return mesh;
    }

    /// <summary>Adds a flat convex polygon, its points in any order, facing away from <paramref name="inside"/>.</summary>
    private static void Convex(SurfaceTool st, Vector3[] pts, Vector3 inside, Color color)
    {
        Vector3 centre = Vector3.Zero;
        foreach (Vector3 p in pts)
        {
            centre += p;
        }

        centre /= pts.Length;

        // Find the plane's normal robustly, then order the points round it.
        Vector3 n = Normal(pts, centre);
        if (n.Dot(centre - inside) < 0)
        {
            n = -n;
        }

        Vector3 u = (pts[0] - centre).Normalized();
        Vector3 v = n.Cross(u);
        Vector3[] ordered = [.. pts.OrderBy(p => Mathf.Atan2((p - centre).Dot(v), (p - centre).Dot(u)))];

        // Counter-clockwise about n; Godot's front faces are clockwise, so emit each triangle reversed.
        for (int i = 1; i < ordered.Length - 1; i++)
        {
            Vertex(st, ordered[0], n, color);
            Vertex(st, ordered[i + 1], n, color);
            Vertex(st, ordered[i], n, color);
        }
    }

    private static Vector3 Normal(Vector3[] pts, Vector3 centre)
    {
        Vector3 best = Vector3.Up;
        float len = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            for (int j = i + 1; j < pts.Length; j++)
            {
                Vector3 n = (pts[i] - centre).Cross(pts[j] - centre);
                if (n.Length() > len)
                {
                    len = n.Length();
                    best = n;
                }
            }
        }

        return best.Normalized();
    }

    /// <summary>A smooth-shaded quad given counter-clockwise from outside, with a normal per corner.</summary>
    private static void Quad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector3 inside)
    {
        // Check the winding against the outward direction, and flip if needed.
        Vector3 face = (b - a).Cross(c - a);
        bool ccw = face.Dot(((a + c) / 2) - inside) > 0;
        if (ccw)
        {
            Vertex(st, a, na, _face);
            Vertex(st, c, nc, _face);
            Vertex(st, b, nb, _face);
            Vertex(st, a, na, _face);
            Vertex(st, d, nd, _face);
            Vertex(st, c, nc, _face);
        }
        else
        {
            Vertex(st, a, na, _face);
            Vertex(st, b, nb, _face);
            Vertex(st, c, nc, _face);
            Vertex(st, a, na, _face);
            Vertex(st, c, nc, _face);
            Vertex(st, d, nd, _face);
        }
    }

    private static void Vertex(SurfaceTool st, Vector3 p, Vector3 n, Color color)
    {
        st.SetNormal(n);
        st.SetColor(color);
        st.AddVertex(p);
    }
}
