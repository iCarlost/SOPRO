using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class UtilidadCalculationServiceTests
{
    [TestMethod]
    public void Calcular_ModoDirecto_DebeCalcularUtilidadBrutaImpuestosYUtilidadNeta()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto();
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var service = new UtilidadCalculationService();
        var result = service.Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadDirecta = 10m,
            Isr = 30m,
            Ptu = 10m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = false
        });

        Assert.AreEqual("Directo", result.Modo);
        Assert.AreEqual(1173.00m, result.BaseUtilidad);
        Assert.AreEqual(10.00000m, result.PorcentajeUtilidadBruta);
        Assert.AreEqual(6.00000m, result.PorcentajeUtilidadNeta);
        Assert.AreEqual(117.30m, result.ImporteUtilidad);
        Assert.AreEqual(35.19m, result.ImporteIsr);
        Assert.AreEqual(11.73m, result.ImportePtu);
        Assert.AreEqual(70.38m, result.UtilidadNetaEstimada);
    }

    [TestMethod]
    public void Calcular_ModoAsistido_DebeConvertirUtilidadNetaDeseadaAUtilidadBruta()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto();
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var service = new UtilidadCalculationService();
        var result = service.Calcular(context, proyecto, new UtilidadCalculationInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            UtilidadNetaDeseada = 6m,
            Isr = 30m,
            Ptu = 10m,
            ModoCalculoPorcentajes = "Acumulables",
            ModoAsistido = true
        });

        Assert.AreEqual("Asistido", result.Modo);
        Assert.AreEqual(1173.00m, result.BaseUtilidad);
        Assert.AreEqual(10.00000m, result.PorcentajeUtilidadBruta);
        Assert.AreEqual(6.00000m, result.PorcentajeUtilidadNeta);
        Assert.AreEqual(117.30m, result.ImporteUtilidad);
        Assert.AreEqual(70.38m, result.UtilidadNetaEstimada);
    }

    private static Proyecto CrearProyecto() => new()
    {
        Nombre = "Proyecto utilidad pruebas",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4
    };
}
