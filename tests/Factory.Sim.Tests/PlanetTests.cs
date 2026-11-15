namespace Factory.Sim.Tests;

/// <summary>The star system: planets generated from their definitions, biomes and resources.</summary>
public class PlanetTests
{
    [Test]
    public async Task World_GeneratesEverySurfacePlanet_StartPlanetFirst()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 7, OtherPlanetSize = 64 });
        int surfaces = TestWorld.Content.PlanetList.Count(d => d.Kind == PlanetKind.Surface);

        await Assert.That(w.Planets.Count).IsEqualTo(surfaces);
        await Assert.That(w.Planets[0].Def.Start).IsTrue();
        await Assert.That(w.Planets.Skip(1).All(p => p.Units.Count == 0)).IsTrue();
    }

    [Test]
    public async Task EachPlanet_HoldsOnlyItsOwnResources()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 7, OtherPlanetSize = 64 });
        foreach (Planet p in w.Planets)
        {
            HashSet<string> allowed = p.Def.Resources.Select(r => r.Resource).ToHashSet();
            allowed.UnionWith(TestWorld.Content.ResourceList.Where(r => r.IsTree).Select(r => r.Id));
            HashSet<string> found = [];
            for (int y = 0; y < p.Height; y++)
            {
                for (int x = 0; x < p.Width; x++)
                {
                    if (p.ResourceAt(new Int2(x, y)) is { } r)
                    {
                        found.Add(r.Id);
                    }
                }
            }

            await Assert.That(found.IsSubsetOf(allowed)).IsTrue().Because($"{p.Name} has {string.Join(",", found.Except(allowed))}");
            await Assert.That(found.Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task Planets_AreTheSameForTheSameSeed_AndDifferForAnother()
    {
        World a = new World(TestWorld.Content, new WorldSettings { Seed = 11, OtherPlanetSize = 64 });
        World b = new World(TestWorld.Content, new WorldSettings { Seed = 11, OtherPlanetSize = 64 });
        World c = new World(TestWorld.Content, new WorldSettings { Seed = 12, OtherPlanetSize = 64 });

        await Assert.That(a.Checksum()).IsEqualTo(b.Checksum());
        await Assert.That(a.Checksum()).IsNotEqualTo(c.Checksum());
    }

    [Test]
    public async Task Cinder_HasLavaThatBlocksBuildingAndWalking()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 3, OtherPlanetSize = 96 });
        Planet cinder = w.Planets.First(p => p.Def.Id == "cinder");
        Int2? lava = null;
        for (int i = 0; (i < cinder.Width * cinder.Height) && (lava is null); i++)
        {
            Int2 t = new Int2(i % cinder.Width, i / cinder.Width);
            if (cinder.TerrainAt(t) == Terrain.Lava)
            {
                lava = t;
            }
        }

        await Assert.That(lava).IsNotNull();
        await Assert.That(cinder.IsWalkable(lava!.Value)).IsFalse();
        await Assert.That(cinder.IsAreaFree(lava.Value, 1, 1)).IsFalse();
    }

    [Test]
    public async Task SystemReadiness_AveragesTheThreeMostReadyTerraformablePlanets()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 1, OtherPlanetSize = 64 });

        await Assert.That(w.TerraformablePlanets.Select(p => p.Def.Id)).IsEquivalentTo(["keplers-rest", "cinder", "rime", "dune"]);
        await Assert.That(w.PlanetsNeeded).IsEqualTo(3);
        int[] top = w.TerraformablePlanets.Select(p => p.Readiness).OrderDescending().Take(3).ToArray();
        await Assert.That(w.SystemReadiness).IsEqualTo(top.Sum() / 3);
    }

    [Test]
    public async Task PlacingAGhost_NeedsOneOfYourRobotsOnThatPlanet()
    {
        World w = new World(TestWorld.Content, new WorldSettings { Seed = 1, OtherPlanetSize = 64 });
        Planet other = w.Planets[1];
        Command place = new PlaceBuilding("chest", new Int2(5, 5), Dir.North) { PlanetId = other.Id };

        await Assert.That(place.Validate(w)).Contains("None of your robots");
    }
}
