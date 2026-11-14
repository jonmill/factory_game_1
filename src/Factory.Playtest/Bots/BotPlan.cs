using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>Snapshot of the bot's reasoning on its last think (for diagnostics).</summary>
public sealed class BotPlan
{
    /// <summary>Technologies still required for the goal, in research order.</summary>
    public List<string> TechPath { get; set; } = [];
    /// <summary>Building definition ids placed during this think.</summary>
    public List<string> Placed { get; } = [];
    /// <summary>Material costs of the structures placed during this think, folded into demand.</summary>
    public List<ItemAmount> PlannedCosts { get; } = [];
    /// <summary>Shortfall of raw resources to gather, keyed by resource id.</summary>
    public Dictionary<string, long> RawDeficit { get; } = new();
    /// <summary>Shortfall of hand-crafted items, keyed by item id.</summary>
    public Dictionary<string, long> CraftDeficit { get; } = new();
    /// <summary>Shortfall of smelted items, keyed by item id.</summary>
    public Dictionary<string, long> SmeltDeficit { get; } = new();
    /// <summary>Recipe depth of each crafted item, so intermediates are crafted before the things that use them.</summary>
    public Dictionary<string, int> CraftDepth { get; } = new();
    /// <summary>Items needed that no recipe or resource can provide, with the missing amounts.</summary>
    public Dictionary<string, long> Unobtainable { get; } = new();
    /// <summary>Jobs the bot wants followers to perform, in priority order.</summary>
    public List<Job> Needs { get; } = [];
    /// <summary>Problems the bot ran into while planning.</summary>
    public List<string> Problems { get; } = [];

    /// <summary>One-line description of the plan for reports and logs.</summary>
    public string Summarize() =>
        $"needs: [{string.Join(", ", Needs.Select(j => j.Kind == JobKind.Gather ? $"Gather {j.Resource}" : j.Kind == JobKind.Haul ? $"Haul {j.FromId}->{j.ToId} {j.Item ?? "*"}" : j.Kind.ToString()))}]; " +
        $"tech path: [{string.Join(", ", TechPath)}]; raw deficit: {Fmt(RawDeficit)}; craft: {Fmt(CraftDeficit)}; smelt: {Fmt(SmeltDeficit)}" +
        (Unobtainable.Count > 0 ? $"; UNOBTAINABLE: {Fmt(Unobtainable)}" : "") + (Problems.Count > 0 ? $"; problems: {string.Join("; ", Problems.Distinct())}" : "");

    private static string Fmt(Dictionary<string, long> d) => d.Count == 0 ? "-" : string.Join(", ", d.OrderBy(k => k.Key).Select(kv => $"{kv.Key}={kv.Value}"));
}
