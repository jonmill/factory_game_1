using Godot;

namespace Factory.Game.Art;

/// <summary>One building's 3D model and how to bake it.</summary>
/// <param name="Id">Building id it draws.</param>
/// <param name="W">Footprint width in tiles.</param>
/// <param name="H">Footprint depth in tiles.</param>
/// <param name="Height">Tallest point in tiles, for framing the camera.</param>
/// <param name="Build">Adds the parts; gets the kit and the building's accent colour.</param>
/// <param name="Frames">Animation frames; 1 for a still model.</param>
/// <param name="Fps">Animation speed while working.</param>
/// <param name="Turns">Whether it is baked in all four facings (built facing north).</param>
/// <param name="Glows">Whether it has emissive parts worth a glow layer.</param>
/// <param name="Anchor">Wire attachment point in model coordinates, for poles.</param>
/// <param name="Smoke">Chimney mouth in model coordinates, where smoke rises while working.</param>
/// <param name="Flicker">Whether its light flickers like fire.</param>
/// <param name="Reach">How far its light reaches past the footprint, in tiles.</param>
/// <param name="Puffs">Other things it gives off while working (steam, sparks, dust), in model coordinates.</param>
public sealed record ModelDef(string Id, int W, int H, float Height, System.Action<Kit, Color> Build, int Frames = 1, float Fps = 10, bool Turns = false, bool Glows = false, Vector3? Anchor = null, Vector3? Smoke = null, bool Flicker = false, float Reach = 1.2f, (Vector3 At, Emit Kind)[]? Puffs = null)
{
    /// <summary>Everything it gives off while working, smoke included.</summary>
    public (Vector3 At, Emit Kind)[] Emitters => [.. Smoke is { } s ? new[] { (s, Emit.Smoke) } : [], .. Puffs ?? []];
}

/// <summary>What a working machine gives off.</summary>
public enum Emit
{
    /// <summary>Grey-brown smoke from burning fuel, drifting downwind.</summary>
    Smoke,

    /// <summary>White steam, quick to rise and fade.</summary>
    Steam,

    /// <summary>Bright sparks that jump and fall.</summary>
    Sparks,

    /// <summary>Dust kicked up at ground level.</summary>
    Dust,

    /// <summary>A faint coloured vapour, for terraforming exhausts.</summary>
    Vapor,
}

/// <summary>
/// The building models. Shared rules keep them one family: everything stands on a foundation plate a little
/// inside its footprint; Salvage Age machines are squat brick and riveted iron with timber; Combustion Age
/// machines are painted plate on concrete with hazard trim; electric and later machines are bevelled white
/// ceramic on a dark base with a glowing accent band at the foot and a status lamp.
/// </summary>
public static partial class Models
{
    private static readonly Color _fire = new(1f, 0.5f, 0.15f);

    /// <summary>Every model.</summary>
    public static ModelDef[] All { get => field ??= [.. _first, .. More()]; }

    private static readonly ModelDef[] _first =
    [
        new("chest", 1, 1, 0.8f, Crate),
        new("power-pole", 1, 1, 2.0f, Pole, Anchor: new Vector3(0.5f, 1.86f, 0.5f)),
        new("burner-smelter", 2, 2, 2.0f, BurnerSmelter, Frames: 16, Fps: 12, Glows: true, Smoke: new Vector3(1.48f, 1.96f, 0.5f), Flicker: true, Reach: 1.6f),
        new("burner-drill", 2, 2, 1.8f, BurnerDrill, Frames: 16, Fps: 12, Turns: true, Glows: true, Smoke: new Vector3(1.4f, 1.25f, 1.45f), Flicker: true, Puffs: [(new Vector3(1f, 0.1f, -0.05f), Emit.Dust)]),
        new("steam-engine", 2, 2, 1.9f, SteamEngine, Frames: 16, Fps: 12, Glows: true, Smoke: new Vector3(0.45f, 1.9f, 0.8f), Flicker: true, Puffs: [(new Vector3(0.62f, 0.62f, 1.66f), Emit.Steam)]),
        new("lab", 2, 2, 1.6f, Lab, Frames: 16, Fps: 10, Glows: true),
        new("assembler", 3, 3, 1.5f, Fabricator, Frames: 16, Fps: 10, Glows: true),
        new("solar-panel", 2, 2, 0.9f, SolarPanel),
        new("lander", 3, 3, 2.2f, Lander, Frames: 16, Fps: 8, Glows: true),
    ];

    private static Basis Yaw(float a) => new(Vector3.Up, a);

    private static Basis Roll(float a) => new(Vector3.Back, a);

    private static Basis Pitch(float a) => new(Vector3.Right, a);

    /// <summary>
    /// What a heavy machine stands on: two sleepers across its width with anchor bolts, so the ground shows
    /// around and under it, or (for pads) a solid slab with a chipped edge.
    /// </summary>
    private static void Foundation(Kit k, int w, int h, Material mat, float inset = 0.06f, float thick = 0.08f, bool solid = false)
    {
        if (solid)
        {
            k.Bevel(inset, inset, w - (2 * inset), h - (2 * inset), thick, 0.12f, mat);

            return;
        }

        foreach (float z in new[] { inset + 0.12f, h - inset - 0.36f })
        {
            k.Slab(inset + 0.04f, z, w - (2 * inset) - 0.08f, 0.24f, thick, mat);
            foreach (float x in new[] { inset + 0.12f, w - inset - 0.12f })
            {
                k.Cyl(new Vector3(x, thick, z + 0.12f), 0.035f, 0.035f, Palette.Steel, -1, 8);
            }
        }
    }

    private static void Crate(Kit k, Color accent)
    {
        k.Slab(0.12f, 0.14f, 0.76f, 0.74f, 0.56f, Palette.Timber);
        k.Slab(0.15f, 0.17f, 0.7f, 0.68f, 0.05f, Palette.Timber, 0.56f);
        foreach ((float x, float z) in new[] { (0.1f, 0.12f), (0.82f, 0.12f), (0.1f, 0.82f), (0.82f, 0.82f) })
        {
            k.Slab(x, z, 0.08f, 0.08f, 0.6f, Palette.Iron);
        }

        k.Slab(0.1f, 0.84f, 0.8f, 0.04f, 0.05f, Palette.Iron, 0.5f);
        k.Box(new Vector3(0.5f, 0.3f, 0.885f), new Vector3(0.8f, 0.05f, 0.02f), Palette.Timber, Roll(0.58f));
        k.Box(new Vector3(0.5f, 0.46f, 0.89f), new Vector3(0.1f, 0.08f, 0.03f), Palette.Steel);
        _ = accent;
    }

    private static void Pole(Kit k, Color accent)
    {
        k.Cyl(new Vector3(0.5f, 0, 0.5f), 0.16f, 0.1f, Palette.Brick, 0.13f, 8);
        k.Cyl(new Vector3(0.5f, 0, 0.5f), 0.075f, 1.9f, Palette.Timber, 0.06f, 10);
        k.Box(new Vector3(0.5f, 1.74f, 0.5f), new Vector3(0.72f, 0.07f, 0.08f), Palette.Timber);
        k.Rod(new Vector3(0.5f, 1.5f, 0.5f), new Vector3(0.24f, 1.72f, 0.5f), 0.02f, Palette.DarkIron);
        k.Rod(new Vector3(0.5f, 1.5f, 0.5f), new Vector3(0.76f, 1.72f, 0.5f), 0.02f, Palette.DarkIron);
        foreach (float x in new[] { 0.2f, 0.5f, 0.8f })
        {
            k.Cyl(new Vector3(x, 1.775f, 0.5f), 0.04f, 0.09f, Palette.Paint(new Color(0.85f, 0.85f, 0.78f), 0f, 0.3f), 0.03f, 10);
        }

        _ = accent;
    }

    private static void BurnerSmelter(Kit k, Color accent)
    {
        Foundation(k, 2, 2, Palette.DarkIron, 0.05f, 0.1f);

        // A squat brick furnace with iron hoops, open at the top over the crucible.
        k.Bevel(0.18f, 0.22f, 1.64f, 1.52f, 1.2f, 0.26f, Palette.Brick, 0.1f);
        foreach (float y in new[] { 0.32f, 0.82f })
        {
            k.Bevel(0.15f, 0.19f, 1.7f, 1.58f, 0.06f, 0.28f, Palette.Iron, y);
        }

        k.Bevel(0.15f, 0.19f, 1.7f, 1.58f, 0.08f, 0.28f, Palette.DarkIron, 1.3f);
        k.Cyl(new Vector3(0.82f, 1.38f, 0.92f), 0.44f, 0.02f, Palette.Paint(new Color(0.08f, 0.05f, 0.04f), 0, 1), -1, 24);
        Embers(k, new Vector3(0.82f, 1.39f, 0.92f), 0.33f);
        for (int i = 0; i < 5; i++)
        {
            float a = (i * 1.3f) + 0.4f;
            Flame(k, new Vector3(0.82f + (Mathf.Cos(a) * 0.16f), 1.4f, 0.92f + (Mathf.Sin(a) * 0.12f)), 0.06f, 0.14f + (0.1f * Mathf.Sin((k.Spin * (i % 2 == 0 ? 2 : 3)) + (i * 1.7f))));
        }

        k.Light(new Vector3(0.82f, 1.75f, 0.92f), _fire, 1.6f, 1.6f);
        k.Light(new Vector3(1f, 0.45f, 2.15f), _fire, 1.6f, 1.8f);
        k.Ring(new Vector3(0.82f, 1.41f, 0.92f), 0.4f, 0.48f, Palette.DarkIron);

        // The fire door on the camera side: an iron frame, a dark mouth, and the fire inside.
        k.Box(new Vector3(1f, 0.55f, 1.76f), new Vector3(0.72f, 0.52f, 0.06f), Palette.DarkIron);
        k.Box(new Vector3(1f, 0.52f, 1.785f), new Vector3(0.54f, 0.36f, 0.04f), Palette.Paint(new Color(0.05f, 0.03f, 0.02f), 0, 1));
        k.Box(new Vector3(1f, 0.44f, 1.79f), new Vector3(0.46f, 0.18f, 0.04f), Palette.Fire(3f));
        k.Rivets(new Vector3(0.7f, 0.79f, 1.8f), new Vector3(1.3f, 0.79f, 1.8f), 5, 0.025f, Palette.Steel);

        // A chimney at the back right.
        k.Cyl(new Vector3(1.48f, 1.3f, 0.5f), 0.19f, 0.65f, Palette.Iron, 0.16f, 16);
        k.Ring(new Vector3(1.48f, 1.94f, 0.5f), 0.14f, 0.2f, Palette.DarkIron);
        _ = accent;
    }

    private static void BurnerDrill(Kit k, Color accent)
    {
        Foundation(k, 2, 2, Palette.DarkIron, 0.05f, 0.14f);

        // Output chute toward the north (the facing), sloping down off the frame.
        k.Box(new Vector3(1f, 0.42f, 0.14f), new Vector3(0.44f, 0.06f, 0.5f), Palette.Iron, Pitch(-0.45f));
        k.Slab(0.76f, -0.06f, 0.06f, 0.4f, 0.18f, Palette.DarkIron, 0.24f);
        k.Slab(1.18f, -0.06f, 0.06f, 0.4f, 0.18f, Palette.DarkIron, 0.24f);

        // The derrick: four legs leaning in over the bore, braced, with a crown block.
        Vector3 bore = new Vector3(1f, 0.14f, 0.8f);
        Vector3 crown = new Vector3(1f, 1.55f, 0.8f);
        foreach ((float dx, float dz) in new[] { (-0.5f, -0.42f), (0.5f, -0.42f), (-0.5f, 0.42f), (0.5f, 0.42f) })
        {
            Vector3 foot = bore + new Vector3(dx, 0, dz);
            k.Rod(foot, crown + new Vector3(dx * 0.25f, 0, dz * 0.25f), 0.04f, Palette.Iron, 8);
            k.Rod(foot + new Vector3(0, 0.5f, 0), new Vector3(bore.X, 0.64f, bore.Z) + new Vector3(dx * 0.7f, 0, -dz * 0.7f), 0.018f, Palette.DarkIron, 6);
        }

        k.Bevel(0.82f, 0.62f, 0.36f, 0.36f, 0.14f, 0.06f, Palette.DarkIron, 1.5f);

        // The drill string and its turning gear.
        k.Cyl(bore, 0.05f, 1.4f, Palette.Steel, -1, 10);
        Kit gear = k.Group(bore + new Vector3(0, 0.3f, 0), Yaw(k.Spin * 0.5f));
        gear.Cyl(Vector3.Zero, 0.3f, 0.1f, Palette.Steel, -1, 20);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            gear.Box(new Vector3(Mathf.Cos(a) * 0.32f, 0.05f, Mathf.Sin(a) * 0.32f), new Vector3(0.08f, 0.1f, 0.06f), Palette.Steel, Yaw(-a));
        }

        gear.Box(new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.04f, 0.06f), Palette.DarkIron);

        // The engine on the camera side: firebox with a glowing grate and a short stack.
        k.Bevel(0.3f, 1.22f, 1.4f, 0.62f, 0.56f, 0.1f, Palette.Iron, 0.14f);
        k.Rivets(new Vector3(0.38f, 0.62f, 1.85f), new Vector3(1.62f, 0.62f, 1.85f), 7, 0.022f, Palette.Steel);
        k.Box(new Vector3(0.7f, 0.38f, 1.845f), new Vector3(0.36f, 0.16f, 0.02f), Palette.Fire(2.8f));
        k.Light(new Vector3(0.7f, 0.35f, 2.1f), _fire, 1.3f, 1.4f);
        k.Cyl(new Vector3(1.4f, 0.7f, 1.45f), 0.11f, 0.55f, Palette.DarkIron, 0.09f, 12);

        // A rocking beam on the engine drives the gear: two strokes per turn of the loop.
        float rock = Mathf.Sin(k.Spin * 2) * 0.22f;
        k.Box(new Vector3(1.05f, 0.82f, 1.5f), new Vector3(0.7f, 0.05f, 0.08f), Palette.Steel, Roll(rock));
        k.Rod(new Vector3(1.05f - (Mathf.Cos(rock) * 0.32f), 0.82f - (Mathf.Sin(rock) * 0.32f), 1.5f), new Vector3(0.75f, 0.7f, 1.5f), 0.025f, Palette.Steel, 6);
        _ = accent;
    }

    private static void SteamEngine(Kit k, Color accent)
    {
        Foundation(k, 2, 2, Palette.DarkIron, 0.05f, 0.1f);

        // Saddles and a horizontal boiler with riveted bands.
        foreach (float x in new[] { 0.45f, 1.45f })
        {
            k.Slab(x - 0.1f, 0.35f, 0.2f, 0.9f, 0.32f, Palette.Iron, 0.1f);
        }

        k.Rod(new Vector3(0.15f, 0.78f, 0.8f), new Vector3(1.75f, 0.78f, 0.8f), 0.44f, Palette.Iron, 30);
        foreach (float x in new[] { 0.3f, 0.75f, 1.2f, 1.62f })
        {
            k.Ring(new Vector3(x, 0.78f, 0.8f), 0.43f, 0.47f, Palette.DarkIron, Roll(Mathf.Pi / 2));
        }

        k.Ball(new Vector3(1.75f, 0.78f, 0.8f), 0.44f, Palette.Iron);
        k.Cyl(new Vector3(0.45f, 1.1f, 0.8f), 0.13f, 0.75f, Palette.DarkIron, 0.11f, 14);
        k.Ring(new Vector3(0.45f, 1.85f, 0.8f), 0.1f, 0.15f, Palette.DarkIron);
        k.Ball(new Vector3(1.15f, 1.24f, 0.8f), 0.09f, Palette.Copper);
        k.Rod(new Vector3(1.15f, 1.15f, 0.8f), new Vector3(1.15f, 1.3f, 0.8f), 0.03f, Palette.Copper);

        // Firebox glow under the boiler's front end.
        k.Box(new Vector3(0.45f, 0.3f, 1.26f), new Vector3(0.32f, 0.12f, 0.02f), Palette.Fire(2.8f));
        k.Light(new Vector3(0.45f, 0.3f, 1.5f), _fire, 1.2f, 1.3f);

        // A flyball governor on the dome, spinning three times as fast as the flywheel.
        Kit governor = k.Group(new Vector3(1.15f, 1.34f, 0.8f), Yaw(k.Spin * 3));
        governor.Rod(Vector3.Zero, new Vector3(0, 0.18f, 0), 0.015f, Palette.Steel, 6);
        foreach (float side in new[] { -1f, 1f })
        {
            governor.Rod(new Vector3(0, 0.16f, 0), new Vector3(side * 0.1f, 0.06f, 0), 0.01f, Palette.Steel, 4);
            governor.Ball(new Vector3(side * 0.1f, 0.06f, 0), 0.03f, Palette.Copper);
        }

        // Flywheel facing the camera, with a crank and piston rod.
        Vector3 hub = new Vector3(1.38f, 0.62f, 1.6f);
        Kit wheel = k.Group(hub, Roll(k.Spin));
        wheel.Ring(Vector3.Zero, 0.36f, 0.44f, Palette.DarkIron, Pitch(Mathf.Pi / 2));
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Tau / 6;
            wheel.Rod(Vector3.Zero, new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a) * 0.38f, 0), 0.025f, Palette.Iron, 6);
        }

        wheel.Cyl(new Vector3(0, 0, -0.04f), 0.07f, 0.04f, Palette.Steel);
        wheel.Ball(Vector3.Zero, 0.07f, Palette.Steel);
        Vector3 crank = hub + new Vector3(Mathf.Cos(k.Spin) * 0.2f, Mathf.Sin(-k.Spin) * 0.2f, 0.06f);
        k.Rod(crank, new Vector3(0.6f, 0.62f, 1.66f), 0.03f, Palette.Steel);
        k.Rod(new Vector3(0.35f, 0.62f, 1.66f), new Vector3(0.75f, 0.62f, 1.66f), 0.07f, Palette.Iron);
        _ = accent;
    }

    /// <summary>
    /// The shared high-tech body: a bevelled hull on four feet and two skids, raised a little off the ground
    /// so it sits on the planet rather than on a plinth. A dark band runs round its foot, a service hatch in
    /// the machine's colour faces the camera, and a status lamp sits at the front-left. Its top is at
    /// 0.195 + <paramref name="bodyHeight"/>.
    /// </summary>
    private static void TechBody(Kit k, int w, int h, float bodyHeight, Color accent)
    {
        // Three families of base, picked by the machine's colour and size, so a row of machines doesn't
        // stand on one identical kit: splayed feet, skid rails, or squat hydraulic jacks.
        int family = (int)((accent.ToRgba32() / 7) + (uint)(w * 5) + (uint)h) % 3;
        Vector3 mid = new Vector3(w / 2f, 0, h / 2f);
        (float X, float Z)[] corners = [(0.16f, 0.16f), (w - 0.16f, 0.16f), (0.16f, h - 0.16f), (w - 0.16f, h - 0.16f)];
        switch (family)
        {
            case 0:
                foreach ((float x, float z) in corners)
                {
                    Vector3 pad = new Vector3(x, 0, z);
                    Vector3 toMid = (mid - pad).Normalized();
                    k.Cyl(pad, 0.13f, 0.05f, Palette.HullDark, 0.1f, 10);
                    k.Rod(pad + new Vector3(0, 0.04f, 0), pad + (toMid * 0.32f) + new Vector3(0, 0.3f, 0), 0.04f, Palette.Steel, 8);
                    k.Rivets(pad + new Vector3(-0.07f, 0.05f, 0), pad + new Vector3(0.07f, 0.05f, 0), 2, 0.018f, Palette.Steel);
                }

                break;
            case 1:
                // Two steel skid rails the length of the machine, with tie bars and towing eyes.
                foreach (float x in new[] { 0.18f, w - 0.36f })
                {
                    k.Slab(x, 0.1f, 0.18f, h - 0.2f, 0.1f, Palette.DarkIron);
                    k.Ring(new Vector3(x + 0.09f, 0.06f, 0.08f), 0.03f, 0.05f, Palette.Steel);
                    k.Ring(new Vector3(x + 0.09f, 0.06f, h - 0.08f), 0.03f, 0.05f, Palette.Steel);
                }

                for (float z = 0.45f; z < h - 0.3f; z += 0.7f)
                {
                    k.Slab(0.3f, z, w - 0.6f, 0.08f, 0.06f, Palette.DarkIron, 0.04f);
                }

                break;
            default:
                // Squat jacks: a footplate, a cylinder and a polished ram at each corner.
                foreach ((float x, float z) in corners)
                {
                    Vector3 pad = new Vector3(x + (x < 1 ? 0.06f : -0.06f), 0, z + (z < 1 ? 0.06f : -0.06f));
                    k.Box(pad + new Vector3(0, 0.02f, 0), new Vector3(0.22f, 0.04f, 0.22f), Palette.DarkIron);
                    k.Cyl(pad + new Vector3(0, 0.04f, 0), 0.06f, 0.1f, Palette.Yellow, -1, 10);
                    k.Cyl(pad + new Vector3(0, 0.14f, 0), 0.035f, 0.12f, Palette.Steel, -1, 10);
                }

                break;
        }

        foreach (float x in new[] { 0.42f, w - 0.58f })
        {
            k.Slab(x, 0.42f, 0.16f, h - 0.84f, 0.12f, Palette.HullDark);
        }

        k.Bevel(0.32f, 0.34f, w - 0.64f, h - 0.68f, 0.11f, 0.22f, Palette.HullDark, 0.12f);
        k.Bevel(0.28f, 0.3f, w - 0.56f, h - 0.6f, bodyHeight - 0.035f, 0.28f, Palette.Hull, 0.23f);

        Dress(k, w, h, 0.195f + bodyHeight, accent);

        // Service hatch and status lamp on the camera side.
        k.Box(new Vector3(w - 0.8f, 0.2f + (bodyHeight * 0.45f), h - 0.29f), new Vector3(0.34f, Mathf.Min(0.3f, bodyHeight * 0.55f), 0.03f), Palette.Paint(Muted(accent), 0.3f, 0.5f));
        k.Ball(new Vector3(0.52f, 0.2f + bodyHeight, h - 0.52f), 0.045f, Palette.Glow(new Color(0.4f, 1f, 0.5f), ((int)(k.Phase * 16) % 5) < 3 ? 3f : 0.8f));

        // Work lights under the hull wash the ground round the feet in the machine's colour.
        k.Light(new Vector3(w / 2f, 0.1f, h - 0.35f), accent.Lerp(Colors.White, 0.4f), 1.1f, 0.5f);
    }

    /// <summary>
    /// The fittings every high-tech body carries, so none reads as a bare block: a coolant pipe along the west
    /// side and a cable loom on the east with a junction box, a vent and a rivet row on the camera face, a
    /// ladder up the east side, and a strip of roof fittings along the back and right edges. The arrangement
    /// is seeded by the machine's colour, so each machine's is its own.
    /// </summary>
    private static void Dress(Kit k, int w, int h, float top, Color accent)
    {
        int seed = (int)(accent.ToRgba32() % 9973) + (w * 31) + h;
        float mid = 0.23f + ((top - 0.23f) * 0.5f);

        // West: a pipe on stand-off brackets, dropping to the ground at the back.
        float px = 0.22f;
        k.Pipe(0.035f, Palette.Steel, new Vector3(px, 0.05f, 0.5f), new Vector3(px, mid, 0.5f), new Vector3(px, mid, h - 0.55f), new Vector3(px + 0.12f, mid, h - 0.55f));
        for (float z = 0.8f; z < h - 0.6f; z += 0.5f)
        {
            k.Box(new Vector3(px + 0.04f, mid, z), new Vector3(0.08f, 0.03f, 0.04f), Palette.HullDark);
        }

        k.Valve(new Vector3(px, mid + 0.03f, (h / 2f) + 0.1f), Palette.Oxide);

        // East: a cable loom running into a junction box, and a ladder to the roof.
        float ex = w - 0.24f;
        k.Slab(ex - 0.06f, h * 0.35f, 0.12f, 0.3f, 0.26f, Palette.HullDark, mid - 0.12f);
        k.Box(new Vector3(ex + 0.005f, mid + 0.02f, (h * 0.35f) + 0.15f), new Vector3(0.02f, 0.12f, 0.18f), Palette.Paint(Muted(accent), 0.3f, 0.5f));
        k.Cable(new Vector3(ex, mid - 0.1f, (h * 0.35f) + 0.05f), new Vector3(w - 0.12f, 0.02f, 0.3f), 0.08f, Palette.Rubber);
        k.Cable(new Vector3(ex, mid - 0.1f, (h * 0.35f) + 0.25f), new Vector3(w - 0.1f, 0.02f, h - 0.2f), 0.1f, Palette.Rubber);
        k.Ladder(new Vector3(ex + 0.02f, 0.05f, h - 0.7f), new Vector3(ex + 0.02f, top + 0.05f, h - 0.7f), Vector3.Right, Palette.Steel);

        // Camera face: a vent and a rivet row under the roof line.
        k.Vent(new Vector3(0.75f, mid, h - 0.29f), 0.4f, Mathf.Min(0.2f, (top - 0.25f) * 0.6f), Vector3.Back, Palette.Steel);
        k.Rivets(new Vector3(0.45f, top - 0.04f, h - 0.295f), new Vector3(w - 0.45f, top - 0.04f, h - 0.295f), (int)(w * 7), 0.012f, Palette.Steel);

        // Roof: fittings along the back edge and down the right-hand side.
        k.Clutter(0.4f, 0.36f, w - 0.8f, 0.2f, top, seed, 3 + w, Palette.HullDark);
        k.Clutter(w - 0.55f, 0.6f, 0.2f, h - 1.1f, top, seed + 1, 1 + h, Palette.HullDark);
    }

    /// <summary>A bed of burning fuel: a dark rim, a red-orange layer and a near-white core, hottest in the middle.</summary>
    private static void Embers(Kit k, Vector3 at, float radius)
    {
        k.Cyl(at, radius, 0.02f, Palette.Fire(2.2f), -1, 24);
        k.Cyl(at + new Vector3(0, 0.005f, 0), radius * 0.62f, 0.02f, Palette.Glow(new Color(1f, 0.55f, 0.15f), 3f), -1, 20);
        k.Cyl(at + new Vector3(0, 0.01f, 0), radius * 0.3f, 0.02f, Palette.Glow(new Color(1f, 0.88f, 0.6f), 4f), -1, 16);
    }

    /// <summary>A tongue of flame: an orange cone with a yellow-white inner cone, <paramref name="height"/> tall.</summary>
    private static void Flame(Kit k, Vector3 at, float radius, float height)
    {
        k.Cyl(at, radius, height, Palette.Glow(new Color(1f, 0.45f, 0.1f), 2.6f), 0f, 10);
        k.Cyl(at, radius * 0.5f, height * 0.6f, Palette.Glow(new Color(1f, 0.85f, 0.5f), 3.6f), 0f, 8);
    }

    /// <summary>A building's colour as paint: darker and less saturated, so it reads as material, not as a label.</summary>
    private static Color Muted(Color c) => Color.FromHsv(c.H, c.S * 0.55f, c.V * 0.55f);

    private static void Lab(Kit k, Color accent)
    {
        TechBody(k, 2, 2, 0.45f, accent);
        Vector3 c = new Vector3(1f, 0.645f, 0.95f);
        k.Cyl(c, 0.68f, 0.08f, Palette.HullDark, 0.64f, 32);

        // A turning ring of light inside a glass dome.
        Kit core = k.Group(c + new Vector3(0, 0.32f, 0), Yaw(k.Spin));
        core.Ring(Vector3.Zero, 0.3f, 0.34f, Palette.Glow(accent, 2.5f), Roll(0.5f));
        core.Ball(Vector3.Zero, 0.08f + (0.03f * Mathf.Sin(k.Spin * 3)), Palette.Glow(accent.Lightened(0.4f), 3f));
        k.Light(c + new Vector3(0, 0.35f, 0), accent, 1.4f, 1.2f);
        k.Ball(c + new Vector3(0, 0.08f, 0), 0.6f, Palette.ClearGlass, true);

        // Sample vials at the corners.
        foreach ((float x, float z) in new[] { (0.36f, 0.38f), (1.64f, 0.38f), (1.64f, 1.56f) })
        {
            k.Cyl(new Vector3(x, 0.645f, z), 0.07f, 0.2f, Palette.Glass, -1, 12);
            k.Cyl(new Vector3(x, 0.645f, z), 0.06f, 0.12f, Palette.Glow(accent, 1.5f), -1, 12);
        }
    }

    private static void Fabricator(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.62f, accent);
        float top = 0.815f;

        // A recessed work bay with a glass floor, and a gantry turning over it.
        k.Bevel(0.55f, 0.5f, 1.9f, 1.86f, 0.04f, 0.4f, Palette.HullDark, top);
        Vector3 c = new Vector3(1.5f, top + 0.04f, 1.43f);
        k.Cyl(c, 0.78f, 0.03f, Palette.Glass, -1, 40);
        k.Ring(c + new Vector3(0, 0.03f, 0), 0.72f, 0.78f, Palette.Glow(accent, 2f));
        k.Light(c + new Vector3(0, 0.5f, 0), accent, 1.6f, 1.3f);
        Kit gantry = k.Group(c + new Vector3(0, 0.05f, 0), Yaw(k.Spin));
        gantry.Cyl(Vector3.Zero, 0.14f, 0.36f, Palette.HullDark, 0.1f, 16);
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 elbow = new Vector3(side * 0.42f, 0.36f + (0.06f * Mathf.Sin((k.Spin * 3) + side)), 0);
            gantry.Rod(new Vector3(0, 0.32f, 0), elbow, 0.04f, Palette.Hull, 10);
            gantry.Rod(elbow, elbow + new Vector3(side * 0.12f, -0.26f, 0), 0.03f, Palette.Hull, 10);
            gantry.Ball(elbow, 0.055f, Palette.HullDark);
            gantry.Ball(elbow + new Vector3(side * 0.12f, -0.28f, 0), 0.035f, Palette.Glow(accent, 3f));
        }

        // Corner columns carrying a rail frame over the bay, with cable runs down the sides.
        foreach ((float x, float z) in new[] { (0.42f, 0.42f), (2.58f, 0.42f), (0.42f, 2.42f), (2.58f, 2.42f) })
        {
            k.Bevel(x - 0.14f, z - 0.14f, 0.28f, 0.28f, 0.42f, 0.06f, Palette.HullDark, top);
            k.Ball(new Vector3(x, top + 0.45f, z), 0.06f, Palette.Glow(accent, 1.6f));
        }

        k.Slab(0.36f, 0.36f, 2.28f, 0.12f, 0.08f, Palette.Steel, top + 0.42f);
        k.Slab(0.36f, 2.36f, 2.28f, 0.12f, 0.08f, Palette.Steel, top + 0.42f);
        k.Rod(new Vector3(0.18f, 0.3f, 0.6f), new Vector3(0.18f, top, 0.6f), 0.035f, Palette.Rubber, 8);
        k.Rod(new Vector3(2.82f, 0.3f, 0.6f), new Vector3(2.82f, top, 0.6f), 0.035f, Palette.Rubber, 8);

        // Ports on the west and east sides, and vents along the front.
        foreach (float x in new[] { 0.06f, 2.82f })
        {
            k.Box(new Vector3(x + 0.06f, 0.45f, 1.5f), new Vector3(0.14f, 0.3f, 0.5f), Palette.HullDark);
            k.Box(new Vector3(x + 0.06f, 0.45f, 1.5f), new Vector3(0.16f, 0.12f, 0.3f), Palette.Glass);
        }

        for (int i = 0; i < 5; i++)
        {
            k.Box(new Vector3(1.9f + (i * 0.12f), 0.5f, 2.7f), new Vector3(0.05f, 0.3f, 0.02f), Palette.HullDark);
        }
    }

    private static void SolarPanel(Kit k, Color accent)
    {
        Foundation(k, 2, 2, Palette.HullDark, 0.12f, 0.05f);
        foreach ((float x, float z) in new[] { (0.4f, 0.45f), (1.6f, 0.45f), (0.4f, 1.55f), (1.6f, 1.55f) })
        {
            k.Rod(new Vector3(x, 0.05f, z), new Vector3(x, z < 1 ? 0.95f : 0.42f, z), 0.04f, Palette.Steel, 8);
        }

        // Four modules on a frame, tilted gently south, with a junction box underneath.
        Basis tilt = Pitch(0.28f);
        k.Box(new Vector3(1f, 0.62f, 1f), new Vector3(1.86f, 0.05f, 1.78f), Palette.HullDark, tilt);
        foreach ((float x, float z) in new[] { (-0.45f, -0.43f), (0.45f, -0.43f), (-0.45f, 0.43f), (0.45f, 0.43f) })
        {
            k.Box(new Vector3(1f, 0.62f, 1f) + (tilt * new Vector3(x, 0.035f, z)), new Vector3(0.84f, 0.02f, 0.8f), Palette.Cells, tilt);
        }

        k.Box(new Vector3(1f, 0.62f, 1f) + (tilt * new Vector3(0, 0.03f, 0.87f)), new Vector3(1.8f, 0.03f, 0.04f), Palette.Glow(accent, 1.2f), tilt);
        k.Box(new Vector3(1f, 0.35f, 1f), new Vector3(0.3f, 0.2f, 0.2f), Palette.HullDark);
    }

    private static void Lander(Kit k, Color accent)
    {
        Vector3 c = new Vector3(1.5f, 0, 1.5f);

        // Four landing legs with pads.
        foreach ((float dx, float dz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            Vector3 pad = c + new Vector3(dx * 1.22f, 0, dz * 1.18f);
            k.Cyl(pad, 0.2f, 0.06f, Palette.HullDark, 0.16f, 16);
            k.Rod(pad + new Vector3(0, 0.05f, 0), c + new Vector3(dx * 0.7f, 0.85f, dz * 0.7f), 0.06f, Palette.Steel, 10);
            k.Rod(pad + new Vector3(0, 0.05f, 0), c + new Vector3(dx * 0.85f, 0.35f, dz * 0.85f), 0.035f, Palette.HullDark, 8);
        }

        // An octagonal hull, a darker upper ring, a glass dome, and lamps.
        k.Cyl(c + new Vector3(0, 0.3f, 0), 1.02f, 0.85f, Palette.Hull, 0.92f, 8);
        k.Cyl(c + new Vector3(0, 0.28f, 0), 0.96f, 0.06f, Palette.Glow(accent, 1.8f), 0.96f, 8);
        k.Cyl(c + new Vector3(0, 1.15f, 0), 0.86f, 0.32f, Palette.HullDark, 0.62f, 8);
        k.Ball(c + new Vector3(0, 1.47f, 0), 0.48f, Palette.Glass, true);
        k.Ring(c + new Vector3(0, 1.48f, 0), 0.46f, 0.52f, Palette.Glow(accent, 2f));
        for (int i = 0; i < 4; i++)
        {
            float a = (i * Mathf.Pi / 2) + (Mathf.Pi / 4);
            bool on = ((int)(k.Phase * 8) + i) % 4 == 0;
            k.Ball(c + new Vector3(Mathf.Cos(a) * 0.86f, 1.18f, Mathf.Sin(a) * 0.86f), 0.06f, Palette.Glow(new Color(1f, 0.3f, 0.25f), on ? 3f : 0.6f));
        }

        // A cargo hatch on the camera side.
        k.Box(c + new Vector3(0, 0.7f, 0.96f), new Vector3(0.6f, 0.5f, 0.05f), Palette.HullDark);
        k.Box(c + new Vector3(0, 0.62f, 0.99f), new Vector3(0.5f, 0.06f, 0.02f), Palette.Hazard);
    }
}
