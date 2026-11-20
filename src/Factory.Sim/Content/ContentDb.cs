using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>All static game data. Loaded from JSON so balance can be iterated without code changes.</summary>
public sealed class ContentDb
{
    /// <summary>Item definitions in file order.</summary>
    public List<ItemDef> ItemList { get; init; } = [];
    /// <summary>Resource definitions in file order.</summary>
    public List<ResourceDef> ResourceList { get; init; } = [];
    /// <summary>Recipe definitions in file order.</summary>
    public List<RecipeDef> RecipeList { get; init; } = [];
    /// <summary>Building definitions in file order.</summary>
    public List<BuildingDef> BuildingList { get; init; } = [];
    /// <summary>Vehicle definitions in file order.</summary>
    public List<VehicleDef> VehicleList { get; init; } = [];
    /// <summary>Technology definitions in file order.</summary>
    public List<TechDef> TechList { get; init; } = [];
    /// <summary>Robot models in file order.</summary>
    public List<RobotDef> RobotList { get; init; } = [];
    /// <summary>Starting conditions for a new game.</summary>
    public StartDef Start { get; init; } = new();
    /// <summary>Planet stats and the readiness bar.</summary>
    public TerraformDef Terraform { get; init; } = new();
    /// <summary>The tech ages in order, first to last.</summary>
    public List<AgeDef> AgeList { get; init; } = [];
    /// <summary>Biome definitions in file order.</summary>
    public List<BiomeDef> BiomeList { get; init; } = [];
    /// <summary>The bodies of the star system in file order; the start planet comes first in the world.</summary>
    public List<PlanetDef> PlanetList { get; init; } = [];

    /// <summary>Items indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, ItemDef> Items { get; private set; } = new();
    /// <summary>Resources indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, ResourceDef> Resources { get; private set; } = new();
    /// <summary>Recipes indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, RecipeDef> Recipes { get; private set; } = new();
    /// <summary>Buildings indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, BuildingDef> Buildings { get; private set; } = new();
    /// <summary>Vehicles indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, VehicleDef> Vehicles { get; private set; } = new();
    /// <summary>Technologies indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, TechDef> Techs { get; private set; } = new();
    /// <summary>Robot models indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, RobotDef> Robots { get; private set; } = new();
    /// <summary>Ages indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, AgeDef> Ages { get; private set; } = new();
    /// <summary>Biomes indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, BiomeDef> Biomes { get; private set; } = new();
    /// <summary>Planets indexed by id.</summary>
    [JsonIgnore] public Dictionary<string, PlanetDef> Planets { get; private set; } = new();

    /// <summary>Position of an age in <see cref="AgeList"/> (0 for the first); unknown ids count as the first.</summary>
    /// <param name="age">The age id.</param>
    /// <returns>Its index.</returns>
    public int AgeIndex(string age) => System.Math.Max(0, AgeList.FindIndex(a => a.Id == age));

    /// <summary>Looks up an item by id, throwing <see cref="KeyNotFoundException"/> if it does not exist.</summary>
    public ItemDef Item(string id) => Items.TryGetValue(id, out ItemDef? d) ? d : throw new KeyNotFoundException($"Unknown item '{id}'");
    /// <summary>Maximum number of an item that fits in one inventory slot.</summary>
    public int Stack(string id) => Item(id).Stack;
    /// <summary>True if the item id exists and has a fuel value.</summary>
    public bool IsFuel(string id) => Items.TryGetValue(id, out ItemDef? d) && (d.FuelKj > 0);

    /// <summary>Recipe category of the research units labs craft (see <see cref="TechDef.UnitRecipe"/>).</summary>
    public const string ResearchCategory = "research";

    private const string ResearchPrefix = "research:";

    /// <summary>The effect (and recipe id) of one research unit for a technology.</summary>
    /// <param name="techId">The technology.</param>
    /// <returns><c>research:&lt;tech id&gt;</c>.</returns>
    public static string ResearchEffect(string techId) => ResearchPrefix + techId;

    /// <summary>The technology a recipe effect researches a unit of ("research:&lt;tech id&gt;"), or null.</summary>
    /// <param name="effect">The recipe's effect.</param>
    /// <returns>The tech id, if the effect is research.</returns>
    public static string? TechForEffect(string? effect) =>
        (effect is not null) && effect.StartsWith(ResearchPrefix, StringComparison.Ordinal) ? effect[ResearchPrefix.Length..] : null;

    /// <summary>The robot model a recipe effect assembles ("helper:&lt;robot id&gt;"), or null.</summary>
    /// <param name="effect">The recipe's effect.</param>
    /// <returns>The robot id, if the effect builds one.</returns>
    public static string? RobotForEffect(string? effect) =>
        (effect is not null) && effect.StartsWith("helper:", StringComparison.Ordinal) ? effect["helper:".Length..] : null;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class Raw
    {
        public List<ItemDef> Items { get; init; } = [];
        public List<ResourceDef> Resources { get; init; } = [];
        public List<RecipeDef> Recipes { get; init; } = [];
        public List<BuildingDef> Buildings { get; init; } = [];
        public List<VehicleDef> Vehicles { get; init; } = [];
        public List<TechDef> Techs { get; init; } = [];
        public List<RobotDef> Robots { get; init; } = [];
        public StartDef Start { get; init; } = new();
        public TerraformDef Terraform { get; init; } = new();
        public List<AgeDef> Ages { get; init; } = [];
        public List<BiomeDef> Biomes { get; init; } = [];
        public List<PlanetDef> Planets { get; init; } = [];
    }

    /// <summary>Parses content JSON, builds the lookup indexes and validates cross-references, throwing <see cref="InvalidDataException"/> listing every problem found.</summary>
    public static ContentDb FromJson(string json)
    {
        Raw raw = JsonSerializer.Deserialize<Raw>(json, _options) ?? throw new InvalidDataException("Empty content");
        ContentDb db = new ContentDb
        {
            ItemList = raw.Items,
            ResourceList = raw.Resources,
            RecipeList = raw.Recipes,
            BuildingList = raw.Buildings,
            VehicleList = raw.Vehicles,
            TechList = raw.Techs,
            RobotList = raw.Robots,
            Start = raw.Start,
            Terraform = raw.Terraform,
            AgeList = raw.Ages,
            BiomeList = raw.Biomes,
            PlanetList = raw.Planets,
        };
        db.Index();
        db.Validate();

        return db;
    }

    /// <summary>Loads the base game content embedded in the assembly (base.json).</summary>
    public static ContentDb LoadDefault()
    {
        using Stream s = typeof(ContentDb).Assembly.GetManifestResourceStream("Factory.Sim.Content.base.json")
                      ?? throw new InvalidOperationException("Embedded base.json content not found");
        using StreamReader r = new StreamReader(s);

        return FromJson(r.ReadToEnd());
    }

    // Sorted so iteration order is deterministic regardless of JSON key order.
    private static ItemAmount[] Sorted(Dictionary<string, int> d) =>
        d.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new ItemAmount(kv.Key, kv.Value)).ToArray();

    private void Index()
    {
        Items = ItemList.ToDictionary(i => i.Id);
        Resources = ResourceList.ToDictionary(i => i.Id);
        Recipes = RecipeList.ToDictionary(i => i.Id);
        Buildings = BuildingList.ToDictionary(i => i.Id);
        Vehicles = VehicleList.ToDictionary(i => i.Id);
        Techs = TechList.ToDictionary(i => i.Id);
        Robots = RobotList.ToDictionary(i => i.Id);
        Ages = AgeList.ToDictionary(i => i.Id);
        Biomes = BiomeList.ToDictionary(i => i.Id);
        Planets = PlanetList.ToDictionary(i => i.Id);
        foreach (RecipeDef r in RecipeList)
        {
            r.Inputs = Sorted(r.In);
            r.Outputs = Sorted(r.Out);
        }

        foreach (BuildingDef b in BuildingList)
        {
            b.CostList = Sorted(b.Cost);
            b.LaunchFuelList = Sorted(b.LaunchFuel);
        }

        foreach (VehicleDef v in VehicleList)
        {
            v.CostList = Sorted(v.Cost);
        }

        foreach (TechDef t in TechList)
        {
            t.UnitCostList = Sorted(t.UnitCost);
            t.UnitRecipe = new RecipeDef
            {
                Id = ResearchEffect(t.Id),
                Name = t.Name,
                Category = ResearchCategory,
                In = t.UnitCost,
                Ticks = t.UnitTicks,
                Effect = ResearchEffect(t.Id),
                Inputs = t.UnitCostList,
            };
            Recipes[t.UnitRecipe.Id] = t.UnitRecipe;
        }

        // A building or vehicle belongs to the age of the technology that unlocks it, unless it says otherwise.
        string first = AgeList.Count > 0 ? AgeList[0].Id : "";
        string AgeOf(string id) => TechList.FirstOrDefault(t => t.Unlocks.Contains(id))?.Age ?? first;
        foreach (BuildingDef b in BuildingList)
        {
            b.AgeId = b.Age ?? AgeOf(b.Id);
        }

        foreach (VehicleDef v in VehicleList)
        {
            v.AgeId = AgeOf(v.Id);
        }
    }

    private void Validate()
    {
        List<string> errors = new List<string>();
        void CheckItems(string owner, IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                if (Items.ContainsKey(id) == false)
                {
                    errors.Add($"{owner}: unknown item '{id}'");
                }
            }
        }

        foreach (ItemDef i in ItemList.Where(i => i.Module is not null))
        {
            CheckItems($"module {i.Id}", i.Module!.Consumes.Keys);
        }

        foreach (ResourceDef r in ResourceList)
        {
            CheckItems($"resource {r.Id}", [r.Item]);
        }

        foreach (RecipeDef r in RecipeList)
        {
            CheckItems($"recipe {r.Id}", r.In.Keys);
            CheckItems($"recipe {r.Id}", r.Out.Keys);
            if ((RobotForEffect(r.Effect) is { } robot) && (Robots.ContainsKey(robot) == false))
            {
                errors.Add($"recipe {r.Id}: unknown robot {robot}");
            }

            if ((Planet.TerraformEffect(r.Effect) is { } t) && (Terraform.Stats.Any(s => s.Id == t.Stat) == false))
            {
                errors.Add($"recipe {r.Id}: unknown planet stat {t.Stat}");
            }
        }

        foreach (BuildingDef b in BuildingList)
        {
            CheckItems($"building {b.Id}", b.Cost.Keys);
            if ((b.Recipe is not null) && (Recipes.ContainsKey(b.Recipe) == false))
            {
                errors.Add($"building {b.Id}: unknown recipe {b.Recipe}");
            }
        }

        foreach (VehicleDef v in VehicleList)
        {
            CheckItems($"vehicle {v.Id}", v.Cost.Keys);
        }

        HashSet<string> unlockable = new HashSet<string>(Recipes.Keys.Concat(Buildings.Keys).Concat(Vehicles.Keys));
        foreach (PlanetDef p in PlanetList)
        {
            foreach (BiomeWeight bw in p.Biomes.Where(bw => Biomes.ContainsKey(bw.Biome) == false))
            {
                errors.Add($"planet {p.Id}: unknown biome {bw.Biome}");
            }

            foreach (PlanetResourceDef pr in p.Resources)
            {
                if (Resources.ContainsKey(pr.Resource) == false)
                {
                    errors.Add($"planet {p.Id}: unknown resource {pr.Resource}");
                }

                if ((pr.Biome is not null) && (p.Biomes.Any(bw => bw.Biome == pr.Biome) == false))
                {
                    errors.Add($"planet {p.Id}: resource {pr.Resource} prefers biome {pr.Biome}, which the planet doesn't have");
                }
            }

            foreach (string r in p.Orbital.Where(r => Resources.ContainsKey(r) == false))
            {
                errors.Add($"planet {p.Id}: unknown orbital resource {r}");
            }

            foreach (string stat in p.Stats.Keys.Where(st => Terraform.Stats.Any(x => x.Id == st) == false))
            {
                errors.Add($"planet {p.Id}: unknown stat {stat}");
            }

            if ((p.Kind == PlanetKind.Surface) && (p.Biomes.Length == 0))
            {
                errors.Add($"planet {p.Id}: a surface planet needs at least one biome");
            }
        }

        if ((PlanetList.Count > 0) && (PlanetList.Count(p => p.Start && (p.Kind == PlanetKind.Surface)) != 1))
        {
            errors.Add("planets: exactly one surface planet must be the start planet");
        }

        foreach (AgeDef a in AgeList)
        {
            if (a.Core is not null)
            {
                CheckItems($"age {a.Id}", [a.Core]);
            }
        }

        foreach (BuildingDef b in BuildingList.Where(b => b.Kind == BuildingKind.LaunchPad))
        {
            CheckItems($"building {b.Id}", b.LaunchFuel.Keys.Append(b.Vessel ?? ""));
            if ((b.LandsAs is null) || (Buildings.TryGetValue(b.LandsAs, out BuildingDef? landed) == false) || (landed.Kind != BuildingKind.Ferry))
            {
                errors.Add($"building {b.Id}: a launch pad must land as a Ferry building");
            }
        }

        foreach (BuildingDef b in BuildingList.Where(b => (b.Age is not null) && (Ages.ContainsKey(b.Age) == false)))
        {
            errors.Add($"building {b.Id}: unknown age {b.Age}");
        }

        foreach (TechDef t in TechList)
        {
            CheckItems($"tech {t.Id}", t.UnitCost.Keys);
            if ((AgeList.Count > 0) && (Ages.ContainsKey(t.Age) == false))
            {
                errors.Add($"tech {t.Id}: unknown age {t.Age}");
            }

            foreach (string p in t.Prereqs.Where(p => Techs.ContainsKey(p) && (AgeIndex(Techs[p].Age) > AgeIndex(t.Age))))
            {
                errors.Add($"tech {t.Id}: prereq {p} is from a later age");
            }

            if ((t.LevelOf is not null) && ((Techs.TryGetValue(t.LevelOf, out TechDef? series) == false) || (series.Repeatable == false) || t.Repeatable))
            {
                errors.Add($"tech {t.Id}: levelOf {t.LevelOf} must name a repeatable tech, and {t.Id} must not repeat itself");
            }

            foreach (string p in t.Prereqs)
            {
                if (Techs.ContainsKey(p) == false)
                {
                    errors.Add($"tech {t.Id}: unknown prereq {p}");
                }
            }

            foreach (string u in t.Unlocks)
            {
                if (unlockable.Contains(u) == false)
                {
                    errors.Add($"tech {t.Id}: unknown unlock {u}");
                }
            }
        }

        foreach (string u in Start.Unlocked)
        {
            if (unlockable.Contains(u) == false)
            {
                errors.Add($"start: unknown unlock {u}");
            }
        }

        CheckItems("start lander", Start.Lander.Keys);
        foreach (string robot in new[] { Start.CommanderRobot, Start.HelperRobot })
        {
            if (Robots.ContainsKey(robot) == false)
            {
                errors.Add($"start: unknown robot {robot}");
            }
        }

        List<string> dupes = Recipes.Keys.Intersect(Buildings.Keys).Concat(Recipes.Keys.Intersect(Vehicles.Keys)).Concat(Buildings.Keys.Intersect(Vehicles.Keys)).ToList();
        foreach (string? d in dupes)
        {
            errors.Add($"id '{d}' is used by more than one of recipe/building/vehicle");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException("Content errors:\n" + string.Join("\n", errors));
        }
    }

    /// <summary>Stable hash of the content so replays/saves can detect mismatched game data.</summary>
    public ulong ComputeHash()
    {
        StateHasher h = new StateHasher();
        h.Add(JsonSerializer.Serialize(new
        {
            ItemList,
            ResourceList,
            RecipeList,
            BuildingList,
            VehicleList,
            TechList,
            RobotList,
            Terraform,
            AgeList,
            BiomeList,
            PlanetList,
            Start
        }));

        return h.Value;
    }
}
