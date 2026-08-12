using System.Collections.ObjectModel;

namespace Sopro.Calculation;

/// <summary>
/// Temporal distribution with residue adjustment in the last period.
/// Equivalent to <c>MotorCalculoSopro.DistribuirImporte</c>/<c>DistribuirCantidad</c>
/// of the legacy domain (N0, decisions 1, 5, 10, 11).
/// </summary>
internal static class AmountDistributor
{
    /// <summary>
    /// Distributes a total amount across N periods proportionally to its weights,
    /// rounding every part to <c>precision.AmountDecimals</c>.
    /// Critical guarantee: the sum of the result equals the total rounded to the
    /// same precision (e.g. <c>100.005</c> with 2 decimals sums <c>100.01</c>).
    /// The last period absorbs the residue (it may end negative, N0 decision 5).
    /// Null or empty weight collections return empty; a zero weight sum returns
    /// zeros (N0 decision 11).
    /// </summary>
    public static IReadOnlyList<decimal> DistributeAmount(
        decimal total,
        IReadOnlyList<decimal>? weights,
        CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        return Distribute(total, weights, precision.AmountDecimals);
    }

    /// <summary>
    /// Same as <see cref="DistributeAmount"/> using <c>precision.QuantityDecimals</c>.
    /// </summary>
    public static IReadOnlyList<decimal> DistributeQuantity(
        decimal total,
        IReadOnlyList<decimal>? weights,
        CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        return Distribute(total, weights, precision.QuantityDecimals);
    }

    private static IReadOnlyList<decimal> Distribute(decimal total, IReadOnlyList<decimal>? weights, int decimals)
    {
        if (weights == null || weights.Count == 0) return new ReadOnlyCollection<decimal>(Array.Empty<decimal>());

        decimal totalWeights = weights.Sum();
        if (totalWeights == 0m)
            return new ReadOnlyCollection<decimal>(weights.Select(_ => 0m).ToArray());

        var result = new decimal[weights.Count];
        decimal accumulated = 0m;

        for (int i = 0; i < weights.Count - 1; i++)
        {
            decimal share = weights[i] / totalWeights;
            result[i] = Math.Round(total * share, decimals, MidpointRounding.AwayFromZero);
            accumulated += result[i];
        }

        result[^1] = Math.Round(total - accumulated, decimals, MidpointRounding.AwayFromZero);
        return new ReadOnlyCollection<decimal>(result);
    }
}
