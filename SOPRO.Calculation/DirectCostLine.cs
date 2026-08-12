namespace Sopro.Calculation;

/// <summary>
/// Linea de costo directo que alimenta la cascada de precio unitario.
/// Equivale al mapeo de <c>ConceptoPresupuesto</c> del dominio legacy.
/// </summary>
/// <remarks>
/// Compatibilidad (Gate N1): <c>HasMatrix</c> debe mapear exactamente
/// <c>MatrizId.HasValue</c> del concepto legacy; no la navegacion cargada
/// (<c>Matriz != null</c>) ni un Id mayor que cero.
/// </remarks>
public sealed record DirectCostLine(
    decimal CostoDirecto,
    PricePercentageInput Porcentajes,
    bool HasMatrix);
