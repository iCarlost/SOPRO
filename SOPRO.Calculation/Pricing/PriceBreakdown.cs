namespace Sopro.Calculation;

/// <summary>
/// Full breakdown of a unit price, with every component already rounded.
/// Equivalent to <c>DesglosePrecios</c> of the legacy domain.
/// Guarantee: DirectCost + IndirectCosts + Financing + Profit + AdditionalCharges
/// == UnitPrice (at the configured decimals, without a cent of drift).
/// </summary>
public sealed record PriceBreakdown(
    decimal DirectCost,
    decimal IndirectCosts,
    decimal Financing,
    decimal Profit,
    decimal AdditionalCharges,
    decimal UnitPrice,
    decimal CentralIndirectsPercentage = 0m,
    decimal FieldIndirectsPercentage = 0m)
{
    /// <summary>
    /// Central office indirects amount, prorated with a fixed precision of 6
    /// decimals and <c>MidpointRounding.AwayFromZero</c> (N0, decision 4).
    /// </summary>
    public decimal CentralIndirectCosts =>
        (CentralIndirectsPercentage + FieldIndirectsPercentage) > 0m
            ? Math.Round(IndirectCosts * CentralIndirectsPercentage
                         / (CentralIndirectsPercentage + FieldIndirectsPercentage),
                         6, MidpointRounding.AwayFromZero)
            : 0m;

    /// <summary>Field indirects amount.</summary>
    public decimal FieldIndirectCosts => IndirectCosts - CentralIndirectCosts;

    /// <summary>Subtotal DirectCost + IndirectCosts.</summary>
    public decimal Subtotal1 => DirectCost + IndirectCosts;

    /// <summary>Subtotal DirectCost + Ind + Financing.</summary>
    public decimal Subtotal2 => Subtotal1 + Financing;

    /// <summary>Subtotal DirectCost + Ind + Fin + Profit.</summary>
    public decimal Subtotal3 => Subtotal2 + Profit;
}
