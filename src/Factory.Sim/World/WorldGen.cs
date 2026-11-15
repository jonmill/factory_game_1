namespace Factory.Sim;

/// <summary>Seeded, integer-only planet generation (no floating point so it is identical on every machine).</summary>
public static class WorldGen
{
    /// <summary>
    /// Fills a freshly created planet from its definition: biome regions, lakes and lava, deposits and
    /// forests. The same seed always gives the same planet, but each seed varies the layout.
    /// </summary>
    /// <param name="p">Planet to populate.</param>
    /// <param name="rng">The planet's own random generator; consumed deterministically.</param>
    /// <param name="settings">World settings; nothing is generated when EmptyPlanet is set.</param>
    public static void Generate(Planet p, Rng rng, WorldSettings settings)
    {
        if (settings.EmptyPlanet)
        {
            return;
        }

        ContentDb content = p.World.Content;
        PlanetDef def = p.Def;
        Int2 center = new Int2(p.Width / 2, p.Height / 2);

        Biomes(p, rng, center);

        // A lake near the landing site gives the first steam engine its water.
        if (def.Start)
        {
            Blob(p, rng, Near(rng, p, center, 9, 14), 6, Terrain.Water);
        }

        for (int i = 0; i < def.Lakes; i++)
        {
            Blob(p, rng, Far(rng, p, center, 25), rng.Range(4, 9), Terrain.Water);
        }

        for (int i = 0; i < def.LavaFlows; i++)
        {
            // Flows are chains of blobs, so they snake across the map instead of pooling.
            Int2 at = Far(rng, p, center, 20);
            int length = rng.Range(3, 7);
            for (int k = 0; k < length; k++)
            {
                Blob(p, rng, at, rng.Range(2, 5), Terrain.Lava);
                at = p.Torus.Wrap(at + new Int2(rng.Range(-5, 6), rng.Range(-5, 6)));
            }
        }

        foreach (PlanetResourceDef pr in def.Resources)
        {
            ResourceDef res = content.Resources[pr.Resource];
            for (int i = 0; i < pr.Patches; i++)
            {
                Int2 at = PatchSite(p, rng, center, pr);
                Patch(p, rng, at, res, rng.Range(pr.MinRadius, pr.MaxRadius + 1), pr.Richness);
            }
        }

        Forests(p, rng, content);

        // Clear the landing zone.
        if (def.Start)
        {
            for (int y = -6; y <= 6; y++)
            {
                for (int x = -6; x <= 6; x++)
                {
                    Int2 t = new Int2(center.X + x, center.Y + y);
                    p.SetTerrain(t, Terrain.Ground);
                    if (p.ResourceAt(t) is { IsTree: true })
                    {
                        p.SetResource(t, null, 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Splits the planet into biome regions: scattered seed points each claim the tiles nearest them,
    /// with a coarse wobble so borders meander. The landing site always sits in the first biome.
    /// </summary>
    private static void Biomes(Planet p, Rng rng, Int2 center)
    {
        BiomeWeight[] biomes = p.Def.Biomes;
        if (biomes.Length <= 1)
        {
            return;
        }

        int total = biomes.Sum(b => b.Weight);
        int count = System.Math.Max(8, p.Width * p.Height / 600);
        List<(Int2 At, int Biome)> seeds = [(center, 0)];
        for (int i = 1; i < count; i++)
        {
            int roll = rng.Range(0, total), biome = 0;
            while (roll >= biomes[biome].Weight)
            {
                roll -= biomes[biome].Weight;
                biome++;
            }

            seeds.Add((new Int2(rng.Range(0, p.Width), rng.Range(0, p.Height)), biome));
        }

        int salt = rng.Range(0, 1 << 20);
        for (int y = 0; y < p.Height; y++)
        {
            for (int x = 0; x < p.Width; x++)
            {
                // Shift the sample point by up to ±6 tiles, varying smoothly across the map, plus a
                // little per-tile roughness, so borders meander instead of running straight.
                Int2 wobble = new Int2(Smooth(x, y, salt, 12, 6) + (int)(Hash(x, y, salt + 2) % 3) - 1, Smooth(x, y, salt + 1, 12, 6) + (int)(Hash(x, y, salt + 3) % 3) - 1);
                Int2 t = new Int2(x, y) + wobble;
                int best = 0;
                long bestD = long.MaxValue;
                for (int k = 0; k < seeds.Count; k++)
                {
                    long d = p.Torus.DistSq(p.Torus.Wrap(t), seeds[k].At);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = seeds[k].Biome;
                    }
                }

                p.SetBiome(new Int2(x, y), best);
            }
        }
    }

    /// <summary>Picks a patch centre within the deposit's distance band, in its preferred biome when it has one.</summary>
    private static Int2 PatchSite(Planet p, Rng rng, Int2 center, PlanetResourceDef pr)
    {
        Int2 at = center;
        for (int tries = 0; tries < 40; tries++)
        {
            at = pr.MaxDistance > 0 ? Near(rng, p, center, pr.MinDistance, pr.MaxDistance) : Far(rng, p, center, System.Math.Max(pr.MinDistance, 20));
            if ((pr.Biome is null) || (p.BiomeAt(at)?.Id == pr.Biome))
            {
                break;
            }
        }

        return at;
    }

    /// <summary>Grows trees in clumps, as thick as each biome allows.</summary>
    private static void Forests(Planet p, Rng rng, ContentDb content)
    {
        ResourceDef? tree = content.ResourceList.FirstOrDefault(r => r.IsTree);
        if ((tree is null) || p.Def.Biomes.All(b => content.Biomes[b.Biome].Trees == 0))
        {
            return;
        }

        int salt = rng.Range(0, 1 << 20);
        for (int y = 0; y < p.Height; y++)
        {
            for (int x = 0; x < p.Width; x++)
            {
                Int2 t = new Int2(x, y);
                int density = p.BiomeAt(t)?.Trees ?? 0;
                if ((density == 0) || (p.TerrainAt(t) != Terrain.Ground) || (p.ResourceAt(t) is not null))
                {
                    continue;
                }

                // Clumps: a coarse 5-tile cell scales the chance from nothing to double.
                int clump = (int)(Hash(x / 5, y / 5, salt) % 3);
                if (rng.Range(0, 100) < density * clump)
                {
                    p.SetResource(t, tree, rng.Range(20, 40));
                }
            }
        }
    }

    /// <summary>
    /// Integer value noise: random values on a grid of <paramref name="cell"/>-tile squares, blended
    /// bilinearly, in the range ±<paramref name="amp"/>.
    /// </summary>
    private static int Smooth(int x, int y, int salt, int cell, int amp)
    {
        int cx = x / cell, cy = y / cell, fx = x % cell, fy = y % cell;
        int V(int i, int j) => (int)(Hash(cx + i, cy + j, salt) % (uint)((2 * amp) + 1)) - amp;
        int sum = (V(0, 0) * (cell - fx) * (cell - fy)) + (V(1, 0) * fx * (cell - fy)) + (V(0, 1) * (cell - fx) * fy) + (V(1, 1) * fx * fy);

        return sum / (cell * cell);
    }

    /// <summary>A cheap integer hash of a cell, for smooth-looking but deterministic variation.</summary>
    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791));
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;

        return h;
    }

    private static Int2 Near(Rng rng, Planet p, Int2 c, int minD, int maxD)
    {
        while (true)
        {
            Int2 d = new Int2(rng.Range(-maxD, maxD + 1), rng.Range(-maxD, maxD + 1));
            long dist = ((long)d.X * d.X) + ((long)d.Y * d.Y);
            if ((dist >= (long)minD * minD) && (dist <= (long)maxD * maxD))
            {
                return p.Torus.Wrap(c + d);
            }
        }
    }

    private static Int2 Far(Rng rng, Planet p, Int2 c, int minD)
    {
        while (true)
        {
            Int2 t = new Int2(rng.Range(0, p.Width), rng.Range(0, p.Height));
            if (p.Torus.DistSq(t, c) >= (long)minD * minD)
            {
                return t;
            }
        }
    }

    private static void Patch(Planet p, Rng rng, Int2 c, ResourceDef res, int r, int richness)
    {
        for (int y = -r - 1; y <= r + 1; y++)
        {
            for (int x = -r - 1; x <= r + 1; x++)
            {
                int d2 = (x * x) + (y * y);
                int jitter = rng.Range(-r, r + 1);
                if (d2 > (r * r) + jitter)
                {
                    continue;
                }

                Int2 t = new Int2(c.X + x, c.Y + y);
                if (p.TerrainAt(t) != Terrain.Ground)
                {
                    continue;
                }
                // Richer in the middle.
                int amount = (richness * ((r * r) + r - d2) / ((r * r) + r)) + rng.Range(50, 200);
                p.SetResource(t, res, System.Math.Max(50, amount));
            }
        }
    }

    private static void Blob(Planet p, Rng rng, Int2 c, int r, Terrain terrain)
    {
        for (int y = -r - 1; y <= r + 1; y++)
        {
            for (int x = -r - 1; x <= r + 1; x++)
            {
                int d2 = (x * x) + (y * y);
                if (d2 > (r * r) + rng.Range(-r, r + 1))
                {
                    continue;
                }

                Int2 t = new Int2(c.X + x, c.Y + y);
                p.SetTerrain(t, terrain);
                p.SetResource(t, null, 0);
            }
        }
    }
}
