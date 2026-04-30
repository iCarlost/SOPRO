using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Invariantes;

[TestClass]
public class SoproCalculationInvariantTests
{
    [TestMethod]
    public void Invariante_ExplosionPorRubros_DebeSumarCostoDirectoTotal()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var result = new ExplosionInsumosService().Calculate(context, scenario.Proyecto.Id, "Todos");

        // En la explosión real de SOPRO, los Básicos/Cuadrillas no son un rubro final separado:
        // se descomponen recursivamente en Materiales, Mano de Obra, Herramienta y Maquinaria.
        var sumaRubros =
            result.Materiales.Values.Sum(x => x.Cantidad) +
            result.ManoObra.Values.Sum(x => x.Cantidad) +
            result.Herramientas.Values.Sum(x => x.Cantidad) +
            result.Maquinaria.Values.Sum(x => x.Cantidad);

        Assert.AreEqual(result.CostoDirectoTotal, sumaRubros, "La suma de rubros explotados debe coincidir con el C.D. explotado.");
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal, "La explosión debe cerrar contra presupuesto.");
    }

    [TestMethod]
    public void Invariante_ProgramaInsumosPorTipo_DebeSumarCostoDirectoDelPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var service = new ProgramacionInsumosService();
        var totalPorTipos =
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Materiales).Rows.Sum(r => r.ImporteTotal) +
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.ManoDeObra).Rows.Sum(r => r.ImporteTotal) +
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Herramienta).Rows.Sum(r => r.ImporteTotal) +
            service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Maquinaria).Rows.Sum(r => r.ImporteTotal);

        Assert.AreEqual(scenario.Concepto.CostoDirectoTotal, totalPorTipos);
    }

    [TestMethod]
    public void Invariante_CurvaS_UltimoAcumuladoDebeSerIgualASumaDePeriodos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var rows = new ProgramacionCurvaSService().BuildFinancialCurve(context, scenario.Programa!.Id, scenario.Proyecto);

        var sumaPeriodos = rows.Sum(r => r.ImportePeriodo);
        var ultimoAcumulado = rows.Last().ImporteAcumulado;

        Assert.AreEqual(sumaPeriodos, ultimoAcumulado);
        Assert.AreEqual(scenario.Concepto.CostoDirectoTotal, ultimoAcumulado);
    }
}
