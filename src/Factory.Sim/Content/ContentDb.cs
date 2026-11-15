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

    /// <summary>Looks up an item by id, throwing <see cref="KeyNotFoundException"/> if it does not exist.</summary>
    public ItemDef Item(string id) => Items.TryGetValue(id, out ItemDef? d) ? d : throw new KeyNotFoundException($"Unknown item '{id}'");
    /// <summary>Maximum number of an item that fits in one inventory slot.</summary>
    public int Stack(string id) => Item(id).Stack;
    /// <summary>True if the item id exists and has a fuel value.</summary>
    public bool IsFuel(string id) => Items.TryGetValue(id, out ItemDef? d) && (d.FuelKj > 0);

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
        foreach (RecipeDef r in RecipeList)
        {
            r.Inputs = Sorted(r.In);
            r.Outputs = Sorted(r.Out);
        }

        foreach (BuildingDef b in BuildingList)
        {
            b.CostList = Sorted(b.Cost);
        }

        foreach (VehicleDef v in VehicleList)
        {
            v.CostList = Sorted(v.Cost);
        }

        foreach (TechDef t in TechList)
        {
            t.UnitCostList = Sorted(t.UnitCost);
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
        foreach (TechDef t in TechList)
        {
            CheckItems($"tech {t.Id}", t.UnitCost.Keys);
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
            Start
        }));

        return h.Value;
    }
}
