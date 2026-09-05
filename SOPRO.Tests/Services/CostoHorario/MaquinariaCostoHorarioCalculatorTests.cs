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
    public void FromBreakdown_MapsEveryPresentationOutput()
    {
        var source = new HourlyCostBreakdown
        {
            NetValue = 201m,
            SalvageValue = 202m,
            AverageValue = 203m,
            Depreciation = 204m,
            Investment = 205m,
            Insurance = 206m,
            Maintenance = 207m,
            FixedChargesTotal = 208m,
            Fuel = 209m,
            Lubricants = 210m,
            Tires = 211m,
            SpecialParts = 212m,
            ConsumptionTotal = 213m,
            RealSalary = 214m,
            Operation = 215m,
            HourlyCost = 216m
        };

        var values = HourlyCostPresentationMapper.FromBreakdown(source);

        Assert.AreEqual(source.NetValue, values.NetValue);
        Assert.AreEqual(source.SalvageValue, values.SalvageValue);
        Assert.AreEqual(source.AverageValue, values.AverageValue);
        Assert.AreEqual(source.Depreciation, values.Depreciation);
        Assert.AreEqual(source.Investment, values.Investment);
        Assert.AreEqual(source.Insurance, values.Insurance);
        Assert.AreEqual(source.Maintenance, values.Maintenance);
        Assert.AreEqual(source.FixedChargesTotal, values.FixedChargesTotal);
        Assert.AreEqual(source.Fuel, values.Fuel);
        Assert.AreEqual(source.Lubricants, values.Lubricants);
        Assert.AreEqual(source.Tires, values.Tires);
        Assert.AreEqual(source.SpecialParts, values.SpecialParts);
        Assert.AreEqual(source.ConsumptionTotal, values.ConsumptionTotal);
        Assert.AreEqual(source.RealSalary, values.RealSalary);
        Assert.AreEqual(source.Operation, values.Operation);
        Assert.AreEqual(source.HourlyCost, values.HourlyCost);
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
