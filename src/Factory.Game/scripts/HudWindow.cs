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
    /// <summary>The selected building's slots beside the commander's pockets.</summary>
    Machine,
    /// <summary>The selected helper bot's module slots and cargo beside the commander's pockets.</summary>
    Robot,
    /// <summary>The star system: every planet, its type, resources and readiness; pick one to view.</summary>
    System,
    /// <summary>The win screen, shown once the settlers have been called.</summary>
    Victory,
    /// <summary>The game menu: resume, save, load, controls, quit. Pauses the game while open.</summary>
    Menu,
    /// <summary>The planet map, opened from the minimap: drag to pan, wheel to zoom.</summary>
    Map,
}
