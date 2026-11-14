namespace Factory.Sim;

/// <summary>What a <see cref="ConstructionSite"/> will do when its work is completed.</summary>
public enum SiteKind
{
    /// <summary>Construct a building (or rail) from its definition once materials are delivered.</summary>
    Build,
    /// <summary>Dismantle an existing building, train or rail tile and refund its materials.</summary>
    Deconstruct,
    /// <summary>Assemble a train consist on a stretch of rail.</summary>
    BuildTrain,
}
