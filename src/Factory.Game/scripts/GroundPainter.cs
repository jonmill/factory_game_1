using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Paints the planet's ground into a texture: each biome's palette blended softly into its neighbours,
/// sandy shores, shallow-to-deep water, frozen lakes and lava. It depends only on terrain and biomes,
/// which almost never change, so it is painted once; ore beds and forest floor are drawn over it each frame. The planet
/// wraps on both axes, so every noise layer is periodic and the texture tiles seamlessly.
/// </summary>
public static class GroundPainter
{
    /// <summary>Texture pixels per map tile; enough for pebbles and soft shorelines at normal zoom.</summary>
    public const int PixelsPerTile = 8;

    private static readonly Color _dirt = new(0.35f, 0.29f, 0.20f);
    private static readonly Color _darkDirt = new(0.27f, 0.22f, 0.16f);
    private static readonly Color _dryGrass = new(0.38f, 0.35f, 0.22f);
    private static readonly Color _ice = new(0.78f, 0.88f, 0.94f);
    private static readonly Color _deepIce = new(0.55f, 0.72f, 0.86f);
    private static readonly Color _lavaHot = new(1.0f, 0.55f, 0.12f);
    private static readonly Color _lavaCool = new(0.75f, 0.2f, 0.06f);
    private static readonly Color _lavaCrust = new(0.16f, 0.1f, 0.08f);
    private static readonly Color _sand = new(0.52f, 0.45f, 0.32f);
    private static readonly Color _shallowWater = new(0.18f, 0.36f, 0.40f);
    private static readonly Color _deepWater = new(0.07f, 0.17f, 0.24f);

    /// <summary>A cheap fingerprint of the land/water layout, used to decide whether a repaint is needed.</summary>
    /// <param name="planet">The planet to fingerprint.</param>
    /// <returns>A hash that changes whenever any tile switches between land and water.</returns>
    public static ulong WaterSignature(Planet planet)
    {
        ulong h = 1469598103934665603UL;
        for (int y = 0; y < planet.Height; y++)
        {
            for (int x = 0; x < planet.Width; x++)
            {
                h = (h ^ (ulong)planet.TerrainAt(new Int2(x, y))) * 1099511628211UL;
            }
        }

        return h ^ (ulong)planet.Width ^ ((ulong)planet.Height << 32);
    }

    /// <summary>Builds the full ground image for <paramref name="planet"/>, with mipmaps for zoomed-out views.</summary>
    /// <param name="planet">The planet whose terrain, resources and trees shape the ground.</param>
    /// <returns>An RGBA image of <c>Width × Height × PixelsPerTile²</c> pixels.</returns>
    public static Image Paint(Planet planet)
    {
        int w = planet.Width * PixelsPerTile;
        int h = planet.Height * PixelsPerTile;
        byte[] data = new byte[w * h * 4];
        float[] waterDistance = WaterDistance(planet);
        Palette palette = Palette.For(planet);

        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                Color c = PixelColor(planet, palette, waterDistance, px, py);
                int i = ((py * w) + px) * 4;
                data[i] = (byte)(Mathf.Clamp(c.R, 0, 1) * 255);
                data[i + 1] = (byte)(Mathf.Clamp(c.G, 0, 1) * 255);
                data[i + 2] = (byte)(Mathf.Clamp(c.B, 0, 1) * 255);
                data[i + 3] = 255;
            }
        }

        Image img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();

        return img;
    }

    private static Color PixelColor(Planet planet, Palette palette, float[] waterDistance, int px, int py)
    {
        int tx = px / PixelsPerTile;
        int ty = py / PixelsPerTile;
        float fx = (px + 0.5f) / PixelsPerTile;
        float fy = (py + 0.5f) / PixelsPerTile;
        float grain = (Hash(px, py, 7) & 255) / 255f;

        // Wetness blends the tile grid smoothly and wobbles it with noise, so shorelines curve instead
        // of following tile edges. Tiles stay the rule in the simulation; this is only how they look.
        float wet = Bilinear(planet, fx, fy, t => planet.TerrainAt(t) == Terrain.Water ? 1f : 0f)
            + ((PeriodicNoise(planet, fx, fy, 1.5f, 17) - 0.5f) * 0.4f);
        float lava = Bilinear(planet, fx, fy, t => planet.TerrainAt(t) == Terrain.Lava ? 1f : 0f)
            + ((PeriodicNoise(planet, fx, fy, 1.5f, 23) - 0.5f) * 0.4f);
        if (lava > 0.5f)
        {
            // Molten channels under a cracked dark crust.
            float flow = PeriodicNoise(planet, fx * 1.3f, fy * 1.3f, 2.5f, 29);
            Color molten = _lavaHot.Lerp(_lavaCool, Mathf.Clamp((lava - 0.5f) * 2.5f, 0, 1) * 0.6f);
            return molten.Lerp(_lavaCrust, Mathf.SmoothStep(0.45f, 0.8f, flow)).Darkened(grain * 0.08f);
        }

        if ((wet > 0.5f) && planet.Def.FrozenWater)
        {
            // Frozen lakes: pale ice with darker cracks.
            float crack = PeriodicNoise(planet, fx * 2.1f, fy * 2.1f, 1.2f, 31);
            Color ice = _ice.Lerp(_deepIce, Mathf.Clamp((Bilinear(planet, fx, fy, t => waterDistance[(t.Y * planet.Width) + t.X]) - 0.5f) / 3f, 0, 1));
            return Mathf.Abs(crack - 0.5f) < 0.025f ? ice.Darkened(0.25f) : ice.Lightened((grain - 0.5f) * 0.05f);
        }

        if (wet > 0.5f)
        {
            // Depth grows with distance from land; slow ripples keep large lakes from looking flat.
            float depth = Mathf.Clamp((Bilinear(planet, fx, fy, t => waterDistance[(t.Y * planet.Width) + t.X]) - 0.5f) / 3f, 0, 1);
            float ripple = PeriodicNoise(planet, fx * 1.7f, fy * 0.9f, 2f, 11);
            Color water = _shallowWater.Lerp(_deepWater, depth);
            water = water.Lerp(_shallowWater.Lightened(0.15f), Mathf.SmoothStep(0.62f, 0.5f, wet) * 0.6f);

            return water.Lightened((ripple - 0.5f) * 0.06f).Darkened(grain * 0.03f);
        }

        float region = PeriodicNoise(planet, fx, fy, 16f, 1);
        float patch = PeriodicNoise(planet, fx, fy, 4f, 2);
        // Biome colours blend smoothly across region borders.
        Color baseC = palette.Sample(planet, fx, fy, 0), darkC = palette.Sample(planet, fx, fy, 1), lightC = palette.Sample(planet, fx, fy, 2);
        Color ground = baseC.Lerp(lightC, Mathf.SmoothStep(0.35f, 0.75f, region));
        ground = ground.Lerp(darkC, Mathf.SmoothStep(0.55f, 0.95f, patch) * 0.6f);
        ground = ground.Lightened((grain - 0.5f) * 0.07f);

        // Pebbles and grass tufts: a few pixels per tile, placed by hash so they never shimmer.
        uint h = Hash(tx, ty, 3);
        int lx = px % PixelsPerTile;
        int ly = py % PixelsPerTile;
        if ((lx == (int)(h % PixelsPerTile)) && (ly == (int)((h >> 4) % PixelsPerTile)))
        {
            ground = ((h >> 9) & 1) == 0 ? ground.Darkened(0.22f) : ground.Lightened(0.07f);
        }
        else if ((((h >> 12) & 3) == 0) && (lx == (int)((h >> 14) % PixelsPerTile)) && (ly == (int)((h >> 18) % PixelsPerTile)))
        {
            ground = ground.Lerp(darkC.Lerp(new Color(0.30f, 0.33f, 0.16f), 0.4f), 0.6f);
        }

        if (wet > 0.22f)
        {
            ground = ground.Lerp(planet.Def.FrozenWater ? _ice.Darkened(0.1f) : _sand.Lerp(lightC, 0.3f), Mathf.SmoothStep(0.22f, 0.48f, wet) * 0.8f);
        }

        return ground;
    }

    /// <summary>Each tile's biome colours (base, dark, light), looked up once per paint.</summary>
    private sealed class Palette
    {
        private readonly Color[][] _colors = [[], [], []];

        public static Palette For(Planet planet)
        {
            Palette p = new Palette();
            int n = planet.Width * planet.Height;
            for (int k = 0; k < 3; k++)
            {
                p._colors[k] = new Color[n];
            }

            Color[] fallback = [_dirt, _darkDirt, _dryGrass];
            for (int y = 0; y < planet.Height; y++)
            {
                for (int x = 0; x < planet.Width; x++)
                {
                    BiomeDef? b = planet.BiomeAt(new Int2(x, y));
                    for (int k = 0; k < 3; k++)
                    {
                        p._colors[k][(y * planet.Width) + x] = (b is not null) && (b.Ground.Length > k) ? Color.FromString(b.Ground[k], fallback[k]) : fallback[k];
                    }
                }
            }

            return p;
        }

        /// <summary>Colour <paramref name="k"/> (0 base, 1 dark, 2 light) blended between the four nearest tiles.</summary>
        public Color Sample(Planet planet, float fx, float fy, int k)
        {
            Color[] c = _colors[k];
            float gx = fx - 0.5f, gy = fy - 0.5f;
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
            float sx = gx - x0, sy = gy - y0;
            Color V(int x, int y)
            {
                Int2 t = planet.Torus.Wrap(new Int2(x, y));

                return c[(t.Y * planet.Width) + t.X];
            }

            return V(x0, y0).Lerp(V(x0 + 1, y0), sx).Lerp(V(x0, y0 + 1).Lerp(V(x0 + 1, y0 + 1), sx), sy);
        }
    }

    /// <summary>Bilinear sample of a per-tile value at a point in tile units, wrapping around the planet.</summary>
    private static float Bilinear(Planet planet, float fx, float fy, Func<Int2, float> value)
    {
        float gx = fx - 0.5f;
        float gy = fy - 0.5f;
        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);
        float sx = gx - x0;
        float sy = gy - y0;
        float V(int x, int y) => value(planet.Torus.Wrap(new Int2(x, y)));
        float top = Mathf.Lerp(V(x0, y0), V(x0 + 1, y0), sx);
        float bottom = Mathf.Lerp(V(x0, y0 + 1), V(x0 + 1, y0 + 1), sx);

        return Mathf.Lerp(top, bottom, sy);
    }

    /// <summary>Per-tile distance (in tiles, up to 6) from each water tile to the nearest land.</summary>
    private static float[] WaterDistance(Planet planet)
    {
        int w = planet.Width;
        int h = planet.Height;
        float[] dist = new float[w * h];
        Queue<Int2> frontier = new Queue<Int2>();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool water = planet.TerrainAt(new Int2(x, y)) == Terrain.Water;
                dist[(y * w) + x] = water ? 6f : 0;
                if (water == false)
                {
                    frontier.Enqueue(new Int2(x, y));
                }
            }
        }

        while (frontier.Count > 0)
        {
            Int2 t = frontier.Dequeue();
            float d = dist[(t.Y * w) + t.X];
            if (d >= 6)
            {
                continue;
            }

            foreach (Dir dir in Dirs.All)
            {
                Int2 n = planet.Torus.Wrap(t + dir.Vec());
                int ni = (n.Y * w) + n.X;
                if (dist[ni] > d + 1)
                {
                    dist[ni] = d + 1;
                    frontier.Enqueue(n);
                }
            }
        }

        return dist;
    }

    /// <summary>Smooth value noise in [0, 1] that repeats exactly once across the planet.</summary>
    /// <param name="planet">Supplies the wrap period.</param>
    /// <param name="fx">X position in tiles.</param>
    /// <param name="fy">Y position in tiles.</param>
    /// <param name="cellTiles">Approximate feature size in tiles.</param>
    /// <param name="seed">Decorrelates independent layers.</param>
    private static float PeriodicNoise(Planet planet, float fx, float fy, float cellTiles, uint seed)
    {
        int cellsX = Mathf.Max(1, Mathf.RoundToInt(planet.Width / cellTiles));
        int cellsY = Mathf.Max(1, Mathf.RoundToInt(planet.Height / cellTiles));
        float gx = fx / planet.Width * cellsX;
        float gy = fy / planet.Height * cellsY;
        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);
        float sx = Mathf.SmoothStep(0, 1, gx - x0);
        float sy = Mathf.SmoothStep(0, 1, gy - y0);
        float Lattice(int x, int y) => (Hash(Mod(x, cellsX), Mod(y, cellsY), seed) & 1023) / 1023f;
        float top = Mathf.Lerp(Lattice(x0, y0), Lattice(x0 + 1, y0), sx);
        float bottom = Mathf.Lerp(Lattice(x0, y0 + 1), Lattice(x0 + 1, y0 + 1), sx);

        return Mathf.Lerp(top, bottom, sy);
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;

    /// <summary>Stable integer hash used for all procedural decoration.</summary>
    /// <param name="x">First coordinate.</param>
    /// <param name="y">Second coordinate.</param>
    /// <param name="seed">Layer seed.</param>
    /// <returns>A well-mixed 32-bit value.</returns>
    public static uint Hash(int x, int y, uint seed = 0)
    {
        uint h = (uint)((x * 374761393) + (y * 668265263)) ^ (seed * 2246822519u);
        h = (h ^ (h >> 13)) * 1274126177;

        return h ^ (h >> 16);
    }
}
