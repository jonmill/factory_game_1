namespace Factory.Sim;

/// <summary>One entry in a train's schedule.</summary>
/// <param name="StopId">Id of the rail stop building to travel to.</param>
/// <param name="Action">What to do with the cargo on arrival.</param>
public sealed record ScheduleStop(int StopId, StopAction Action);
