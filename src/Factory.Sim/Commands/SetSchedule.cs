using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Replaces a train's schedule.</summary>
/// <param name="TrainId">Entity id of the train.</param>
/// <param name="Stops">Rail stops to visit in order, with the action at each; empty leaves the train idle.</param>
public sealed record SetSchedule(int TrainId, ScheduleStop[] Stops) : Command
{
    /// <summary>Checks the train exists and every stop is a rail stop.</summary>
    public override string? Validate(World w)
    {
        if (w.Get<Train>(TrainId) is null)
        {
            return "No such train";
        }

        foreach (ScheduleStop s in Stops)
        {
            if (w.Get<Building>(s.StopId) is not { Def.Kind: BuildingKind.RailStop })
            {
                return "Schedule stops must be rail stops";
            }
        }

        return null;
    }
    /// <summary>Installs the schedule, halts the train and restarts it from the first stop.</summary>
    public override void Apply(World w)
    {
        Train t = w.Get<Train>(TrainId)!;
        t.Schedule.Clear();
        t.Schedule.AddRange(Stops);
        t.StopIndex = 0;
        t.Path.Clear();
        t.Progress = 0;
        t.Speed = 0;
        t.State = Stops.Length == 0 ? TrainState.NoSchedule : TrainState.Pathing;
    }
}
