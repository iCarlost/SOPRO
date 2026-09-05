namespace Sopro.Calculation.Pricing;

/// <summary>
/// Pure calculation of utility (utilidad) with ISR/PTU net-to-gross conversion.
/// </summary>
public static class UtilityCalculator
{
    public static UtilityResult Calculate(UtilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal isr = Math.Max(0m, input.IsrPercentage);
        decimal ptu = Math.Max(0m, input.PtuPercentage);
        decimal factor = 1m - (isr + ptu) / 100m;
        int d = Math.Max(0, input.AmountDecimals);

        decimal grossPercentage;
        decimal netPercentage;

        if (input.IsAssistedMode)
        {
            netPercentage = Math.Max(0m, input.NetUtilidadPercentage);
            grossPercentage = factor > 0m
                ? Math.Round(netPercentage / factor, 5, MidpointRounding.AwayFromZero)
                : 0m;
        }
        else
        {
            grossPercentage = Math.Max(0m, input.GrossUtilidadPercentage);
            netPercentage = Math.Round(grossPercentage * factor, 5, MidpointRounding.AwayFromZero);
        }

        decimal importeUtilidad = Math.Round(input.BaseUtilidad * grossPercentage / 100m, d, MidpointRounding.AwayFromZero);
        decimal importeIsr      = Math.Round(importeUtilidad * isr / 100m, d, MidpointRounding.AwayFromZero);
        decimal importePtu      = Math.Round(importeUtilidad * ptu / 100m, d, MidpointRounding.AwayFromZero);
        decimal utilidadNeta   = Math.Round(importeUtilidad - importeIsr - importePtu, d, MidpointRounding.AwayFromZero);

        return new UtilityResult
        {
            BaseUtilidad = input.BaseUtilidad,
            GrossUtilidadPercentage = grossPercentage,
            NetUtilidadPercentage = netPercentage,
            IsrPercentage = isr,
            PtuPercentage = ptu,
            ImporteUtilidad = importeUtilidad,
            ImporteIsr = importeIsr,
            ImportePtu = importePtu,
            UtilidadNetaEstimada = utilidadNeta
        };
    }
}

public sealed record UtilityInput
{
    public decimal BaseUtilidad { get; init; }
    public decimal GrossUtilidadPercentage { get; init; }
    public decimal NetUtilidadPercentage { get; init; }
    public decimal IsrPercentage { get; init; }
    public decimal PtuPercentage { get; init; }
    public bool IsAssistedMode { get; init; }
    public int AmountDecimals { get; init; } = 2;
}

public sealed record UtilityResult
{
    public decimal BaseUtilidad { get; init; }
    public decimal GrossUtilidadPercentage { get; init; }
    public decimal NetUtilidadPercentage { get; init; }
    public decimal IsrPercentage { get; init; }
    public decimal PtuPercentage { get; init; }
    public decimal ImporteUtilidad { get; init; }
    public decimal ImporteIsr { get; init; }
    public decimal ImportePtu { get; init; }
    public decimal UtilidadNetaEstimada { get; init; }
}
