namespace Factory.Sim;

/// <summary>Which of a building's inventories a hand transfer takes from.</summary>
public enum InventoryPart
{
    /// <summary>Output first, then inputs and fuel (storage buildings have one inventory).</summary>
    Any,
    /// <summary>Inputs and fuel only.</summary>
    Input,
    /// <summary>Products only.</summary>
    Output,
}
