using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>
/// The ONLY way to change simulation state. Commands are small serializable records, validated
/// against the world before being applied. This is the seam for multiplayer: in lockstep each peer
/// broadcasts its commands for a tick; with a server, clients send commands and the host applies them.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$cmd")]
[JsonDerivedType(typeof(PlaceBuilding), "place")]
[JsonDerivedType(typeof(PlaceTrain), "placeTrain")]
[JsonDerivedType(typeof(DeconstructEntity), "deconstruct")]
[JsonDerivedType(typeof(DeconstructRail), "deconstructRail")]
[JsonDerivedType(typeof(CancelSite), "cancelSite")]
[JsonDerivedType(typeof(SetRecipe), "setRecipe")]
[JsonDerivedType(typeof(QueueCraft), "queueCraft")]
[JsonDerivedType(typeof(CancelQueuedCrafts), "clearCraft")]
[JsonDerivedType(typeof(SetResearch), "setResearch")]
[JsonDerivedType(typeof(AssignJob), "assignJob")]
[JsonDerivedType(typeof(SetMoveIntent), "move")]
[JsonDerivedType(typeof(MineTile), "mine")]
[JsonDerivedType(typeof(DepositToBuilding), "deposit")]
[JsonDerivedType(typeof(TakeFromBuilding), "take")]
[JsonDerivedType(typeof(SetSchedule), "setSchedule")]
[JsonDerivedType(typeof(RenameBuilding), "rename")]
public abstract record Command
{
    /// <summary>Issuing player (0 in single player).</summary>
    public int Player { get; init; }
    /// <summary>Index of the planet the command acts on, in <see cref="World.Planets"/>.</summary>
    public int PlanetId { get; init; }

    /// <summary>Null if the command is legal right now; otherwise a reason shown to the player.</summary>
    public abstract string? Validate(World w);
    /// <summary>Performs the state change. Only called after <see cref="Validate"/> returned null for the same tick.</summary>
    public abstract void Apply(World w);

    /// <summary>The planet this command targets.</summary>
    protected Planet P(World w) => w.Planets[PlanetId];
    /// <summary>True if <see cref="PlanetId"/> refers to an existing planet.</summary>
    protected bool ValidPlanet(World w) => (PlanetId >= 0) && (PlanetId < w.Planets.Count);

    /// <summary>Returns the unit if it is a leader owned by the issuing player, otherwise null.</summary>
    protected Unit? Leader(World w, int unitId)
    {
        Unit? u = w.Get<Unit>(unitId);

        return (u is { IsLeader: true }) && (u.OwnerPlayer == Player) ? u : null;
    }
}
