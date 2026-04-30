using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Regresion;

[TestClass]
public class SuiteValidacionOficialSoproTests
{
    [TestMethod]
    public void CasoOficial001_PresupuestoBase_DebeMantenerCostoDirectoCongelado()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        Assert.AreEqual(100.00m, scenario.MatrizApu.CostoDirecto, "El C.D. unitario oficial del caso 001 no debe cambiar.");
        Assert.AreEqual(10.00m, scenario.Concepto.Cantidad);
        Assert.AreEqual(1000.00m, scenario.Concepto.CostoDirectoTotal, "El C.D. total oficial del caso 001 no debe cambiar.");
    }

    [TestMethod]
    public void CasoOficial001_Explosion_DebeMantenerImportesPorRubroCongelados()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var result = new ExplosionInsumosService().Calculate(context, scenario.Proyecto.Id, "Todos");

        Assert.AreEqual(600.00m, result.Materiales[scenario.Cemento.Id].Cantidad, "Materiales del caso 001.");
        Assert.AreEqual(300.00m, result.ManoObra[scenario.Oficial.Id].Cantidad, "MO normal del caso 001.");
        Assert.AreEqual(30.00m, result.ManoObra[scenario.CaboPorcentajeMo.Id].Cantidad, "%MO del caso 001.");
        Assert.AreEqual(30.00m, result.Herramientas[scenario.HerramientaMenorPorcentajeMo.Id].Cantidad, "Herramienta %MO del caso 001.");
        Assert.AreEqual(40.00m, result.Maquinaria[scenario.Revolvedora.Id].Cantidad, "Maquinaria del caso 001.");
        Assert.AreEqual(1000.00m, result.CostoDirectoTotal);
    }

    [TestMethod]
    public void CasoOficial001_ProgramaInsumos_DebeCerrarContraCostoDirectoCongelado()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var service = new ProgramacionInsumosService();
        var total =
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Materiales).Rows.Sum(r => r.ImporteTotal) +
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.ManoDeObra).Rows.Sum(r => r.ImporteTotal) +
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Herramienta).Rows.Sum(r => r.ImporteTotal) +
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Maquinaria).Rows.Sum(r => r.ImporteTotal);

        Assert.AreEqual(1000.00m, total, "El programa de insumos debe cerrar contra el C.D. oficial del caso 001.");
    }

    [TestMethod]
    public void CasoOficial001_CurvaS_DebeCerrarEnUltimoPeriodoContraPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var rows = new ProgramacionCurvaSService().BuildFinancialCurve(context, scenario.Programa!.Id, scenario.Proyecto);
        var ultimo = rows.Last();

        Assert.AreEqual(1000.00m, ultimo.ImporteAcumulado);
        Assert.AreEqual(10.00m, ultimo.CantidadAcumulada);
        Assert.AreEqual(100.0000m, ultimo.PorcentajeAcumulado);
        Assert.AreEqual(100.0000m, ultimo.PorcentajeFisicoAcumulado);
    }
}
