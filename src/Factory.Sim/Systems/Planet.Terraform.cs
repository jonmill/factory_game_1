namespace Factory.Sim;

/// <summary>
/// Terraforming: each planet has stats (atmosphere, hydrosphere, biosphere) that terraformer buildings
/// raise a point at a time by consuming products. A planet's readiness is the average of its stats'
/// progress toward their targets; once every planet in the system reaches the bar, a Settler Uplink can
/// call the settlers, which wins the game.
/// </summary>
public sealed partial class Planet
{
    /// <summary>Points in each planet stat, by stat id.</summary>
    public SortedDictionary<string, int> Stats { get; } = new(StringComparer.Ordinal);

    /// <summary>How far a stat is toward its target, 0 to 100.</summary>
    /// <param name="stat">The stat.</param>
    /// <returns>Percent complete.</returns>
    public int StatPercent(PlanetStatDef stat) =>
        stat.Target <= 0 ? 100 : System.Math.Min(100, Stats.GetValueOrDefault(stat.Id) * 100 / stat.Target);

    /// <summary>The planet's readiness for settlers: the average of its stats' progress, 0 to 100.</summary>
    public int Readiness
    {
        get
        {
            PlanetStatDef[] stats = World.Content.Terraform.Stats;

            return stats.Length == 0 ? 100 : stats.Sum(StatPercent) / stats.Length;
        }
    }

    /// <summary>True once the planet's readiness has reached the bar.</summary>
    public bool IsReady => Readiness >= World.Content.Terraform.ReadyBar;

    internal void InitStats()
    {
        foreach (PlanetStatDef s in World.Content.Terraform.Stats)
        {
            Stats[s.Id] = s.Initial;
        }
    }

    /// <summary>The stat and points a recipe effect adds ("terraform:&lt;stat&gt;:&lt;points&gt;"), or null.</summary>
    /// <param name="effect">A recipe's effect.</param>
    /// <returns>The stat id and points, if it is a terraforming effect.</returns>
    public static (string Stat, int Points)? TerraformEffect(string? effect)
    {
        string[]? parts = effect?.Split(':');
        if ((parts is not { Length: 3 }) || (parts[0] != "terraform") || (int.TryParse(parts[2], out int points) == false))
        {
            return null;
        }

        return (parts[1], points);
    }

    /// <summary>Whether a recipe with this effect can start now: terraformers stop at their stat's target and the uplink waits for every planet.</summary>
    private MachineStatus? EffectBlocked(RecipeDef r)
    {
        if ((TerraformEffect(r.Effect) is { } t) && (World.Content.Terraform.Stats.FirstOrDefault(s => s.Id == t.Stat) is { } stat) &&
            (Stats.GetValueOrDefault(stat.Id) >= stat.Target))
        {
            return MachineStatus.Complete;
        }

        if ((r.Effect == CallSettlersEffect) && ((World.AllPlanetsReady == false) || World.SettlersCalled))
        {
            return MachineStatus.NotReady;
        }

        return null;
    }

    /// <summary>The recipe effect that calls the settlers.</summary>
    public const string CallSettlersEffect = "call-settlers";

    /// <summary>Applies a finished terraforming or settler-call recipe.</summary>
    private void ApplyTerraformEffect(Building b, RecipeDef r)
    {
        if (TerraformEffect(r.Effect) is { } t)
        {
            PlanetStatDef? stat = World.Content.Terraform.Stats.FirstOrDefault(s => s.Id == t.Stat);
            bool wasReady = IsReady;
            int before = Stats.GetValueOrDefault(t.Stat);
            Stats[t.Stat] = before + t.Points;
            if ((stat is not null) && (before < stat.Target) && (before + t.Points >= stat.Target))
            {
                World.Emit($"{Name}: {stat.Name} fully terraformed", Id, b.Origin);
            }

            if ((wasReady == false) && IsReady)
            {
                World.Emit(World.AllPlanetsReady ? "Every planet is ready. Call the settlers from a Settler Uplink!" : $"{Name} is ready for settlers", Id, b.Origin);
            }
        }
        else if (r.Effect == CallSettlersEffect)
        {
            World.SettlersCalled = true;
            World.Emit("The settlers are on their way. The system is ready for them. You win!", Id, b.Origin);
        }
    }
}
