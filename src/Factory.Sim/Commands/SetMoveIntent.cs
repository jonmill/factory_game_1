using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Leader movement input (sent when the held direction changes, not every frame).</summary>
/// <param name="UnitId">Entity id of the issuing player's leader.</param>
/// <param name="Dx">Horizontal input; only the sign is used (-1, 0 or 1).</param>
/// <param name="Dy">Vertical input; only the sign is used (-1, 0 or 1, positive is south).</param>
public sealed record SetMoveIntent(int UnitId, int Dx, int Dy) : Command
{
    /// <summary>Checks the unit is the player's leader.</summary>
    public override string? Validate(World w) => Leader(w, UnitId) is null ? "Not your leader" : null;
    /// <summary>Sets the leader's movement direction, clamped to -1..1 on each axis.</summary>
    public override void Apply(World w) => Leader(w, UnitId)!.MoveIntent = new Int2(System.Math.Sign(Dx), System.Math.Sign(Dy));
}
