using Factory.Game.Platform;
using Factory.Sim;
using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game;

/// <summary>The interaction mode that determines what a left click on the map does.</summary>
public enum ToolKind
{
    /// <summary>Clicking selects the entity under the cursor for the inspector.</summary>
    Select,

    /// <summary>Clicking places the chosen building; rails can be dragged.</summary>
    Place,

    /// <summary>Clicking a rail tile places the chosen train preset.</summary>
    PlaceTrain,

    /// <summary>Clicking marks a building, track, train or ghost for removal.</summary>
    Deconstruct,

    /// <summary>Picking the building a follower's haul order takes items from.</summary>
    PickHaulFrom,

    /// <summary>Picking the building a follower's haul order delivers items to.</summary>
    PickHaulTo,

    /// <summary>Picking the train a follower should operate.</summary>
    PickTrain,

    /// <summary>Clicking rail stops appends them to the selected train's schedule.</summary>
    PickStop,
}
