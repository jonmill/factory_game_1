namespace Factory.Sim;

/// <summary>
/// One planet: a fixed-size wrap-around grid (east-west and north-south) plus everything on it.
/// Each planet updates independently, which keeps the door open to per-planet threading later.
/// </summary>
public sealed partial class Planet
{
    /// <summary>World this planet belongs to.</summary>
    public World World { get; }
    /// <summary>Planet index within the world.</summary>
    public int Id { get; }
    /// <summary>Display name of the planet.</summary>
    public string Name { get; }
    /// <summary>Width of the grid in tiles.</summary>
    public int Width { get; }
    /// <summary>Height of the grid in tiles.</summary>
    public int Height { get; }
    /// <summary>Wrap-around geometry in tile coordinates.</summary>
    public Torus Torus { get; }
    /// <summary>Torus in sub-unit coordinates (for unit positions).</summary>
    public Torus SubTorus { get; }

    private readonly Terrain[] _terrain;
    private readonly byte[] _resource;   // 0 = none, else index+1 into Content.ResourceList
    private readonly int[] _amount;
    private readonly bool[] _rail;
    private readonly int[] _occupant;    // building or construction site id

    /// <summary>Every building on the planet, in creation order.</summary>
    public List<Building> Buildings { get; } = [];
    /// <summary>Pending construction, deconstruction and train-building sites.</summary>
    public List<ConstructionSite> Sites { get; } = [];
    /// <summary>Leaders and followers on the planet.</summary>
    public List<Unit> Units { get; } = [];
    /// <summary>Trains on the planet.</summary>
    public List<Train> Trains { get; } = [];
    /// <summary>Id of the colony lander building, the starting storage and crafter.</summary>
    public int LanderId { get; internal set; }

    /// <summary>Bumped whenever terrain/resources/rails change, so clients know to redraw cached terrain.</summary>
    public int TerrainVersion { get; private set; }
    internal bool PowerDirty { get; set; } = true;

    /// <summary>Creates an empty planet of the given size; terrain is filled in by world generation.</summary>
    /// <param name="world">Owning world.</param>
    /// <param name="id">Planet index.</param>
    /// <param name="name">Display name.</param>
    /// <param name="width">Width in tiles.</param>
    /// <param name="height">Height in tiles.</param>
    public Planet(World world, int id, string name, int width, int height)
    {
        World = world;
        Id = id;
        Name = name;
        Width = width;
        Height = height;
        Torus = new Torus(width, height);
        SubTorus = new Torus(width * Fixed.One, height * Fixed.One);
        int n = width * height;
        _terrain = new Terrain[n];
        _resource = new byte[n];
        _amount = new int[n];
        _rail = new bool[n];
        _occupant = new int[n];
    }

    private int Index(Int2 t)
    {
        Int2 w = Torus.Wrap(t);

        return (w.Y * Width) + w.X;
    }

    /// <summary>Terrain of a tile (coordinates are wrapped).</summary>
    public Terrain TerrainAt(Int2 t) => _terrain[Index(t)];
    /// <summary>Resource deposit or tree on a tile, or null if none.</summary>
    public ResourceDef? ResourceAt(Int2 t)
    {
        int r = _resource[Index(t)];

        return r == 0 ? null : World.Content.ResourceList[r - 1];
    }
    /// <summary>Units of resource left on a tile.</summary>
    public int AmountAt(Int2 t) => _amount[Index(t)];
    /// <summary>True if the tile has rail track.</summary>
    public bool IsRail(Int2 t) => _rail[Index(t)];
    /// <summary>Id of the building or construction site covering a tile, or 0 if free.</summary>
    public int OccupantAt(Int2 t) => _occupant[Index(t)];
    /// <summary>Building covering a tile, or null if free or only a construction site.</summary>
    public Building? BuildingAt(Int2 t) => World.Get<Building>(OccupantAt(t));

    internal void SetTerrain(Int2 t, Terrain v)
    {
        _terrain[Index(t)] = v;
        TerrainVersion++;
    }
    internal void SetResource(Int2 t, ResourceDef? r, int amount)
    {
        int i = Index(t);
        _resource[i] = r is null ? (byte)0 : (byte)(World.Content.ResourceList.IndexOf(r) + 1);
        _amount[i] = r is null ? 0 : amount;
        TerrainVersion++;
    }
    internal void SetRail(Int2 t, bool v)
    {
        _rail[Index(t)] = v;
        TerrainVersion++;
    }
    internal void SetOccupant(Int2 t, int id) => _occupant[Index(t)] = id;

    /// <summary>Removes one unit of resource from a tile. Returns the item id mined or null.</summary>
    internal string? MineOne(Int2 t, ItemFlow flow)
    {
        int i = Index(t);
        if ((_resource[i] == 0) || (_amount[i] <= 0))
        {
            return null;
        }

        ResourceDef res = World.Content.ResourceList[_resource[i] - 1];
        _amount[i]--;
        if (_amount[i] <= 0)
        {
            _resource[i] = 0;
            TerrainVersion++;
        }

        World.Stats.Record(flow, res.Item, 1);

        return res.Item;
    }

    /// <summary>True if any tile bordering the given rectangle (including corners) is water.</summary>
    /// <param name="origin">Top-left tile of the rectangle.</param>
    /// <param name="w">Width in tiles.</param>
    /// <param name="h">Height in tiles.</param>
    public bool IsAdjacentToWater(Int2 origin, int w, int h)
    {
        for (int x = -1; x <= w; x++)
        {
            for (int y = -1; y <= h; y++)
            {
                bool edge = (x == -1) || (y == -1) || (x == w) || (y == h);
                if (edge && (TerrainAt(new Int2(origin.X + x, origin.Y + y)) == Terrain.Water))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- entity factories

    internal Building CreateBuilding(BuildingDef def, Int2 origin, Dir rot)
    {
        ContentDb content = World.Content;
        Building b = new Building
        {
            Id = World.NextId(),
            Planet = this,
            Def = def,
            Origin = Torus.Wrap(origin),
            Rot = rot,
            RecipeId = def.Recipe,
            Name = def.Name,
        };
        b.Inv = new Inventory(content, def.Slots);
        b.Out = def.IsStorage ? b.Inv : new Inventory(content, def.OutSlots);
        if (def.Kind == BuildingKind.RailStop)
        {
            b.Name = $"Stop {Buildings.Count(x => x.Def.Kind == BuildingKind.RailStop) + 1}";
        }

        foreach (Int2 t in b.Footprint())
        {
            SetOccupant(t, b.Id);
        }

        Buildings.Add(b);
        World.Register(b);
        PowerDirty = true;

        return b;
    }

    internal void RemoveBuilding(Building b)
    {
        foreach (Int2 t in b.Footprint())
        {
            if (OccupantAt(t) == b.Id)
            {
                SetOccupant(t, 0);
            }
        }

        Buildings.Remove(b);
        World.Unregister(b);
        PowerDirty = true;
    }

    internal Unit CreateUnit(string name, bool isLeader, int owner, Int2 pos)
    {
        Unit u = new Unit
        {
            Id = World.NextId(),
            Planet = this,
            Name = name,
            IsLeader = isLeader,
            OwnerPlayer = owner,
            Pos = SubTorus.Wrap(pos),
            Speed = isLeader ? 130 : 90,
            Carry = new Inventory(World.Content, isLeader ? 10 : 4),
        };
        Units.Add(u);
        World.Register(u);

        return u;
    }

    internal ConstructionSite CreateSite(SiteKind kind, string defId, Int2 origin, Dir rot, int w, int h, ItemAmount[] cost, int work)
    {
        ConstructionSite s = new ConstructionSite
        {
            Id = World.NextId(),
            Planet = this,
            Kind = kind,
            DefId = defId,
            Origin = Torus.Wrap(origin),
            Rot = rot,
            W = w,
            H = h,
            Cost = cost,
            WorkNeeded = work,
            Delivered = new Inventory(World.Content, int.MaxValue),
        };
        Sites.Add(s);
        World.Register(s);

        return s;
    }

    internal void RemoveSite(ConstructionSite s)
    {
        if (s.Kind == SiteKind.Build)
        {
            for (int y = 0; y < s.H; y++)
            {
                for (int x = 0; x < s.W; x++)
                {
                    Int2 t = new Int2(s.Origin.X + x, s.Origin.Y + y);
                    if (OccupantAt(t) == s.Id)
                    {
                        SetOccupant(t, 0);
                    }
                }
            }
        }

        Sites.Remove(s);
        World.Unregister(s);
    }

    internal Train CreateTrain(IReadOnlyList<string> vehicleIds, IReadOnlyList<Int2> tiles)
    {
        ContentDb content = World.Content;
        Train t = new Train { Id = World.NextId(), Planet = this, State = TrainState.NoSchedule };
        for (int i = 0; i < vehicleIds.Count; i++)
        {
            VehicleDef def = content.Vehicles[vehicleIds[i]];
            t.Vehicles.Add(new Vehicle { Def = def, Cargo = new Inventory(content, def.Slots) });
            t.Tiles.Add(Torus.Wrap(tiles[i]));
        }

        t.Name = $"Train {Trains.Count + 1}";
        Trains.Add(t);
        World.Register(t);

        return t;
    }

    // ---------------------------------------------------------------- queries

    /// <summary>All storage buildings, nearest first.</summary>
    public IEnumerable<Building> StoragesByDistance(Int2 fromSub) =>
        Buildings.Where(b => b.Def.Kind is BuildingKind.Lander or BuildingKind.Chest)
                 .OrderBy(b => SubTorus.DistSq(fromSub, SubTorus.Wrap(b.CenterSub))).ThenBy(b => b.Id);

    /// <summary>True if a rectangle can be built on: no water, occupants or trees, and no rail unless allowed.</summary>
    /// <param name="origin">Top-left tile of the rectangle.</param>
    /// <param name="w">Width in tiles.</param>
    /// <param name="h">Height in tiles.</param>
    /// <param name="allowRail">Whether rail tiles count as free (e.g. for rail stops).</param>
    public bool IsAreaFree(Int2 origin, int w, int h, bool allowRail = false)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Int2 t = new Int2(origin.X + x, origin.Y + y);
                if (TerrainAt(t) == Terrain.Water)
                {
                    return false;
                }

                if (OccupantAt(t) != 0)
                {
                    return false;
                }

                if ((allowRail == false) && IsRail(t))
                {
                    return false;
                }

                ResourceDef? r = ResourceAt(t);
                if (r is { IsTree: true })
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ---------------------------------------------------------------- update

    internal void Update()
    {
        if (PowerDirty)
        {
            RebuildPowerNetworks();
            PowerDirty = false;
        }

        UpdatePower();
        foreach (Building b in Buildings.ToArray())
        {
            UpdateBuilding(b);
        }

        foreach (Unit u in Units.ToArray())
        {
            UpdateUnit(u);
        }

        UpdateTrains();
    }

    internal void Hash(StateHasher h)
    {
        h.Add(Id);
        for (int i = 0; i < _amount.Length; i++)
        {
            h.Add(_amount[i]);
            h.Add(_rail[i]);
        }

        foreach (Building b in Buildings)
        {
            h.Add(b.Id);
            h.Add(b.Def.Id);
            h.Add(b.Origin);
            h.Add((int)b.Rot);
            h.Add(b.RecipeId);
            h.Add(b.Progress);
            h.Add(b.FuelJ);
            h.Add(b.StoredJ);
            h.Add(b.Crafting);
            b.Inv.Hash(h);
            if (b.Def.IsStorage == false)
            {
                b.Out.Hash(h);
            }
        }

        foreach (ConstructionSite s in Sites)
        {
            h.Add(s.Id);
            h.Add(s.WorkDone);
            s.Delivered.Hash(h);
            h.Add(s.ClaimedBy);
        }

        foreach (Unit u in Units)
        {
            h.Add(u.Id);
            h.Add(u.Pos);
            h.Add((int)u.Phase);
            h.Add(u.WorkTicks);
            u.Carry.Hash(h);
        }

        foreach (Train t in Trains)
        {
            h.Add(t.Id);
            h.Add(t.Progress);
            h.Add(t.Speed);
            h.Add((int)t.State);
            h.Add(t.FuelJ);
            h.Add(t.Water);
            foreach (Int2 tile in t.Tiles)
            {
                h.Add(tile);
            }

            foreach (Vehicle v in t.Vehicles)
            {
                v.Cargo.Hash(h);
            }
        }
    }
}
