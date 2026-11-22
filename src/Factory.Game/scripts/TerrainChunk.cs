using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// One square of the planet's ore, ore beds and trees, built in local pixels at zoom 1 as a single
/// vertex-coloured triangle mesh, so the whole chunk is one draw call. Godot keeps the recorded command,
/// so the mesh is only rebuilt when <see cref="TerrainLayer"/> sees the chunk's resources change; moving
/// the camera just changes the node's position and scale.
/// </summary>
public partial class TerrainChunk : Node2D
{
    private const float T = GameRoot.TilePx;
    private const int CircleSegments = 12;

    private readonly List<Vector2> _verts = [];
    private readonly List<Color> _colors = [];
    private ArrayMesh? _mesh;

    /// <summary>Planet whose tiles are drawn.</summary>
    public Planet Planet { get; set; } = null!;

    /// <summary>First tile covered by this chunk.</summary>
    public Int2 Origin { get; set; }

    /// <summary>Edge length in tiles.</summary>
    public int Size { get; set; }

    /// <summary>How many crystals a tile shows for its remaining amount: richer tiles grow bigger clusters.</summary>
    /// <param name="amount">Units of resource left on the tile.</param>
    /// <returns>Between one and three crystals.</returns>
    public static int Crystals(int amount) => 1 + System.Math.Min(2, amount / 350);

    /// <inheritdoc />
    public override void _Draw()
    {
        int x1 = System.Math.Min(Planet.Width, Origin.X + Size);
        int y1 = System.Math.Min(Planet.Height, Origin.Y + Size);
        _verts.Clear();
        _colors.Clear();
        DrawGroundDetail(x1, y1);
        DrawBeds(x1, y1);
        DrawOre(x1, y1);
        DrawTrees(x1, y1);
        if (_verts.Count == 0)
        {
            return;
        }

        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        _mesh ??= new ArrayMesh();
        _mesh.ClearSurfaces();
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        DrawMesh(_mesh, null);
    }

    private void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        _verts.Add(a);
        _verts.Add(b);
        _verts.Add(c);
        _colors.Add(col);
        _colors.Add(col);
        _colors.Add(col);
    }

    /// <summary>A convex polygon as a triangle fan.</summary>
    private void Poly(Vector2[] pts, Color col)
    {
        for (int i = 1; i < pts.Length - 1; i++)
        {
            Tri(pts[0], pts[i], pts[i + 1], col);
        }
    }

    private void Circle(Vector2 c, float r, Color col)
    {
        Vector2 prev = c + new Vector2(r, 0);
        for (int i = 1; i <= CircleSegments; i++)
        {
            float a = i * Mathf.Tau / CircleSegments;
            Vector2 next = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            Tri(c, prev, next, col);
            prev = next;
        }
    }

    private void Line(Vector2 a, Vector2 b, Color col, float width)
    {
        Vector2 n = (b - a).Normalized().Orthogonal() * (width / 2);
        Tri(a + n, b + n, b - n, col);
        Tri(a + n, b - n, a - n, col);
    }

    private Vector2 Local(Int2 t) => new Vector2(t.X - Origin.X, t.Y - Origin.Y) * T;

    private static uint Hash(int x, int y, uint seed) => GroundPainter.Hash(x, y, seed);

    private static Color A(Color c, float a) => new(c.R, c.G, c.B, a);

    /// <summary>
    /// Small things on open ground so it doesn't read as a flat wash: grass tufts where the biome grows plants,
    /// pebbles and hairline cracks everywhere, all tinted from the biome's own ground colours. Clumped by a
    /// coarse hash so some patches are bare and others busy.
    /// </summary>
    private void DrawGroundDetail(int x1, int y1)
    {
        for (int y = Origin.Y; y < y1; y++)
        {
            for (int x = Origin.X; x < x1; x++)
            {
                Int2 t = new Int2(x, y);
                if ((Planet.TerrainAt(t) != Terrain.Ground) || (Planet.ResourceAt(t) is not null))
                {
                    continue;
                }

                BiomeDef? biome = Planet.BiomeAt(t);
                Color ground = WorldView.Hex(biome?.Ground[0] ?? "#5a4a33");
                bool grassy = (biome?.Trees ?? 0) > 0;
                uint h = Hash(t.X, t.Y, 77);
                uint patch = Hash(t.X >> 2, t.Y >> 2, 78);
                int busy = (int)(patch & 3);
                Vector2 p = Local(t);
                Vector2 At(uint bits) => p + new Vector2(T * (0.1f + ((bits & 15) / 19f)), T * (0.12f + (((bits >> 4) & 15) / 19f)));

                // Grass tufts: three or four blades fanning up from one root.
                int tufts = grassy ? busy + (int)((h >> 20) & 1) - 1 : 0;
                for (int k = 0; k < tufts; k++)
                {
                    uint hk = Hash((t.X * 5) + k, (t.Y * 3) - k, 79);
                    Vector2 root = At(hk);
                    float len = T * (0.1f + (((hk >> 8) & 7) * 0.014f));
                    Color blade = ground.Lerp(new Color(0.42f, 0.5f, 0.22f), 0.45f).Lightened(((hk >> 12) & 3) * 0.05f);
                    for (int b = -1; b <= 2; b++)
                    {
                        float lean = (b * 0.35f) + ((((hk >> 16) & 7) - 3.5f) * 0.03f);
                        Vector2 tip = root + new Vector2(Mathf.Sin(lean) * len, -Mathf.Cos(lean) * len);
                        Vector2 side = new Vector2(len * 0.09f, 0);
                        Tri(root - side, root + side, tip, b == 2 ? blade.Darkened(0.25f) : blade);
                    }
                }

                // Pebbles: a shadow, the stone, and a lit top.
                int pebbles = (int)((h >> 24) & 3) + busy - 3;
                for (int k = 0; k < pebbles; k++)
                {
                    uint hk = Hash((t.X * 11) - k, (t.Y * 7) + k, 80);
                    Vector2 c = At(hk);
                    float r = T * (0.025f + (((hk >> 8) & 3) * 0.012f));
                    Color stone = ground.Lerp(new Color(0.55f, 0.52f, 0.48f), 0.4f);
                    Circle(c + new Vector2(r * 0.3f, r * 0.4f), r, new Color(0, 0, 0, 0.22f));
                    Circle(c, r, stone);
                    Circle(c - new Vector2(r * 0.3f, r * 0.35f), r * 0.45f, stone.Lightened(0.25f));
                }

                // An occasional dry crack.
                if ((h & 31) == 0)
                {
                    Vector2 a = At(h >> 8);
                    Vector2 b = a + new Vector2(T * 0.22f, T * 0.06f);
                    Vector2 c = b + new Vector2(T * 0.1f, -T * 0.12f);
                    Color crack = A(ground.Darkened(0.45f), 0.6f);
                    Line(a, b, crack, Mathf.Max(0.6f, T * 0.018f));
                    Line(b, c, crack, Mathf.Max(0.6f, T * 0.014f));
                }
            }
        }
    }

    /// <summary>Leaf litter under trees and a faint wash of the ore's colour under ore, as soft blobs.</summary>
    private void DrawBeds(int x1, int y1)
    {
        for (int y = Origin.Y; y < y1; y++)
        {
            for (int x = Origin.X; x < x1; x++)
            {
                Int2 t = new Int2(x, y);
                ResourceDef? res = Planet.ResourceAt(t);
                if (res is null)
                {
                    continue;
                }

                Vector2 c = Local(t) + new Vector2(T / 2, T / 2);
                uint h = Hash(t.X, t.Y, 5);
                Color bed = res.IsTree
                    ? new Color(0.14f, 0.16f, 0.08f, 0.22f)
                    : A(WorldView.Hex(res.Color).Lerp(new Color(0.3f, 0.26f, 0.2f), 0.35f), 0.13f);
                for (int k = 0; k < 2; k++)
                {
                    Vector2 jitter = new Vector2(((h >> (k * 4)) & 15) / 15f, ((h >> ((k * 4) + 8)) & 15) / 15f) - new Vector2(0.5f, 0.5f);
                    Circle(c + (jitter * T * 0.3f), T * (0.55f + (0.08f * k)), bed);
                }
            }
        }
    }

    /// <summary>Ore as faceted, glossy crystals with a lit face, a shaded face and a bright tip.</summary>
    private void DrawOre(int x1, int y1)
    {
        for (int y = Origin.Y; y < y1; y++)
        {
            for (int x = Origin.X; x < x1; x++)
            {
                Int2 t = new Int2(x, y);
                ResourceDef? res = Planet.ResourceAt(t);
                if ((res is null) || res.IsTree)
                {
                    continue;
                }

                Vector2 p = Local(t);
                Color baseColor = WorldView.Hex(res.Color);
                int crystals = Crystals(Planet.AmountAt(t));
                for (int k = 0; k < crystals; k++)
                {
                    uint hk = Hash((t.X * 7) + k, (t.Y * 13) - k, 9);
                    Vector2 cp = p + new Vector2(T * (0.22f + ((hk & 15) / 28f)), T * (0.42f + (((hk >> 4) & 15) / 32f)));
                    float s = T * (0.13f + (((hk >> 8) & 3) * 0.035f));
                    float lean = ((((hk >> 10) & 7) / 7f) - 0.5f) * 0.5f;
                    DrawCrystal(cp, s, lean, baseColor);
                }
            }
        }
    }

    private void DrawCrystal(Vector2 foot, float s, float lean, Color color)
    {
        Vector2 tip = foot + new Vector2(lean * s, -s * 2.1f);
        Vector2 left = foot + new Vector2(-s * 0.7f, -s * 0.35f);
        Vector2 right = foot + new Vector2(s * 0.7f, -s * 0.35f);
        Vector2 shoulderL = foot + new Vector2((-s * 0.55f) + (lean * s * 0.6f), -s * 1.35f);
        Vector2 shoulderR = foot + new Vector2((s * 0.55f) + (lean * s * 0.6f), -s * 1.35f);

        Poly([foot + new Vector2(s * 0.2f, s * 0.15f), right + new Vector2(s * 0.5f, s * 0.2f), tip + new Vector2(s * 0.9f, s * 1.3f)], new Color(0, 0, 0, 0.3f));
        Poly([foot, left, shoulderL, tip], color.Lightened(0.15f));
        Poly([foot, tip, shoulderR, right], color.Darkened(0.35f));
        Line(foot, tip, color.Lightened(0.45f), Mathf.Max(1f, s * 0.12f));
        Circle(tip + new Vector2(0, s * 0.35f), s * 0.18f, color.Lightened(0.6f));
    }

    /// <summary>Trees as clumps of foliage with a shadow.</summary>
    private void DrawTrees(int x1, int y1)
    {
        for (int y = Origin.Y; y < y1; y++)
        {
            for (int x = Origin.X; x < x1; x++)
            {
                Int2 t = new Int2(x, y);
                ResourceDef? res = Planet.ResourceAt(t);
                if ((res is null) || (res.IsTree == false))
                {
                    continue;
                }

                // The trunk stands where the sim puts its collision box; the canopy sits just above it.
                uint h = Hash(t.X, t.Y, 13);
                Int2 trunk = Planet.TreeTrunkOffset(Planet.Torus.Wrap(t));
                Vector2 c = Local(t) + (new Vector2(trunk.X, trunk.Y) * T / Fixed.One) - new Vector2(0, T * 0.08f);
                float r = T * (0.38f + (((h >> 6) & 3) * 0.04f));
                Color leaf = WorldView.Hex(res.Color).Lerp(new Color(0.25f, 0.32f, 0.12f), 0.55f);

                // Each tree's hue drifts a little so a forest isn't one flat colour.
                leaf = leaf.Lerp(new Color(0.42f, 0.4f, 0.12f), ((h >> 14) & 7) / 7f * 0.25f).Darkened(((h >> 17) & 3) * 0.04f);
                int lobes = 6 + (int)((h >> 19) & 1);
                float turn = ((h >> 9) & 15) / 15f * Mathf.Tau;

                // A soft shadow, the trunk's foot, then the canopy from its shaded underside up to its lit crown.
                Circle(c + new Vector2(r * 0.4f, r * 0.5f), r * 1.1f, new Color(0, 0, 0, 0.18f));
                Circle(c + new Vector2(r * 0.3f, r * 0.4f), r * 0.95f, new Color(0, 0, 0, 0.18f));
                Circle(c + new Vector2(0, T * 0.08f), T * 0.06f, new Color(0.25f, 0.18f, 0.1f));
                for (int k = 0; k < lobes; k++)
                {
                    float a = (k * Mathf.Tau / lobes) + turn;
                    float reach = r * (0.5f + ((((h >> (k + 3)) & 1) == 0) ? 0f : 0.08f));
                    Circle(c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach), r * 0.5f, leaf.Darkened(0.32f));
                }

                for (int k = 0; k < lobes; k++)
                {
                    float a = (k * Mathf.Tau / lobes) + turn + 0.4f;
                    Vector2 lobe = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.36f) - new Vector2(r * 0.05f, r * 0.08f);
                    float lit = Mathf.Clamp(0.5f - (0.5f * ((Mathf.Cos(a) + Mathf.Sin(a)) * 0.7f)), 0, 1);
                    Circle(lobe, r * 0.42f, leaf.Darkened(0.18f).Lerp(leaf, lit));
                }

                Circle(c - new Vector2(r * 0.12f, r * 0.15f), r * 0.48f, leaf);
                Circle(c - new Vector2(r * 0.28f, r * 0.32f), r * 0.26f, leaf.Lightened(0.14f));
                Circle(c - new Vector2(r * 0.36f, r * 0.42f), r * 0.11f, leaf.Lightened(0.3f));
                Circle(c + new Vector2(r * 0.18f, -r * 0.3f), r * 0.08f, leaf.Lightened(0.22f));
            }
        }
    }
}
