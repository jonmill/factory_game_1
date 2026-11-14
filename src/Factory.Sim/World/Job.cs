namespace Factory.Sim;

/// <summary>An order given to a unit. Immutable value so it can be serialized as part of a command.</summary>
public sealed record Job(JobKind Kind, string? Resource = null, int FromId = 0, int ToId = 0, string? Item = null, int TrainId = 0)
{
    /// <summary>Shared instance of the idle job.</summary>
    public static readonly Job Idle = new(JobKind.Idle);
    /// <summary>Shared instance of the manual (player-controlled) job used by the leader.</summary>
    public static readonly Job Manual = new(JobKind.Manual);
}
