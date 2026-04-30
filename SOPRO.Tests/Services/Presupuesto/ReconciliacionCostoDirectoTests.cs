using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class ReconciliacionCostoDirectoTests
{
    [TestMethod]
    public void PresupuestoExplosionYProgramaInsumos_DebenCuadrarElMismoCostoDirecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var explosion = new ExplosionInsumosService()
            .Calculate(context, scenario.Proyecto.Id, "Todos");

        var programacion = new ProgramacionInsumosService();
        var mat = programacion.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Materiales).Rows.Sum(r => r.ImporteTotal);
        var mo = programacion.Build(context, scenario.Proyecto, ProgramaInsumoTipo.ManoDeObra).Rows.Sum(r => r.ImporteTotal);
        var maq = programacion.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Maquinaria).Rows.Sum(r => r.ImporteTotal);
        var her = programacion.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Herramienta).Rows.Sum(r => r.ImporteTotal);
        var totalProgramaInsumos = mat + mo + maq + her;

        Assert.AreEqual(1000m, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(scenario.Concepto.CostoDirectoTotal, explosion.CostoDirectoTotal);
        Assert.AreEqual(scenario.Concepto.CostoDirectoTotal, totalProgramaInsumos);
    }
}
