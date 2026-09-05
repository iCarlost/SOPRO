using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Pricing;

namespace SOPRO.Tests.Calculation.Pricing;

[TestClass]
public class UtilityCalculatorTests
{
    [TestMethod]
    public void Calculate_DirectMode_DefaultRates_ReturnsGoldenValues()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = false,
            AmountDecimals = 2
        });

        Assert.AreEqual(1_000_000m, result.BaseUtilidad);
        Assert.AreEqual(10m, result.GrossUtilidadPercentage);
        Assert.AreEqual(6m, result.NetUtilidadPercentage);
        Assert.AreEqual(100_000m, result.ImporteUtilidad);
        Assert.AreEqual(30_000m, result.ImporteIsr);
        Assert.AreEqual(10_000m, result.ImportePtu);
        Assert.AreEqual(60_000m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calculate_AssistedMode_DefaultRates_ReturnsGoldenValues()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            NetUtilidadPercentage = 6m,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = true,
            AmountDecimals = 2
        });

        Assert.AreEqual(10m, result.GrossUtilidadPercentage);
        Assert.AreEqual(6m, result.NetUtilidadPercentage);
        Assert.AreEqual(100_000m, result.ImporteUtilidad);
        Assert.AreEqual(30_000m, result.ImporteIsr);
        Assert.AreEqual(10_000m, result.ImportePtu);
        Assert.AreEqual(60_000m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calculate_DirectMode_CustomRates_ReturnsCorrectValues()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 2_000_000m,
            GrossUtilidadPercentage = 15m,
            IsrPercentage = 25m,
            PtuPercentage = 5m,
            IsAssistedMode = false,
            AmountDecimals = 2
        });

        Assert.AreEqual(300_000m, result.ImporteUtilidad);
        Assert.AreEqual(75_000m, result.ImporteIsr);
        Assert.AreEqual(15_000m, result.ImportePtu);
        Assert.AreEqual(210_000m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calculate_AssistedMode_CustomRates_ReturnsCorrectValues()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 2_000_000m,
            NetUtilidadPercentage = 10.5m,
            IsrPercentage = 25m,
            PtuPercentage = 5m,
            IsAssistedMode = true,
            AmountDecimals = 2
        });

        Assert.AreEqual(15m, result.GrossUtilidadPercentage);
        Assert.AreEqual(10.5m, result.NetUtilidadPercentage);
        Assert.AreEqual(300_000m, result.ImporteUtilidad);
    }

    [TestMethod]
    public void Calculate_ZeroIsrPtu_FactorIsOne()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = 0m,
            PtuPercentage = 0m,
            IsAssistedMode = false,
            AmountDecimals = 2
        });

        Assert.AreEqual(10m, result.NetUtilidadPercentage);
        Assert.AreEqual(100_000m, result.ImporteUtilidad);
        Assert.AreEqual(0m, result.ImporteIsr);
        Assert.AreEqual(0m, result.ImportePtu);
        Assert.AreEqual(100_000m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calculate_NegativeIsrPtu_ClampedToZero()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = -30m,
            PtuPercentage = -10m,
            IsAssistedMode = false,
            AmountDecimals = 2
        });

        Assert.AreEqual(0m, result.IsrPercentage);
        Assert.AreEqual(0m, result.PtuPercentage);
        Assert.AreEqual(10m, result.NetUtilidadPercentage);
    }

    [TestMethod]
    public void Calculate_DifferentDecimalPrecisions_RespectsAmountDecimals()
    {
        var r0 = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = false,
            AmountDecimals = 0
        });

        var r4 = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = false,
            AmountDecimals = 4
        });

        Assert.AreEqual(100_000m, r0.ImporteUtilidad);
        Assert.AreEqual(100_000m, r4.ImporteUtilidad);
    }

    [TestMethod]
    public void Calculate_NegativeAmountDecimals_ClampsToZero()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = false,
            AmountDecimals = -1
        });

        Assert.AreEqual(100_000m, result.ImporteUtilidad);
        Assert.AreEqual(30_000m, result.ImporteIsr);
        Assert.AreEqual(10_000m, result.ImportePtu);
        Assert.AreEqual(60_000m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calculate_NullInput_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => UtilityCalculator.Calculate(null!));
    }

    [TestMethod]
    public void Calculate_AssistedMode_ZeroFactor_ReturnsZeroGross()
    {
        var result = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            NetUtilidadPercentage = 10m,
            IsrPercentage = 50m,
            PtuPercentage = 50m,
            IsAssistedMode = true,
            AmountDecimals = 2
        });

        Assert.AreEqual(0m, result.GrossUtilidadPercentage);
        Assert.AreEqual(0m, result.ImporteUtilidad);
    }

    [TestMethod]
    public void Roundtrip_DirectAndAssisted_Converge()
    {
        var direct = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            GrossUtilidadPercentage = 10m,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = false,
            AmountDecimals = 2
        });

        var assisted = UtilityCalculator.Calculate(new UtilityInput
        {
            BaseUtilidad = 1_000_000m,
            NetUtilidadPercentage = direct.NetUtilidadPercentage,
            IsrPercentage = 30m,
            PtuPercentage = 10m,
            IsAssistedMode = true,
            AmountDecimals = 2
        });

        Assert.AreEqual(direct.GrossUtilidadPercentage, assisted.GrossUtilidadPercentage);
        Assert.AreEqual(direct.ImporteUtilidad, assisted.ImporteUtilidad);
    }
}
