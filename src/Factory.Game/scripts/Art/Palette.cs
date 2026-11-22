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
    private static readonly Dictionary<string, StandardMaterial3D> _cache = [];

    /// <summary>Weathered iron with rust and grime.</summary>
    public static StandardMaterial3D Iron => Get("iron", () => Textured(Weathered(new Color(0.42f, 0.38f, 0.34f), new Color(0.58f, 0.3f, 0.13f), 11), 0.55f, 0.6f, 1.5f));

    /// <summary>Dark, heavily used iron for frames and fittings.</summary>
    public static StandardMaterial3D DarkIron => Get("dark-iron", () => Textured(Weathered(new Color(0.22f, 0.2f, 0.19f), new Color(0.36f, 0.2f, 0.1f), 12), 0.55f, 0.6f, 1.5f));

    /// <summary>Rust-red iron.</summary>
    public static StandardMaterial3D Rust => Get("rust", () => Textured(Weathered(new Color(0.48f, 0.25f, 0.13f), new Color(0.3f, 0.15f, 0.08f), 13), 0.3f, 0.85f, 2f));

    /// <summary>Fire brick in courses.</summary>
    public static StandardMaterial3D Brick => Get("brick", () => Textured(Bricks(new Color(0.66f, 0.42f, 0.3f), new Color(0.32f, 0.28f, 0.25f), 14), 0f, 0.95f, 2f));

    /// <summary>Timber planks.</summary>
    public static StandardMaterial3D Timber => Get("timber", () => Textured(Planks(new Color(0.55f, 0.39f, 0.22f), 15), 0f, 0.85f, 2f));

    /// <summary>Copper, for coils and wire.</summary>
    public static StandardMaterial3D Copper => Get("copper", () => Plain(new Color(0.8f, 0.48f, 0.28f), 0.9f, 0.35f));

    /// <summary>Safety-yellow painted steel.</summary>
    public static StandardMaterial3D Yellow => Get("yellow", () => Textured(Weathered(new Color(0.85f, 0.66f, 0.18f), new Color(0.45f, 0.36f, 0.2f), 16, 0.25f), 0.2f, 0.5f, 1.5f));

    /// <summary>Oxide-red painted steel.</summary>
    public static StandardMaterial3D Oxide => Get("oxide", () => Textured(Weathered(new Color(0.56f, 0.2f, 0.13f), new Color(0.3f, 0.14f, 0.1f), 17, 0.25f), 0.2f, 0.55f, 1.5f));

    /// <summary>Bright machined steel.</summary>
    public static StandardMaterial3D Steel => Get("steel", () => Textured(Weathered(new Color(0.62f, 0.64f, 0.66f), new Color(0.4f, 0.4f, 0.4f), 18, 0.2f), 0.85f, 0.3f, 2f));

    /// <summary>Black rubber and plastic.</summary>
    public static StandardMaterial3D Rubber => Get("rubber", () => Plain(new Color(0.08f, 0.08f, 0.08f), 0f, 0.8f));

    /// <summary>Yellow and black hazard stripes.</summary>
    public static StandardMaterial3D Hazard => Get("hazard", () => Textured(Stripes(), 0.1f, 0.55f, 2f));

    /// <summary>Bone-grey ceramic hull panels with fine seams.</summary>
    public static StandardMaterial3D Hull => Get("hull", () => Textured(Panels(new Color(0.6f, 0.57f, 0.51f), 19), 0.08f, 0.5f, 1f));

    /// <summary>Grey composite for the darker parts of high-tech machines.</summary>
    public static StandardMaterial3D HullDark => Get("hull-dark", () => Textured(Panels(new Color(0.27f, 0.28f, 0.29f), 20), 0.45f, 0.45f, 1f));

    /// <summary>Dark, glossy glass.</summary>
    public static StandardMaterial3D Glass => Get("glass", () => Plain(new Color(0.03f, 0.06f, 0.09f), 0.65f, 0.05f));

    /// <summary>Tinted see-through glass for domes and windows.</summary>
    public static StandardMaterial3D ClearGlass => Get("clear-glass", () =>
    {
        StandardMaterial3D m = Plain(new Color(0.2f, 0.4f, 0.5f, 0.45f), 0.5f, 0.04f);
        m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        return m;
    });

    /// <summary>Photovoltaic cells: deep blue with a silver grid.</summary>
    public static StandardMaterial3D Cells => Get("cells", () => Textured(CellGrid(), 0.4f, 0.12f, 2f));

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
    public static StandardMaterial3D Paint(Color color, float metallic = 0.2f, float roughness = 0.5f) =>
        Get($"paint-{color.ToHtml()}-{metallic}-{roughness}", () => Plain(color, metallic, roughness));

    /// <summary>The unshaded version of a glow material, for the glow pass.</summary>
    /// <param name="glow">A material made by <see cref="Glow"/>.</param>
    /// <returns>An unshaded material in the glow colour.</returns>
    public static StandardMaterial3D GlowOnly(StandardMaterial3D glow) => Get($"only-{glow.Emission.ToHtml()}", () =>
        new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = glow.Emission });

    private static StandardMaterial3D Get(string key, System.Func<StandardMaterial3D> make)
    {
        if (_cache.TryGetValue(key, out StandardMaterial3D? m) == false)
        {
            m = make();
            _cache[key] = m;
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

    private static StandardMaterial3D Textured(Image img, float metallic, float roughness, float scale)
    {
        img.GenerateMipmaps();
        StandardMaterial3D m = Plain(Colors.White, metallic, roughness);
        m.AlbedoTexture = ImageTexture.CreateFromImage(img);
        m.Uv1Triplanar = true;
        m.Uv1Scale = new Vector3(scale, scale, scale);
        m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;

        return m;
    }

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

    /// <summary>A base colour broken up by soft grime and patches of a second colour (rust, dirt).</summary>
    private static Image Weathered(Color baseColor, Color patch, int seed, float patchAmount = 0.6f) => Fill((x, y) =>
    {
        float grime = Noise(x, y, seed);
        float spots = Mathf.Clamp((Noise(x, y, seed + 50) - 0.55f) * 4f, 0, 1) * patchAmount;
        float streak = Noise(x / 4, y, seed + 9) * 0.15f;

        return baseColor.Lerp(patch, spots).Darkened(((1 - grime) * 0.3f) + streak);
    });

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

        return bus ? new Color(0.45f, 0.5f, 0.6f) : finger ? new Color(0.14f, 0.22f, 0.42f) : new Color(0.1f, 0.17f, 0.36f).Lightened(Noise(x, y, 33) * 0.1f);
    });
}
