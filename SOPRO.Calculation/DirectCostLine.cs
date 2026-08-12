namespace Sopro.Calculation;

/// <summary>
/// Direct cost line that feeds <see cref="SoproCalculationEngine.SumDirectCost"/>.
/// Equivalent to the mapping of the legacy domain <c>ConceptoPresupuesto</c>.
/// </summary>
/// <remarks>
/// Compatibility (Gate N1): <c>HasMatrix</c> must map exactly
/// <c>ConceptoPresupuesto.MatrizId.HasValue</c>; not the loaded navigation
/// (<c>Matriz != null</c>) nor an Id greater than zero.
/// </remarks>
public sealed record DirectCostLine(
    decimal Quantity,
    decimal UnitDirectCost,
    bool IsGrouping,
    bool HasMatrix);
