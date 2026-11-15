namespace Factory.Sim;

/// <summary>Pick the planet a payload launcher fires at.</summary>
/// <param name="BuildingId">Entity id of the payload launcher.</param>
/// <param name="TargetPlanet">Index of the target planet in <see cref="World.Planets"/>, or -1 to stop.</param>
public sealed record SetLaunchTarget(int BuildingId, int TargetPlanet) : Command
{
    /// <summary>Checks the building is a payload launcher and the target is another planet.</summary>
    public override string? Validate(World w)
    {
        if (w.Get<Building>(BuildingId) is not { Def.Kind: BuildingKind.PayloadLauncher } b)
        {
            return "Invalid";
        }

        if (TargetPlanet == -1)
        {
            return null;
        }

        return (TargetPlanet < 0) || (TargetPlanet >= w.Planets.Count) || (w.Planets[TargetPlanet] == b.Planet) ? "Pick another planet" : null;
    }

    /// <summary>Sets the target.</summary>
    public override void Apply(World w) => w.Get<Building>(BuildingId)!.TargetPlanetId = TargetPlanet;
}
