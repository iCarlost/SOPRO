using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Core.Services;
using Sopro.Calculation.Equipment;

namespace SOPRO.Tests.Services.CostoHorario;

[TestClass]
public class MaquinariaCostoHorarioCalculatorTests
{
    [TestMethod]
    public void FromEntity_MapsEveryCalculationInput()
    {
        var source = new Maquinaria
        {
            ValorAdquisicion = 101m,
            ValorLlantas = 102m,
            ValorPiezasEspeciales = 103m,
            FactorRescate = 0.04m,
            VidaEconomica = 105m,
            TasaInteres = 6m,
            HorasEfectivasAnio = 107m,
            PrimaSeguro = 8m,
            FactorMantenimiento = 0.09m,
            CantidadCombustible = 110m,
            PrecioCombustible = 11m,
            CantidadAceite = 112m,
            PrecioAceite = 13m,
            NumeroLlantas = 4,
            VidaEconomicaLlantas = 115m,
            VidaPiezasEspeciales = 116m,
            SalarioOperador = 117m,
            FactorSalarioReal = 1.18m,
            HorasEfectivasTurno = 119m
        };

        var input = MaquinariaHourlyCostAdapter.FromEntity(source);

        Assert.AreEqual(source.ValorAdquisicion, input.AcquisitionValue);
        Assert.AreEqual(source.ValorLlantas, input.TireValue);
        Assert.AreEqual(source.ValorPiezasEspeciales, input.SpecialPartsValue);
        Assert.AreEqual(source.FactorRescate, input.SalvageFactor);
        Assert.AreEqual(source.VidaEconomica, input.EconomicLifeHours);
        Assert.AreEqual(source.TasaInteres, input.InterestRatePercentage);
        Assert.AreEqual(source.HorasEfectivasAnio, input.EffectiveHoursPerYear);
        Assert.AreEqual(source.PrimaSeguro, input.InsuranceRatePercentage);
        Assert.AreEqual(source.FactorMantenimiento, input.MaintenanceFactor);
        Assert.AreEqual(source.CantidadCombustible, input.FuelQuantity);
        Assert.AreEqual(source.PrecioCombustible, input.FuelPrice);
        Assert.AreEqual(source.CantidadAceite, input.OilQuantity);
        Assert.AreEqual(source.PrecioAceite, input.OilPrice);
        Assert.AreEqual(source.VidaEconomicaLlantas, input.TireLifeHours);
        Assert.AreEqual(source.VidaPiezasEspeciales, input.SpecialPartsLifeHours);
        Assert.AreEqual(source.SalarioOperador, input.OperatorSalary);
        Assert.AreEqual(source.FactorSalarioReal, input.RealSalaryFactor);
        Assert.AreEqual(source.HorasEfectivasTurno, input.EffectiveHoursPerShift);
    }

    [TestMethod]
    public void FromValues_MapsEveryCalculationInput()
    {
        var input = MaquinariaHourlyCostAdapter.FromValues(
            101m, 102m, 103m, 0.04m, 105m, 6m, 107m, 8m, 0.09m,
            110m, 11m, 112m, 13m, 115m, 116m, 117m, 1.18m, 119m);

        Assert.AreEqual(101m, input.AcquisitionValue);
        Assert.AreEqual(102m, input.TireValue);
        Assert.AreEqual(103m, input.SpecialPartsValue);
        Assert.AreEqual(0.04m, input.SalvageFactor);
        Assert.AreEqual(105m, input.EconomicLifeHours);
        Assert.AreEqual(6m, input.InterestRatePercentage);
        Assert.AreEqual(107m, input.EffectiveHoursPerYear);
        Assert.AreEqual(8m, input.InsuranceRatePercentage);
        Assert.AreEqual(0.09m, input.MaintenanceFactor);
        Assert.AreEqual(110m, input.FuelQuantity);
        Assert.AreEqual(11m, input.FuelPrice);
        Assert.AreEqual(112m, input.OilQuantity);
        Assert.AreEqual(13m, input.OilPrice);
        Assert.AreEqual(115m, input.TireLifeHours);
        Assert.AreEqual(116m, input.SpecialPartsLifeHours);
        Assert.AreEqual(117m, input.OperatorSalary);
        Assert.AreEqual(1.18m, input.RealSalaryFactor);
        Assert.AreEqual(119m, input.EffectiveHoursPerShift);
    }

    [TestMethod]
    public void Calcular_DevuelveTodosLosComponentesDelGolden()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = 800000m,
            TireValue = 50000m,
            SpecialPartsValue = 60000m,
            SalvageFactor = 0.10m,
            EconomicLifeHours = 12000m,
            InterestRatePercentage = 21.24m,
            EffectiveHoursPerYear = 1600m,
            InsuranceRatePercentage = 3.00m,
            MaintenanceFactor = 0.20m,
            FuelQuantity = 10m,
            FuelPrice = 20m,
            OilQuantity = 1m,
            OilPrice = 40m,
            TireLifeHours = 3000m,
            SpecialPartsLifeHours = 5000m,
            OperatorSalary = 300m,
            RealSalaryFactor = 1.60m,
            EffectiveHoursPerShift = 8m
        };

        var result = HourlyCostCalculator.Calculate(input);

        Assert.AreEqual(690000m, result.NetValue);
        Assert.AreEqual(69000m, result.SalvageValue);
        Assert.AreEqual(379500m, result.AverageValue);
        Assert.AreEqual(51.75m, result.Depreciation);
        Assert.AreEqual(50.378625m, result.Investment);
        Assert.AreEqual(7.115625m, result.Insurance);
        Assert.AreEqual(10.35m, result.Maintenance);
        Assert.AreEqual(119.59425m, result.FixedChargesTotal);
        Assert.AreEqual(200m, result.Fuel);
        Assert.AreEqual(40m, result.Lubricants);
        Assert.AreEqual(16.666666666666666666666666667m, result.Tires);
        Assert.AreEqual(12m, result.SpecialParts);
        Assert.AreEqual(268.66666666666666666666666667m, result.ConsumptionTotal);
        Assert.AreEqual(480m, result.RealSalary);
        Assert.AreEqual(60m, result.Operation);
        Assert.AreEqual(448.26091666666666666666666667m, result.HourlyCost);
    }

    [TestMethod]
    public void Calculate_NonPositiveDenominators_SkipsDependentOperations()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = 800000m,
            TireValue = 50000m,
            SpecialPartsValue = 60000m,
            SalvageFactor = 0.10m,
            EconomicLifeHours = 0m,
            EffectiveHoursPerYear = 0m,
            TireLifeHours = 0m,
            SpecialPartsLifeHours = 0m,
            OperatorSalary = 300m,
            RealSalaryFactor = 1.60m,
            EffectiveHoursPerShift = 0m
        };

        var result = HourlyCostCalculator.Calculate(input);

        Assert.AreEqual(379500m, result.AverageValue);
        Assert.AreEqual(0m, result.Depreciation);
        Assert.AreEqual(0m, result.Investment);
        Assert.AreEqual(0m, result.Insurance);
        Assert.AreEqual(0m, result.Tires);
        Assert.AreEqual(0m, result.SpecialParts);
        Assert.AreEqual(480m, result.RealSalary);
        Assert.AreEqual(0m, result.Operation);
    }

    [TestMethod]
    public void Calculate_NullInput_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => HourlyCostCalculator.Calculate(null!));
    }

    [TestMethod]
    public void Calculate_PositiveYearDenominator_DoesNotHideAverageOverflow()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = decimal.MaxValue,
            SalvageFactor = 1m,
            EffectiveHoursPerYear = 1m
        };

        Assert.ThrowsException<OverflowException>(() => HourlyCostCalculator.Calculate(input));
    }

    [TestMethod]
    public void Calculate_PositiveShiftDenominator_DoesNotHideRealSalaryOverflow()
    {
        var input = new HourlyCostInput
        {
            OperatorSalary = decimal.MaxValue,
            RealSalaryFactor = 2m,
            EffectiveHoursPerShift = 1m
        };

        Assert.ThrowsException<OverflowException>(() => HourlyCostCalculator.Calculate(input));
    }

    [TestMethod]
    public void Calculate_NonPositiveDenominators_StillPropagatesIntermediateOverflow()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = decimal.MaxValue,
            SalvageFactor = 1m,
            EffectiveHoursPerYear = 0m,
            OperatorSalary = decimal.MaxValue,
            RealSalaryFactor = 2m,
            EffectiveHoursPerShift = 0m
        };

        Assert.ThrowsException<OverflowException>(() => HourlyCostCalculator.Calculate(input));
    }

    [TestMethod]
    public void Calculate_NegativeDenominators_UseTheSameGuards()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = 100m,
            TireValue = 10m,
            SpecialPartsValue = 5m,
            EconomicLifeHours = -1m,
            EffectiveHoursPerYear = -1m,
            TireLifeHours = -1m,
            SpecialPartsLifeHours = -1m,
            OperatorSalary = 300m,
            RealSalaryFactor = 1.6m,
            EffectiveHoursPerShift = -1m
        };

        var result = HourlyCostCalculator.Calculate(input);

        Assert.AreEqual(42.5m, result.AverageValue);
        Assert.AreEqual(0m, result.Depreciation);
        Assert.AreEqual(0m, result.Investment);
        Assert.AreEqual(0m, result.Insurance);
        Assert.AreEqual(0m, result.Tires);
        Assert.AreEqual(0m, result.SpecialParts);
        Assert.AreEqual(480m, result.RealSalary);
        Assert.AreEqual(0m, result.Operation);
    }

    [TestMethod]
    public void Calculate_NegativeDenominators_StillPropagatesIntermediateOverflow()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = decimal.MaxValue,
            SalvageFactor = 1m,
            EffectiveHoursPerYear = -1m,
            OperatorSalary = decimal.MaxValue,
            RealSalaryFactor = 2m,
            EffectiveHoursPerShift = -1m
        };

        Assert.ThrowsException<OverflowException>(() => HourlyCostCalculator.Calculate(input));
    }

    [TestMethod]
    public void Calculate_DoesNotMutateInput()
    {
        var input = new HourlyCostInput
        {
            AcquisitionValue = 100m,
            TireValue = 10m,
            SpecialPartsValue = 5m,
            MaintenanceFactor = 0.2m
        };

        var before = input with { };

        HourlyCostCalculator.Calculate(input);

        Assert.AreEqual(before, input);
    }
}
