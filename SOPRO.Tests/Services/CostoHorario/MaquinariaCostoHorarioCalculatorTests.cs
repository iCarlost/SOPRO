using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Services;

namespace SOPRO.Tests.Services.CostoHorario;

[TestClass]
public class MaquinariaCostoHorarioCalculatorTests
{
    [TestMethod]
    public void Calcular_DevuelveTodosLosComponentesDelGolden()
    {
        var input = new MaquinariaCostoHorarioInput
        {
            ValorAdquisicion = 800000m,
            ValorLlantas = 50000m,
            ValorPiezasEspeciales = 60000m,
            FactorRescate = 0.10m,
            VidaEconomica = 12000m,
            TasaInteres = 21.24m,
            HorasEfectivasAnio = 1600m,
            PrimaSeguro = 3.00m,
            FactorMantenimiento = 0.20m,
            CantidadCombustible = 10m,
            PrecioCombustible = 20m,
            CantidadAceite = 1m,
            PrecioAceite = 40m,
            VidaEconomicaLlantas = 3000m,
            VidaPiezasEspeciales = 5000m,
            SalarioOperador = 300m,
            FactorSalarioReal = 1.60m,
            HorasEfectivasTurno = 8m
        };

        var result = MaquinariaCostoHorarioCalculator.Calcular(input);

        Assert.AreEqual(690000m, result.ValorNeto);
        Assert.AreEqual(69000m, result.ValorRescate);
        Assert.AreEqual(379500m, result.ValorNetoMedio);
        Assert.AreEqual(51.75m, result.Depreciacion);
        Assert.AreEqual(50.378625m, result.Inversion);
        Assert.AreEqual(7.115625m, result.Seguros);
        Assert.AreEqual(10.35m, result.Mantenimiento);
        Assert.AreEqual(119.59425m, result.TotalCargosFijos);
        Assert.AreEqual(200m, result.Combustible);
        Assert.AreEqual(40m, result.Lubricantes);
        Assert.AreEqual(16.666666666666666666666666667m, result.Llantas);
        Assert.AreEqual(12m, result.PiezasEspeciales);
        Assert.AreEqual(268.66666666666666666666666667m, result.TotalConsumos);
        Assert.AreEqual(480m, result.SalarioReal);
        Assert.AreEqual(60m, result.Operacion);
        Assert.AreEqual(448.26091666666666666666666667m, result.CostoHorario);
    }
}
