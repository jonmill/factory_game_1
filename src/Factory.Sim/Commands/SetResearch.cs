using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Selects the technology labs work on.</summary>
/// <param name="TechId">Technology to research, or null to stop researching.</param>
public sealed record SetResearch(string? TechId) : Command
{
    /// <summary>Checks the technology exists, is available, not already complete and has all prerequisites done.</summary>
    public override string? Validate(World w)
    {
        if (TechId is null)
        {
            return null;
        }

        if (w.Content.Techs.TryGetValue(TechId, out TechDef? t) == false)
        {
            return "Unknown technology";
        }

        if (t.ComingSoon)
        {
            return $"{t.Name} isn't available in this playtest";
        }

        if (w.Research.Completed.Contains(TechId))
        {
            return "Already researched";
        }

        List<string> missing = t.Prereqs.Where(p => w.Research.Completed.Contains(p) == false).ToList();

        return missing.Count > 0 ? $"Requires {string.Join(", ", missing.Select(m => w.Content.Techs[m].Name))}" : null;
    }
    /// <summary>Sets the current research target.</summary>
    public override void Apply(World w) => w.Research.Current = TechId;
}
