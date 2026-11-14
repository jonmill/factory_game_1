using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Gives a building a player-chosen name (e.g. to label rail stops).</summary>
/// <param name="BuildingId">Entity id of the building.</param>
/// <param name="Name">New name; must be non-blank and at most 32 characters. Surrounding whitespace is trimmed.</param>
public sealed record RenameBuilding(int BuildingId, string Name) : Command
{
    /// <summary>Checks the building exists and the name is non-blank and at most 32 characters.</summary>
    public override string? Validate(World w) => (w.Get<Building>(BuildingId) is null) || string.IsNullOrWhiteSpace(Name) || (Name.Length > 32) ? "Invalid name" : null;
    /// <summary>Sets the trimmed name.</summary>
    public override void Apply(World w) => w.Get<Building>(BuildingId)!.Name = Name.Trim();
}
