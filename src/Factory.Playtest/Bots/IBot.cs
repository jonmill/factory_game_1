using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>A headless player. Bots observe the world and act ONLY by submitting Commands.</summary>
public interface IBot
{
    /// <summary>Display name used in logs and reports.</summary>
    string Name { get; }
    /// <summary>Called once per tick before the world steps.</summary>
    void Tick(World world);
}
