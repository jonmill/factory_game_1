using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>What a bot is trying to achieve. Parse from strings like "tech:automation", "helpers:1", "settlers", "item:iron-plate:200".</summary>
public abstract record Goal
{
    /// <summary>Whether the goal has been achieved in the given world.</summary>
    public abstract bool IsMet(World w);
    /// <summary>Human-readable description of the goal, using display names from the content.</summary>
    public abstract string Describe(ContentDb c);

    /// <summary>Parses a goal spec such as <c>tech:automation</c>, <c>helpers:1</c> or <c>item:iron-plate:200</c>.</summary>
    /// <param name="spec">The goal specification string.</param>
    /// <exception cref="ArgumentException">The spec is not in a recognised form.</exception>
    public static Goal Parse(string spec)
    {
        string[] parts = spec.Split(':');

        return parts[0] switch
        {
            "tech" when parts.Length == 2 => new TechGoal(parts[1]),
            "helpers" when parts.Length == 2 => new HelpersGoal(int.Parse(parts[1])),
            "settlers" when parts.Length == 1 => new SettlersGoal(),
            "item" when parts.Length == 3 => new ItemGoal(parts[1], int.Parse(parts[2])),
            _ => throw new ArgumentException($"Bad goal '{spec}'. Use tech:<id>, helpers:<n>, settlers or item:<id>:<count>"),
        };
    }

    /// <summary>Checks that the tech or item the goal refers to exists in the content.</summary>
    /// <exception cref="ArgumentException">The referenced tech or item is unknown.</exception>
    public void Validate(ContentDb c)
    {
        switch (this)
        {
            case TechGoal t when c.Techs.ContainsKey(t.TechId) == false:
                throw new ArgumentException($"Unknown tech {t.TechId}");
            case ItemGoal i when c.Items.ContainsKey(i.Item) == false:
                throw new ArgumentException($"Unknown item {i.Item}");
        }
    }
}
