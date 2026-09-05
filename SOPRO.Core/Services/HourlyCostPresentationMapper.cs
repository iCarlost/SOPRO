using Sopro.Calculation.Equipment;

namespace SOPRO.Core.Services;

/// <summary>Immutable presentation values consumed by the legacy hourly-cost form.</summary>
public sealed record HourlyCostPresentationValues
{
    public decimal NetValue { get; init; }
    public decimal SalvageValue { get; init; }
    public decimal AverageValue { get; init; }
    public decimal Depreciation { get; init; }
    public decimal Investment { get; init; }
    public decimal Insurance { get; init; }
    public decimal Maintenance { get; init; }
    public decimal FixedChargesTotal { get; init; }
    public decimal Fuel { get; init; }
    public decimal Lubricants { get; init; }
    public decimal Tires { get; init; }
    public decimal SpecialParts { get; init; }
    public decimal ConsumptionTotal { get; init; }
    public decimal RealSalary { get; init; }
    public decimal Operation { get; init; }
    public decimal HourlyCost { get; init; }
}

public static class HourlyCostPresentationMapper
{
    public static HourlyCostPresentationValues FromBreakdown(HourlyCostBreakdown source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new HourlyCostPresentationValues
        {
            NetValue = source.NetValue,
            SalvageValue = source.SalvageValue,
            AverageValue = source.AverageValue,
            Depreciation = source.Depreciation,
            Investment = source.Investment,
            Insurance = source.Insurance,
            Maintenance = source.Maintenance,
            FixedChargesTotal = source.FixedChargesTotal,
            Fuel = source.Fuel,
            Lubricants = source.Lubricants,
            Tires = source.Tires,
            SpecialParts = source.SpecialParts,
            ConsumptionTotal = source.ConsumptionTotal,
            RealSalary = source.RealSalary,
            Operation = source.Operation,
            HourlyCost = source.HourlyCost
        };
    }
}
