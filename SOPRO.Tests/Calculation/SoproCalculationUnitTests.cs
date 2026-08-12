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

        Assert.AreEqual(0, precision.DecimalesCantidad);
        Assert.AreEqual(0, precision.DecimalesImporte);
        Assert.AreEqual(0, precision.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Constructor_ConservaValoresPositivos()
    {
        var precision = new CalculationPrecision(4, 2, 4);

        Assert.AreEqual(4, precision.DecimalesCantidad);
        Assert.AreEqual(2, precision.DecimalesImporte);
        Assert.AreEqual(4, precision.DecimalesPorcentaje);
    }
}

[TestClass]
public class PercentageCalculationModesTests
{
    [DataTestMethod]
    [DataRow("SobreCD")]
    [DataRow("SOBRECD")]
    [DataRow("sobrecd")]
    [DataRow("sObReCd")]
    public void Parse_ConSobreCDEnCualquierCasing_DevuelveSobreCD(string modo)
    {
        Assert.AreEqual(PercentageCalculationMode.SobreCD, PercentageCalculationModes.Parse(modo));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Acumulables")]
    [DataRow("acumulables")]
    [DataRow("Acumulado")]
    [DataRow("X")]
    public void Parse_ConCualquierOtroValor_DevuelveAcumulables(string modo)
    {
        Assert.AreEqual(PercentageCalculationMode.Acumulables, PercentageCalculationModes.Parse(modo));
    }
}

[TestClass]
public class SoproCalculationEngineUnitTests
{
    private static SoproCalculationEngine Motor2()
        => new(new CalculationPrecision(2, 2, 4));

    [TestMethod]
    public void Multiplicar_RedondeaPrecioUnitarioVisible_AntesDeMultiplicar()
    {
        // 652 × 13.3875 → 652 × 13.39 = 8,730.28 (no 8,728.65 con decimales ocultos)
        Assert.AreEqual(8730.28m, Motor2().Multiplicar(652m, 13.3875m));
    }

    [TestMethod]
    public void Precisiones_SeExponenDespuesDeLaNormalizacion()
    {
        var motor = new SoproCalculationEngine(-1, 3, 0);

        Assert.AreEqual(0, motor.DecimalesCantidad);
        Assert.AreEqual(3, motor.DecimalesImporte);
        Assert.AreEqual(0, motor.DecimalesPorcentaje);
        Assert.AreEqual(0, motor.Precision.DecimalesCantidad);
        Assert.AreEqual(3, motor.Precision.DecimalesImporte);
        Assert.AreEqual(0, motor.Precision.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Multiplicar_NoRedondeaLaCantidadPreviamente()
    {
        // Fila 2: la cantidad 1.234 se usa completa (1.234 × 10 = 12.34);
        // redondearla antes daría 1.23 × 10 = 12.30.
        var motor = new SoproCalculationEngine(2, 2, 4);
        Assert.AreEqual(12.34m, motor.Multiplicar(1.234m, 10m));
    }

    [TestMethod]
    public void RedondearPorcentaje_UsaDecimalesDePorcentaje()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        Assert.AreEqual(33.3333m, motor.RedondearPorcentaje(33.333349m));
        Assert.AreEqual(33.3334m, motor.RedondearPorcentaje(33.33335m));

        var uno = new SoproCalculationEngine(2, 2, 1);
        Assert.AreEqual(33.3m, uno.RedondearPorcentaje(33.333333m));
        Assert.AreEqual(33.4m, uno.RedondearPorcentaje(33.35m));

        var cero = new SoproCalculationEngine(2, 2, 0);
        Assert.AreEqual(33m, cero.RedondearPorcentaje(33.33335m));
        Assert.AreEqual(34m, cero.RedondearPorcentaje(33.5m));
    }

    [TestMethod]
    public void SumarImportes_ConColeccionNula_DevuelveCero()
    {
        Assert.AreEqual(0m, Motor2().SumarImportes(null));
    }

    [TestMethod]
    public void SumarCantidades_ConColeccionNula_DevuelveCero()
    {
        Assert.AreEqual(0m, Motor2().SumarCantidades(null));
    }

    [TestMethod]
    public void SumarImportes_RedondeaCadaElementoAntesDeAcumular()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        decimal total = motor.SumarImportes(new[] { 1.005m, 2.005m, 3.005m });

        // Cada elemento se redondea a 2 decimales (1.01 + 2.01 + 3.01 = 6.03),
        // no se acumulan los valores crudos (6.015 → 6.02).
        Assert.AreEqual(6.03m, total);
    }

    [TestMethod]
    public void SumarCantidades_RedondeaCadaElementoAntesDeAcumular()
    {
        var motor = new SoproCalculationEngine(2, 2, 4);
        decimal total = motor.SumarCantidades(new[] { 1.005m, 2.005m, 3.005m });

        Assert.AreEqual(6.03m, total);
    }

    [TestMethod]
    public void CalcularImporteSobreBase_DelegaEnMultiplicar()
    {
        Assert.AreEqual(8730.28m, Motor2().CalcularImporteSobreBase(652m, 13.3875m));
    }
}

[TestClass]
public class DirectCostLineUnitTests
{
    [TestMethod]
    public void Constructor_ConservaTodosLosCamposDelMapeoDeConcepto()
    {
        var porcentajes = new PricePercentageInput { Utilidad = 8m };
        var linea = new DirectCostLine(CostoDirecto: 1000m, Porcentajes: porcentajes, HasMatrix: true);

        Assert.AreEqual(1000m, linea.CostoDirecto);
        Assert.AreSame(porcentajes, linea.Porcentajes);
        Assert.IsTrue(linea.HasMatrix);
    }
}

[TestClass]
public class AmountDistributorUnitTests
{
    private static SoproCalculationEngine Motor()
        => new(new CalculationPrecision(2, 2, 4));

    [TestMethod]
    public void DistributeImporte_ConPesosNulosOVacios_DevuelveVacio()
    {
        Assert.AreEqual(0, Motor().DistribuirImporte(100m, null).Count);
        Assert.AreEqual(0, Motor().DistribuirImporte(100m, Array.Empty<decimal>()).Count);
    }

    [TestMethod]
    public void DistributeImporte_ConSumaDePesosCero_DevuelveCeros()
    {
        var resultado = Motor().DistribuirImporte(1000m, new[] { 0m, 0m, 0m });

        CollectionAssert.AreEqual(new[] { 0m, 0m, 0m }, resultado.ToArray());
    }

    [TestMethod]
    public void DistributeImporte_ConPesosIguales_AjustaResiduoEnUltimoPeriodo()
    {
        // 100.005 con pesos [1,1,1] → [33.33, 33.33, 33.35], suma 100.01 (fila 1)
        var resultado = Motor().DistribuirImporte(100.005m, new[] { 1m, 1m, 1m });

        CollectionAssert.AreEqual(new[] { 33.33m, 33.33m, 33.35m }, resultado.ToArray());
        Assert.AreEqual(100.01m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeImporte_ElUltimoPeriodoAbsorbeElResiduo()
    {
        var resultado = Motor().DistribuirImporte(1000m, new[] { 33m, 33m, 34m });

        CollectionAssert.AreEqual(new[] { 330.00m, 330.00m, 340.00m }, resultado.ToArray());
    }

    [TestMethod]
    public void DistributeImporte_ConResiduoNegativo_UltimoElementoPuedeQuedarNegativo()
    {
        // total pequeño con pesos grandes: los redondeos individuales superan el total
        var resultado = Motor().DistribuirImporte(1m, new[] { 1m, 1000000m, 1m });

        Assert.AreEqual(1m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeImporte_ConPesosNegativos_NoLanzaYAjustaResiduo()
    {
        // Fila 10: sin validacion de pesos (caso legacy [-10, 110] con suma distinta de cero)
        var resultado = Motor().DistribuirImporte(100m, new[] { -10m, 110m });

        CollectionAssert.AreEqual(new[] { -10.00m, 110.00m }, resultado.ToArray());
        Assert.AreEqual(100m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeImporte_ConUnSoloPeso_DevuelveElTotal()
    {
        var resultado = Motor().DistribuirImporte(777.77m, new[] { 42m });

        CollectionAssert.AreEqual(new[] { 777.77m }, resultado.ToArray());
    }

    [TestMethod]
    public void DistributeCantidad_UsaDecimalesDeCantidad()
    {
        var motor = new SoproCalculationEngine(3, 2, 4);
        var resultado = motor.DistribuirCantidad(10m, new[] { 1m, 1m, 1m });

        CollectionAssert.AreEqual(new[] { 3.333m, 3.333m, 3.334m }, resultado.ToArray());
        Assert.AreEqual(10m, resultado.Sum());
    }

    [TestMethod]
    public void DistributeCantidad_ConPesosNulosOVacios_DevuelveVacio()
    {
        Assert.AreEqual(0, Motor().DistribuirCantidad(10m, null).Count);
        Assert.AreEqual(0, Motor().DistribuirCantidad(10m, Array.Empty<decimal>()).Count);
    }
}

[TestClass]
public class PriceBreakdownUnitTests
{
    [TestMethod]
    public void IndirectosCentral_ProrrateaConSeisDecimales_SobreElTotalDeIndirectos()
    {
        var desglose = new PriceBreakdown(
            CostoDirecto: 1000m,
            Indirectos: 100m,
            Financiamiento: 0m,
            Utilidad: 0m,
            CargosAdicionales: 0m,
            PrecioUnitario: 1100m,
            PctIndirectosCentral: 5m,
            PctIndirectosCampo: 5m);

        Assert.AreEqual(50m, desglose.IndirectosCentral);
        Assert.AreEqual(50m, desglose.IndirectosCampo);
    }

    [TestMethod]
    public void IndirectosCentral_ConPorcentajesSumadosCero_DevuelveCero()
    {
        var desglose = new PriceBreakdown(
            CostoDirecto: 1000m,
            Indirectos: 100m,
            Financiamiento: 0m,
            Utilidad: 0m,
            CargosAdicionales: 0m,
            PrecioUnitario: 1100m,
            PctIndirectosCentral: 0m,
            PctIndirectosCampo: 0m);

        Assert.AreEqual(0m, desglose.IndirectosCentral);
        Assert.AreEqual(100m, desglose.IndirectosCampo);
    }

    [TestMethod]
    public void IndirectosCentral_ProrrateoConSeisDecimalesYResiduoEnCampo()
    {
        var desglose = new PriceBreakdown(
            CostoDirecto: 0m,
            Indirectos: 100m,
            Financiamiento: 0m,
            Utilidad: 0m,
            CargosAdicionales: 0m,
            PrecioUnitario: 0m,
            PctIndirectosCentral: 1m,
            PctIndirectosCampo: 3m);

        // 100 × 1/4 = 25 (exacto); caso no exacto: 100 × 2/3 = 66.666667
        var noExacto = new PriceBreakdown(
            CostoDirecto: 0m,
            Indirectos: 100m,
            Financiamiento: 0m,
            Utilidad: 0m,
            CargosAdicionales: 0m,
            PrecioUnitario: 0m,
            PctIndirectosCentral: 2m,
            PctIndirectosCampo: 1m);

        Assert.AreEqual(66.666667m, noExacto.IndirectosCentral);
        Assert.AreEqual(33.333333m, noExacto.IndirectosCampo);
        Assert.AreEqual(100m, noExacto.IndirectosCentral + noExacto.IndirectosCampo);
        Assert.AreEqual(25m, desglose.IndirectosCentral);
    }

    [TestMethod]
    public void Subtotales_SeConstruyenEnCascada()
    {
        var desglose = new PriceBreakdown(1000m, 100m, 60m, 80m, 30m, 1270m);

        Assert.AreEqual(1100m, desglose.Subtotal1);
        Assert.AreEqual(1160m, desglose.Subtotal2);
        Assert.AreEqual(1240m, desglose.Subtotal3);
        Assert.AreEqual(1270m, desglose.PrecioUnitario);
    }
}
