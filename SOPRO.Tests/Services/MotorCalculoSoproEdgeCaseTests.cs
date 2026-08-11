using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services;

[TestClass]
public class MotorCalculoSoproEdgeCaseTests
{
    [TestMethod]
    public void RedondearCantidad_ConPrecisionCero_RedondeaAEntero()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 0, decimalesImporte: 2, decimalesPorcentaje: 4);

        Assert.AreEqual(1m, motor.RedondearCantidad(0.5m));
        Assert.AreEqual(1m, motor.RedondearCantidad(1.4m));
        Assert.AreEqual(-1m, motor.RedondearCantidad(-0.5m));
    }

    [TestMethod]
    public void RedondearImporte_ConPrecisionUno_RedondeaADecimo()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 1, decimalesPorcentaje: 4);

        Assert.AreEqual(1.3m, motor.RedondearImporte(1.25m));
        Assert.AreEqual(1.2m, motor.RedondearImporte(1.24m));
    }

    [TestMethod]
    public void RedondearPorcentaje_ConPrecisionCuatro_RedondeaADiezmilesimo()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        Assert.AreEqual(12.3457m, motor.RedondearPorcentaje(12.34565m));
        Assert.AreEqual(12.3456m, motor.RedondearPorcentaje(12.34564m));
    }

    [TestMethod]
    public void RedondearImporte_ConPrecisionVeintiocho_ConservaElValorCompleto()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 28, decimalesPorcentaje: 4);

        Assert.AreEqual(123.456789m, motor.RedondearImporte(123.456789m));
    }

    [TestMethod]
    public void RedondearImporte_ConPrecisionMayorA28_LanzaArgumentOutOfRangeException()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 29, decimalesPorcentaje: 4);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => motor.RedondearImporte(1.5m));
    }

    [TestMethod]
    public void Multiplicar_ConOverflowDecimal_LanzaOverflowException()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        Assert.ThrowsException<OverflowException>(() => motor.Multiplicar(decimal.MaxValue, 10m));
    }

    [TestMethod]
    public void DistribuirImporte_ConPesosNegativos_NoLanzaYAjustaResiduo()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.DistribuirImporte(100m, new List<decimal> { -10m, 110m });

        Assert.AreEqual(-10.00m, resultado[0]);
        Assert.AreEqual(110.00m, resultado[1]);
        Assert.AreEqual(100.00m, resultado.Sum());
    }

    [TestMethod]
    public void DistribuirImporte_ConPesosMezclados_AjustaResiduoEnUltimoPeriodo()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.DistribuirImporte(100m, new List<decimal> { 1m, 2m, 3m });

        Assert.AreEqual(16.67m, resultado[0]);
        Assert.AreEqual(33.33m, resultado[1]);
        Assert.AreEqual(50.00m, resultado[2]);
        Assert.AreEqual(100.00m, resultado.Sum());
    }

    [TestMethod]
    public void DistribuirImporte_ConResiduoNegativo_UltimoElementoPuedeQuedarNegativo()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.DistribuirImporte(1m, new List<decimal> { 2m, -1m });

        Assert.AreEqual(2.00m, resultado[0]);
        Assert.AreEqual(-1.00m, resultado[1], "El último elemento absorbe el residuo y puede quedar negativo.");
        Assert.AreEqual(1.00m, resultado.Sum());
    }

    [TestMethod]
    public void CalcularPrecioUnitario_ConModoNulo_CaeEnAcumulables()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 10m,
            Financiamiento = 2m,
            Utilidad = 8m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = null!
        };

        var desglose = motor.CalcularPrecioUnitario(1000m, porcentajes);

        Assert.AreEqual(1279.51m, desglose.PrecioUnitario);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_ConModoDesconocido_CaeEnAcumulables()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 10m,
            Financiamiento = 2m,
            Utilidad = 8m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "SobreCosto"
        };

        var desglose = motor.CalcularPrecioUnitario(1000m, porcentajes);

        Assert.AreEqual(1279.51m, desglose.PrecioUnitario);
    }

    [TestMethod]
    public void CalcularPrecioUnitario_IndirectosCentralYCampo_SeSumanAntesDelRedondeo()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var porcentajes = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 5m,
            Financiamiento = 0m,
            Utilidad = 0m,
            CargosAdicionales = 0m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var desglose = motor.CalcularPrecioUnitario(0.05m, porcentajes);

        // Legacy: 5% + 5% se suman antes del redondeo monetario.
        // round(0.05 * 10/100, 2) = round(0.005) = 0.01;
        // en cambio redondear por separado daría 0.00 + 0.00.
        Assert.AreEqual(0.01m, desglose.Indirectos);
    }

    [TestMethod]
    public void FormatCantidad_ConsultaCurrentCultureEnCadaLlamada()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Culturas con separadores REALMENTE distintos: si la cultura quedara cacheada
            // en la primera llamada, la segunda no cambiaría de formato y el test fallaría.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-MX");
            var esMx = motor.FormatCantidad(1234.5m);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var deDe = motor.FormatCantidad(1234.5m);

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var inv = motor.FormatCantidad(1234.5m);

            Assert.AreEqual("1,234.50", esMx);
            Assert.AreEqual("1.234,50", deDe, "de-DE usa coma decimal y punto de millar; si el formato es igual a es-MX la cultura está cacheada.");
            Assert.AreEqual("1,234.50", inv);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void DesglosePrecios_IgualdadYDeconstruccion_SeComportanComoRecord()
    {
        var a = new DesglosePrecios(100m, 15m, 2m, 3m, 1m, 121m, 5m, 10m);
        var b = new DesglosePrecios(100m, 15m, 2m, 3m, 1m, 121m, 5m, 10m);
        var c = a with { Utilidad = 4m };

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);

        var (cd, ind, fin, util, cargos, pu, pctCentral, pctCampo) = a;
        Assert.AreEqual(100m, cd);
        Assert.AreEqual(15m, ind);
        Assert.AreEqual(2m, fin);
        Assert.AreEqual(3m, util);
        Assert.AreEqual(1m, cargos);
        Assert.AreEqual(121m, pu);
        Assert.AreEqual(5m, pctCentral);
        Assert.AreEqual(10m, pctCampo);
    }
}
