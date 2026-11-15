namespace Factory.Sim;

/// <summary>
/// What an item does when installed in a robot's module slot. Every bonus is additive across the
/// modules a robot carries.
/// </summary>
public sealed class ModuleDef
{
    /// <summary>Which robots take this module: <see cref="ModuleFit.Any"/>, only the commander, or only helper bots.</summary>
    public ModuleFit Fits { get; init; } = ModuleFit.Any;
    /// <summary>Extra walking speed, in percent.</summary>
    public int SpeedPct { get; init; }
    /// <summary>Extra hand-mining and gathering speed, in percent.</summary>
    public int MiningPct { get; init; }
    /// <summary>Extra cargo (pocket) stack slots.</summary>
    public int CarrySlots { get; init; }
    /// <summary>Whether the robot floats over water and lava.</summary>
    public bool Hover { get; init; }
    /// <summary>Jobs a helper bot can take only with this module installed.</summary>
    public JobKind[] Jobs { get; init; } = [];
    /// <summary>Machines: extra crafting speed, in percent, while the module is supplied.</summary>
    public int BoostPct { get; init; }
    /// <summary>Machines: items the module uses up each craft to give its boost, keyed by item id.</summary>
    public Dictionary<string, int> Consumes { get; init; } = new();
    /// <summary><see cref="Consumes"/> as an id-sorted list, for deterministic iteration.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public ItemAmount[] ConsumesList => Consumes.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new ItemAmount(kv.Key, kv.Value)).ToArray();
    /// <summary>What the module does, for tooltips.</summary>
    public string Description { get; init; } = "";

    /// <summary>True if a robot of this kind can take the module.</summary>
    /// <param name="commander">Whether the robot is the commander.</param>
    /// <returns>Whether it fits.</returns>
    public bool FitsRobot(bool commander) => Fits switch
    {
        ModuleFit.Commander => commander,
        ModuleFit.Helper => commander == false,
        ModuleFit.Machine => false,
        _ => true,
    };
}
