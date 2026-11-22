using Godot;

namespace Factory.Game.Art;

/// <summary>Which image a bake renders: the lit model, its shadow on the ground, or only its glowing parts.</summary>
public enum BakePass
{
    /// <summary>The model, lit and shaded, on a transparent background.</summary>
    Color,

    /// <summary>The model flattened onto the ground along the sun's direction, as a solid white mask: its cast shadow.</summary>
    Shadow,

    /// <summary>The model flattened straight down, as a solid mask: where it touches and covers the ground.</summary>
    Footprint,

    /// <summary>Only the emissive parts, everything else black; turned into a glow layer.</summary>
    Glow,
}

/// <summary>
/// Builds a building's 3D model from primitives. Coordinates are in tiles: x runs east, y up, z south, with
/// the origin at the footprint's north-west corner on the ground. The same model code runs once per pass and
/// animation frame; the kit handles what each pass needs (shadow casters only, glow-only colours).
/// </summary>
public sealed class Kit
{
    private static readonly StandardMaterial3D _black = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.Black };
    private static readonly StandardMaterial3D _white = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.White, CullMode = BaseMaterial3D.CullModeEnum.Disabled };

    private readonly Node3D _root;

    /// <summary>Creates a kit that adds parts under <paramref name="root"/>.</summary>
    /// <param name="root">Parent node for every part.</param>
    /// <param name="pass">The pass being rendered.</param>
    /// <param name="phase">Animation phase, 0 to 1, for moving parts.</param>
    public Kit(Node3D root, BakePass pass, float phase)
    {
        _root = root;
        Pass = pass;
        Phase = phase;
    }

    /// <summary>The pass being rendered.</summary>
    public BakePass Pass { get; }

    /// <summary>Animation phase from 0 to 1; a full turn of a spinning part, a full stroke of a piston.</summary>
    public float Phase { get; }

    /// <summary>The phase as an angle, for spinning parts.</summary>
    public float Spin => Phase * Mathf.Tau;

    /// <summary>A child kit whose parts sit relative to <paramref name="pivot"/>, turned by <paramref name="rotation"/>.</summary>
    /// <param name="pivot">Where the group's origin sits.</param>
    /// <param name="rotation">The group's rotation.</param>
    /// <returns>The child kit.</returns>
    public Kit Group(Vector3 pivot, Basis rotation)
    {
        Node3D n = new Node3D { Transform = new Transform3D(rotation, pivot) };
        _root.AddChild(n);

        return new Kit(n, Pass, Phase);
    }

    /// <summary>Adds a mesh with a material and transform, adjusted for the pass.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="mat">Its material.</param>
    /// <param name="xf">Its transform within this kit.</param>
    public void Add(Mesh mesh, Material mat, Transform3D xf)
    {
        MeshInstance3D mi = new MeshInstance3D { Mesh = mesh, Transform = xf };
        bool glow = (mat is StandardMaterial3D s) && s.EmissionEnabled;
        switch (Pass)
        {
            case BakePass.Shadow:
            case BakePass.Footprint:
                mi.MaterialOverride = _white;
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                break;
            case BakePass.Glow:
                mi.MaterialOverride = glow ? Palette.GlowOnly((StandardMaterial3D)mat) : _black;
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                break;
            default:
                mi.MaterialOverride = mat;
                break;
        }

        _root.AddChild(mi);
    }

    /// <summary>A box.</summary>
    /// <param name="center">Centre of the box.</param>
    /// <param name="size">Edge lengths.</param>
    /// <param name="mat">Material.</param>
    /// <param name="rotation">Optional rotation about its centre.</param>
    /// <returns>This kit.</returns>
    public Kit Box(Vector3 center, Vector3 size, Material mat, Basis? rotation = null)
    {
        Add(new BoxMesh { Size = size }, mat, new Transform3D(rotation ?? Basis.Identity, center));

        return this;
    }

    /// <summary>A box resting on the ground plane of this kit, given by its footprint corner and size.</summary>
    /// <param name="x">West edge.</param>
    /// <param name="z">North edge.</param>
    /// <param name="w">Width (east-west).</param>
    /// <param name="d">Depth (north-south).</param>
    /// <param name="h">Height.</param>
    /// <param name="mat">Material.</param>
    /// <param name="y">Height of its base.</param>
    /// <returns>This kit.</returns>
    public Kit Slab(float x, float z, float w, float d, float h, Material mat, float y = 0) =>
        Box(new Vector3(x + (w / 2), y + (h / 2), z + (d / 2)), new Vector3(w, h, d), mat);

    /// <summary>A box with its four vertical edges chamfered by <paramref name="cut"/>, so the corners catch the light.</summary>
    /// <param name="x">West edge.</param>
    /// <param name="z">North edge.</param>
    /// <param name="w">Width.</param>
    /// <param name="d">Depth.</param>
    /// <param name="h">Height.</param>
    /// <param name="cut">How far each corner is cut back along both edges.</param>
    /// <param name="mat">Material.</param>
    /// <param name="y">Height of its base.</param>
    /// <returns>This kit.</returns>
    public Kit Bevel(float x, float z, float w, float d, float h, float cut, Material mat, float y = 0)
    {
        float cy = y + (h / 2);
        Box(new Vector3(x + (w / 2), cy, z + (d / 2)), new Vector3(w, h, d - (2 * cut)), mat);
        Box(new Vector3(x + (w / 2), cy, z + (d / 2)), new Vector3(w - (2 * cut), h, d), mat);
        Basis turn = new Basis(Vector3.Up, Mathf.Pi / 4);
        float side = cut * Mathf.Sqrt2;
        foreach (Vector3 c in new[] { new Vector3(x + cut, cy, z + cut), new Vector3(x + w - cut, cy, z + cut), new Vector3(x + cut, cy, z + d - cut), new Vector3(x + w - cut, cy, z + d - cut) })
        {
            Box(c, new Vector3(side, h, side), mat, turn);
        }

        return this;
    }

    /// <summary>An upright cylinder (or cone, or frustum) standing on <paramref name="baseCenter"/>.</summary>
    /// <param name="baseCenter">Centre of its base.</param>
    /// <param name="radius">Bottom radius.</param>
    /// <param name="height">Height.</param>
    /// <param name="mat">Material.</param>
    /// <param name="topRadius">Top radius; negative for the same as the bottom.</param>
    /// <param name="sides">Number of sides; 6 or 8 for a faceted look.</param>
    /// <returns>This kit.</returns>
    public Kit Cyl(Vector3 baseCenter, float radius, float height, Material mat, float topRadius = -1, int sides = 28)
    {
        CylinderMesh m = new CylinderMesh { BottomRadius = radius, TopRadius = topRadius < 0 ? radius : topRadius, Height = height, RadialSegments = sides, Rings = 1 };
        Add(m, mat, new Transform3D(Basis.Identity, baseCenter + new Vector3(0, height / 2, 0)));

        return this;
    }

    /// <summary>A cylinder from <paramref name="a"/> to <paramref name="b"/>: a pipe, strut or axle.</summary>
    /// <param name="a">One end.</param>
    /// <param name="b">The other end.</param>
    /// <param name="radius">Radius.</param>
    /// <param name="mat">Material.</param>
    /// <param name="sides">Number of sides.</param>
    /// <returns>This kit.</returns>
    public Kit Rod(Vector3 a, Vector3 b, float radius, Material mat, int sides = 14)
    {
        Vector3 d = b - a;
        float len = d.Length();
        if (len < 1e-4f)
        {
            return this;
        }

        Vector3 up = d / len;
        Vector3 side = Mathf.Abs(up.Dot(Vector3.Right)) < 0.9f ? up.Cross(Vector3.Right).Normalized() : up.Cross(Vector3.Forward).Normalized();
        Vector3 third = side.Cross(up);
        Basis basis = new Basis(third, up, side);
        CylinderMesh m = new CylinderMesh { BottomRadius = radius, TopRadius = radius, Height = len, RadialSegments = sides, Rings = 1 };
        Add(m, mat, new Transform3D(basis, (a + b) / 2));

        return this;
    }

    /// <summary>A sphere, or the upper half of one for a dome.</summary>
    /// <param name="center">Centre.</param>
    /// <param name="radius">Radius.</param>
    /// <param name="mat">Material.</param>
    /// <param name="dome">True for just the top half.</param>
    /// <returns>This kit.</returns>
    public Kit Ball(Vector3 center, float radius, Material mat, bool dome = false)
    {
        SphereMesh m = new SphereMesh { Radius = radius, Height = dome ? radius : radius * 2, IsHemisphere = dome, RadialSegments = 32, Rings = 16 };
        Add(m, mat, new Transform3D(Basis.Identity, center));

        return this;
    }

    /// <summary>A ring lying flat (its hole facing up) unless rotated.</summary>
    /// <param name="center">Centre.</param>
    /// <param name="inner">Inner radius.</param>
    /// <param name="outer">Outer radius.</param>
    /// <param name="mat">Material.</param>
    /// <param name="rotation">Optional rotation, e.g. to stand it upright.</param>
    /// <returns>This kit.</returns>
    public Kit Ring(Vector3 center, float inner, float outer, Material mat, Basis? rotation = null)
    {
        TorusMesh m = new TorusMesh { InnerRadius = inner, OuterRadius = outer, Rings = 32, RingSegments = 10 };
        Add(m, mat, new Transform3D(rotation ?? Basis.Identity, center));

        return this;
    }

    /// <summary>A row of rivets or bolts along a line, as small domes.</summary>
    /// <param name="from">First rivet.</param>
    /// <param name="to">Last rivet.</param>
    /// <param name="count">How many.</param>
    /// <param name="radius">Rivet radius.</param>
    /// <param name="mat">Material.</param>
    /// <returns>This kit.</returns>
    public Kit Rivets(Vector3 from, Vector3 to, int count, float radius, Material mat)
    {
        for (int i = 0; i < count; i++)
        {
            float f = count == 1 ? 0.5f : i / (float)(count - 1);
            Ball(from.Lerp(to, f), radius, mat);
        }

        return this;
    }
}
