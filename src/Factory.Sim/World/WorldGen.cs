namespace Factory.Sim;

/// <summary>Seeded, integer-only planet generation (no floating point so it is identical on every machine).</summary>
public static class WorldGen
{
    /// <summary>Fills a freshly created planet with terrain, lakes, resource patches and trees.</summary>
    /// <param name="p">Planet to populate.</param>
    /// <param name="rng">World random generator; consumed deterministically.</param>
    /// <param name="settings">World settings; nothing is generated when EmptyPlanet is set.</param>
    public static void Generate(Planet p, Rng rng, WorldSettings settings)
    {
        if (settings.EmptyPlanet)
        {
            return;
        }

        ContentDb content = p.World.Content;
        Int2 center = new Int2(p.Width / 2, p.Height / 2);
        Func<string, ResourceDef> R = (string id) => content.Resources[id];

        // Guaranteed starter patches near the lander, then extras scattered around the planet.
        // Galena is deliberately far away: it is the first reason to build a railway.
        Blob(p, rng, Near(rng, p, center, 9, 14), 6, Terrain.Water);
        Patch(p, rng, Near(rng, p, center, 10, 16), R("siderite"), 6, 900);
        Patch(p, rng, Near(rng, p, center, 10, 16), R("cuprite"), 5, 800);
        Patch(p, rng, Near(rng, p, center, 10, 18), R("carbon"), 5, 800);
        Patch(p, rng, Near(rng, p, center, 10, 18), R("regolith"), 4, 700);
        Patch(p, rng, Near(rng, p, center, 34, 46), R("galena"), 6, 1000);

        for (int i = 0; i < 3; i++)
        {
            Blob(p, rng, Far(rng, p, center, 25), rng.Range(4, 9), Terrain.Water);
        }

        foreach (string? id in new[] { "siderite", "cuprite", "carbon", "regolith", "galena" })
        {
            for (int i = 0; i < 2; i++)
            {
                Patch(p, rng, Far(rng, p, center, 30), R(id), rng.Range(5, 9), 1400);
            }
        }

        // Forests: clusters of trees.
        for (int i = 0; i < 40; i++)
        {
            Int2 c = Far(rng, p, center, 8);
            int r = rng.Range(3, 7);
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if ((x * x) + (y * y) > r * r)
                    {
                        continue;
                    }

                    if (rng.Range(0, 100) > 55)
                    {
                        continue;
                    }

                    Int2 t = new Int2(c.X + x, c.Y + y);
                    if ((p.TerrainAt(t) != Terrain.Ground) || (p.ResourceAt(t) is not null))
                    {
                        continue;
                    }

                    p.SetResource(t, R("xylo"), rng.Range(20, 40));
                }
            }
        }

        // Clear the landing zone.
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
                if (p.TerrainAt(t) == Terrain.Water)
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
