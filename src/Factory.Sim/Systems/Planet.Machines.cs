namespace Factory.Sim;

/// <summary>Drills, furnaces, fabricators, labs, robot bays and sorters.</summary>
public sealed partial class Planet
{
    /// <summary>The repeatable tech that speeds up research stations.</summary>
    public const string ResearchSpeed = "research-speed";

    /// <summary>Extra research station speed per level of <see cref="ResearchSpeed"/>, in percent.</summary>
    public const int ResearchSpeedPct = 20;

    private const int MaxFuelStock = 10;
    private const int DrillBuffer = 10;

    internal static long PerTickJ(int kw) => kw * 1000L / World.TicksPerSecond;

    /// <summary>How many of an item a building will accept right now (used by drills, sorters, haulers, the leader).</summary>
    public int AcceptAmount(Building b, string item)
    {
        ContentDb c = World.Content;
        int Cap(int max) => System.Math.Clamp(max - b.Inv.Count(item), 0, b.Inv.SpaceFor(item));
        bool burner = (b.Def.Energy == EnergyKind.Burner) && ((b.Def.Fuels.Length == 0) || b.Def.Fuels.Contains(item));

        switch (b.Def.Kind)
        {
            case BuildingKind.Lander or BuildingKind.Chest or BuildingKind.RailStop or BuildingKind.Ferry or BuildingKind.LaunchPad:
                return b.Inv.SpaceFor(item);
            case BuildingKind.Furnace:
                {
                    RecipeDef? smelt = c.RecipeList.FirstOrDefault(r => (r.Category == "smelting") && r.In.ContainsKey(item) && World.Research.Unlocked.Contains(r.Id));
                    if (smelt is not null)
                    {
                        // Don't mix ores in one furnace.
                        bool otherOre = b.Inv.Items.Any(kv => (kv.Key != item) && (c.IsFuel(kv.Key) == false));
                        if (otherOre)
                        {
                            return 0;
                        }

                        if (b.Crafting && (b.Recipe is { } cur) && (cur.In.ContainsKey(item) == false))
                        {
                            return 0;
                        }

                        return Cap(smelt.In[item] * 10);
                    }

                    return burner && c.IsFuel(item) ? Cap(MaxFuelStock) : 0;
                }
            case BuildingKind.LogisticsStation or BuildingKind.OrbitalStation:
                return b.StationSlots.FirstOrDefault(s => s.Item == item) is { } slot ? Cap(StationCap(b, slot)) : 0;
            case BuildingKind.Drill or BuildingKind.SteamEngine:
                return burner && c.IsFuel(item) ? Cap(MaxFuelStock) : 0;
            case BuildingKind.PayloadLauncher:
                return c.RecipeList.FirstOrDefault(r => b.Def.Categories.Contains(r.Category) && r.In.ContainsKey(item)) is { } payload ? Cap(payload.In[item] * 10) : 0;
            case BuildingKind.Assembler or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink:
                {
                    RecipeDef? r = b.Recipe;
                    if (ModuleSupply(b, item) is int perCraft and > 0)
                    {
                        return Cap(perCraft * 10);
                    }

                    if ((r is null) || (r.In.TryGetValue(item, out int need) == false))
                    {
                        return 0;
                    }

                    // Fabricators hold four crafts' worth; climate engines, whose surges are quick, ten; the rest two.
                    int crafts = b.Def.Kind == BuildingKind.Assembler ? 4 : (b.Def.Kind == BuildingKind.Terraformer) && (b.Def.Recipe is null) ? 10 : 2;

                    return Cap(need * crafts);
                }
            case BuildingKind.Lab:
                {
                    TechDef? tech = CurrentTech;
                    if ((tech is null) || (tech.UnitCost.TryGetValue(item, out int need) == false))
                    {
                        return 0;
                    }

                    return Cap(need * 10);
                }
            default:
                return 0;
        }
    }

    /// <summary>The inventory others take products from.</summary>
    public static Inventory OutputOf(Building b) => b.Out;

    /// <summary>True while a powered space elevator stands on this planet.</summary>
    public bool HasSpaceElevator => Buildings.Any(b => (b.Def.Kind == BuildingKind.SpaceElevator) && (b.Satisfaction > 0));

    private TechDef? CurrentTech => World.Research.Current is { } id ? World.Content.Techs[id] : null;

    /// <summary>The research unit labs start next: the current technology's unit recipe, or null.</summary>
    private RecipeDef? CurrentResearchUnit => CurrentTech?.UnitRecipe;

    /// <summary>Whether an electric building wants power this tick (drives network demand).</summary>
    private bool WantsPower(Building b)
    {
        switch (b.Def.Kind)
        {
            case BuildingKind.Drill:
                return (b.Out.Total < DrillBuffer) && (FindMinable(b) is not null);
            case BuildingKind.Assembler or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink:
                return b.Crafting || ((b.Recipe is { } r) && b.Inv.Has(r.Inputs) && b.Out.CanAddAll(r.Outputs));
            case BuildingKind.Lab:
                return b.Crafting || ((CurrentResearchUnit is { } u) && b.Inv.Has(u.Inputs));
            case BuildingKind.PayloadLauncher:
                return b.Crafting || (NextPayload(b) is not null);
            case BuildingKind.Sorter:
                return b.Progress < b.Def.Period * 100L;
            case BuildingKind.LogisticsStation or BuildingKind.OrbitalStation or BuildingKind.SpaceElevator:
                return true;
            default:
                return false;
        }
    }

    private bool BurnerHasEnergy(Building b, long needJ)
    {
        ContentDb c = World.Content;
        while (b.FuelJ < needJ)
        {
            string? fuel = null;
            foreach ((string? k, int _) in b.Inv.Items)
            {
                if ((c.IsFuel(k) == false) || ((b.Def.Fuels.Length > 0) && (b.Def.Fuels.Contains(k) == false)))
                {
                    continue;
                }

                if ((b.Recipe is { } r) && r.In.ContainsKey(k))
                {
                    continue;
                }

                fuel = k;
                break;
            }

            if (fuel is null)
            {
                return false;
            }

            b.Inv.Remove(fuel, 1);
            World.Stats.Record(ItemFlow.Fuel, fuel, 1, b.Def.Id, "", Id);
            b.FuelJ += c.Item(fuel).FuelKj * 1000L;
            b.BurningFuel = fuel;
        }

        return true;
    }

    /// <summary>Energy available to a consumer this tick, 0..1000. Burns fuel for burner buildings.</summary>
    private int EnergyFactor(Building b)
    {
        switch (b.Def.Energy)
        {
            case EnergyKind.None:
                return 1000;
            case EnergyKind.Burner:
                {
                    long need = PerTickJ(b.Def.PowerKw);
                    if (BurnerHasEnergy(b, need) == false)
                    {
                        b.Status = MachineStatus.NoFuel;

                        return 0;
                    }

                    b.FuelJ -= need;

                    return 1000;
                }
            default:
                if (b.Satisfaction <= 0)
                {
                    b.Status = MachineStatus.NoPower;

                    return 0;
                }

                return b.Satisfaction;
        }
    }

    private void UpdateBuilding(Building b)
    {
        switch (b.Def.Kind)
        {
            case BuildingKind.Drill:
                UpdateDrill(b);
                break;
            case BuildingKind.Furnace or BuildingKind.Assembler or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink or BuildingKind.Lander or BuildingKind.PayloadLauncher or BuildingKind.Lab:
                UpdateCrafter(b);
                break;
            case BuildingKind.Sorter:
                UpdateSorter(b);
                break;
            case BuildingKind.Belt:
                UpdateBelt(b);
                break;
            case BuildingKind.LogisticsStation or BuildingKind.OrbitalStation:
                UpdateStation(b);
                break;
            case BuildingKind.SpaceElevator:
                b.Status = EnergyFactor(b) > 0 ? MachineStatus.Working : MachineStatus.NoPower;
                break;
        }
    }

    /// <summary>
    /// The tile a drill mines next: ore under its footprint first, then ore within its mining radius around it
    /// (row by row), or null once all of that is worked out.
    /// </summary>
    public Int2? FindMinable(Building b)
    {
        foreach (Int2 t in b.Footprint().Concat(MiningArea(b)))
        {
            ResourceDef? r = ResourceAt(t);
            if ((r is not null) && (r.IsTree == false) && (AmountAt(t) > 0))
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>The tiles around a drill's footprint that its mining radius reaches, row by row.</summary>
    /// <param name="b">A drill.</param>
    /// <returns>The tiles outside the footprint, wrapped onto the planet.</returns>
    public IEnumerable<Int2> MiningArea(Building b)
    {
        int r = b.Def.MiningRadius;
        for (int y = -r; y < b.H + r; y++)
        {
            for (int x = -r; x < b.W + r; x++)
            {
                if ((x < 0) || (y < 0) || (x >= b.W) || (y >= b.H))
                {
                    yield return Torus.Wrap(b.Origin + new Int2(x, y));
                }
            }
        }
    }

    private void UpdateDrill(Building b)
    {
        if (b.Out.IsEmpty == false)
        {
            PushOut(b);
        }

        if (b.Out.Total >= DrillBuffer)
        {
            b.Status = MachineStatus.OutputFull;

            return;
        }

        Int2? tile = FindMinable(b);
        if (tile is null)
        {
            b.Status = MachineStatus.NoResource;

            return;
        }

        int f = EnergyFactor(b);
        if (f == 0)
        {
            return;
        }

        b.Status = MachineStatus.Working;
        b.Progress += b.Def.Speed * f / 1000;
        long need = b.Def.MiningTicks * 100L;
        if (b.Progress >= need)
        {
            b.Progress -= need;
            string? item = MineOne(tile.Value, ItemFlow.DrillMined, b.Def.Id);
            if (item is not null)
            {
                b.Out.AddUnchecked(item, 1);
                PushOut(b);
            }
        }
    }

    /// <summary>Pushes products into whatever building sits at the front tile (e.g. drill → furnace, drill → rail stop).</summary>
    private void PushOut(Building b)
    {
        Building? target = BuildingAt(b.FrontTile());
        if ((target is null) || (target == b))
        {
            return;
        }

        if (target.Def.Kind == BuildingKind.Belt)
        {
            DrillOntoBelt(b);

            return;
        }

        foreach ((string? item, int count) in b.Out.Items.ToList())
        {
            int n = System.Math.Min(count, AcceptAmount(target, item));
            if (n > 0)
            {
                b.Out.Remove(item, n);
                target.Inv.AddUnchecked(item, n);
            }
        }
    }

    private bool TryStartCraft(Building b)
    {
        ContentDb c = World.Content;
        RecipeDef? r;
        if (b.Def.Kind == BuildingKind.Lander)
        {
            if (b.CraftQueue.Count == 0)
            {
                b.Status = MachineStatus.Idle;

                return false;
            }
            // Skip past entries whose ingredients are missing so one stalled recipe can't jam the queue.
            int ready = b.CraftQueue.FindIndex(e => LanderHas(b, c.Recipes[e.RecipeId].Inputs));
            if (ready < 0)
            {
                b.Status = MachineStatus.NoInput;

                return false;
            }

            if (ready > 0)
            {
                CraftEntry e = b.CraftQueue[ready];
                b.CraftQueue.RemoveAt(ready);
                b.CraftQueue.Insert(0, e);
            }

            r = c.Recipes[b.CraftQueue[0].RecipeId];
        }
        else if (b.Def.Kind == BuildingKind.PayloadLauncher)
        {
            r = NextPayload(b);
            if (r is null)
            {
                b.Status = (b.TargetPlanetId < 0) || (b.TargetPlanetId >= World.Planets.Count) ? MachineStatus.NoTarget
                    : PayloadsFor(b).Any(p => b.Inv.Has(p.Inputs)) ? MachineStatus.Complete : MachineStatus.NoInput;

                return false;
            }

            b.RecipeId = r.Id;
        }
        else if (b.Def.Kind == BuildingKind.Lab)
        {
            // A lab crafts research units of the current technology. A unit already under way keeps its
            // recipe (and so its tech) until it finishes; the switch applies from the next one.
            r = CurrentResearchUnit;
            b.RecipeId = r?.Id;
            if (r is null)
            {
                b.Status = MachineStatus.NoResearch;

                return false;
            }
        }
        else if (b.Def.Kind == BuildingKind.Furnace)
        {
            r = (b.Recipe is { } cur) && b.Inv.Has(cur.Inputs) ? cur
                : c.RecipeList.FirstOrDefault(x => (x.Category == "smelting") && b.Inv.Has(x.Inputs) && World.Research.Unlocked.Contains(x.Id));
            if (r is null)
            {
                b.Status = MachineStatus.NoInput;

                return false;
            }

            b.RecipeId = r.Id;
        }
        else
        {
            r = b.Recipe;
            if (r is null)
            {
                b.Status = MachineStatus.NoRecipe;

                return false;
            }
        }

        if (EffectBlocked(b, r) is { } blocked)
        {
            b.Status = blocked;

            return false;
        }

        bool isLander = b.Def.Kind == BuildingKind.Lander;
        if ((isLander ? LanderHas(b, r.Inputs) : b.Inv.Has(r.Inputs)) == false)
        {
            b.Status = MachineStatus.NoInput;

            return false;
        }

        if ((b.Def.Kind != BuildingKind.Lander) && (b.Out.CanAddAll(r.Outputs) == false))
        {
            b.Status = MachineStatus.OutputFull;

            return false;
        }

        if (isLander)
        {
            LanderTake(b, r.Inputs);
        }
        else
        {
            b.Inv.RemoveAll(r.Inputs);
        }

        // Research units are logged as research, against the technology, as they always were.
        string? researching = ContentDb.TechForEffect(r.Effect);
        ItemFlow used = researching is not null ? ItemFlow.Research : r.Category == "smelting" ? ItemFlow.SmeltInput : ItemFlow.CraftInput;
        World.Stats.Record(used, r.Inputs, 1, b.Def.Id, researching ?? r.Id, Id);
        SupplyModules(b);
        if (isLander)
        {
            b.CraftingIntermediate = b.CraftQueue[0].Intermediate;
            b.CraftQueue.RemoveAt(0);
            b.RecipeId = r.Id;
        }

        b.Crafting = true;
        b.Progress = 0;

        return true;
    }

    private void UpdateCrafter(Building b)
    {
        if ((b.Crafting == false) && (TryStartCraft(b) == false))
        {
            return;
        }

        int f = EnergyFactor(b);
        if (f == 0)
        {
            return;
        }

        b.Status = MachineStatus.Working;
        RecipeDef r = b.Recipe!;
        int speed = (r.Category == "orbital") && HasSpaceElevator ? 2 * b.Def.Speed : b.Def.Speed;
        speed = speed * (100 + b.BoostPct) / 100;
        if (b.Def.Kind == BuildingKind.Lab)
        {
            speed = speed * (100 + (ResearchSpeedPct * World.Research.Level(ResearchSpeed))) / 100;
        }

        b.Progress += speed * f / 1000;
        if (b.Progress < r.Ticks * 100L)
        {
            return;
        }

        // A part for a queued lander craft goes into its reserve, unless the queue was cleared meanwhile.
        Inventory into = b.CraftingIntermediate && (b.CraftQueue.Count > 0) ? b.CraftReserve! : b.Out;
        b.CraftingIntermediate = false;
        foreach (ItemAmount o in r.Outputs)
        {
            into.AddUnchecked(o.Item, o.Count);
        }

        World.Stats.Record(r.Category == "smelting" ? ItemFlow.Smelted : ItemFlow.Crafted, r.Outputs, 1, b.Def.Id, r.Id, Id);
        b.Crafting = false;
        b.Progress = 0;
        if ((ContentDb.RobotForEffect(r.Effect) is { } robot) && World.Content.Robots.TryGetValue(robot, out RobotDef? def))
        {
            Unit u = World.SpawnHelper(this, owner: 0, def, b);
            World.HelpersBuilt++;
            World.Emit($"{b.Name} assembled {u.Name}, a {def.Name}", Id, Fixed.ToTile(u.Pos));
        }

        ApplyTerraformEffect(b, r);
        if (ContentDb.TechForEffect(r.Effect) is { } tech)
        {
            World.AddResearchUnit(tech);
        }

        if ((b.Def.Kind == BuildingKind.Lander) && (b.CraftQueue.Count == 0))
        {
            b.RecipeId = null;
            // Spare parts (a craft that makes two when one was needed) go back to storage.
            ReleaseCraftReserve(b);
        }
    }

    private void UpdateSorter(Building b)
    {
        long period = b.Def.Period * 100L;
        if (b.Progress < period)
        {
            int f = EnergyFactor(b);
            if (f == 0)
            {
                return;
            }

            b.Progress = System.Math.Min(period, b.Progress + (100L * f / 1000));
            if (b.Progress < period)
            {
                b.Status = MachineStatus.Working;

                return;
            }
        }

        Building? src = BuildingAt(b.BackTile());
        Building? dst = BuildingAt(b.FrontTile());
        if ((src is null) || (dst is null) || (src == b) || (dst == b) || (src == dst))
        {
            b.Status = MachineStatus.Idle;

            return;
        }

        if (dst.Def.Kind == BuildingKind.Belt)
        {
            // Onto a belt: one item from the source's output, if the belt has room at the back.
            if (BeltHasRoom(dst) && (src.Def.Kind != BuildingKind.Belt) && (src.Out.IsEmpty == false))
            {
                string item = src.Out.Items.First().Key;
                src.Out.Remove(item, 1);
                PutOnBelt(dst, item);
                b.Progress = 0;
                b.Status = MachineStatus.Working;

                return;
            }
        }
        else if (src.Def.Kind == BuildingKind.Belt)
        {
            // Off a belt: the frontmost item the destination wants.
            if (TakeFromBelt(src, dst) is { } item)
            {
                dst.Inv.AddUnchecked(item, 1);
                b.Progress = 0;
                b.Status = MachineStatus.Working;

                return;
            }
        }
        else
        {
            foreach ((string? item, int _) in src.Out.Items)
            {
                if (AcceptAmount(dst, item) <= 0)
                {
                    continue;
                }

                src.Out.Remove(item, 1);
                dst.Inv.AddUnchecked(item, 1);
                b.Progress = 0;
                b.Status = MachineStatus.Working;

                return;
            }
        }

        b.Status = MachineStatus.Idle;
    }

    /// <summary>The machine modules installed in a building.</summary>
    /// <param name="b">The building.</param>
    /// <returns>Their module definitions, in slot order.</returns>
    public IEnumerable<ModuleDef> MachineModulesOf(Building b) => b.Modules.Select(m => World.Content.Item(m).Module!);

    /// <summary>How many of an item a building's modules use up per craft, or 0 if none do.</summary>
    private int ModuleSupply(Building b, string item) => MachineModulesOf(b).Sum(m => m.Consumes.GetValueOrDefault(item));

    /// <summary>
    /// At the start of a craft, each module whose supplies are in the machine uses them up and adds its boost;
    /// a module left without supplies does nothing this craft.
    /// </summary>
    private void SupplyModules(Building b)
    {
        b.BoostPct = 0;
        foreach (string id in b.Modules)
        {
            ModuleDef m = World.Content.Item(id).Module!;
            ItemAmount[] use = m.ConsumesList;
            if (b.Inv.Has(use) == false)
            {
                continue;
            }

            b.Inv.RemoveAll(use);
            World.Stats.Record(ItemFlow.CraftInput, use, 1, b.Def.Id, id, Id);
            b.BoostPct += m.BoostPct;
        }
    }

    /// <summary>The most of a slot's item a station keeps: the slot's limit, if set, within the station's capacity.</summary>
    /// <param name="station">The station.</param>
    /// <param name="slot">One of its slots.</param>
    /// <returns>The item cap.</returns>
    public static int StationCap(Building station, StationSlot slot) => slot.Max > 0 ? System.Math.Min(slot.Max, station.Def.ItemCap) : station.Def.ItemCap;
}
