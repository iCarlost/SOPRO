namespace Sopro.Calculation;

/// <summary>
/// Screen precision: number of decimals for quantities, amounts and percentages.
/// Equivalent to the precision properties of the legacy domain <c>Proyecto</c>
/// (<c>DecimalesCantidad</c>, <c>DecimalesImporte</c>, <c>DecimalesPorcentaje</c>).
/// </summary>
/// <remarks>
/// Compatibility (N0, decision 7): negative decimals are normalized to zero with
/// <c>Math.Max(0, value)</c>, exactly like the legacy engine.
/// </remarks>
public sealed record CalculationPrecision
{
    public int QuantityDecimals { get; }

    public int AmountDecimals { get; }

    public int PercentageDecimals { get; }

    public CalculationPrecision(int quantityDecimals, int amountDecimals, int percentageDecimals)
    {
        QuantityDecimals = Math.Max(0, quantityDecimals);
        AmountDecimals = Math.Max(0, amountDecimals);
        PercentageDecimals = Math.Max(0, percentageDecimals);
    }
}
