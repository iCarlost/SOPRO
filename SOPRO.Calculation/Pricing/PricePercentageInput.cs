namespace Sopro.Calculation;

/// <summary>
/// Percentage input of the unit price cascade.
/// Equivalent to <c>BudgetPercentageInput</c> of the legacy domain.
/// </summary>
/// <remarks>
/// <c>ReferenceDirectCost</c> is kept only for mapping parity with the legacy:
/// it does not participate in any calculation of the pricing cascade.
/// </remarks>
public sealed record PricePercentageInput
{
    public decimal ReferenceDirectCost { get; init; }

    public decimal CentralIndirectsPercentage { get; init; }

    public decimal FieldIndirectsPercentage { get; init; }

    public decimal FinancingPercentage { get; init; }

    public decimal ProfitPercentage { get; init; }

    public decimal AdditionalChargesPercentage { get; init; }

    public PercentageCalculationMode Mode { get; init; } = PercentageCalculationMode.Accumulative;
}
