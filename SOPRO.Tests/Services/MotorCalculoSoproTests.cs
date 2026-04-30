using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services;

[TestClass]
public class MotorCalculoSoproTests
{
    [TestMethod]
    public void Multiplicar_DebeUsarPrecioUnitarioVisibleAntesDeMultiplicar()
    {
        // Arrange
        // Regla SOPRO: el P.U. se redondea primero a la precisión visible.
        // 13.3875 visible a 2 decimales = 13.39; 652 * 13.39 = 8,730.28.
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        // Act
        var resultado = motor.Multiplicar(652m, 13.3875m);

        // Assert
        Assert.AreEqual(8730.28m, resultado);
    }

    [TestMethod]
    public void RedondearImporte_DebeUsarAwayFromZero()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.RedondearImporte(10.005m);

        Assert.AreEqual(10.01m, resultado);
    }

    [TestMethod]
    public void DistribuirImporte_DebeAjustarResiduoEnUltimoPeriodo()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.DistribuirImporte(1000m, new List<decimal> { 33m, 33m, 34m });

        Assert.AreEqual(330.00m, resultado[0]);
        Assert.AreEqual(330.00m, resultado[1]);
        Assert.AreEqual(340.00m, resultado[2]);
        Assert.AreEqual(1000.00m, resultado.Sum());
    }

    [TestMethod]
    public void DistribuirCantidad_DebeRespetarPrecisionDeCantidadYAjustarResiduo()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 3,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var resultado = motor.DistribuirCantidad(1m, new List<decimal> { 1m, 1m, 1m });

        Assert.AreEqual(0.333m, resultado[0]);
        Assert.AreEqual(0.333m, resultado[1]);
        Assert.AreEqual(0.334m, resultado[2]);
        Assert.AreEqual(1.000m, resultado.Sum());
    }

    [TestMethod]
    public void SumarCostoDirecto_DebeIgnorarAgrupadoresYConceptosSinMatriz()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var conceptos = new List<ConceptoPresupuesto>
        {
            new()
            {
                EsAgrupador = true,
                Cantidad = 999m,
                CostoDirectoUnitario = 999m,
                MatrizId = 1
            },
            new()
            {
                EsAgrupador = false,
                Cantidad = 10m,
                CostoDirectoUnitario = 100.005m,
                MatrizId = 2
            },
            new()
            {
                EsAgrupador = false,
                Cantidad = 5m,
                CostoDirectoUnitario = 250m,
                MatrizId = null
            }
        };

        var total = motor.SumarCostoDirecto(conceptos);

        // Solo entra el concepto hoja con MatrizId.
        // P.U. visible: 100.01; 10 * 100.01 = 1,000.10.
        Assert.AreEqual(1000.10m, total);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_DebeCuadrarSumaDePartesConPrecioFinal()
    {
        var motor = new MotorCalculoSopro(
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 10m,
            Financiamiento = 2m,
            Utilidad = 8m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var desglose = motor.CalcularPrecioUnitario(1000m, porcentajes);

        var sumaPartes = desglose.CostoDirecto
            + desglose.Indirectos
            + desglose.Financiamiento
            + desglose.Utilidad
            + desglose.CargosAdicionales;

        Assert.AreEqual(desglose.PrecioUnitario, sumaPartes);
        Assert.AreEqual(1000.00m, desglose.CostoDirecto);
        Assert.AreEqual(150.00m, desglose.Indirectos);
        Assert.AreEqual(23.00m, desglose.Financiamiento);
        Assert.AreEqual(93.84m, desglose.Utilidad);
        Assert.AreEqual(12.67m, desglose.CargosAdicionales);
        Assert.AreEqual(1279.51m, desglose.PrecioUnitario);
    }
}
