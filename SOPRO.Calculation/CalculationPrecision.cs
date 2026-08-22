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
    /// <summary>Number of decimals used for quantities.</summary>
    public int QuantityDecimals { get; }

    /// <summary>Number of decimals used for monetary amounts.</summary>
    public int AmountDecimals { get; }

    /// <summary>Number of decimals used for percentages.</summary>
    public int PercentageDecimals { get; }

    /// <summary>Creates a precision configuration, normalizing negative values to zero.</summary>
    /// <param name="quantityDecimals">Decimals used for quantities.</param>
    /// <param name="amountDecimals">Decimals used for amounts.</param>
    /// <param name="percentageDecimals">Decimals used for percentages.</param>
    public CalculationPrecision(int quantityDecimals, int amountDecimals, int percentageDecimals)
    {
        QuantityDecimals = Math.Max(0, quantityDecimals);
        AmountDecimals = Math.Max(0, amountDecimals);
        PercentageDecimals = Math.Max(0, percentageDecimals);
    }
}
