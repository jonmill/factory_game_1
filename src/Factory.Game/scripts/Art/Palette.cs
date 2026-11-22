using Godot;

namespace Factory.Game.Art;

/// <summary>
/// The game's materials, by tech age, so every building in an age is made of the same stuff:
/// <list type="bullet">
/// <item>Salvage Age: weathered iron streaked with rust, fire brick and timber. Warm, dark and worn.</item>
/// <item>Combustion Age: painted steel plate in safety yellow and oxide red, bright machined steel, rubber and hazard stripes.</item>
/// <item>Clean and Stellar Ages: white ceramic panels with fine seams, dark glass and glowing accent trim.</item>
/// </list>
/// Surface detail comes from small procedural textures (grime, brick courses, planks, panel seams) mapped
/// triplanar, so primitives need no UVs.
/// </summary>
public static class Palette
{
    private static readonly Dictionary<string, Material> _cache = [];
    private static readonly Shader _weathered = GD.Load<Shader>("res://scripts/Art/Weathered.gdshader");

    /// <summary>Weathered iron: brown-grey, rusting low down, streaked and worn bright at the edges.</summary>
    public static Material Iron => Get("iron", () => Surface(new Color(0.42f, 0.38f, 0.33f), 0.55f, 0.6f, rust: 0.55f, grime: 0.6f, streaks: 0.6f, panel: 0.5f));

    /// <summary>Dark, heavily used iron for frames and fittings.</summary>
    public static Material DarkIron => Get("dark-iron", () => Surface(new Color(0.24f, 0.22f, 0.2f), 0.55f, 0.55f, rust: 0.4f, grime: 0.5f, streaks: 0.4f));

    /// <summary>Rust-red iron.</summary>
    public static Material Rust => Get("rust", () => Surface(new Color(0.46f, 0.25f, 0.13f), 0.3f, 0.85f, rust: 0.8f, grime: 0.5f, streaks: 0.7f, wear: 0.3f));

    /// <summary>Fire brick in courses.</summary>
    public static Material Brick => Get("brick", () => Surface(Colors.White, 0f, 0.95f, rust: 0f, grime: 0.7f, streaks: 0.5f, wear: 0.2f, pattern: Bricks(new Color(0.62f, 0.4f, 0.28f), new Color(0.3f, 0.27f, 0.24f), 14), scale: 2f));

    /// <summary>Timber planks.</summary>
    public static Material Timber => Get("timber", () => Surface(Colors.White, 0f, 0.85f, rust: 0f, grime: 0.5f, streaks: 0.4f, wear: 0.35f, pattern: Planks(new Color(0.52f, 0.37f, 0.22f), 15), scale: 2f));

    /// <summary>Copper, for coils, pipes and wire; tarnished green-brown where it's old.</summary>
    public static Material Copper => Get("copper", () => Surface(new Color(0.72f, 0.42f, 0.24f), 0.85f, 0.35f, rust: 0.25f, grime: 0.3f, streaks: 0.2f, rustColor: new Color(0.3f, 0.42f, 0.32f)));

    /// <summary>Safety-yellow painted steel, chipped to bare metal at the edges.</summary>
    public static Material Yellow => Get("yellow", () => Surface(new Color(0.78f, 0.6f, 0.17f), 0.15f, 0.5f, rust: 0.2f, grime: 0.55f, streaks: 0.5f, panel: 0.6f));

    /// <summary>Oxide-red painted steel.</summary>
    public static Material Oxide => Get("oxide", () => Surface(new Color(0.5f, 0.19f, 0.12f), 0.15f, 0.55f, rust: 0.25f, grime: 0.55f, streaks: 0.5f, panel: 0.6f));

    /// <summary>Bright machined steel.</summary>
    public static Material Steel => Get("steel", () => Surface(new Color(0.64f, 0.63f, 0.6f), 0.85f, 0.2f, rust: 0.1f, grime: 0.3f, streaks: 0.2f, wear: 0.5f));

    /// <summary>Black rubber and plastic.</summary>
    public static Material Rubber => Get("rubber", () => Surface(new Color(0.08f, 0.075f, 0.07f), 0f, 0.8f, rust: 0f, grime: 0.4f, streaks: 0.1f, wear: 0f));

    /// <summary>Yellow and black hazard stripes.</summary>
    public static Material Hazard => Get("hazard", () => Surface(Colors.White, 0.1f, 0.55f, rust: 0.1f, grime: 0.6f, streaks: 0.4f, wear: 0.6f, pattern: Stripes(), scale: 2f));

    /// <summary>Bone-grey ceramic hull panels with fine seams and rivets.</summary>
    public static Material Hull => Get("hull", () => Surface(new Color(0.52f, 0.49f, 0.43f), 0.08f, 0.5f, rust: 0.05f, grime: 0.55f, streaks: 0.45f, wear: 0.6f, panel: 0.5f, wearColor: new Color(0.82f, 0.8f, 0.76f)));

    /// <summary>Warm dark composite for the darker parts of high-tech machines.</summary>
    public static Material HullDark => Get("hull-dark", () => Surface(new Color(0.27f, 0.245f, 0.21f), 0.45f, 0.45f, rust: 0.05f, grime: 0.45f, streaks: 0.35f, wear: 0.7f, panel: 0.35f, wearColor: new Color(0.66f, 0.64f, 0.6f)));

    /// <summary>Poured concrete, stained low down.</summary>
    public static Material Concrete => Get("concrete", () => Surface(new Color(0.42f, 0.4f, 0.36f), 0f, 0.95f, rust: 0.1f, grime: 0.7f, streaks: 0.55f, wear: 0.25f, panel: 1f, wearColor: new Color(0.55f, 0.53f, 0.5f)));

    /// <summary>Dark, glossy glass.</summary>
    public static Material Glass => Get("glass", () => Plain(new Color(0.05f, 0.055f, 0.06f), 0.65f, 0.08f));

    /// <summary>Tinted see-through glass for domes and windows.</summary>
    public static Material ClearGlass => Get("clear-glass", () =>
    {
        StandardMaterial3D m = Plain(new Color(0.2f, 0.4f, 0.5f, 0.45f), 0.5f, 0.04f);
        m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        return m;
    });

    /// <summary>Photovoltaic cells: deep blue with a silver grid.</summary>
    public static Material Cells => Get("cells", () => Surface(Colors.White, 0.4f, 0.15f, rust: 0f, grime: 0.35f, streaks: 0.2f, wear: 0.2f, pattern: CellGrid(), scale: 2f));

    /// <summary>A material that glows with <paramref name="color"/>: lamps, trim, fire.</summary>
    /// <param name="color">The glow colour.</param>
    /// <param name="energy">Emission strength.</param>
    /// <returns>The material.</returns>
    public static StandardMaterial3D Glow(Color color, float energy = 2f) => Get($"glow-{color.ToHtml()}-{energy}", () =>
    {
        StandardMaterial3D m = Plain(color.Darkened(0.4f), 0f, 0.4f);
        m.EmissionEnabled = true;
        m.Emission = color;
        m.EmissionEnergyMultiplier = energy;

        return m;
    });

    /// <summary>A plain colour, for one-off accent parts.</summary>
    /// <param name="color">Albedo.</param>
    /// <param name="metallic">Metalness.</param>
    /// <param name="roughness">Roughness.</param>
    /// <returns>The material.</returns>
    public static Material Paint(Color color, float metallic = 0.2f, float roughness = 0.5f) =>
        Get($"paint-{color.ToHtml()}-{metallic}-{roughness}", () => Surface(color, metallic, roughness, rust: 0.05f, grime: 0.35f, streaks: 0.25f, wear: 0.35f));

    /// <summary>A glow material as it looks while its machine is idle: the same colour, barely glowing.</summary>
    /// <param name="glow">A material made by <see cref="Glow"/> or <see cref="Fire"/>.</param>
    /// <returns>The dimmed material.</returns>
    public static StandardMaterial3D Dim(StandardMaterial3D glow) => Get($"dim-{glow.GetInstanceId()}", () =>
    {
        StandardMaterial3D m = (StandardMaterial3D)glow.Duplicate();
        m.EmissionEnergyMultiplier = glow.EmissionEnergyMultiplier * 0.12f;
        m.AlbedoColor = glow.AlbedoColor.Darkened(0.5f);

        return m;
    });

    /// <summary>Burning fuel: a flickering ramp from deep red through orange to a near-white core.</summary>
    /// <param name="energy">Emission strength.</param>
    /// <returns>The material.</returns>
    public static StandardMaterial3D Fire(float energy = 2.5f) => Get($"fire-{energy}", () =>
    {
        Image img = Fill((x, y) =>
        {
            float n = (Noise(x, y, 41) * 0.7f) + (Noise(x * 3 % Size, y * 3 % Size, 42) * 0.3f);
            float t = Mathf.Clamp((n - 0.3f) * 1.9f, 0, 1);

            return t < 0.5f ? new Color(0.55f, 0.08f, 0.02f).Lerp(new Color(1f, 0.45f, 0.08f), t * 2) : new Color(1f, 0.45f, 0.08f).Lerp(new Color(1f, 0.9f, 0.6f), (t - 0.5f) * 2);
        });
        img.GenerateMipmaps();
        StandardMaterial3D m = Plain(new Color(0.3f, 0.1f, 0.03f), 0f, 0.6f);
        m.EmissionEnabled = true;
        m.Emission = new Color(1f, 0.5f, 0.15f);
        m.EmissionTexture = ImageTexture.CreateFromImage(img);
        m.EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Multiply;
        m.EmissionEnergyMultiplier = energy;
        m.Uv1Triplanar = true;
        m.Uv1Scale = new Vector3(3, 3, 3);

        return m;
    });

    private static T Get<T>(string key, System.Func<T> make)
        where T : Material
    {
        if (_cache.TryGetValue(key, out Material? m) == false)
        {
            m = make();
            _cache[key] = m;
        }

        return (T)m;
    }

    /// <summary>A surface in the shared weathered material (see Weathered.gdshader).</summary>
    private static ShaderMaterial Surface(Color baseColor, float metallic, float roughness, float rust = 0.3f, float grime = 0.5f, float streaks = 0.5f, float wear = 0.7f, float panel = 0f, Image? pattern = null, float scale = 1f, Color? wearColor = null, Color? rustColor = null)
    {
        ShaderMaterial m = new ShaderMaterial { Shader = _weathered };
        m.SetShaderParameter("base_color", baseColor);
        m.SetShaderParameter("metallic", metallic);
        m.SetShaderParameter("roughness", roughness);
        m.SetShaderParameter("rust", rust);
        m.SetShaderParameter("grime", grime);
        m.SetShaderParameter("streaks", streaks);
        m.SetShaderParameter("wear", wear);
        m.SetShaderParameter("panel", panel);
        m.SetShaderParameter("pattern_scale", scale);
        if (pattern is not null)
        {
            pattern.GenerateMipmaps();
            m.SetShaderParameter("pattern", ImageTexture.CreateFromImage(pattern));
        }

        if (wearColor is { } wc)
        {
            m.SetShaderParameter("wear_color", wc);
        }

        if (rustColor is { } rc)
        {
            m.SetShaderParameter("rust_color", rc);
        }

        return m;
    }

    private static StandardMaterial3D Plain(Color c, float metallic, float roughness) => new()
    {
        AlbedoColor = c,
        Metallic = metallic,
        Roughness = roughness,
        MetallicSpecular = 0.5f,
    };

    private const int Size = 128;

    /// <summary>Tileable value noise, 0..1, summed over a few octaves.</summary>
    private static float Noise(int x, int y, int seed)
    {
        float v = 0, amp = 0.5f, total = 0;
        for (int o = 0; o < 4; o++)
        {
            int cells = 4 << o;
            float fx = x * cells / (float)Size, fy = y * cells / (float)Size;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - (2 * tx));
            ty = ty * ty * (3 - (2 * ty));
            float Corner(int cx, int cy) => (GroundPainter.Hash(((cx % cells) + cells) % cells, ((cy % cells) + cells) % cells, (uint)(seed + (o * 97))) & 0xffff) / 65535f;
            float a = Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), tx);
            float b = Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), tx);
            v += Mathf.Lerp(a, b, ty) * amp;
            total += amp;
            amp *= 0.5f;
        }

        return v / total;
    }

    private static Image Fill(System.Func<int, int, Color> pixel)
    {
        Image img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                img.SetPixel(x, y, pixel(x, y));
            }
        }

        return img;
    }

    private static Image Bricks(Color brick, Color mortar, int seed) => Fill((x, y) =>
    {
        int course = y / 16;
        int bx = (x + (course % 2 * 16)) % Size;
        bool joint = ((y % 16) < 2) || ((bx % 32) < 2);
        if (joint)
        {
            return mortar;
        }

        uint h = GroundPainter.Hash(bx / 32, course, (uint)seed);
        float tone = ((h & 15) / 15f * 0.22f) - 0.11f;

        return brick.Lightened(tone).Darkened((1 - Noise(x, y, seed)) * 0.25f);
    });

    private static Image Planks(Color wood, int seed) => Fill((x, y) =>
    {
        int plank = y / 32;
        if ((y % 32) < 2)
        {
            return wood.Darkened(0.55f);
        }

        uint h = GroundPainter.Hash(plank, 0, (uint)seed);
        float grain = Mathf.Sin((x * 0.25f) + (Noise(x, y, seed) * 9f) + h) * 0.06f;

        return wood.Darkened(((h & 7) / 7f * 0.18f) + grain + ((1 - Noise(x, y, seed + 3)) * 0.12f));
    });

    private static Image Panels(Color hull, int seed) => Fill((x, y) =>
    {
        bool seam = ((x % 64) == 0) || ((y % 64) == 0);
        bool lip = ((x % 64) == 1) || ((y % 64) == 1);

        return seam ? hull.Darkened(0.3f) : lip ? hull.Lightened(0.15f) : hull.Darkened((1 - Noise(x, y, seed)) * 0.06f);
    });

    private static Image Stripes() => Fill((x, y) => ((x + y) % 64) < 32 ? new Color(0.9f, 0.7f, 0.12f).Darkened((1 - Noise(x, y, 31)) * 0.2f) : new Color(0.08f, 0.08f, 0.08f));

    private static Image CellGrid() => Fill((x, y) =>
    {
        bool bus = ((x % 64) < 1) || ((y % 64) < 1);
        bool finger = (x % 16) == 8;

        return bus ? new Color(0.62f, 0.58f, 0.5f) : finger ? new Color(0.24f, 0.24f, 0.26f) : new Color(0.2f, 0.21f, 0.25f).Lightened(Noise(x, y, 33) * 0.12f);
    });
}
