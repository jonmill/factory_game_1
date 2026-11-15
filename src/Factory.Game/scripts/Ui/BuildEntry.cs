using Godot;

namespace Factory.Game.Ui;

/// <summary>One thing on the build bar: a building, a train preset or a tool.</summary>
/// <param name="Label">Display name.</param>
/// <param name="Tooltip">Tooltip with description and cost.</param>
/// <param name="Icon">Draws the icon into a rect.</param>
/// <param name="Activate">Selects it for placement.</param>
/// <param name="IsActive">True while it is the selected tool.</param>
public sealed record BuildEntry(string Label, string Tooltip, System.Action<CanvasItem, Rect2> Icon, System.Action Activate, System.Func<bool> IsActive);
