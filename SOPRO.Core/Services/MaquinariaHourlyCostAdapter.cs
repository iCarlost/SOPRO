using SOPRO.Core.Entities;
using Sopro.Calculation.Equipment;

namespace SOPRO.Core.Services;

/// <summary>Maps the legacy machinery entity to the independent hourly-cost engine.</summary>
public static class MaquinariaHourlyCostAdapter
{
    public static HourlyCostInput FromEntity(Maquinaria source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return FromValues(
            source.ValorAdquisicion,
            source.ValorLlantas,
            source.ValorPiezasEspeciales,
            source.FactorRescate,
            source.VidaEconomica,
            source.TasaInteres,
            source.HorasEfectivasAnio,
            source.PrimaSeguro,
            source.FactorMantenimiento,
            source.CantidadCombustible,
            source.PrecioCombustible,
            source.CantidadAceite,
            source.PrecioAceite,
            source.VidaEconomicaLlantas,
            source.VidaPiezasEspeciales,
            source.SalarioOperador,
            source.FactorSalarioReal,
            source.HorasEfectivasTurno);
    }

    public static HourlyCostInput FromValues(
        decimal acquisitionValue,
        decimal tireValue,
        decimal specialPartsValue,
        decimal salvageFactor,
        decimal economicLifeHours,
        decimal interestRatePercentage,
        decimal effectiveHoursPerYear,
        decimal insuranceRatePercentage,
        decimal maintenanceFactor,
        decimal fuelQuantity,
        decimal fuelPrice,
        decimal oilQuantity,
        decimal oilPrice,
        decimal tireLifeHours,
        decimal specialPartsLifeHours,
        decimal operatorSalary,
        decimal realSalaryFactor,
        decimal effectiveHoursPerShift)
        => new()
        {
            AcquisitionValue = acquisitionValue,
            TireValue = tireValue,
            SpecialPartsValue = specialPartsValue,
            SalvageFactor = salvageFactor,
            EconomicLifeHours = economicLifeHours,
            InterestRatePercentage = interestRatePercentage,
            EffectiveHoursPerYear = effectiveHoursPerYear,
            InsuranceRatePercentage = insuranceRatePercentage,
            MaintenanceFactor = maintenanceFactor,
            FuelQuantity = fuelQuantity,
            FuelPrice = fuelPrice,
            OilQuantity = oilQuantity,
            OilPrice = oilPrice,
            TireLifeHours = tireLifeHours,
            SpecialPartsLifeHours = specialPartsLifeHours,
            OperatorSalary = operatorSalary,
            RealSalaryFactor = realSalaryFactor,
            EffectiveHoursPerShift = effectiveHoursPerShift
        };

    public static HourlyCostBreakdown Calculate(Maquinaria source)
        => HourlyCostCalculator.Calculate(FromEntity(source));

    public static HourlyCostBreakdown Calculate(HourlyCostInput source)
        => HourlyCostCalculator.Calculate(source);
}
