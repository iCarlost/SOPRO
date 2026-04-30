using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Precision;

[TestClass]
public class PrecisionPantallaEdgeCaseTests
{
    [TestMethod]
    public void Multiplicar_ConImportesA2Decimales_DebeUsarPrecioVisibleAntesDeMultiplicar()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 4, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.Multiplicar(3m, 10.005m);

        Assert.AreEqual(30.03m, resultado, "Debe calcular 3 x 10.01, no 3 x 10.005.");
    }

    [TestMethod]
    public void Multiplicar_ConImportesA4Decimales_DebeConservarMayorPrecisionVisible()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 4, decimalesImporte: 4, decimalesPorcentaje: 4);

        var resultado = motor.Multiplicar(3m, 10.00504m);

        Assert.AreEqual(30.0150m, resultado, "Con 4 decimales visibles el P.U. queda en 10.0050.");
    }

    [TestMethod]
    public void DistribuirCantidad_DebeAjustarResiduoConDecimalesDeCantidad()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.DistribuirCantidad(1m, new List<decimal> { 1m, 1m, 1m });

        Assert.AreEqual(0.33m, resultado[0]);
        Assert.AreEqual(0.33m, resultado[1]);
        Assert.AreEqual(0.34m, resultado[2], "El último periodo debe absorber el residuo de cantidad.");
        Assert.AreEqual(1.00m, resultado.Sum());
    }

    [TestMethod]
    public void SumarImportes_DebeRedondearDespuesDeAcumularImportesYaVisibles()
    {
        var motor = new MotorCalculoSopro(decimalesCantidad: 4, decimalesImporte: 2, decimalesPorcentaje: 4);

        var resultado = motor.SumarImportes(new[] { 10.005m, 20.005m, 30.005m });

        Assert.AreEqual(60.03m, resultado, "Cada importe se vuelve visible antes de formar el total.");
    }
}
