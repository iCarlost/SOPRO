namespace Sopro.Calculation.Matrices;

/// <summary>Component kinds understood by the materialized matrix graph.</summary>
public enum MatrixComponentType
{
    /// <summary>Material input.</summary>
    Material,
    /// <summary>Normal or percentage labor input.</summary>
    Labor,
    /// <summary>Machinery input.</summary>
    Machinery,
    /// <summary>Reference to another matrix node.</summary>
    Auxiliary,
    /// <summary>Normal or percentage tool input.</summary>
    Tool
}

/// <summary>
/// Immutable, adapter-ready input for one matrix component.
/// </summary>
/// <param name="Id">Stable component identifier.</param>
/// <param name="Order">Stable display/calculation order inside its matrix.</param>
/// <param name="Type">Component kind.</param>
/// <param name="Quantity">Quantity or percentage factor.</param>
/// <param name="ResolvedUnitPrice">
/// Unit price already resolved by the adapter. It is ignored for matrix references
/// and for percentage-of-labor components.
/// </param>
/// <param name="IsPercentageOfLabor">
/// Explicitly identifies labor and tool percentage components; no unit text is parsed.
/// </param>
/// <param name="ReferencedMatrixId">Referenced node for an auxiliary component.</param>
public sealed record MatrixComponentInput(
    int Id,
    int Order,
    MatrixComponentType Type,
    decimal Quantity,
    decimal ResolvedUnitPrice,
    bool IsPercentageOfLabor,
    int? ReferencedMatrixId = null);
