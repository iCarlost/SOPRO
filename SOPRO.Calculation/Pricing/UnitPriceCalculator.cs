namespace Sopro.Calculation;

/// <summary>
/// Percentage cascade with rounding at every visible step.
/// Equivalent to <c>MotorCalculoSopro.CalcularPrecioUnitario</c> of the legacy
/// domain (N0, decision 3: central + field indirects are added before the
/// monetary rounding).
/// </summary>
internal static class UnitPriceCalculator
{
    /// <summary>
    /// Computes the full breakdown rounding EVERY intermediate step to
    /// <c>precision.AmountDecimals</c> with <c>MidpointRounding.AwayFromZero</c>.
    /// The unit price is built as the sum of already-rounded parts to guarantee
    /// the balance.
    /// </summary>
    public static PriceBreakdown Calculate(
        decimal directCost,
        PricePercentageInput percentages,
        CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(percentages);
        ArgumentNullException.ThrowIfNull(precision);

        bool overDirectCost = percentages.Mode == PercentageCalculationMode.OverDirectCost;
        decimal cd = Round(precision, directCost);

        decimal totalIndirects = percentages.CentralIndirectsPercentage + percentages.FieldIndirectsPercentage;
        decimal amountIndirects = Round(precision, cd * totalIndirects / 100m);
        decimal subtotal1 = Round(precision, cd + amountIndirects);

        decimal baseFinancing = overDirectCost ? cd : subtotal1;
        decimal amountFinancing = Round(precision, baseFinancing * percentages.FinancingPercentage / 100m);
        decimal subtotal2 = Round(precision, subtotal1 + amountFinancing);

        decimal baseProfit = overDirectCost ? cd : subtotal2;
        decimal amountProfit = Round(precision, baseProfit * percentages.ProfitPercentage / 100m);
        decimal subtotal3 = Round(precision, subtotal2 + amountProfit);

        decimal baseCharges = overDirectCost ? cd : subtotal3;
        decimal amountCharges = Round(precision, baseCharges * percentages.AdditionalChargesPercentage / 100m);

        decimal unitPrice = Round(precision, subtotal3 + amountCharges);

        return new PriceBreakdown(
            cd,
            amountIndirects,
            amountFinancing,
            amountProfit,
            amountCharges,
            unitPrice,
            percentages.CentralIndirectsPercentage,
            percentages.FieldIndirectsPercentage);
    }

    private static decimal Round(CalculationPrecision precision, decimal value)
        => Math.Round(value, precision.AmountDecimals, MidpointRounding.AwayFromZero);
}
