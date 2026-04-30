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
