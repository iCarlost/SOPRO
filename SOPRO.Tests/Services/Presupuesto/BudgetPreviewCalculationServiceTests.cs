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

    [TestMethod]
    public void BuildPreview_SinConceptos_DebeDelegarAlPreviewDeReferenciaConProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyectoEnContexto(context);

        var input = new BudgetPercentageInput
        {
            CostoDirectoReferencia = 1000m,
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 10m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var result = BudgetPreviewCalculationService.BuildPreview(context, proyecto, input);

        Assert.IsFalse(result.CalculadoDesdeConceptos);
        Assert.AreEqual(0, result.ConceptosProcesados);
        Assert.AreEqual(1303.20m, result.PrecioUnitarioFinal);
        Assert.AreEqual(1303.20m,
            BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(1000m, input, proyecto).PrecioUnitarioFinal);
    }

    [TestMethod]
    public void BuildPreviewFromReferenceCost_SinProyecto_DebeConservarAltaPrecisionSinRedondeo()
    {
        var input = new BudgetPercentageInput
        {
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 10m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var result = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(10.005m, input);

        Assert.AreEqual(10.005m, result.CostoDirecto);
        Assert.AreEqual(1.0005m, result.MontoIndirectosCentral);
        Assert.AreEqual(0.50025m, result.MontoIndirectosCampo);
        Assert.AreEqual(13.038546015m, result.PrecioUnitarioFinal);
    }

    [TestMethod]
    public void BuildPreviewFromReferenceCost_ConProyecto_DebeRedondearCadaPasoConElMotor()
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

        var result = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(10.005m, input, proyecto);

        Assert.AreEqual(10.01m, result.CostoDirecto);
        Assert.AreEqual(1.00m, result.MontoIndirectosCentral);
        Assert.AreEqual(0.50m, result.MontoIndirectosCampo);
        Assert.AreEqual(11.51m, result.Subtotal1);
        Assert.AreEqual(0.23m, result.MontoFinanciamiento);
        Assert.AreEqual(1.17m, result.MontoUtilidad);
        Assert.AreEqual(0.13m, result.MontoCargosAdicionales);
        Assert.AreEqual(13.04m, result.PrecioUnitarioFinal);
    }

    [TestMethod]
    public void BuildPreview_CasoDivergenteCdMinimo_DebeEvidenciarLaDivergenciaConLaReferencia()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // CD unitario mínimo 0.05 con indirectos 5% + 5%: el caso verdaderamente divergente.
        scenario.Concepto.CostoDirectoUnitario = 0.05m;
        scenario.Concepto.Cantidad = 1m;
        scenario.Concepto.CostoDirectoTotal = 0.05m;
        context.SaveChanges();

        var input = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 5m,
            Financiamiento = 0m,
            Utilidad = 0m,
            CargosAdicionales = 0m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var conConceptos = BudgetPreviewCalculationService.BuildPreview(context, scenario.Proyecto, input);
        var referencia = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(0.05m, input, scenario.Proyecto);

        Assert.IsTrue(conConceptos.CalculadoDesdeConceptos);
        Assert.AreEqual(0.05m, conConceptos.CostoDirecto);

        // Ruta "con conceptos" (vía CalcularPrecioUnitario): central+campo se SUMAN antes de
        // redondear → mInd = R(0.05×10/100) = R(0.005) = 0.01; cada componente se prorratea a 0.005
        // y luego Multiplicar redondea el P.U. a 0.01 → total indirectos 0.02.
        Assert.AreEqual(0.02m, conConceptos.MontoIndirectosCentral + conConceptos.MontoIndirectosCampo);
        Assert.AreEqual(0.06m, conConceptos.PrecioUnitarioFinal);

        // Ruta de referencia: indirectos se redondean POR SEPARADO → R(0.05×5/100)=R(0.0025)=0.00.
        Assert.AreEqual(0.00m, referencia.MontoIndirectosCentral);
        Assert.AreEqual(0.00m, referencia.MontoIndirectosCampo);
        Assert.AreEqual(0.05m, referencia.PrecioUnitarioFinal);

        Assert.AreNotEqual(conConceptos.PrecioUnitarioFinal, referencia.PrecioUnitarioFinal,
            "Con CD=0.05 e indirectos 5%+5% ambas rutas deben divergir (fila 16 de la tabla de divergencias).");
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

    private static Proyecto CrearProyectoEnContexto(SOPRO.Data.Context.SOPROContext context)
    {
        var proyecto = CrearProyecto();
        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }
}
