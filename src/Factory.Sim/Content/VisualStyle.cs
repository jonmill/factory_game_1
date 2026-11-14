namespace Factory.Sim;

/// <summary>
/// The art direction a building or vehicle is drawn in, so its look matches the technology behind it.
/// Content data only: the simulation never reads it.
/// </summary>
public enum VisualStyle
{
    /// <summary>Steam, coal and wood era: weathered iron, stone, timber and soot.</summary>
    Industrial,
    /// <summary>Electric and ship technology: clean white hulls with glowing accent trim.</summary>
    HighTech,
}
