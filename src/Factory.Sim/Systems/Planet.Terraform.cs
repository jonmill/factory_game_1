namespace Factory.Sim;

/// <summary>
/// Terraforming: each planet has stats (atmosphere, hydrosphere, biosphere) that terraformer buildings
/// raise a point at a time by consuming products. A planet's readiness is the average of its stats'
/// progress toward their targets; once enough planets in the system reach the bar, a Settler Uplink can
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
            Stats[s.Id] = Def.Stats.TryGetValue(s.Id, out int initial) ? initial : s.Initial;
        }
    }

    /// <summary>The stat and points a recipe effect adds ("terraform:&lt;stat&gt;:&lt;points&gt;"), or null.</summary>
    /// <param name="effect">A recipe's effect.</param>
    /// <returns>The stat id and points, if it is a terraforming effect.</returns>
    public static (string Stat, int Points)? TerraformEffect(string? effect) => ParseEffect(effect, "terraform");

    /// <summary>The stat and points a payload adds to the launcher's target planet ("payload:&lt;stat&gt;:&lt;points&gt;"), or null.</summary>
    /// <param name="effect">A recipe's effect.</param>
    /// <returns>The stat id and points, if it is a payload.</returns>
    public static (string Stat, int Points)? PayloadEffect(string? effect) => ParseEffect(effect, "payload");

    private static (string Stat, int Points)? ParseEffect(string? effect, string kind)
    {
        string[]? parts = effect?.Split(':');
        if ((parts is not { Length: 3 }) || (parts[0] != kind) || (int.TryParse(parts[2], out int points) == false))
        {
            return null;
        }

        return (parts[1], points);
    }

    /// <summary>True once a stat has reached its target on this planet.</summary>
    /// <param name="stat">Stat id.</param>
    /// <returns>Whether there's nothing left to terraform in it.</returns>
    public bool StatComplete(string stat) =>
        (World.Content.Terraform.Stats.FirstOrDefault(s => s.Id == stat) is { } def) && (Stats.GetValueOrDefault(stat) >= def.Target);

    /// <summary>The planet a payload launcher fires at, or null if none is picked.</summary>
    /// <param name="launcher">A payload launcher.</param>
    /// <returns>Its target.</returns>
    public Planet? TargetOf(Building launcher) =>
        (launcher.TargetPlanetId >= 0) && (launcher.TargetPlanetId < World.Planets.Count) && (launcher.TargetPlanetId != Id) ? World.Planets[launcher.TargetPlanetId] : null;

    /// <summary>The payload recipes a launcher can fire (researched, in its categories).</summary>
    private IEnumerable<RecipeDef> PayloadsFor(Building b) =>
        World.Content.RecipeList.Where(r => b.Def.Categories.Contains(r.Category) && World.Research.Unlocked.Contains(r.Id));

    /// <summary>The payload a launcher fires next: one it has loaded whose stat the target still needs.</summary>
    private RecipeDef? NextPayload(Building b) =>
        TargetOf(b) is { } target ? PayloadsFor(b).FirstOrDefault(r => b.Inv.Has(r.Inputs) && (PayloadEffect(r.Effect) is { } e) && (target.StatComplete(e.Stat) == false)) : null;

    /// <summary>Whether a recipe with this effect can start now: terraformers stop at their stat's target and the uplink waits for enough planets.</summary>
    private MachineStatus? EffectBlocked(Building b, RecipeDef r)
    {
        if ((TerraformEffect(r.Effect) is { } t) && StatComplete(t.Stat))
        {
            return MachineStatus.Complete;
        }

        if (PayloadEffect(r.Effect) is { } p)
        {
            if (TargetOf(b) is not { } target)
            {
                return MachineStatus.NoTarget;
            }

            if (target.StatComplete(p.Stat))
            {
                return MachineStatus.Complete;
            }
        }

        if ((r.Effect == CallSettlersEffect) && ((World.EnoughPlanetsReady == false) || World.SettlersCalled))
        {
            return MachineStatus.NotReady;
        }

        return null;
    }

    /// <summary>The recipe effect that calls the settlers.</summary>
    public const string CallSettlersEffect = "call-settlers";

    /// <summary>Applies a finished terraforming, payload or settler-call recipe.</summary>
    private void ApplyTerraformEffect(Building b, RecipeDef r)
    {
        if (TerraformEffect(r.Effect) is { } t)
        {
            AddTerraform(t.Stat, t.Points, b.Origin);
        }
        else if ((PayloadEffect(r.Effect) is { } p) && (TargetOf(b) is { } target))
        {
            target.AddTerraform(p.Stat, p.Points, new Int2(target.Width / 2, target.Height / 2));
        }
        else if (r.Effect == CallSettlersEffect)
        {
            World.SettlersCalled = true;
            World.Emit("The settlers are on their way. The system is ready for them. You win!", Id, b.Origin);
        }
    }

    /// <summary>Adds terraforming points to a stat, announcing a finished stat or a planet that has become ready.</summary>
    private void AddTerraform(string statId, int points, Int2 at)
    {
        PlanetStatDef? stat = World.Content.Terraform.Stats.FirstOrDefault(s => s.Id == statId);
        bool wasReady = IsReady;
        int before = Stats.GetValueOrDefault(statId);
        Stats[statId] = before + points;
        if ((stat is not null) && (before < stat.Target) && (before + points >= stat.Target))
        {
            World.Emit($"{Name}: {stat.Name} fully terraformed", Id, at);
        }

        if ((wasReady == false) && IsReady)
        {
            World.Emit(World.EnoughPlanetsReady ? "Enough planets are ready. Call the settlers from a Settler Uplink!" : $"{Name} is ready for settlers ({World.ReadyPlanetCount} of {World.PlanetsNeeded})", Id, at);
        }
    }
}
