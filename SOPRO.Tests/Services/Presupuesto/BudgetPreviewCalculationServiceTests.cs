using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class BudgetPreviewCalculationServiceTests
{
    [TestMethod]
    public void BuildPreviewFromReferenceCost_Acumulables_DebeAplicarPorcentajesEnCascada()
    {
        var proyecto = CrearProyecto();
        var input = new BudgetPercentageInput
        {
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 10m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var result = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(1000m, input, proyecto);

        Assert.AreEqual(1000.00m, result.CostoDirecto);
        Assert.AreEqual(100.00m, result.MontoIndirectosCentral);
        Assert.AreEqual(50.00m, result.MontoIndirectosCampo);
        Assert.AreEqual(1150.00m, result.Subtotal1);
        Assert.AreEqual(23.00m, result.MontoFinanciamiento);
        Assert.AreEqual(1173.00m, result.Subtotal2);
        Assert.AreEqual(117.30m, result.MontoUtilidad);
        Assert.AreEqual(1290.30m, result.Subtotal3);
        Assert.AreEqual(12.90m, result.MontoCargosAdicionales);
        Assert.AreEqual(1303.20m, result.PrecioUnitarioFinal);
    }

    [TestMethod]
    public void BuildPreviewFromReferenceCost_SobreCD_DebeAplicarTodosLosPorcentajesSobreCostoDirecto()
    {
        var proyecto = CrearProyecto();
        var input = new BudgetPercentageInput
        {
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 10m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "SobreCD"
        };

        var result = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(1000m, input, proyecto);

        Assert.AreEqual(1000.00m, result.CostoDirecto);
        Assert.AreEqual(100.00m, result.MontoIndirectosCentral);
        Assert.AreEqual(50.00m, result.MontoIndirectosCampo);
        Assert.AreEqual(20.00m, result.MontoFinanciamiento);
        Assert.AreEqual(100.00m, result.MontoUtilidad);
        Assert.AreEqual(10.00m, result.MontoCargosAdicionales);
        Assert.AreEqual(1150.00m, result.Subtotal1);
        Assert.AreEqual(1170.00m, result.Subtotal2);
        Assert.AreEqual(1270.00m, result.Subtotal3);
        Assert.AreEqual(1280.00m, result.PrecioUnitarioFinal);
    }

    [TestMethod]
    public void BuildPreview_ConConceptos_DebeAcumularImportesConMotorDeCalculoDeSopro()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var input = new BudgetPercentageInput
        {
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 10m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var result = BudgetPreviewCalculationService.BuildPreview(context, scenario.Proyecto, input);

        Assert.IsTrue(result.CalculadoDesdeConceptos);
        Assert.AreEqual(1, result.ConceptosProcesados);
        Assert.AreEqual(1000.00m, result.CostoDirecto);
        Assert.AreEqual(1303.20m, result.PrecioUnitarioFinal);
    }

    private static Proyecto CrearProyecto() => new()
    {
        Nombre = "Proyecto preview pruebas",
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
