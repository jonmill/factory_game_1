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

    /// <summary>
    /// The light a working machine gives off, on black: its glowing parts at full strength plus what its lamps
    /// and fires light on its own surfaces and the ground around it. Drawn additively while it works.
    /// </summary>
    Light,
}

/// <summary>
/// Builds a building's 3D model from primitives. Coordinates are in tiles: x runs east, y up, z south, with
/// the origin at the footprint's north-west corner on the ground. The same model code runs once per pass and
/// animation frame; the kit handles what each pass needs (shadow casters only, glow-only colours).
/// </summary>
public sealed class Kit
{
    private static readonly StandardMaterial3D _white = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.White, CullMode = BaseMaterial3D.CullModeEnum.Disabled };

    private readonly Node3D _root;
    private readonly Transform3D _toModel;

    /// <summary>Creates a kit that adds parts under <paramref name="root"/>.</summary>
    /// <param name="root">Parent node for every part.</param>
    /// <param name="pass">The pass being rendered.</param>
    /// <param name="phase">Animation phase, 0 to 1, for moving parts.</param>
    public Kit(Node3D root, BakePass pass, float phase)
        : this(root, pass, phase, Transform3D.Identity, [])
    {
    }

    private Kit(Node3D root, BakePass pass, float phase, Transform3D toModel, List<PointLight> lights)
    {
        _root = root;
        Pass = pass;
        Phase = phase;
        _toModel = toModel;
        Lights = lights;
    }

    /// <summary>A lamp or fire's light, in model coordinates.</summary>
    /// <param name="At">Where it shines from.</param>
    /// <param name="Color">Its colour.</param>
    /// <param name="Range">How far it reaches, in tiles.</param>
    /// <param name="Energy">Its strength.</param>
    /// <param name="Shadows">Whether it casts shadows (the main fires and lamps do; small glowing trim doesn't).</param>
    public readonly record struct PointLight(Vector3 At, Color Color, float Range, float Energy, bool Shadows);

    /// <summary>Every light the model has added, for the light pass.</summary>
    public List<PointLight> Lights { get; }

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
        Transform3D xf = new Transform3D(rotation, pivot);
        Node3D n = new Node3D { Transform = xf };
        _root.AddChild(n);

        return new Kit(n, Pass, Phase, _toModel * xf, Lights);
    }

    /// <summary>A light that, while the machine works, falls on its own surfaces and the ground around it.</summary>
    /// <param name="at">Where it shines from.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="range">How far it reaches, in tiles.</param>
    /// <param name="energy">Its strength.</param>
    /// <returns>This kit.</returns>
    public Kit Light(Vector3 at, Color color, float range = 1.6f, float energy = 1.5f)
    {
        Lights.Add(new PointLight(_toModel * at, color, range, energy, true));

        return this;
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
            case BakePass.Light:
                // Lit only by the machine's own lights; glowing parts at full strength, each also spilling a
                // little light of its colour onto what's around it.
                mi.MaterialOverride = mat;
                if (glow && (mesh.GetAabb().Size.Length() > 0.16f))
                {
                    StandardMaterial3D g = (StandardMaterial3D)mat;
                    Aabb box = mesh.GetAabb();
                    float reach = Mathf.Clamp(box.Size.Length() * 1.6f, 0.5f, 2f);
                    Lights.Add(new PointLight(_toModel * (xf * box.GetCenter()), g.Emission, reach, Mathf.Min(1.2f, g.EmissionEnergyMultiplier * 0.3f), false));
                }

                break;
            default:
                // Idle, glowing parts are only faintly warm; the light layer brings them up while working.
                mi.MaterialOverride = glow ? Palette.Dim((StandardMaterial3D)mat) : mat;
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
        float least = Mathf.Min(size.X, Mathf.Min(size.Y, size.Z));
        Add(Meshes.Box(size, Mathf.Min(0.025f, least * 0.2f)), mat, new Transform3D(rotation ?? Basis.Identity, center));

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
        if (height <= 0.0001f)
        {
            return this;
        }

        float top = topRadius < 0 ? radius : topRadius;
        Add(Meshes.Cylinder(radius, top, height, sides, Mathf.Min(0.02f, Mathf.Min(radius, height) * 0.2f)), mat, new Transform3D(Basis.Identity, baseCenter));

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
        Add(Meshes.Cylinder(radius, radius, len, sides, Mathf.Min(0.012f, radius * 0.25f)), mat, new Transform3D(basis, a));

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
            Ball(from.Lerp(to, f), Mathf.Max(radius, 0.022f), mat);
        }

        return this;
    }

    /// <summary>A pipe run through <paramref name="points"/>, with a bolted flange at each end and an elbow at each bend.</summary>
    /// <param name="radius">Pipe radius.</param>
    /// <param name="mat">Material.</param>
    /// <param name="points">The run, in order.</param>
    /// <returns>This kit.</returns>
    public Kit Pipe(float radius, Material mat, params Vector3[] points)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            Rod(points[i], points[i + 1], radius, mat, 12);
        }

        for (int i = 1; i < points.Length - 1; i++)
        {
            Ball(points[i], radius * 1.08f, mat);
        }

        Flange(points[0], points[1] - points[0], radius, mat);
        Flange(points[^1], points[^2] - points[^1], radius, mat);

        return this;
    }

    /// <summary>A bolted flange collar on a pipe end, facing along <paramref name="dir"/>.</summary>
    private void Flange(Vector3 at, Vector3 dir, float radius, Material mat)
    {
        Vector3 d = dir.Normalized();
        Rod(at, at + (d * radius * 0.5f), radius * 1.45f, mat, 12);
    }

    /// <summary>A louvred vent on a face: slats across a dark recess. <paramref name="facing"/> is the face's outward direction (horizontal).</summary>
    /// <param name="center">Centre of the vent on the face.</param>
    /// <param name="width">Width along the face.</param>
    /// <param name="height">Height.</param>
    /// <param name="facing">Outward direction of the face.</param>
    /// <param name="frame">Frame and slat material.</param>
    /// <returns>This kit.</returns>
    public Kit Vent(Vector3 center, float width, float height, Vector3 facing, Material frame)
    {
        Vector3 f = facing.Normalized();
        Basis turn = Basis.LookingAt(-f, Vector3.Up);
        Box(center, new Vector3(width, height, 0.02f), Palette.Paint(new Color(0.05f, 0.045f, 0.04f), 0, 1), turn);
        int slats = Mathf.Max(2, (int)(height / 0.05f));
        for (int i = 0; i < slats; i++)
        {
            float y = (-height / 2) + ((i + 0.5f) * height / slats);
            Box(center + (turn * new Vector3(0, y, -0.012f)), new Vector3(width * 0.94f, 0.012f, 0.02f), frame, turn * new Basis(Vector3.Right, 0.5f));
        }

        Box(center + (turn * new Vector3(0, height / 2, -0.01f)), new Vector3(width + 0.03f, 0.02f, 0.025f), frame, turn);
        Box(center + (turn * new Vector3(0, -height / 2, -0.01f)), new Vector3(width + 0.03f, 0.02f, 0.025f), frame, turn);

        return this;
    }

    /// <summary>A ring of bolt heads lying flat, as round a hatch or a tank lid.</summary>
    /// <param name="center">Centre.</param>
    /// <param name="radius">Ring radius.</param>
    /// <param name="count">Number of bolts.</param>
    /// <param name="mat">Material.</param>
    /// <returns>This kit.</returns>
    public Kit BoltCircle(Vector3 center, float radius, int count, Material mat)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.Tau / count;
            Cyl(center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), 0.024f, 0.018f, mat, -1, 6);
        }

        return this;
    }

    /// <summary>A ladder from a foot to a top, standing off a wall facing <paramref name="facing"/>.</summary>
    /// <param name="foot">Bottom centre.</param>
    /// <param name="top">Top centre.</param>
    /// <param name="facing">Outward direction of the wall.</param>
    /// <param name="mat">Material.</param>
    /// <returns>This kit.</returns>
    public Kit Ladder(Vector3 foot, Vector3 top, Vector3 facing, Material mat)
    {
        Vector3 side = Vector3.Up.Cross(facing.Normalized()) * 0.06f;
        Rod(foot - side, top - side, 0.01f, mat, 5);
        Rod(foot + side, top + side, 0.01f, mat, 5);
        float len = foot.DistanceTo(top);
        for (float f = 0.08f; f < len; f += 0.09f)
        {
            Vector3 at = foot.Lerp(top, f / len);
            Rod(at - side, at + side, 0.007f, mat, 4);
        }

        return this;
    }

    /// <summary>A hanging cable from <paramref name="a"/> to <paramref name="b"/>, sagging by <paramref name="sag"/>.</summary>
    /// <param name="a">One end.</param>
    /// <param name="b">The other end.</param>
    /// <param name="sag">How far the middle hangs below the straight line.</param>
    /// <param name="mat">Material.</param>
    /// <returns>This kit.</returns>
    public Kit Cable(Vector3 a, Vector3 b, float sag, Material mat)
    {
        Vector3 prev = a;
        for (int i = 1; i <= 6; i++)
        {
            float f = i / 6f;
            Vector3 p = a.Lerp(b, f) - new Vector3(0, sag * 4 * f * (1 - f), 0);
            Rod(prev, p, 0.014f, mat, 6);
            prev = p;
        }

        return this;
    }

    /// <summary>A handwheel valve on a short stem, standing up from <paramref name="at"/>.</summary>
    /// <param name="at">Base of the stem.</param>
    /// <param name="mat">Wheel material.</param>
    /// <returns>This kit.</returns>
    public Kit Valve(Vector3 at, Material mat)
    {
        Cyl(at, 0.025f, 0.08f, Palette.DarkIron, -1, 8);
        Ring(at + new Vector3(0, 0.09f, 0), 0.04f, 0.055f, mat);
        Rod(at + new Vector3(-0.045f, 0.09f, 0), at + new Vector3(0.045f, 0.09f, 0), 0.007f, mat, 4);

        return this;
    }

    /// <summary>A small dial gauge on a stalk, its face toward the camera.</summary>
    /// <param name="at">Base of the stalk.</param>
    /// <returns>This kit.</returns>
    public Kit Gauge(Vector3 at)
    {
        Rod(at, at + new Vector3(0, 0.1f, 0), 0.008f, Palette.DarkIron, 5);
        Basis face = new Basis(Vector3.Right, (Mathf.Pi / 2) - 0.5f);
        Add(Meshes.Cylinder(0.04f, 0.04f, 0.02f, 14, 0.006f), Palette.DarkIron, new Transform3D(face, at + new Vector3(0, 0.13f, 0)));
        Add(Meshes.Cylinder(0.032f, 0.032f, 0.005f, 14, 0), Palette.Paint(new Color(0.85f, 0.82f, 0.72f), 0, 0.4f), new Transform3D(face, at + new Vector3(0, 0.13f, 0) + (face * new Vector3(0, 0.02f, 0))));

        return this;
    }

    /// <summary>
    /// Scatters small fittings over a flat roof area, seeded so a model always gets the same ones: bolt
    /// rows, inspection hatches, junction boxes, small vents, pipe stubs and valves. Fills the empty tops
    /// that would otherwise read as blank slabs.
    /// </summary>
    /// <param name="x">West edge of the area.</param>
    /// <param name="z">North edge.</param>
    /// <param name="w">Width.</param>
    /// <param name="d">Depth.</param>
    /// <param name="y">Roof height.</param>
    /// <param name="seed">Seed for the arrangement.</param>
    /// <param name="count">Roughly how many fittings.</param>
    /// <param name="mat">Main material for boxes and hatches.</param>
    /// <returns>This kit.</returns>
    public Kit Clutter(float x, float z, float w, float d, float y, int seed, int count, Material mat)
    {
        uint h = (uint)(seed * 2654435761u);
        float Next()
        {
            h ^= h << 13;
            h ^= h >> 17;
            h ^= h << 5;

            return (h & 0xffff) / 65535f;
        }

        for (int i = 0; i < count; i++)
        {
            float px = x + (Next() * w), pz = z + (Next() * d);
            switch ((int)(Next() * 6))
            {
                case 0:
                    float bw = 0.12f + (Next() * 0.14f), bd = 0.1f + (Next() * 0.12f);
                    Slab(px - (bw / 2), pz - (bd / 2), bw, bd, 0.06f + (Next() * 0.08f), mat, y);
                    break;
                case 1:
                    Cyl(new Vector3(px, y, pz), 0.09f, 0.025f, Palette.HullDark, -1, 16);
                    BoltCircle(new Vector3(px, y + 0.025f, pz), 0.07f, 6, Palette.Steel);
                    break;
                case 2:
                    Vent(new Vector3(px, y + 0.05f, pz), 0.16f, 0.09f, Next() > 0.5f ? Vector3.Back : Vector3.Right, Palette.Steel);
                    Slab(px - 0.09f, pz - 0.05f, 0.18f, 0.1f, 0.1f, mat, y);
                    break;
                case 3:
                    Pipe(0.022f, Palette.Steel, new Vector3(px, y, pz), new Vector3(px, y + 0.12f, pz), new Vector3(px + 0.12f, y + 0.12f, pz));
                    break;
                case 4:
                    Valve(new Vector3(px, y, pz), Palette.Oxide);
                    break;
                default:
                    Rivets(new Vector3(px, y, pz), new Vector3(px + 0.25f, y, pz), 5, 0.014f, Palette.Steel);
                    break;
            }
        }

        return this;
    }
}
