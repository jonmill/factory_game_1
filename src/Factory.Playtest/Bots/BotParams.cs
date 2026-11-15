using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Tunable strategy knobs for <see cref="GoalBot"/>. The speedrun experiment searches over these.</summary>
public sealed record BotParams
{
    /// <summary>Ticks between planning passes; the bot does nothing on other ticks.</summary>
    public int ThinkInterval { get; init; } = 60;
    /// <summary>Number of furnaces to build.</summary>
    public int Furnaces { get; init; } = 2;
    /// <summary>Number of research labs to build while research remains.</summary>
    public int Labs { get; init; } = 1;
    /// <summary>Resource gathered for fuel ("xylo" or "carbon").</summary>
    public string FuelResource { get; init; } = "xylo";
    /// <summary>Minimum fuel to keep in storage; gathering switches to fuel when stock falls well below this.</summary>
    public int MinFuelStock { get; init; } = 40;
    /// <summary>Maximum number of batches queued in a single craft order.</summary>
    public int CraftBatch { get; init; } = 5;
    /// <summary>Maximum number of followers assigned to construction at once.</summary>
    public int MaxBuilders { get; init; } = 1;
    /// <summary>Haul plates out of a furnace once it holds this many.</summary>
    public int FurnaceCollectAt { get; init; } = 8;
    /// <summary>Research look-ahead: how many units of each remaining tech to plan materials for.</summary>
    public int ResearchLookahead { get; init; } = 10;

    /// <summary>Creates a random strategy with every knob drawn from its allowed range.</summary>
    /// <param name="rng">Random source.</param>
    public static BotParams Random(Rng rng) => new()
    {
        ThinkInterval = rng.Range(30, 181),
        Furnaces = rng.Range(1, 5),
        Labs = rng.Range(1, 3),
        FuelResource = rng.Range(0, 2) == 0 ? "xylo" : "carbon",
        MinFuelStock = rng.Range(10, 81),
        CraftBatch = rng.Range(1, 11),
        MaxBuilders = rng.Range(1, 3),
        FurnaceCollectAt = rng.Range(3, 25),
        ResearchLookahead = rng.Range(3, 31),
    };

    /// <summary>Returns a copy with one randomly chosen knob nudged within its range.</summary>
    /// <param name="rng">Random source.</param>
    public BotParams Mutate(Rng rng)
    {
        int Nudge(int v, int lo, int hi, int step) => System.Math.Clamp(v + rng.Range(-step, step + 1), lo, hi);

        return (rng.Range(0, 9)) switch
        {
            0 => this with { ThinkInterval = Nudge(ThinkInterval, 30, 180, 30) },
            1 => this with { Furnaces = Nudge(Furnaces, 1, 4, 1) },
            2 => this with { Labs = Nudge(Labs, 1, 2, 1) },
            3 => this with { FuelResource = FuelResource == "xylo" ? "carbon" : "xylo" },
            4 => this with { MinFuelStock = Nudge(MinFuelStock, 10, 80, 15) },
            5 => this with { CraftBatch = Nudge(CraftBatch, 1, 10, 3) },
            6 => this with { MaxBuilders = Nudge(MaxBuilders, 1, 2, 1) },
            7 => this with { FurnaceCollectAt = Nudge(FurnaceCollectAt, 3, 25, 5) },
            _ => this with { ResearchLookahead = Nudge(ResearchLookahead, 3, 30, 6) },
        };
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"think={ThinkInterval} furnaces={Furnaces} labs={Labs} fuel={FuelResource} minFuel={MinFuelStock} batch={CraftBatch} builders={MaxBuilders} collect={FurnaceCollectAt} lookahead={ResearchLookahead}";
}
