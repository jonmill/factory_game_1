namespace Factory.Sim;

/// <summary>
/// Plays a replay back a batch of ticks at a time, so a client can rebuild a long game across many frames
/// behind a progress bar. Stepping it to the end gives exactly the world <see cref="Replay.Play"/> gives:
/// each command is submitted, in recorded order, just before the tick it was applied on, so it gets the
/// same submission sequence and is applied on the same tick in the same order as when it was recorded.
/// </summary>
public sealed class ReplayPlayback
{
    private readonly Replay _replay;
    private int _next;

    /// <summary>Creates the starting world for a replay, ready to be stepped.</summary>
    /// <param name="replay">The replay to play back.</param>
    /// <param name="content">Game content to simulate with.</param>
    /// <param name="untilTick">Tick to stop on; the recorded end by default.</param>
    public ReplayPlayback(Replay replay, ContentDb content, long? untilTick = null)
    {
        _replay = replay;
        EndTick = untilTick ?? replay.FinalTick;
        World = new World(content, replay.Settings);
    }

    /// <summary>The world being rebuilt. Only hand it to the game once <see cref="Done"/>.</summary>
    public World World { get; }

    /// <summary>Tick playback stops on.</summary>
    public long EndTick { get; }

    /// <summary>True once the world has reached <see cref="EndTick"/>.</summary>
    public bool Done => World.Tick >= EndTick;

    /// <summary>How far playback has got, from 0 to 1.</summary>
    public double Progress => EndTick <= 0 ? 1 : Math.Clamp(World.Tick / (double)EndTick, 0, 1);

    /// <summary>Advances up to <paramref name="maxTicks"/> ticks, stopping at <see cref="EndTick"/>.</summary>
    /// <param name="maxTicks">Most ticks to simulate in this call.</param>
    /// <returns>The number of ticks simulated.</returns>
    public int Step(int maxTicks)
    {
        int steps = 0;
        List<ScheduledCommand> commands = _replay.Commands;
        while ((steps < maxTicks) && (Done == false))
        {
            while ((_next < commands.Count) && (commands[_next].Tick <= World.Tick))
            {
                World.SubmitAt(commands[_next].Tick, commands[_next].Command);
                _next++;
            }

            World.Step();
            steps++;
        }

        return steps;
    }

    /// <summary>Plays to the end in one go.</summary>
    /// <returns>The finished world.</returns>
    public World Finish()
    {
        while (Done == false)
        {
            Step(int.MaxValue);
        }

        return World;
    }
}
