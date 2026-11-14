using Factory.Game.Platform;
using Factory.Sim;
using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game;

/// <summary>A notification line shown in the HUD message log.</summary>
/// <param name="Text">The message text.</param>
/// <param name="Color">Colour the message is drawn in.</param>
/// <param name="Time">Value of <see cref="GameRoot.Clock"/> when the message was posted, used to fade it out.</param>
public sealed record Message(string Text, Color Color, double Time);
