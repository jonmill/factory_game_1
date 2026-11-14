namespace Factory.Sim;

/// <summary>One of the four cardinal directions, used for building facing, belt flow and movement. Values run clockwise from north.</summary>
public enum Dir : byte
{
    /// <summary>Towards negative Y (up on screen).</summary>
    North = 0,
    /// <summary>Towards positive X.</summary>
    East = 1,
    /// <summary>Towards positive Y (down on screen).</summary>
    South = 2,
    /// <summary>Towards negative X.</summary>
    West = 3
}
