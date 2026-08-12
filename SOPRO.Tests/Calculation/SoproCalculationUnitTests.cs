using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;

namespace SOPRO.Tests.Calculation;

[TestClass]
public class CalculationPrecisionTests
{
    [TestMethod]
    public void Constructor_ConDecimalesNegativos_NormalizaACero()
    {
        var precision = new CalculationPrecision(-1, -2, -3);

        Assert.AreEqual(0, precision.QuantityDecimals);
        Assert.AreEqual(0, precision.AmountDecimals);
        Assert.AreEqual(0, precision.PercentageDecimals);
    }

    [TestMethod]
    public void Constructor_ConservaValoresPositivos()
    {
        var precision = new CalculationPrecision(4, 2, 4);

        Assert.AreEqual(4, precision.QuantityDecimals);
        Assert.AreEqual(2, precision.AmountDecimals);
        Assert.AreEqual(4, precision.PercentageDecimals);
    }
}

[TestClass]
public class SoproCalculationEngineUnitTests
{
    private static SoproCalculationEngine Motor2()
        => new(new CalculationPrecision(2, 2, 4));

    [TestMethod]
    public void Constructor_ConPrecisionNula_LanzaArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new SoproCalculationEngine(null));
    }

    [TestMethod]
    public void Precisiones_SeExponenDespuesDeLaNormalizacion()
    {
        var motor = new SoproCalculationEngine(-1, 3, 0);

        Assert.AreEqual(0, motor.QuantityDecimals);
        Assert.AreEqual(3, motor.AmountDecimals);
        Assert.AreEqual(0, motor.PercentageDecimals);
        Assert.AreEqual(0, motor.Precision.QuantityDecimals);
        Assert.AreEqual(3, motor.Precision.AmountDecimals);
        Assert.AreEqual(0, motor.Precision.PercentageDecimals);
    }

    [TestMethod]
    public void Multiply_RedondeaPrecioUnitarioVisible_AntesDeMultiplicar()
    {
        // 652 × 13.3875 → 652 × 13.39 = 8,730.28 (no 8,728.65 con decimales ocultos)
        Assert.AreEqual(8730.28m, Motor2().Multiply(652m, 13.3875m));
    }

    [TestMethod]
    public void Multiply_NoRedondeaLaCantidadPreviamente()
    {
        // N0 fila 2: la cantidad 1.234 se usa completa (1.234 × 10 = 12.34);
        // redondearla antes daría 1.23 × 10 = 12.30.
        var motor = new SoproCalculationEngine(2, 2, 4);
        Assert.AreEqual(12.34m, motor.Multiply(1.234m, 10m));
    }

    [TestMethod]
    public void RoundPercentage_UsaDecimalesDePorcentaje()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        Assert.AreEqual(33.3333m, motor.RoundPercentage(33.333349m));
        Assert.AreEqual(33.3334m, motor.RoundPercentage(33.33335m));

        var uno = new SoproCalculationEngine(2, 2, 1);
        Assert.AreEqual(33.3m, uno.RoundPercentage(33.333333m));
        Assert.AreEqual(33.4m, uno.RoundPercentage(33.35m));

        var cero = new SoproCalculationEngine(2, 2, 0);
        Assert.AreEqual(33m, cero.RoundPercentage(33.33335m));
        Assert.AreEqual(34m, cero.RoundPercentage(33.5m));
    }

    [TestMethod]
    public void SumAmounts_ConColeccionNula_DevuelveCero()
    {
        Assert.AreEqual(0m, Motor2().SumAmounts(null));
    }

    [TestMethod]
    public void SumQuantities_ConColeccionNula_DevuelveCero()
    {
        Assert.AreEqual(0m, Motor2().SumQuantities(null));
    }

    [TestMethod]
    public void SumAmounts_RedondeaCadaElementoAntesDeAcumular()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        decimal total = motor.SumAmounts(new[] { 1.005m, 2.005m, 3.005m });

        // Cada elemento se redondea a 2 decimales (1.01 + 2.01 + 3.01 = 6.03),
        // no se acumulan los valores crudos (6.015 → 6.02).
        Assert.AreEqual(6.03m, total);
    }

    [TestMethod]
    public void SumQuantities_RedondeaCadaElementoAntesDeAcumular()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        decimal total = motor.SumQuantities(new[] { 1.005m, 2.005m, 3.005m });

        Assert.AreEqual(6.03m, total);
    }

    [TestMethod]
    public void CalculateAmountOverBase_DelegaEnMultiply()
    {
        Assert.AreEqual(8730.28m, Motor2().CalculateAmountOverBase(652m, 13.3875m));
    }

    [TestMethod]
    public void CalculateUnitPrice_ConPorcentajesNulos_LanzaArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => Motor2().CalculateUnitPrice(1000m, null));
    }
}

[TestClass]
public class DirectCostLineUnitTests
{
    [TestMethod]
    public void Constructor_ConservaTodosLosCamposDelMapeoDeConcepto()
    {
        var linea = new DirectCostLine(
            Quantity: 10m,
            UnitDirectCost: 60.005m,
            IsGrouping: false,
            HasMatrix: true);

        Assert.AreEqual(10m, linea.Quantity);
        Assert.AreEqual(60.005m, linea.UnitDirectCost);
        Assert.IsFalse(linea.IsGrouping);
        Assert.IsTrue(linea.HasMatrix);
    }

    [TestMethod]
    public void SumDirectCost_ConLineasNulasOVacias_DevuelveCero()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);

        Assert.AreEqual(0m, motor.SumDirectCost(null));
        Assert.AreEqual(0m, motor.SumDirectCost(Array.Empty<DirectCostLine>()));
    }

    [TestMethod]
    public void SumDirectCost_OmitirAgrupadoresYLineasSinMatriz()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        var lineas = new[]
        {
            new DirectCostLine(10m, 60.005m, IsGrouping: false, HasMatrix: true),   // 600.10
            new DirectCostLine(2m, 100.25m, IsGrouping: false, HasMatrix: true),    // 200.50
            new DirectCostLine(999m, 1m, IsGrouping: true, HasMatrix: true),        // se omite
            new DirectCostLine(999m, 1m, IsGrouping: false, HasMatrix: false),      // se omite
        };

        Assert.AreEqual(800.60m, motor.SumDirectCost(lineas));
    }

    [TestMethod]
    public void SumDirectCost_ConCantidadesNegativas_RespetaElMapeoExactoDeHasMatrix()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        var lineas = new[]
        {
            new DirectCostLine(-1m, 5m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(1m, 5m, IsGrouping: false, HasMatrix: false), // se omite (MatrizId == null)
        };

        Assert.AreEqual(-5.00m, motor.SumDirectCost(lineas));
    }
}

[TestClass]
public class AmountDistributorUnitTests
{
    private static SoproCalculationEngine Motor()
        => new(new CalculationPrecision(2, 2, 4));

    [TestMethod]
    public void DistributeAmount_ConPesosNulosOVacios_DevuelveVacio()
    {
        Assert.AreEqual(0, Motor().DistributeAmount(100m, null).Count);
        Assert.AreEqual(0, Motor().DistributeAmount(100m, Array.Empty<decimal>()).Count);
    }

    [TestMethod]
    public void DistributeAmount_ConSumaDePesosCero_DevuelveCeros()
    {
        var resultado = Motor().DistributeAmount(1000m, new[] { 0m, 0m, 0m });

        CollectionAssert.AreEqual(new[] { 0m, 0m, 0m }, resultado.ToArray());
    }

    [TestMethod]
    public void DistributeAmount_ConPesosIguales_AjustaResiduoEnUltimoPeriodo()
    {
        // 100.005 con pesos [1,1,1] → [33.33, 33.33, 33.35], suma 100.01 (fila 1)
        var resultado = Motor().DistributeAmount(100.005m, new[] { 1m, 1m, 1m });

        CollectionAssert.AreEqual(new[] { 33.33m, 33.33m, 33.35m }, resultado.ToArray());
        Assert.AreEqual(100.01m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeAmount_ElUltimoPeriodoAbsorbeElResiduo()
    {
        var resultado = Motor().DistributeAmount(1000m, new[] { 33m, 33m, 34m });

        CollectionAssert.AreEqual(new[] { 330.00m, 330.00m, 340.00m }, resultado.ToArray());
    }

    [TestMethod]
    public void DistributeAmount_ConResiduoNegativo_UltimoElementoQuedaNegativo()
    {
        // 0.02 × 3/12 = 0.005 → redondea a 0.01 en los tres primeros; el último
        // absorbe el exceso: 0.02 − 0.03 = −0.01 (N0 fila 5).
        var resultado = Motor().DistributeAmount(0.02m, new[] { 3m, 3m, 3m, 3m });

        CollectionAssert.AreEqual(new[] { 0.01m, 0.01m, 0.01m, -0.01m }, resultado.ToArray());
        Assert.AreEqual(0.02m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeAmount_ConPesosNegativos_NoLanzaYAjustaResiduo()
    {
        // N0 fila 10: sin validacion de pesos (caso legacy [-10, 110] con suma distinta de cero)
        var resultado = Motor().DistributeAmount(100m, new[] { -10m, 110m });

        CollectionAssert.AreEqual(new[] { -10.00m, 110.00m }, resultado.ToArray());
        Assert.AreEqual(100m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeAmount_ConUnSoloPeso_DevuelveElTotal()
    {
        var resultado = Motor().DistributeAmount(777.77m, new[] { 42m });

        CollectionAssert.AreEqual(new[] { 777.77m }, resultado.ToArray());
    }

    [TestMethod]
    public void DistributeAmount_DevuelveUnaColeccionInmutable()
    {
        var resultado = Motor().DistributeAmount(1000m, new[] { 33m, 33m, 34m });

        Assert.IsInstanceOfType<ReadOnlyCollection<decimal>>(resultado);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<decimal>)resultado).Add(1m));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<decimal>)resultado).RemoveAt(0));
    }

    [TestMethod]
    public void DistributeQuantity_UsaDecimalesDeCantidad()
    {
        var motor = new SoproCalculationEngine(3, 2, 4);
        var resultado = motor.DistributeQuantity(10m, new[] { 1m, 1m, 1m });

        CollectionAssert.AreEqual(new[] { 3.333m, 3.333m, 3.334m }, resultado.ToArray());
        Assert.AreEqual(10m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeQuantity_ConPesosNulosOVacios_DevuelveVacio()
    {
        Assert.AreEqual(0, Motor().DistributeQuantity(10m, null).Count);
        Assert.AreEqual(0, Motor().DistributeQuantity(10m, Array.Empty<decimal>()).Count);
    }

    [TestMethod]
    public void DistributeQuantity_DevuelveUnaColeccionInmutable()
    {
        var resultado = Motor().DistributeQuantity(10m, new[] { 1m, 1m });

        Assert.IsInstanceOfType<ReadOnlyCollection<decimal>>(resultado);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<decimal>)resultado)[0] = 5m);
    }
}

[TestClass]
public class PriceBreakdownUnitTests
{
    [TestMethod]
    public void CentralIndirectCosts_ProrrateaConSeisDecimales_SobreElTotalDeIndirectos()
    {
        var desglose = new PriceBreakdown(
            DirectCost: 1000m,
            IndirectCosts: 100m,
            Financing: 0m,
            Profit: 0m,
            AdditionalCharges: 0m,
            UnitPrice: 1100m,
            CentralIndirectsPercentage: 5m,
            FieldIndirectsPercentage: 5m);

        Assert.AreEqual(50m, desglose.CentralIndirectCosts);
        Assert.AreEqual(50m, desglose.FieldIndirectCosts);
    }

    [TestMethod]
    public void CentralIndirectCosts_ConPorcentajesSumadosCero_DevuelveCero()
    {
        var desglose = new PriceBreakdown(
            DirectCost: 1000m,
            IndirectCosts: 100m,
            Financing: 0m,
            Profit: 0m,
            AdditionalCharges: 0m,
            UnitPrice: 1100m,
            CentralIndirectsPercentage: 0m,
            FieldIndirectsPercentage: 0m);

        Assert.AreEqual(0m, desglose.CentralIndirectCosts);
        Assert.AreEqual(100m, desglose.FieldIndirectCosts);
    }

    [TestMethod]
    public void CentralIndirectCosts_ProrrateoConSeisDecimalesYResiduoEnCampo()
    {
        // 100 × 2/3 = 66.666667 con 6 decimales AwayFromZero; campo absorbe el residuo
        var noExacto = new PriceBreakdown(
            DirectCost: 0m,
            IndirectCosts: 100m,
            Financing: 0m,
            Profit: 0m,
            AdditionalCharges: 0m,
            UnitPrice: 0m,
            CentralIndirectsPercentage: 2m,
            FieldIndirectsPercentage: 1m);

        Assert.AreEqual(66.666667m, noExacto.CentralIndirectCosts);
        Assert.AreEqual(33.333333m, noExacto.FieldIndirectCosts);
        Assert.AreEqual(100m, noExacto.CentralIndirectCosts + noExacto.FieldIndirectCosts);
    }

    [TestMethod]
    public void Subtotales_SeConstruyenEnCascada()
    {
        var desglose = new PriceBreakdown(1000m, 100m, 60m, 80m, 30m, 1270m);

        Assert.AreEqual(1100m, desglose.Subtotal1);
        Assert.AreEqual(1160m, desglose.Subtotal2);
        Assert.AreEqual(1240m, desglose.Subtotal3);
        Assert.AreEqual(1270m, desglose.UnitPrice);
    }
}
