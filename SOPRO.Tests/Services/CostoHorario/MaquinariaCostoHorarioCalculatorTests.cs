using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Equipment;

namespace SOPRO.Tests.Services.CostoHorario;

[TestClass]
public class MaquinariaCostoHorarioCalculatorTests
{
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

        Assert.AreEqual(0m, result.AverageValue);
        Assert.AreEqual(0m, result.Depreciation);
        Assert.AreEqual(0m, result.Investment);
        Assert.AreEqual(0m, result.Insurance);
        Assert.AreEqual(0m, result.Tires);
        Assert.AreEqual(0m, result.SpecialParts);
        Assert.AreEqual(0m, result.RealSalary);
        Assert.AreEqual(0m, result.Operation);
    }

    [TestMethod]
    public void Calculate_ZeroDenominators_DoesNotEvaluateOverflowingTerms()
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

        var result = HourlyCostCalculator.Calculate(input);

        Assert.AreEqual(0m, result.AverageValue);
        Assert.AreEqual(0m, result.Investment);
        Assert.AreEqual(0m, result.Insurance);
        Assert.AreEqual(0m, result.RealSalary);
        Assert.AreEqual(0m, result.Operation);
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

        Assert.AreEqual(0m, result.AverageValue);
        Assert.AreEqual(0m, result.Depreciation);
        Assert.AreEqual(0m, result.Investment);
        Assert.AreEqual(0m, result.Insurance);
        Assert.AreEqual(0m, result.Tires);
        Assert.AreEqual(0m, result.SpecialParts);
        Assert.AreEqual(0m, result.RealSalary);
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
