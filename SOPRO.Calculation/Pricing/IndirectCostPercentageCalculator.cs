namespace Sopro.Calculation.Pricing;

/// <summary>
/// Pure calculation of indirect cost percentages from raw totals.
/// </summary>
public static class IndirectCostPercentageCalculator
{
    public static IndirectCostPercentageResult Calculate(IndirectCostPercentageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal porcOC = input.AnnualWorkVolume > 0m
            ? input.OfficeCentralAnnualTotal / input.AnnualWorkVolume * 100m
            : 0m;

        decimal porcField = input.DirectCost > 0m
            ? input.FieldTotal / input.DirectCost * 100m
            : 0m;

        return new IndirectCostPercentageResult
        {
            OfficeCentralPercentage = porcOC,
            FieldPercentage = porcField,
            TotalPercentage = porcOC + porcField
        };
    }
}

public sealed record IndirectCostPercentageInput
{
    public decimal OfficeCentralAnnualTotal { get; init; }
    public decimal AnnualWorkVolume { get; init; }
    public decimal FieldTotal { get; init; }
    public decimal DirectCost { get; init; }
}

public sealed record IndirectCostPercentageResult
{
    public decimal OfficeCentralPercentage { get; init; }
    public decimal FieldPercentage { get; init; }
    public decimal TotalPercentage { get; init; }
}
