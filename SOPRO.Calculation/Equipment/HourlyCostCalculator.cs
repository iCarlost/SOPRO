namespace Sopro.Calculation.Equipment;

/// <summary>Immutable inputs for the hourly machinery cost calculation.</summary>
public sealed record HourlyCostInput
{
    public decimal AcquisitionValue { get; init; }
    public decimal TireValue { get; init; }
    public decimal SpecialPartsValue { get; init; }
    public decimal SalvageFactor { get; init; }
    public decimal EconomicLifeHours { get; init; }
    public decimal InterestRatePercentage { get; init; }
    public decimal EffectiveHoursPerYear { get; init; }
    public decimal InsuranceRatePercentage { get; init; }
    public decimal MaintenanceFactor { get; init; }
    public decimal FuelQuantity { get; init; }
    public decimal FuelPrice { get; init; }
    public decimal OilQuantity { get; init; }
    public decimal OilPrice { get; init; }
    public decimal TireLifeHours { get; init; }
    public decimal SpecialPartsLifeHours { get; init; }
    public decimal OperatorSalary { get; init; }
    public decimal RealSalaryFactor { get; init; }
    public decimal EffectiveHoursPerShift { get; init; }
}

/// <summary>Detailed, deterministic output of the hourly machinery cost calculation.</summary>
public sealed record HourlyCostBreakdown
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

/// <summary>
/// Single implementation of the legacy OPUS/RLOPSRM hourly machinery cost formula.
/// The calculation is pure and does not depend on SOPRO domain or infrastructure types.
/// </summary>
public static class HourlyCostCalculator
{
    public static HourlyCostBreakdown Calculate(HourlyCostInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal netValue = input.AcquisitionValue - input.TireValue - input.SpecialPartsValue;
        decimal salvageValue = netValue * input.SalvageFactor;

        decimal averageValue = (netValue + salvageValue) / 2m;

        decimal depreciation = input.EconomicLifeHours > 0
            ? (netValue - salvageValue) / input.EconomicLifeHours
            : 0m;
        decimal investment = input.EffectiveHoursPerYear > 0
            ? averageValue * (input.InterestRatePercentage / 100m) / input.EffectiveHoursPerYear
            : 0m;
        decimal insurance = input.EffectiveHoursPerYear > 0
            ? averageValue * (input.InsuranceRatePercentage / 100m) / input.EffectiveHoursPerYear
            : 0m;
        decimal maintenance = input.MaintenanceFactor * depreciation;
        decimal fixedChargesTotal = depreciation + investment + insurance + maintenance;

        decimal fuel = input.FuelQuantity * input.FuelPrice;
        decimal lubricants = input.OilQuantity * input.OilPrice;
        decimal tires = input.TireLifeHours > 0
            ? input.TireValue / input.TireLifeHours
            : 0m;
        decimal specialParts = input.SpecialPartsLifeHours > 0
            ? input.SpecialPartsValue / input.SpecialPartsLifeHours
            : 0m;
        decimal consumptionTotal = fuel + lubricants + tires + specialParts;

        decimal realSalary = input.OperatorSalary * input.RealSalaryFactor;
        decimal operation = input.EffectiveHoursPerShift > 0
            ? realSalary / input.EffectiveHoursPerShift
            : 0m;

        return new HourlyCostBreakdown
        {
            NetValue = netValue,
            SalvageValue = salvageValue,
            AverageValue = averageValue,
            Depreciation = depreciation,
            Investment = investment,
            Insurance = insurance,
            Maintenance = maintenance,
            FixedChargesTotal = fixedChargesTotal,
            Fuel = fuel,
            Lubricants = lubricants,
            Tires = tires,
            SpecialParts = specialParts,
            ConsumptionTotal = consumptionTotal,
            RealSalary = realSalary,
            Operation = operation,
            HourlyCost = fixedChargesTotal + consumptionTotal + operation
        };
    }
}
