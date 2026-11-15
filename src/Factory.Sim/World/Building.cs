namespace Factory.Sim;

/// <summary>A placed structure on a planet: a machine, storage, power pole, rail stop or the lander.</summary>
public sealed class Building : Entity
{
    /// <summary>Content definition this building was built from (kind, size, recipes, power figures).</summary>
    public BuildingDef Def { get; internal set; } = null!;
    /// <summary>Top-left tile of the footprint, in tile coordinates.</summary>
    public Int2 Origin { get; internal set; }
    /// <summary>Facing direction; determines the front/back tiles and, for East/West, swaps width and height.</summary>
    public Dir Rot { get; internal set; }
    /// <summary>Inputs + fuel (or all contents for storage buildings).</summary>
    public Inventory Inv { get; internal set; } = null!;
    /// <summary>Products. For storage buildings this is the same object as Inv.</summary>
    public Inventory Out { get; internal set; } = null!;

    /// <summary>Id of the recipe the machine is set to craft, or null if none is selected.</summary>
    public string? RecipeId { get; internal set; }
    /// <summary>True while a recipe cycle is in progress (ingredients have already been consumed).</summary>
    public bool Crafting { get; internal set; }
    /// <summary>Work progress in "percent-ticks": a recipe of N ticks needs N*100.</summary>
    public long Progress { get; internal set; }
    /// <summary>Remaining burner energy in joules.</summary>
    public long FuelJ { get; internal set; }
    /// <summary>Stored energy in joules (batteries).</summary>
    public long StoredJ { get; internal set; }
    /// <summary>What the building did on its last update, or why it could not work.</summary>
    public MachineStatus Status { get; internal set; }
    /// <summary>Power satisfaction this tick, 0..1000.</summary>
    public int Satisfaction { get; internal set; }
    /// <summary>Id of the power network the building belongs to, or 0 when it is not connected.</summary>
    public int NetworkId { get; internal set; }
    /// <summary>Display name shown to the player.</summary>
    public string Name { get; internal set; } = "";
    /// <summary>Lander craft queue (recipe ids).</summary>
    public List<string> CraftQueue { get; } = [];
    /// <summary>Landed ferries: the launch pad it flew from (0 if that pad is gone).</summary>
    public int HomePadId { get; internal set; }
    /// <summary>Landed ferries: the planet it flew from, where it returns.</summary>
    public int HomePlanetId { get; internal set; }
    /// <summary>Generation actually delivered last tick (W), for UI.</summary>
    public long LastOutputJ { get; internal set; }

    /// <summary>Footprint width in tiles, from the definition.</summary>
    public int W => Def.W;
    /// <summary>Footprint height in tiles, from the definition.</summary>
    public int H => Def.H;

    /// <summary>Enumerates every tile covered by the building, wrapped onto the planet torus.</summary>
    public IEnumerable<Int2> Footprint()
    {
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                yield return Planet.Torus.Wrap(new Int2(Origin.X + x, Origin.Y + y));
            }
        }
    }

    /// <summary>Center in sub-units (non-wrapped, may be out of range at the seam; callers wrap as needed).</summary>
    public Int2 CenterSub => new((Origin.X * Fixed.One) + (W * Fixed.Half), (Origin.Y * Fixed.One) + (H * Fixed.Half));

    /// <summary>The tile just outside the building's front face (drill output / rail stop track / sorter target).</summary>
    public Int2 FrontTile() => Planet.Torus.Wrap(Rot switch
    {
        Dir.East => new Int2(Origin.X + W, Origin.Y + ((H - 1) / 2)),
        Dir.West => new Int2(Origin.X - 1, Origin.Y + ((H - 1) / 2)),
        Dir.North => new Int2(Origin.X + ((W - 1) / 2), Origin.Y - 1),
        _ => new Int2(Origin.X + ((W - 1) / 2), Origin.Y + H),
    });

    /// <summary>The tile just outside the building's rear face (e.g. where an inserter-style sorter takes items from).</summary>
    public Int2 BackTile() => Planet.Torus.Wrap(Rot switch
    {
        Dir.East => new Int2(Origin.X - 1, Origin.Y + ((H - 1) / 2)),
        Dir.West => new Int2(Origin.X + W, Origin.Y + ((H - 1) / 2)),
        Dir.North => new Int2(Origin.X + ((W - 1) / 2), Origin.Y + H),
        _ => new Int2(Origin.X + ((W - 1) / 2), Origin.Y - 1),
    });

    /// <summary>Definition of the selected recipe, or null if none is selected or the id is unknown.</summary>
    public RecipeDef? Recipe => RecipeId is null ? null : Planet.World.Content.Recipes.GetValueOrDefault(RecipeId);
}
