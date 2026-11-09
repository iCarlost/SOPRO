using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services.Programacion;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

[TestClass]
public class ProgramacionInsumosServiceTests
{
    [TestMethod]
    public void Build_Materiales_VinculosInternosNoGeneranFilasYUsoDirectoPermanece()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);
        var componentesNoDirectos = scenario.MatrizApu.Componentes
            .Where(c => c.MaterialId != scenario.Cemento.Id
                && c.ManoDeObraId != scenario.Oficial.Id
                && c.MaquinariaId != scenario.Revolvedora.Id)
            .ToList();
        context.ComponentesMatriz.RemoveRange(componentesNoDirectos);
        foreach (var component in componentesNoDirectos)
            scenario.MatrizApu.Componentes.Remove(component);
        scenario.MatrizApu.Componentes.Single(c => c.MaterialId == scenario.Cemento.Id).Cantidad = 0.5m;
        var totals = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(scenario.MatrizApu.Componentes.ToList(), scenario.Proyecto.DecimalesImporte);
        scenario.MatrizApu.CostoDirecto = totals.CostoDirectoTotal;
        scenario.Concepto.CostoDirectoUnitario = totals.CostoDirectoTotal;
        scenario.Concepto.CostoDirectoTotal = scenario.Concepto.Cantidad * totals.CostoDirectoTotal;
        scenario.Revolvedora.CantidadCombustible = 10m;
        scenario.Revolvedora.MaterialCombustibleId = scenario.Cemento.Id;
        scenario.Revolvedora.MaterialAceiteId = scenario.Cemento.Id;
        scenario.Revolvedora.ManoDeObraOperadorId = scenario.Oficial.Id;
        context.SaveChanges();

        var result = new ProgramacionInsumosService().Build(context, scenario.Proyecto, ProgramaInsumoTipo.Materiales);

        var material = result.Rows.Single(r => r.InsumoId == scenario.Cemento.Id);
        Assert.AreEqual(5m, material.Total);
        Assert.AreEqual(1, result.Rows.Count);
        Assert.IsFalse(result.Rows.Any(r => r.Total == 0m));

        var manoDeObra = new ProgramacionInsumosService().Build(context, scenario.Proyecto, ProgramaInsumoTipo.ManoDeObra);
        Assert.AreEqual(1, manoDeObra.Rows.Count);
        Assert.AreEqual(10m, manoDeObra.Rows.Single(r => r.InsumoId == scenario.Oficial.Id).Total);
        Assert.IsFalse(manoDeObra.Rows.Any(r => r.Total == 0m));
    }

    [TestMethod]
    public void Build_Materiales_DebeCoincidirConExplosionYDistribuirPorPeriodos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var service = new ProgramacionInsumosService();
        var result = service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.Materiales);

        var row = result.Rows.Single(r => r.InsumoId == scenario.Cemento.Id);

        Assert.AreEqual(600m, row.ImporteTotal);
        Assert.AreEqual(10.00m, row.Total);
        Assert.AreEqual(60m, row.PrecioUnitario);
        Assert.AreEqual(240m, row.ImportesPorPeriodo[scenario.Periodo1!.Id]);
        Assert.AreEqual(360m, row.ImportesPorPeriodo[scenario.Periodo2!.Id]);
        Assert.AreEqual(4.00m, row.CantidadesPorPeriodo[scenario.Periodo1.Id]);
        Assert.AreEqual(6.00m, row.CantidadesPorPeriodo[scenario.Periodo2.Id]);
        Assert.AreEqual(600m, row.ImportesAcumuladosPorPeriodo[scenario.Periodo2.Id]);
    }

    [TestMethod]
    public void Build_ManoDeObraPorcentajeMO_DebeMantenerCantidadFisicaYPuInferido()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var service = new ProgramacionInsumosService();
        var result = service.Build(context, scenario.Proyecto, ProgramaInsumoTipo.ManoDeObra);

        var cabo = result.Rows.Single(r => r.InsumoId == scenario.CaboPorcentajeMo.Id);

        Assert.AreEqual(30m, cabo.ImporteTotal);
        Assert.AreEqual(1.00m, cabo.Total);
        Assert.AreEqual(30.00m, cabo.PrecioUnitario);
        Assert.AreEqual(12m, cabo.ImportesPorPeriodo[scenario.Periodo1!.Id]);
        Assert.AreEqual(18m, cabo.ImportesPorPeriodo[scenario.Periodo2!.Id]);
        Assert.AreEqual(0.40m, cabo.CantidadesPorPeriodo[scenario.Periodo1.Id]);
        Assert.AreEqual(0.60m, cabo.CantidadesPorPeriodo[scenario.Periodo2.Id]);
    }
}
