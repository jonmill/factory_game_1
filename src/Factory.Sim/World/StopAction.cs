namespace Factory.Sim;

/// <summary>What a train does with its cargo wagons when it reaches a scheduled stop.</summary>
public enum StopAction
{
    /// <summary>Move items from the stop's inventory into the wagons until nothing more transfers.</summary>
    Load,
    /// <summary>Move items from the wagons into the stop's inventory until nothing more transfers.</summary>
    Unload,
    /// <summary>Pause briefly (still taking on fuel and water) and move on without transferring cargo.</summary>
    Pass,
}
