namespace Factory.Sim;

/// <summary>Drills, furnaces, fabricators, labs, robot bays and sorters.</summary>
public sealed partial class Planet
{
    private const int MaxFuelStock = 10;
    private const int DrillBuffer = 10;

    internal static long PerTickJ(int kw) => kw * 1000L / World.TicksPerSecond;

    /// <summary>How many of an item a building will accept right now (used by drills, sorters, haulers, the leader).</summary>
    public int AcceptAmount(Building b, string item)
    {
        ContentDb c = World.Content;
        int Cap(int max) => System.Math.Clamp(max - b.Inv.Count(item), 0, b.Inv.SpaceFor(item));
        bool burner = b.Def.Energy == EnergyKind.Burner;

        switch (b.Def.Kind)
        {
            case BuildingKind.Lander or BuildingKind.Chest or BuildingKind.RailStop:
                return b.Inv.SpaceFor(item);
            case BuildingKind.Furnace:
                {
                    RecipeDef? smelt = c.RecipeList.FirstOrDefault(r => (r.Category == "smelting") && r.In.ContainsKey(item));
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
            case BuildingKind.Drill or BuildingKind.SteamEngine:
                return burner && c.IsFuel(item) ? Cap(MaxFuelStock) : 0;
            case BuildingKind.Assembler or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink:
                {
                    RecipeDef? r = b.Recipe;
                    if ((r is null) || (r.In.TryGetValue(item, out int need) == false))
                    {
                        return 0;
                    }

                    return Cap(need * (b.Def.Kind == BuildingKind.Assembler ? 4 : 2));
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

    private TechDef? CurrentTech => World.Research.Current is { } id ? World.Content.Techs[id] : null;

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
                return b.Crafting || ((CurrentTech is { } t) && b.Inv.Has(t.UnitCostList));
            case BuildingKind.Sorter:
                return b.Progress < b.Def.Period * 100L;
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
                if (c.IsFuel(k) == false)
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
            World.Stats.Record(ItemFlow.Fuel, fuel, 1);
            b.FuelJ += c.Item(fuel).FuelKj * 1000L;
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
            case BuildingKind.Furnace or BuildingKind.Assembler or BuildingKind.RobotBay or BuildingKind.Terraformer or BuildingKind.Uplink or BuildingKind.Lander:
                UpdateCrafter(b);
                break;
            case BuildingKind.Lab:
                UpdateLab(b);
                break;
            case BuildingKind.Sorter:
                UpdateSorter(b);
                break;
        }
    }

    /// <summary>First tile under a drill's footprint with a minable (non-tree) resource left, or null if exhausted.</summary>
    public Int2? FindMinable(Building b)
    {
        foreach (Int2 t in b.Footprint())
        {
            ResourceDef? r = ResourceAt(t);
            if ((r is not null) && (r.IsTree == false) && (AmountAt(t) > 0))
            {
                return t;
            }
        }

        return null;
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
            string? item = MineOne(tile.Value, ItemFlow.DrillMined);
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
            int ready = b.CraftQueue.FindIndex(id => b.Inv.Has(c.Recipes[id].Inputs));
            if (ready < 0)
            {
                b.Status = MachineStatus.NoInput;

                return false;
            }

            if (ready > 0)
            {
                string id = b.CraftQueue[ready];
                b.CraftQueue.RemoveAt(ready);
                b.CraftQueue.Insert(0, id);
            }

            r = c.Recipes[b.CraftQueue[0]];
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

        if (EffectBlocked(r) is { } blocked)
        {
            b.Status = blocked;

            return false;
        }

        if (b.Inv.Has(r.Inputs) == false)
        {
            b.Status = MachineStatus.NoInput;

            return false;
        }

        if ((b.Def.Kind != BuildingKind.Lander) && (b.Out.CanAddAll(r.Outputs) == false))
        {
            b.Status = MachineStatus.OutputFull;

            return false;
        }

        b.Inv.RemoveAll(r.Inputs);
        World.Stats.Record(r.Category == "smelting" ? ItemFlow.SmeltInput : ItemFlow.CraftInput, r.Inputs);
        if (b.Def.Kind == BuildingKind.Lander)
        {
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
        b.Progress += b.Def.Speed * f / 1000;
        RecipeDef r = b.Recipe!;
        if (b.Progress < r.Ticks * 100L)
        {
            return;
        }

        foreach (ItemAmount o in r.Outputs)
        {
            b.Out.AddUnchecked(o.Item, o.Count);
        }

        World.Stats.Record(r.Category == "smelting" ? ItemFlow.Smelted : ItemFlow.Crafted, r.Outputs);
        b.Crafting = false;
        b.Progress = 0;
        if ((ContentDb.RobotForEffect(r.Effect) is { } robot) && World.Content.Robots.TryGetValue(robot, out RobotDef? def))
        {
            Unit u = World.SpawnHelper(this, owner: 0, def, b);
            World.HelpersBuilt++;
            World.Emit($"{b.Name} assembled {u.Name}, a {def.Name}", Id, Fixed.ToTile(u.Pos));
        }

        ApplyTerraformEffect(b, r);

        if ((b.Def.Kind == BuildingKind.Lander) && (b.CraftQueue.Count == 0))
        {
            b.RecipeId = null;
        }
    }

    private void UpdateLab(Building b)
    {
        TechDef? tech = CurrentTech;
        if (tech is null)
        {
            b.Status = MachineStatus.NoResearch;

            return;
        }

        if (b.Crafting == false)
        {
            if (b.Inv.Has(tech.UnitCostList) == false)
            {
                b.Status = MachineStatus.NoInput;

                return;
            }

            b.Inv.RemoveAll(tech.UnitCostList);
            World.Stats.Record(ItemFlow.Research, tech.UnitCostList);
            b.Crafting = true;
            b.Progress = 0;
        }

        int f = EnergyFactor(b);
        if (f == 0)
        {
            return;
        }

        b.Status = MachineStatus.Working;
        b.Progress += b.Def.Speed * f / 1000;
        if (b.Progress < tech.UnitTicks * 100L)
        {
            return;
        }

        b.Crafting = false;
        b.Progress = 0;
        World.AddResearchUnit();
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

        b.Status = MachineStatus.Idle;
    }
}
