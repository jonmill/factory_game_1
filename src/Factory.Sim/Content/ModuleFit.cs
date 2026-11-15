namespace Factory.Sim;

/// <summary>Which robots a module can be installed in.</summary>
public enum ModuleFit
{
    /// <summary>The commander and helper bots alike.</summary>
    Any,
    /// <summary>Only the commander.</summary>
    Commander,
    /// <summary>Only helper bots.</summary>
    Helper,
}
