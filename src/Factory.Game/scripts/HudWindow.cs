namespace Factory.Game;

/// <summary>The HUD's centred windows; at most one is open at a time.</summary>
public enum HudWindow
{
    /// <summary>No window open.</summary>
    None,
    /// <summary>The commander's pockets and the colony's storage.</summary>
    Inventory,
    /// <summary>Queue crafts on the lander's fabricator.</summary>
    Replicator,
    /// <summary>The research tree.</summary>
    Tech,
    /// <summary>Controls and how to play.</summary>
    Help,
}
