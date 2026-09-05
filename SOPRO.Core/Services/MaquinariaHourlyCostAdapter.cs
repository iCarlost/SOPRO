using SOPRO.Core.Entities;
using Sopro.Calculation.Equipment;

namespace SOPRO.Core.Services;

/// <summary>Maps the legacy machinery entity to the independent hourly-cost engine.</summary>
public static class MaquinariaHourlyCostAdapter
{
    public static HourlyCostInput FromEntity(Maquinaria source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new HourlyCostInput
        {
            AcquisitionValue = source.ValorAdquisicion,
            TireValue = source.ValorLlantas,
            SpecialPartsValue = source.ValorPiezasEspeciales,
            SalvageFactor = source.FactorRescate,
            EconomicLifeHours = source.VidaEconomica,
            InterestRatePercentage = source.TasaInteres,
            EffectiveHoursPerYear = source.HorasEfectivasAnio,
            InsuranceRatePercentage = source.PrimaSeguro,
            MaintenanceFactor = source.FactorMantenimiento,
            FuelQuantity = source.CantidadCombustible,
            FuelPrice = source.PrecioCombustible,
            OilQuantity = source.CantidadAceite,
            OilPrice = source.PrecioAceite,
            TireLifeHours = source.VidaEconomicaLlantas,
            SpecialPartsLifeHours = source.VidaPiezasEspeciales,
            OperatorSalary = source.SalarioOperador,
            RealSalaryFactor = source.FactorSalarioReal,
            EffectiveHoursPerShift = source.HorasEfectivasTurno
        };
    }

    public static HourlyCostBreakdown Calculate(Maquinaria source)
        => HourlyCostCalculator.Calculate(FromEntity(source));
}
