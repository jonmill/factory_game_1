namespace Factory.Sim;

/// <summary>What a building did on its last update, or why it could not work. Shown to the player as machine state.</summary>
public enum MachineStatus
{
    /// <summary>Nothing to do (e.g. a generator with no demand or a battery neither charging nor discharging).</summary>
    Idle,
    /// <summary>Actively producing, generating or transferring.</summary>
    Working,
    /// <summary>A burner machine has run out of fuel.</summary>
    NoFuel,
    /// <summary>An electric machine is not connected to a network or the network supplied no power.</summary>
    NoPower,
    /// <summary>The recipe's ingredients are not available.</summary>
    NoInput,
    /// <summary>The output buffer is full, so production is paused until it is emptied.</summary>
    OutputFull,
    /// <summary>A drill has no minable resource left under its footprint.</summary>
    NoResource,
    /// <summary>A crafting machine has no recipe selected.</summary>
    NoRecipe,
    /// <summary>A steam engine is not placed beside water.</summary>
    NoWater,
    /// <summary>A lab has no active research to work on.</summary>
    NoResearch,
    /// <summary>A terraformer's stat has reached its target, so there is nothing left for it to do.</summary>
    Complete,
    /// <summary>A Settler Uplink is waiting for every planet to be ready.</summary>
    NotReady,
    /// <summary>A payload launcher with no target planet picked.</summary>
    NoTarget,
}
