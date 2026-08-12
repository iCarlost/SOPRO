namespace Sopro.Calculation;

/// <summary>
/// Entrada de porcentajes de la cascada de precio unitario.
/// Equivale a <c>BudgetPercentageInput</c> del dominio legacy.
/// </summary>
/// <remarks>
/// <c>CostoDirectoReferencia</c> se conserva solo por paridad de mapeo con el
/// legacy: no participa en ningun calculo de <see cref="UnitPriceCalculator"/>.
/// </remarks>
public sealed record PricePercentageInput
{
    public decimal CostoDirectoReferencia { get; init; }

    public decimal IndirectosCentral { get; init; }

    public decimal IndirectosCampo { get; init; }

    public decimal Financiamiento { get; init; }

    public decimal Utilidad { get; init; }

    public decimal CargosAdicionales { get; init; }

    public PercentageCalculationMode ModoCalculoPorcentajes { get; init; } = PercentageCalculationMode.Acumulables;
}
