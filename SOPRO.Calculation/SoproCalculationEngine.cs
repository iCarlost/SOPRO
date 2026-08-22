namespace Sopro.Calculation;

/// <summary>
/// Calculation engine of the package: arithmetic with screen precision.
/// Equivalent to <c>MotorCalculoSopro</c> of the legacy domain, without culture
/// formatting (formatting stays in the facade, N0 decision 9) and without SOPRO
/// domain types.
/// </summary>
public sealed class SoproCalculationEngine
{
    /// <summary>Precision configuration used by this engine.</summary>
    public CalculationPrecision Precision { get; }

    /// <summary>Creates an engine from an explicit precision configuration.</summary>
    /// <param name="precision">Precision used for all calculations.</param>
    public SoproCalculationEngine(CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        Precision = precision;
    }

    /// <summary>Creates an engine from quantity, amount and percentage decimals.</summary>
    /// <param name="quantityDecimals">Decimals used for quantities.</param>
    /// <param name="amountDecimals">Decimals used for amounts.</param>
    /// <param name="percentageDecimals">Decimals used for percentages.</param>
    public SoproCalculationEngine(int quantityDecimals, int amountDecimals, int percentageDecimals)
        : this(new CalculationPrecision(quantityDecimals, amountDecimals, percentageDecimals))
    {
    }

    /// <summary>Number of decimals used for quantities.</summary>
    public int QuantityDecimals => Precision.QuantityDecimals;

    /// <summary>Number of decimals used for amounts.</summary>
    public int AmountDecimals => Precision.AmountDecimals;

    /// <summary>Number of decimals used for percentages.</summary>
    public int PercentageDecimals => Precision.PercentageDecimals;

    // ═══ Rounding ═══

    /// <summary>Rounds a quantity using the configured quantity precision.</summary>
    public decimal RoundQuantity(decimal value)
        => Math.Round(value, Precision.QuantityDecimals, MidpointRounding.AwayFromZero);

    /// <summary>Rounds an amount using the configured amount precision.</summary>
    public decimal RoundAmount(decimal value)
        => Math.Round(value, Precision.AmountDecimals, MidpointRounding.AwayFromZero);

    /// <summary>Rounds a percentage using the configured percentage precision.</summary>
    public decimal RoundPercentage(decimal value)
        => Math.Round(value, Precision.PercentageDecimals, MidpointRounding.AwayFromZero);

    // ═══ Main screen operation ═══

    /// <summary>
    /// Multiplication with screen precision: rounds the visible unit price,
    /// multiplies and rounds the result (N0, decision 2: the quantity is NOT
    /// rounded beforehand).
    /// </summary>
    public decimal Multiply(decimal quantity, decimal unitPrice)
    {
        decimal visibleUnitPrice = RoundAmount(unitPrice);
        return RoundAmount(quantity * visibleUnitPrice);
    }

    /// <summary>Calculates an amount over a base using screen precision.</summary>
    public decimal CalculateAmountOverBase(decimal factor, decimal baseAmount)
        => Multiply(factor, baseAmount);

    // ═══ Percentage cascade ═══

    /// <summary>Calculates the complete unit-price percentage cascade.</summary>
    public PriceBreakdown CalculateUnitPrice(decimal directCost, PricePercentageInput percentages)
        => UnitPriceCalculator.Calculate(directCost, percentages, Precision);

    // ═══ Temporal distribution ═══

    /// <summary>Returns an immutable read-only list (see <see cref="AmountDistributor"/>).</summary>
    public IReadOnlyList<decimal> DistributeAmount(decimal total, IReadOnlyList<decimal>? weights)
        => AmountDistributor.DistributeAmount(total, weights, Precision);

    internal IReadOnlyList<decimal> DistributeAmount(
        decimal total,
        IReadOnlyList<decimal> weights,
        decimal totalWeights)
        => AmountDistributor.DistributeAmount(total, weights, totalWeights, Precision);

    /// <summary>Returns an immutable read-only list (see <see cref="AmountDistributor"/>).</summary>
    public IReadOnlyList<decimal> DistributeQuantity(decimal total, IReadOnlyList<decimal>? weights)
        => AmountDistributor.DistributeQuantity(total, weights, Precision);

    internal IReadOnlyList<decimal> DistributeQuantity(
        decimal total,
        IReadOnlyList<decimal> weights,
        decimal totalWeights)
        => AmountDistributor.DistributeQuantity(total, weights, totalWeights, Precision);

    // ═══ Precision sums ═══

    /// <summary>Null collections return zero (N0, decision 12); every element is rounded before accumulating.</summary>
    public decimal SumAmounts(IEnumerable<decimal>? values)
    {
        if (values == null) return 0m;
        decimal acc = 0m;
        foreach (var v in values)
            acc += RoundAmount(v);
        return RoundAmount(acc);
    }

    /// <summary>Null collections return zero; every element is rounded to quantity decimals before accumulating (N0, decision 14).</summary>
    public decimal SumQuantities(IEnumerable<decimal>? values)
    {
        if (values == null) return 0m;
        decimal acc = 0m;
        foreach (var v in values)
            acc += RoundQuantity(v);
        return RoundQuantity(acc);
    }

    /// <summary>
    /// Sums the direct cost of all terminal lines with a matrix assigned,
    /// applying <see cref="Multiply"/> per line and rounding the total.
    /// Equivalent to <c>MotorCalculoSopro.SumarCostoDirecto</c>.
    /// Null collections return zero.
    /// </summary>
    public decimal SumDirectCost(IEnumerable<DirectCostLine>? lines)
    {
        if (lines == null) return 0m;
        decimal total = 0m;
        foreach (var line in lines)
        {
            if (line.IsGrouping || !line.HasMatrix) continue;
            total += Multiply(line.Quantity, line.UnitDirectCost);
        }
        return RoundAmount(total);
    }
}
