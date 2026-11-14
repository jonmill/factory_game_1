namespace Factory.Sim;

/// <summary>A command queued for application at a given tick.</summary>
/// <param name="Tick">Tick at (or after) which the command is applied; in the command log, the tick it was actually applied.</param>
/// <param name="Seq">Submission sequence number, breaking ties between commands due on the same tick.</param>
/// <param name="Command">The command itself.</param>
public sealed record ScheduledCommand(long Tick, int Seq, Command Command);
