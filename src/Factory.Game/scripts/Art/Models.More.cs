using Godot;

namespace Factory.Game.Art;

/// <summary>The rest of the building models, by age.</summary>
public static partial class Models
{
    private static readonly Color _plasma = new(0.78f, 0.45f, 1f);

    private static ModelDef[] More() =>
    [
        // Salvage Age.
        new("cell-bank", 2, 2, 1.0f, CellBank, Glows: true),
        new("water-tower", 1, 1, 2.6f, WaterTower),

        // Combustion Age.
        new("kerogen-refinery", 3, 3, 2.8f, Refinery, Glows: true, Smoke: new Vector3(0.8f, 2.75f, 1.0f)),
        new("kerogen-generator", 2, 2, 1.6f, Generator, Frames: 8, Fps: 12, Glows: true, Smoke: new Vector3(1.55f, 1.6f, 0.55f)),
        new("lattice-pylon", 1, 1, 3.0f, Pylon, Anchor: new Vector3(0.5f, 2.86f, 0.5f)),
        new("launch-pad", 3, 3, 1.8f, LaunchPad, Glows: true),
        new("ferry", 3, 3, 3.4f, (k, a) => Rocket(k, a, true)),
        new("rocket-on-pad", 3, 3, 3.4f, (k, a) => Rocket(k, a, false)),

        // Clean Age.
        new("electric-drill", 2, 2, 1.5f, ElectricDrill, Frames: 8, Fps: 12, Turns: true, Glows: true),
        new("arc-smelter", 2, 2, 1.6f, ArcSmelter, Glows: true),
        new("wind-turbine", 2, 2, 3.7f, WindTurbine, Frames: 12, Fps: 10),
        new("assembler-mk2", 3, 3, 1.6f, (k, a) => FabricatorTier(k, a, 2), Frames: 8, Fps: 10, Glows: true),
        new("logistics-station", 3, 3, 2.3f, LogisticsStation, Frames: 8, Fps: 6, Glows: true),
        new("orbital-station", 4, 4, 3.2f, OrbitalStation, Frames: 8, Fps: 6, Glows: true),
        new("orbital-collector", 3, 3, 2.4f, Collector, Frames: 8, Fps: 6, Glows: true),
        new("payload-launcher", 3, 3, 2.6f, PayloadLauncher, Glows: true),
        new("robot-bay", 3, 3, 1.7f, RobotBay, Frames: 8, Fps: 8, Glows: true),
        new("atmo-processor", 3, 3, 2.4f, (k, a) => Terraformer(k, a, 0), Frames: 8, Fps: 8, Glows: true, Smoke: new Vector3(1.5f, 2.35f, 0.9f)),
        new("hydro-seeder", 3, 3, 1.7f, (k, a) => Terraformer(k, a, 1), Frames: 8, Fps: 8, Glows: true),
        new("bio-incubator", 3, 3, 2.0f, (k, a) => Terraformer(k, a, 2), Glows: true),

        // Stellar Age.
        new("assembler-mk3", 3, 3, 1.7f, (k, a) => FabricatorTier(k, a, 3), Frames: 8, Fps: 12, Glows: true),
        new("plasma-smelter", 2, 2, 1.5f, PlasmaSmelter, Frames: 8, Fps: 12, Glows: true),
        new("plasma-drill", 3, 3, 1.9f, PlasmaDrill, Frames: 8, Fps: 12, Turns: true, Glows: true),
        new("fusion-reactor", 4, 4, 2.0f, FusionReactor, Frames: 8, Fps: 10, Glows: true),
        new("space-elevator", 5, 5, 6.0f, SpaceElevator, Frames: 8, Fps: 6, Glows: true),
        new("climate-engine", 4, 4, 2.6f, ClimateEngine, Frames: 8, Fps: 10, Glows: true, Smoke: new Vector3(2f, 2.6f, 1.6f)),
        new("settler-uplink", 3, 3, 3.0f, SettlerUplink, Frames: 8, Fps: 4, Glows: true),
    ];

    private static void CellBank(Kit k, Color accent)
    {
        Foundation(k, 2, 2, Palette.DarkIron, 0.05f, 0.08f);

        // A timber rack holding six cell cases with copper terminals and charge lamps.
        k.Slab(0.14f, 0.2f, 1.72f, 1.6f, 0.06f, Palette.Timber, 0.08f);
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                float x = 0.2f + (col * 0.55f), z = 0.28f + (row * 0.75f);
                k.Bevel(x, z, 0.48f, 0.62f, 0.62f, 0.05f, Palette.Paint(new Color(0.2f, 0.22f, 0.2f), 0.1f, 0.6f), 0.14f);
                k.Slab(x + 0.03f, z + 0.03f, 0.42f, 0.56f, 0.05f, Palette.Paint(new Color(0.3f, 0.32f, 0.3f), 0.1f, 0.5f), 0.76f);
                k.Cyl(new Vector3(x + 0.12f, 0.81f, z + 0.18f), 0.04f, 0.07f, Palette.Copper, -1, 10);
                k.Cyl(new Vector3(x + 0.36f, 0.81f, z + 0.18f), 0.04f, 0.07f, Palette.Steel, -1, 10);
                k.Box(new Vector3(x + 0.24f, 0.5f, z + 0.625f), new Vector3(0.3f, 0.06f, 0.01f), Palette.Glow(accent, 1.6f));
            }
        }

        k.Rod(new Vector3(0.32f, 0.88f, 0.46f), new Vector3(1.52f, 0.88f, 0.46f), 0.02f, Palette.Copper, 6);
        k.Rod(new Vector3(0.32f, 0.88f, 1.21f), new Vector3(1.52f, 0.88f, 1.21f), 0.02f, Palette.Copper, 6);
    }

    private static void WaterTower(Kit k, Color accent)
    {
        Vector3 c = new Vector3(0.5f, 0, 0.5f);
        foreach ((float dx, float dz) in new[] { (-0.34f, -0.34f), (0.34f, -0.34f), (-0.34f, 0.34f), (0.34f, 0.34f) })
        {
            k.Rod(c + new Vector3(dx, 0, dz), c + new Vector3(dx * 0.7f, 1.55f, dz * 0.7f), 0.045f, Palette.Timber, 8);
        }

        k.Rod(c + new Vector3(-0.3f, 0.7f, 0.3f), c + new Vector3(0.3f, 0.7f, 0.3f), 0.025f, Palette.Timber, 6);
        k.Rod(c + new Vector3(-0.3f, 0.7f, 0.3f), c + new Vector3(0.27f, 1.2f, 0.27f), 0.02f, Palette.DarkIron, 6);
        k.Cyl(c + new Vector3(0, 1.55f, 0), 0.42f, 0.7f, Palette.Iron, 0.4f, 24);
        foreach (float y in new[] { 1.7f, 2.05f })
        {
            k.Ring(c + new Vector3(0, y, 0), 0.41f, 0.45f, Palette.DarkIron);
        }

        k.Cyl(c + new Vector3(0, 2.25f, 0), 0.43f, 0.22f, Palette.Rust, 0.06f, 24);
        k.Box(c + new Vector3(0, 1.9f, 0.42f), new Vector3(0.24f, 0.12f, 0.02f), Palette.Paint(accent.Darkened(0.2f), 0.1f, 0.5f));
        k.Rod(c + new Vector3(0.1f, 0, 0.1f), c + new Vector3(0.1f, 1.55f, 0.1f), 0.05f, Palette.DarkIron, 8);
    }

    /// <summary>Combustion-age concrete pad.</summary>
    private static StandardMaterial3D Concrete => Palette.Paint(new Color(0.5f, 0.49f, 0.46f), 0f, 0.95f);

    private static void Refinery(Kit k, Color accent)
    {
        Foundation(k, 3, 3, Concrete, 0.05f, 0.1f);
        k.Bevel(0.2f, 1.7f, 2.6f, 1.1f, 0.7f, 0.12f, Palette.Oxide, 0.1f);
        k.Slab(0.2f, 2.7f, 2.6f, 0.08f, 0.1f, Palette.Hazard, 0.1f);

        // Two distillation columns with ladders and bands, a squat storage tank, and pipework between.
        foreach ((float x, float z, float r, float h) in new[] { (0.8f, 1.0f, 0.36f, 2.6f), (1.6f, 0.85f, 0.3f, 2.2f) })
        {
            k.Cyl(new Vector3(x, 0.1f, z), r, h - 0.1f, Palette.Steel, -1, 24);
            k.Ball(new Vector3(x, h, z), r, Palette.Steel, true);
            for (float y = 0.5f; y < h - 0.2f; y += 0.5f)
            {
                k.Ring(new Vector3(x, y, z), r - 0.01f, r + 0.04f, Palette.DarkIron);
            }

            k.Slab(x - 0.08f, z + r, 0.03f, 0.04f, h - 0.3f, Palette.Yellow, 0.1f);
            k.Slab(x + 0.05f, z + r, 0.03f, 0.04f, h - 0.3f, Palette.Yellow, 0.1f);
        }

        k.Cyl(new Vector3(2.35f, 0.1f, 1.2f), 0.48f, 0.75f, Palette.Yellow, -1, 28);
        k.Ball(new Vector3(2.35f, 0.85f, 1.2f), 0.48f, Palette.Yellow, true);
        k.Rod(new Vector3(0.8f, 1.6f, 1.0f), new Vector3(1.6f, 1.6f, 0.85f), 0.07f, Palette.Iron);
        k.Rod(new Vector3(1.6f, 0.9f, 0.85f), new Vector3(2.35f, 0.9f, 1.2f), 0.07f, Palette.Iron);
        k.Rod(new Vector3(0.8f, 0.5f, 1.0f), new Vector3(0.8f, 0.5f, 1.9f), 0.06f, Palette.Iron);

        // The flare stack's pilot flame and control-room windows.
        k.Cyl(new Vector3(0.8f, 2.6f, 1.0f), 0.05f, 0.18f, Palette.DarkIron, -1, 8);
        k.Ball(new Vector3(0.8f, 2.8f, 1.0f), 0.06f, Palette.Glow(new Color(1f, 0.55f, 0.15f), 3f));
        for (int i = 0; i < 4; i++)
        {
            k.Box(new Vector3(0.6f + (i * 0.5f), 0.55f, 2.81f), new Vector3(0.3f, 0.18f, 0.02f), Palette.Glow(new Color(1f, 0.85f, 0.5f), 1f));
        }
    }

    private static void Generator(Kit k, Color accent)
    {
        Foundation(k, 2, 2, Concrete, 0.05f, 0.08f);
        k.Bevel(0.2f, 0.4f, 1.6f, 1.3f, 0.75f, 0.12f, Palette.Yellow, 0.08f);
        k.Slab(0.2f, 1.62f, 1.6f, 0.08f, 0.1f, Palette.Hazard, 0.08f);

        // Radiator fins on the front, a cooling fan on top, and two exhaust stacks.
        for (int i = 0; i < 9; i++)
        {
            k.Box(new Vector3(0.4f + (i * 0.15f), 0.5f, 1.71f), new Vector3(0.04f, 0.5f, 0.04f), Palette.DarkIron);
        }

        Vector3 fan = new Vector3(0.85f, 0.84f, 1.0f);
        k.Cyl(fan, 0.42f, 0.04f, Palette.DarkIron, -1, 28);
        Kit blades = k.Group(fan + new Vector3(0, 0.05f, 0), Yaw(k.Spin / 3));
        for (int i = 0; i < 5; i++)
        {
            blades.Box(new Vector3(0, 0, 0), new Vector3(0.7f, 0.02f, 0.1f), Palette.Steel, Yaw(i * Mathf.Tau / 10));
        }

        k.Ring(fan + new Vector3(0, 0.06f, 0), 0.38f, 0.43f, Palette.Steel);
        foreach (float z in new[] { 0.55f, 0.95f })
        {
            k.Cyl(new Vector3(1.55f, 0.83f, z), 0.09f, 0.75f, Palette.DarkIron, 0.08f, 12);
            k.Cyl(new Vector3(1.55f, 1.57f, z), 0.07f, 0.02f, Palette.Glow(new Color(1f, 0.5f, 0.15f), 1.5f), -1, 12);
        }
    }

    private static void Pylon(Kit k, Color accent)
    {
        Vector3 c = new Vector3(0.5f, 0, 0.5f);
        Vector3[] feet = [c + new Vector3(-0.36f, 0, -0.36f), c + new Vector3(0.36f, 0, -0.36f), c + new Vector3(0.36f, 0, 0.36f), c + new Vector3(-0.36f, 0, 0.36f)];
        Vector3 Leg(int i, float y) => feet[i].Lerp(c + new Vector3(0, y, 0), y / 3.4f) with { Y = y };
        foreach (Vector3 foot in feet)
        {
            k.Slab(foot.X - 0.08f, foot.Z - 0.08f, 0.16f, 0.16f, 0.08f, Concrete);
        }

        for (int i = 0; i < 4; i++)
        {
            k.Rod(Leg(i, 0), Leg(i, 2.8f), 0.03f, Palette.Steel, 6);
            for (float y = 0; y < 2.6f; y += 0.55f)
            {
                k.Rod(Leg(i, y), Leg((i + 1) % 4, y + 0.55f), 0.012f, Palette.Steel, 4);
                k.Rod(Leg(i, y + 0.55f), Leg((i + 1) % 4, y + 0.55f), 0.014f, Palette.Steel, 4);
            }
        }

        k.Box(c + new Vector3(0, 2.76f, 0), new Vector3(1.0f, 0.06f, 0.08f), Palette.Steel);
        foreach (float x in new[] { -0.42f, 0f, 0.42f })
        {
            for (int d = 0; d < 3; d++)
            {
                k.Cyl(c + new Vector3(x, 2.79f + (d * 0.035f), 0), 0.045f, 0.025f, Palette.Paint(new Color(0.5f, 0.62f, 0.7f), 0.2f, 0.2f), -1, 10);
            }
        }

        _ = accent;
    }

    private static void LaunchPad(Kit k, Color accent)
    {
        Foundation(k, 3, 3, Concrete, 0.04f, 0.14f);
        Vector3 c = new Vector3(1.5f, 0.14f, 1.5f);

        // A blast plate with a hazard ring, flame trench, service tower and fuel tanks.
        k.Cyl(c, 0.95f, 0.04f, Palette.Hazard, -1, 40);
        k.Cyl(c, 0.8f, 0.05f, Palette.DarkIron, -1, 40);
        k.Box(c + new Vector3(0, 0.03f, 1.0f), new Vector3(0.5f, 0.05f, 0.8f), Palette.Paint(new Color(0.08f, 0.07f, 0.06f), 0, 1));
        foreach ((float x, float z) in new[] { (0.35f, 0.35f), (2.65f, 0.35f), (0.35f, 2.65f), (2.65f, 2.65f) })
        {
            k.Ball(new Vector3(x, 0.17f, z), 0.06f, Palette.Glow(new Color(1f, 0.3f, 0.2f), 2.5f));
        }

        Vector3 tower = new Vector3(2.55f, 0.14f, 0.55f);
        foreach ((float dx, float dz) in new[] { (-0.18f, -0.18f), (0.18f, -0.18f), (-0.18f, 0.18f), (0.18f, 0.18f) })
        {
            k.Rod(tower + new Vector3(dx, 0, dz), tower + new Vector3(dx, 1.55f, dz), 0.03f, Palette.Oxide, 6);
        }

        for (float y = 0.3f; y < 1.6f; y += 0.4f)
        {
            k.Slab(tower.X - 0.2f, tower.Z - 0.2f, 0.4f, 0.4f, 0.04f, Palette.Oxide, y);
        }

        k.Rod(tower + new Vector3(0, 1.2f, 0), c + new Vector3(0.35f, 1.2f, -0.3f), 0.035f, Palette.Yellow, 6);
        foreach (float z in new[] { 1.9f, 2.45f })
        {
            k.Rod(new Vector3(0.3f, 0.36f, z), new Vector3(1.0f, 0.36f, z), 0.2f, Palette.Paint(new Color(0.85f, 0.85f, 0.82f), 0.3f, 0.4f), 20);
        }

        _ = accent;
    }

    /// <summary>The ferry rocket, standing on its legs; landed (with a scorch ring) or on the pad.</summary>
    private static void Rocket(Kit k, Color accent, bool landed)
    {
        Vector3 c = new Vector3(1.5f, landed ? 0 : 0.18f, 1.5f);
        if (landed)
        {
            k.Cyl(c, 1.2f, 0.01f, Palette.Paint(new Color(0.12f, 0.1f, 0.08f), 0, 1), -1, 40);
        }

        Color hullWhite = new Color(0.88f, 0.88f, 0.86f);
        foreach ((float dx, float dz) in new[] { (-1f, 0f), (1f, 0f), (0f, -1f), (0f, 1f) })
        {
            k.Rod(c + new Vector3(dx * 0.82f, 0, dz * 0.82f), c + new Vector3(dx * 0.35f, 0.75f, dz * 0.35f), 0.04f, Palette.Steel, 8);
            k.Box(c + new Vector3(dx * 0.48f, 0.55f, dz * 0.48f), new Vector3(dz == 0 ? 0.5f : 0.04f, 0.6f, dx == 0 ? 0.5f : 0.04f), Palette.Oxide);
        }

        k.Cyl(c + new Vector3(0, 0.25f, 0), 0.3f, 0.2f, Palette.DarkIron, 0.36f, 20);
        k.Cyl(c + new Vector3(0, 0.45f, 0), 0.38f, 2.0f, Palette.Paint(hullWhite, 0.2f, 0.4f), 0.38f, 28);
        k.Ring(c + new Vector3(0, 1.2f, 0), 0.37f, 0.41f, Palette.Oxide);
        k.Ring(c + new Vector3(0, 2.1f, 0), 0.37f, 0.41f, Palette.DarkIron);
        k.Cyl(c + new Vector3(0, 2.45f, 0), 0.38f, 0.85f, Palette.Paint(hullWhite, 0.2f, 0.4f), 0.04f, 28);
        k.Ball(c + new Vector3(0.0f, 1.75f, 0.37f), 0.09f, Palette.Glass);
        k.Box(c + new Vector3(0, 1.0f, 0.385f), new Vector3(0.25f, 0.3f, 0.02f), Palette.Paint(accent.Darkened(0.3f), 0.2f, 0.4f));
    }

    private static void ElectricDrill(Kit k, Color accent)
    {
        TechBody(k, 2, 2, 0.35f, accent);
        float top = 0.545f;

        // A turning drill frame over the bore, its motor housing, and the output port to the north.
        Vector3 bore = new Vector3(1f, top, 1.0f);
        Kit head = k.Group(bore, Yaw(k.Spin / 4));
        head.Cyl(Vector3.Zero, 0.55f, 0.1f, Palette.HullDark, -1, 24);
        for (int i = 0; i < 3; i++)
        {
            head.Box(new Vector3(0, 0.13f, 0), new Vector3(1.0f, 0.06f, 0.12f), Palette.Steel, Yaw(i * Mathf.Pi / 3));
        }

        head.Ring(new Vector3(0, 0.11f, 0), 0.5f, 0.56f, Palette.Glow(accent, 1.8f));
        k.Cyl(bore + new Vector3(0, 0.16f, 0), 0.22f, 0.55f, Palette.Hull, 0.18f, 20);
        k.Cyl(bore + new Vector3(0, 0.71f, 0), 0.2f, 0.06f, Palette.HullDark, -1, 20);
        k.Ball(bore + new Vector3(0, 0.78f, 0), 0.06f, Palette.Glow(accent, 2.5f));
        k.Box(new Vector3(1f, 0.32f, 0.1f), new Vector3(0.6f, 0.3f, 0.14f), Palette.HullDark);
        k.Box(new Vector3(1f, 0.32f, 0.03f), new Vector3(0.42f, 0.14f, 0.02f), Palette.Glow(accent, 1.6f));
    }

    private static void ArcSmelter(Kit k, Color accent)
    {
        TechBody(k, 2, 2, 0.5f, accent);
        float top = 0.695f;

        // A crucible with three electrodes dipping into a glowing melt, and a transformer cabinet.
        Vector3 c = new Vector3(0.9f, top, 1.0f);
        k.Cyl(c, 0.55f, 0.2f, Palette.Paint(new Color(0.28f, 0.26f, 0.25f), 0.3f, 0.8f), 0.6f, 24);
        k.Cyl(c + new Vector3(0, 0.2f, 0), 0.45f, 0.02f, Palette.Glow(new Color(1f, 0.55f, 0.2f), 3f), -1, 24);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * Mathf.Tau / 3) + 0.4f;
            Vector3 at = c + new Vector3(Mathf.Cos(a) * 0.2f, 0, Mathf.Sin(a) * 0.2f);
            k.Cyl(at + new Vector3(0, 0.15f, 0), 0.06f, 0.75f, Palette.Paint(new Color(0.15f, 0.15f, 0.16f), 0.2f, 0.7f), -1, 10);
            k.Rod(at + new Vector3(0, 0.85f, 0), new Vector3(1.6f, top + 0.8f, 0.5f), 0.03f, Palette.Copper, 6);
        }

        k.Bevel(1.4f, 0.3f, 0.42f, 0.42f, 0.8f, 0.06f, Palette.HullDark, top);
        k.Box(new Vector3(1.61f, top + 0.4f, 0.73f), new Vector3(0.3f, 0.1f, 0.01f), Palette.Glow(accent, 1.6f));
    }

    private static void WindTurbine(Kit k, Color accent)
    {
        TechBody(k, 2, 2, 0.15f, accent);
        Vector3 c = new Vector3(1f, 0.345f, 1.0f);
        k.Cyl(c, 0.16f, 2.35f, Palette.Hull, 0.09f, 20);
        Vector3 hub = c + new Vector3(0, 2.4f, 0.12f);
        k.Box(hub + new Vector3(0, 0, -0.2f), new Vector3(0.22f, 0.22f, 0.55f), Palette.Hull);
        Kit rotor = k.Group(hub + new Vector3(0, 0, 0.12f), Roll(k.Spin / 3));
        rotor.Ball(Vector3.Zero, 0.12f, Palette.HullDark);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3;
            rotor.Box(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.62f, new Vector3(1.1f, 0.12f, 0.03f), Palette.Hull, Roll(a));
        }
    }

    private static void FabricatorTier(Kit k, Color accent, int tier)
    {
        Fabricator(k, accent);
        float top = 0.815f;
        if (tier == 2)
        {
            // Precision: twin calibration towers at the back corners.
            foreach (float x in new[] { 0.55f, 2.45f })
            {
                k.Cyl(new Vector3(x, top, 0.55f), 0.16f, 0.65f, Palette.Hull, 0.12f, 16);
                k.Ring(new Vector3(x, top + 0.5f, 0.55f), 0.13f, 0.18f, Palette.Glow(accent, 2f));
            }
        }
        else
        {
            // Plasma: a containment ring around the bay that glows violet.
            k.Ring(new Vector3(1.5f, top + 0.25f, 1.43f), 0.85f, 0.97f, Palette.HullDark);
            k.Ring(new Vector3(1.5f, top + 0.33f, 1.43f), 0.86f, 0.9f, Palette.Glow(_plasma, 2.5f));
        }
    }

    private static void LogisticsStation(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.5f, accent);
        float top = 0.695f;

        // A central tower topped by a landing ring, with storage pods around its foot.
        Vector3 c = new Vector3(1.5f, top, 1.45f);
        k.Cyl(c, 0.45f, 1.0f, Palette.Hull, 0.35f, 8);
        k.Cyl(c + new Vector3(0, 1.0f, 0), 0.8f, 0.12f, Palette.HullDark, 0.85f, 32);
        Kit ring = k.Group(c + new Vector3(0, 1.13f, 0), Yaw(k.Spin / 8));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            ring.Ball(new Vector3(Mathf.Cos(a) * 0.72f, 0, Mathf.Sin(a) * 0.72f), 0.05f, Palette.Glow(accent, 2.5f));
        }

        k.Cyl(c + new Vector3(0, 1.12f, 0), 0.4f, 0.02f, Palette.Glass, -1, 24);
        foreach ((float x, float z) in new[] { (0.55f, 0.55f), (2.45f, 0.55f), (0.55f, 2.3f), (2.45f, 2.3f) })
        {
            k.Cyl(new Vector3(x, top, z), 0.26f, 0.4f, Palette.HullDark, 0.22f, 16);
            k.Ball(new Vector3(x, top + 0.4f, z), 0.22f, Palette.Hull, true);
        }
    }

    private static void OrbitalStation(Kit k, Color accent)
    {
        TechBody(k, 4, 4, 0.6f, accent);
        float top = 0.795f;
        Vector3 c = new Vector3(2f, top, 1.9f);

        // A launch ring, a tall uplink mast and four cargo silos.
        k.Cyl(c, 0.7f, 1.3f, Palette.Hull, 0.5f, 8);
        k.Ring(c + new Vector3(0, 1.3f, 0), 0.85f, 1.0f, Palette.HullDark);
        Kit ring = k.Group(c + new Vector3(0, 1.36f, 0), Yaw(k.Spin / 8));
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            ring.Ball(new Vector3(Mathf.Cos(a) * 0.92f, 0, Mathf.Sin(a) * 0.92f), 0.05f, Palette.Glow(accent, 2.5f));
        }

        k.Cyl(c + new Vector3(0, 1.3f, 0), 0.45f, 1.1f, Palette.Hull, 0.08f, 16);
        k.Ball(c + new Vector3(0, 2.42f, 0), 0.08f, Palette.Glow(new Color(1f, 0.3f, 0.25f), 3f));
        foreach ((float x, float z) in new[] { (0.7f, 0.7f), (3.3f, 0.7f), (0.7f, 3.2f), (3.3f, 3.2f) })
        {
            k.Cyl(new Vector3(x, top, z), 0.34f, 0.7f, Palette.HullDark, -1, 20);
            k.Ball(new Vector3(x, top + 0.7f, z), 0.34f, Palette.Hull, true);
        }
    }

    /// <summary>A dish on a turning mount, pointing at <paramref name="elevation"/> radians above the horizon.</summary>
    private static void Dish(Kit k, Vector3 at, float radius, float elevation, float yaw, Color accent)
    {
        Kit mount = k.Group(at, Yaw(yaw));
        mount.Cyl(Vector3.Zero, radius * 0.25f, radius * 0.5f, Palette.HullDark, -1, 16);
        Kit dish = mount.Group(new Vector3(0, radius * 0.6f, 0), Pitch(-(Mathf.Pi / 2) + elevation));
        dish.Cyl(Vector3.Zero, radius * 0.25f, radius * 0.35f, Palette.Hull, radius, 32);
        dish.Cyl(new Vector3(0, radius * 0.36f, 0), radius * 0.95f, 0.01f, Palette.HullDark, -1, 32);
        dish.Rod(new Vector3(0, radius * 0.36f, 0), new Vector3(0, radius * 1.1f, 0), 0.02f, Palette.Steel, 6);
        dish.Ball(new Vector3(0, radius * 1.1f, 0), 0.05f, Palette.Glow(accent, 3f));
    }

    private static void Collector(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.45f, accent);
        Dish(k, new Vector3(1.5f, 0.645f, 1.35f), 0.95f, 1.1f, k.Spin * 0.15f, accent);
        foreach (float x in new[] { 0.5f, 2.5f })
        {
            k.Cyl(new Vector3(x, 0.645f, 2.3f), 0.25f, 0.45f, Palette.HullDark, -1, 16);
            k.Ring(new Vector3(x, 1.0f, 2.3f), 0.2f, 0.27f, Palette.Glow(accent, 1.6f));
        }
    }

    private static void PayloadLauncher(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.45f, accent);
        float top = 0.645f;

        // A mass-driver rail angled up to the north, ringed by accelerator coils.
        Vector3 from = new Vector3(1.5f, top + 0.2f, 2.3f), to = new Vector3(1.5f, top + 1.9f, 0.4f);
        k.Rod(from, to, 0.12f, Palette.HullDark, 12);
        for (int i = 1; i < 6; i++)
        {
            Vector3 at = from.Lerp(to, i / 6f);
            Basis face = new Basis(Vector3.Right, -0.73f);
            k.Ring(at, 0.15f, 0.27f, Palette.Hull, face);
            k.Ring(at, 0.13f, 0.17f, Palette.Glow(accent, 2f), face);
        }

        k.Rod(new Vector3(1.5f, top, 1.2f), from.Lerp(to, 0.55f), 0.07f, Palette.Hull, 10);
        k.Bevel(0.4f, 1.9f, 0.6f, 0.7f, 0.5f, 0.1f, Palette.HullDark, top);
        k.Bevel(2.0f, 1.9f, 0.6f, 0.7f, 0.5f, 0.1f, Palette.HullDark, top);
    }

    private static void RobotBay(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.4f, accent);
        float top = 0.595f;
        Vector3 c = new Vector3(1.5f, top, 1.55f);
        k.Cyl(c, 0.8f, 0.05f, Palette.HullDark, -1, 32);
        k.Ring(c + new Vector3(0, 0.05f, 0), 0.74f, 0.8f, Palette.Hazard);

        // A gantry over the pad with two arms working.
        foreach (float x in new[] { 0.45f, 2.55f })
        {
            k.Bevel(x - 0.12f, 0.5f, 0.24f, 0.24f, 0.95f, 0.05f, Palette.Hull, top);
        }

        k.Slab(0.35f, 0.5f, 2.3f, 0.24f, 0.12f, Palette.Hull, top + 0.95f);
        float swing = Mathf.Sin(k.Spin) * 0.25f;
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 root = new Vector3(1.5f + (side * 0.45f), top + 0.95f, 0.62f);
            Vector3 elbow = root + new Vector3(side * 0.1f, -0.25f, 0.45f + (swing * side));
            Vector3 tip = elbow + new Vector3(-side * 0.15f, -0.32f, 0.1f);
            k.Rod(root, elbow, 0.045f, Palette.Hull, 10);
            k.Rod(elbow, tip, 0.035f, Palette.Hull, 10);
            k.Ball(elbow, 0.06f, Palette.HullDark);
            k.Ball(tip, 0.04f, Palette.Glow(new Color(1f, 0.85f, 0.5f), 3f));
        }
    }

    /// <summary>The three terraformers: 0 vents catalyst from stacks, 1 sprays a water basin, 2 grows plants under a dome.</summary>
    private static void Terraformer(Kit k, Color accent, int kind)
    {
        TechBody(k, 3, 3, 0.55f, accent);
        float top = 0.745f;
        switch (kind)
        {
            case 0:
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 at = new Vector3(1.5f + (i * 0.72f), top, 0.95f);
                    float h = i == 0 ? 1.6f : 1.15f;
                    k.Cyl(at, 0.28f, h, Palette.Hull, 0.22f, 20);
                    k.Ring(at + new Vector3(0, h, 0), 0.18f, 0.25f, Palette.HullDark);
                    k.Cyl(at + new Vector3(0, h - 0.02f, 0), 0.18f, 0.02f, Palette.Glow(accent, 2f), -1, 16);
                }

                for (int i = 0; i < 6; i++)
                {
                    k.Box(new Vector3(0.75f + (i * 0.3f), top + 0.02f, 2.15f), new Vector3(0.18f, 0.03f, 0.7f), Palette.HullDark);
                }

                Kit fan = k.Group(new Vector3(1.5f, top + 0.05f, 2.15f), Yaw(k.Spin));
                fan.Box(Vector3.Zero, new Vector3(0.6f, 0.02f, 0.08f), Palette.Glow(accent, 1.2f));
                break;
            case 1:
                Vector3 basin = new Vector3(1.5f, top, 1.5f);
                k.Cyl(basin, 1.05f, 0.2f, Palette.HullDark, 1.08f, 40);
                k.Cyl(basin + new Vector3(0, 0.18f, 0), 0.95f, 0.02f, Palette.Paint(new Color(0.12f, 0.35f, 0.55f), 0.3f, 0.05f), -1, 40);
                Kit arm = k.Group(basin + new Vector3(0, 0.2f, 0), Yaw(k.Spin));
                arm.Cyl(Vector3.Zero, 0.12f, 0.5f, Palette.Hull, 0.08f, 12);
                arm.Rod(new Vector3(0, 0.45f, 0), new Vector3(0.85f, 0.3f, 0), 0.03f, Palette.Hull, 8);
                for (int i = 1; i <= 3; i++)
                {
                    arm.Ball(new Vector3(i * 0.27f, 0.34f - (i * 0.02f), 0), 0.035f, Palette.Glow(accent, 2f));
                }

                break;
            default:
                Vector3 dome = new Vector3(1.5f, top, 1.45f);
                k.Cyl(dome, 1.05f, 0.08f, Palette.HullDark, -1, 40);
                k.Cyl(dome + new Vector3(0, 0.08f, 0), 0.98f, 0.02f, Palette.Paint(new Color(0.25f, 0.18f, 0.1f), 0, 1), -1, 40);
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.4f, r = 0.2f + (i % 3 * 0.25f);
                    Vector3 at = dome + new Vector3(Mathf.Cos(a) * r, 0.1f, Mathf.Sin(a) * r);
                    k.Cyl(at, 0.1f + (i % 2 * 0.04f), 0.35f + (i % 3 * 0.12f), Palette.Paint(new Color(0.2f, 0.55f + (i % 3 * 0.08f), 0.2f), 0, 0.8f), 0f, 7);
                }

                k.Ball(dome + new Vector3(0, 0.1f, 0), 1.0f, Palette.ClearGlass, true);
                k.Ring(dome + new Vector3(0, 0.1f, 0), 0.98f, 1.06f, Palette.Glow(accent, 1.6f));
                break;
        }
    }

    private static void PlasmaSmelter(Kit k, Color accent)
    {
        TechBody(k, 2, 2, 0.45f, accent);
        Vector3 c = new Vector3(1f, 0.645f, 0.98f);
        k.Cyl(c, 0.6f, 0.15f, Palette.HullDark, 0.55f, 28);
        Kit ring = k.Group(c + new Vector3(0, 0.38f, 0), Yaw(k.Spin));
        ring.Ring(Vector3.Zero, 0.4f, 0.52f, Palette.Hull);
        ring.Ring(new Vector3(0, 0.02f, 0), 0.42f, 0.47f, Palette.Glow(_plasma, 3f));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4;
            ring.Box(new Vector3(Mathf.Cos(a) * 0.46f, 0, Mathf.Sin(a) * 0.46f), new Vector3(0.12f, 0.16f, 0.12f), Palette.HullDark, Yaw(-a));
        }

        k.Ball(c + new Vector3(0, 0.36f, 0), 0.16f, Palette.Glow(_plasma.Lightened(0.3f), 3.5f));
    }

    private static void PlasmaDrill(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.45f, accent);
        float top = 0.645f;
        Vector3 bore = new Vector3(1.5f, top, 1.55f);
        k.Cyl(bore, 0.95f, 0.15f, Palette.HullDark, 0.9f, 32);
        Kit head = k.Group(bore + new Vector3(0, 0.15f, 0), Yaw(k.Spin / 4));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4;
            head.Bevel(-0.12f + (Mathf.Cos(a) * 0.55f), -0.12f + (Mathf.Sin(a) * 0.55f), 0.24f, 0.24f, 0.55f, 0.06f, Palette.Hull);
            head.Ball(new Vector3(Mathf.Cos(a) * 0.55f, 0.6f, Mathf.Sin(a) * 0.55f), 0.06f, Palette.Glow(_plasma, 3f));
        }

        k.Ring(bore + new Vector3(0, 0.17f, 0), 0.3f, 0.42f, Palette.Glow(_plasma, 3f));
        k.Box(new Vector3(1.5f, 0.45f, 0.12f), new Vector3(0.8f, 0.4f, 0.16f), Palette.HullDark);
        k.Box(new Vector3(1.5f, 0.45f, 0.035f), new Vector3(0.55f, 0.18f, 0.02f), Palette.Glow(accent, 1.6f));
    }

    private static void FusionReactor(Kit k, Color accent)
    {
        TechBody(k, 4, 4, 0.5f, accent);
        float top = 0.695f;
        Vector3 c = new Vector3(2f, top, 1.95f);

        // A tokamak: a fat torus wrapped in field coils, a glowing plasma band in its throat.
        k.Ring(c + new Vector3(0, 0.45f, 0), 0.75f, 1.55f, Palette.Hull);
        k.Ring(c + new Vector3(0, 0.62f, 0), 0.8f, 0.92f, Palette.Glow(_plasma, 2.5f));
        for (int i = 0; i < 12; i++)
        {
            float a = (i * Mathf.Tau / 12) + (k.Spin / 12);
            Vector3 at = c + new Vector3(Mathf.Cos(a) * 1.15f, 0.45f, Mathf.Sin(a) * 1.15f);
            k.Ring(at, 0.36f, 0.46f, Palette.HullDark, Yaw(-a) * Roll(Mathf.Pi / 2));
        }

        k.Cyl(c, 0.5f, 1.1f, Palette.HullDark, 0.4f, 24);
        k.Ball(c + new Vector3(0, 1.1f, 0), 0.4f, Palette.Glass, true);
        k.Ball(c + new Vector3(0, 1.12f, 0), 0.18f, Palette.Glow(_plasma.Lightened(0.3f), 3f));
    }

    private static void SpaceElevator(Kit k, Color accent)
    {
        TechBody(k, 5, 5, 0.6f, accent);
        float top = 0.795f;
        Vector3 c = new Vector3(2.5f, top, 2.45f);

        // A stepped anchor, a tapering tower, and the tether rising off the top with a climber on it.
        k.Cyl(c, 1.8f, 0.4f, Palette.Hull, 1.6f, 8);
        k.Cyl(c + new Vector3(0, 0.4f, 0), 1.3f, 0.6f, Palette.HullDark, 1.0f, 8);
        k.Cyl(c + new Vector3(0, 1.0f, 0), 0.7f, 3.2f, Palette.Hull, 0.25f, 8);
        foreach ((float dx, float dz) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (-1.6f, 1.6f), (1.6f, 1.6f) })
        {
            k.Rod(c + new Vector3(dx * 0.95f, 0, dz * 0.95f), c + new Vector3(dx * 0.22f, 2.4f, dz * 0.22f), 0.06f, Palette.HullDark, 8);
        }

        k.Rod(c + new Vector3(0, 4.2f, 0), c + new Vector3(0, 5.6f, 0), 0.05f, Palette.Glow(accent, 2f), 8);
        float climb = 4.3f + (k.Phase * 1.1f);
        k.Cyl(c + new Vector3(0, climb, 0), 0.22f, 0.2f, Palette.HullDark, 0.18f, 8);
        k.Ring(c + new Vector3(0, climb + 0.1f, 0), 0.2f, 0.25f, Palette.Glow(accent, 2.5f));
        for (int i = 0; i < 8; i++)
        {
            float a = (i * Mathf.Tau / 8) + (Mathf.Pi / 8);
            k.Ball(c + new Vector3(Mathf.Cos(a) * 1.72f, 0.42f, Mathf.Sin(a) * 1.72f), 0.06f, Palette.Glow(accent, 2.5f));
        }
    }

    private static void ClimateEngine(Kit k, Color accent)
    {
        TechBody(k, 4, 4, 0.7f, accent);
        float top = 0.895f;

        // Four big intake fans around a central exhaust stack.
        foreach ((float x, float z) in new[] { (1.05f, 1.0f), (2.95f, 1.0f), (1.05f, 2.85f), (2.95f, 2.85f) })
        {
            Vector3 at = new Vector3(x, top, z);
            k.Cyl(at, 0.68f, 0.12f, Palette.HullDark, -1, 32);
            k.Cyl(at + new Vector3(0, 0.08f, 0), 0.6f, 0.02f, Palette.Paint(new Color(0.05f, 0.07f, 0.08f), 0.2f, 0.6f), -1, 32);
            Kit fan = k.Group(at + new Vector3(0, 0.1f, 0), Yaw(k.Spin / 4));
            for (int i = 0; i < 6; i++)
            {
                fan.Box(Vector3.Zero, new Vector3(1.1f, 0.02f, 0.12f), Palette.Hull, Yaw(i * Mathf.Pi / 6) * Roll(0.2f));
            }

            k.Ball(at + new Vector3(0, 0.12f, 0), 0.1f, Palette.HullDark);
        }

        Vector3 c = new Vector3(2f, top, 1.92f);
        k.Cyl(c, 0.42f, 1.7f, Palette.Hull, 0.32f, 20);
        k.Ring(c + new Vector3(0, 1.7f, 0), 0.26f, 0.36f, Palette.HullDark);
        k.Cyl(c + new Vector3(0, 1.68f, 0), 0.26f, 0.02f, Palette.Glow(accent, 2.5f), -1, 16);
        k.Ring(c + new Vector3(0, 0.6f, 0), 0.4f, 0.46f, Palette.Glow(accent, 2f));
    }

    private static void SettlerUplink(Kit k, Color accent)
    {
        TechBody(k, 3, 3, 0.5f, accent);
        Dish(k, new Vector3(1.5f, 0.695f, 1.45f), 1.05f, 1.25f, 0.35f, accent);
        k.Rod(new Vector3(2.55f, 0.695f, 0.5f), new Vector3(2.55f, 2.6f, 0.5f), 0.03f, Palette.Steel, 6);
        k.Ball(new Vector3(2.55f, 2.62f, 0.5f), 0.07f, Palette.Glow(new Color(1f, 0.3f, 0.25f), 3f + (2 * Mathf.Sin(k.Spin))));
    }
}
