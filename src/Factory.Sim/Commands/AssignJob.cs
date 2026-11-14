using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Give a follower an order.</summary>
/// <param name="UnitId">Entity id of the follower to command; must belong to the issuing player.</param>
/// <param name="Job">The job to assign.</param>
public sealed record AssignJob(int UnitId, Job Job) : Command
{
    /// <summary>Checks the unit is the player's follower and the job's targets (resource, buildings or train) exist.</summary>
    public override string? Validate(World w)
    {
        Unit? u = w.Get<Unit>(UnitId);
        if ((u is null) || u.IsLeader)
        {
            return "Not a follower";
        }

        if (u.OwnerPlayer != Player)
        {
            return "That follower answers to someone else";
        }

        switch (Job.Kind)
        {
            case JobKind.Gather:
                return (Job.Resource is not null) && w.Content.Resources.ContainsKey(Job.Resource) ? null : "Unknown resource";
            case JobKind.Haul:
                if ((w.Get<Building>(Job.FromId) is null) || (w.Get<Building>(Job.ToId) is null))
                {
                    return "Pick two buildings";
                }

                return Job.FromId == Job.ToId ? "Pick two different buildings" : null;
            case JobKind.OperateTrain:
                return w.Get<Train>(Job.TrainId) is null ? "No such train" : null;
            case JobKind.Manual:
                return "Followers can't be manually controlled";
            default:
                return null;
        }
    }
    /// <summary>Replaces the follower's current job.</summary>
    public override void Apply(World w)
    {
        Unit u = w.Get<Unit>(UnitId)!;
        u.Planet.SetJob(u, Job);
    }
}
