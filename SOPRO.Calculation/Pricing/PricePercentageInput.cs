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
    /// <summary>Reference direct cost retained for legacy mapping parity.</summary>
    public decimal ReferenceDirectCost { get; init; }

    /// <summary>Central office indirects percentage.</summary>
    public decimal CentralIndirectsPercentage { get; init; }

    /// <summary>Field indirects percentage.</summary>
    public decimal FieldIndirectsPercentage { get; init; }

    /// <summary>Financing percentage.</summary>
    public decimal FinancingPercentage { get; init; }

    /// <summary>Profit percentage.</summary>
    public decimal ProfitPercentage { get; init; }

    /// <summary>Additional charges percentage.</summary>
    public decimal AdditionalChargesPercentage { get; init; }

    /// <summary>Percentage cascade mode.</summary>
    public PercentageCalculationMode Mode { get; init; } = PercentageCalculationMode.Accumulative;
}
