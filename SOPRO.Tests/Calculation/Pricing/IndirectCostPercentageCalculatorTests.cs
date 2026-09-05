using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Pricing;

namespace SOPRO.Tests.Calculation.Pricing;

[TestClass]
public class IndirectCostPercentageCalculatorTests
{
    [TestMethod]
    public void Calculate_StandardCase_ReturnsExactPercentages()
    {
        var result = IndirectCostPercentageCalculator.Calculate(new IndirectCostPercentageInput
        {
            OfficeCentralAnnualTotal = 500_000m,
            AnnualWorkVolume = 10_000_000m,
            FieldTotal = 200_000m,
            DirectCost = 5_000_000m
        });

        Assert.AreEqual(5.0m, result.OfficeCentralPercentage);
        Assert.AreEqual(4.0m, result.FieldPercentage);
        Assert.AreEqual(9.0m, result.TotalPercentage);
    }

    [TestMethod]
    public void Calculate_ZeroAnnualVolume_ReturnsZeroOC()
    {
        var result = IndirectCostPercentageCalculator.Calculate(new IndirectCostPercentageInput
        {
            OfficeCentralAnnualTotal = 500_000m,
            AnnualWorkVolume = 0m,
            FieldTotal = 200_000m,
            DirectCost = 5_000_000m
        });

        Assert.AreEqual(0m, result.OfficeCentralPercentage);
        Assert.AreEqual(4.0m, result.FieldPercentage);
    }

    [TestMethod]
    public void Calculate_ZeroDirectCost_ReturnsZeroField()
    {
        var result = IndirectCostPercentageCalculator.Calculate(new IndirectCostPercentageInput
        {
            OfficeCentralAnnualTotal = 500_000m,
            AnnualWorkVolume = 10_000_000m,
            FieldTotal = 200_000m,
            DirectCost = 0m
        });

        Assert.AreEqual(5.0m, result.OfficeCentralPercentage);
        Assert.AreEqual(0m, result.FieldPercentage);
    }

    [TestMethod]
    public void Calculate_AllZero_ReturnsZero()
    {
        var result = IndirectCostPercentageCalculator.Calculate(new IndirectCostPercentageInput());

        Assert.AreEqual(0m, result.TotalPercentage);
    }

    [TestMethod]
    public void Calculate_NullInput_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => IndirectCostPercentageCalculator.Calculate(null!));
    }

    [TestMethod]
    public void Calculate_NegativeTotals_PreservesLegacyBehavior()
    {
        var result = IndirectCostPercentageCalculator.Calculate(new IndirectCostPercentageInput
        {
            OfficeCentralAnnualTotal = -100m,
            AnnualWorkVolume = 1000m,
            FieldTotal = -50m,
            DirectCost = 1000m
        });

        Assert.AreEqual(-10.0m, result.OfficeCentralPercentage);
        Assert.AreEqual(-5.0m, result.FieldPercentage);
    }
}
