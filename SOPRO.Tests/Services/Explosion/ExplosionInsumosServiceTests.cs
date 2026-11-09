using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using SOPRO.Application.Services;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Explosion;

[TestClass]
public class ExplosionInsumosServiceTests
{
    [TestMethod]
    public void Calculate_VinculosInternosDeMaquinariaNoSonInsumosSeparados()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var componentesNoDirectos = scenario.MatrizApu.Componentes
            .Where(c => c.MaterialId != scenario.Cemento.Id
                && c.ManoDeObraId != scenario.Oficial.Id
                && c.MaquinariaId != scenario.Revolvedora.Id)
            .ToList();
        context.ComponentesMatriz.RemoveRange(componentesNoDirectos);
        foreach (var component in componentesNoDirectos)
            scenario.MatrizApu.Componentes.Remove(component);
        scenario.MatrizApu.Componentes.Single(c => c.MaterialId == scenario.Cemento.Id).Cantidad = 0.5m;
        var totals = MatrixComponentCalculationService.Recalculate(scenario.MatrizApu.Componentes.ToList(), scenario.Proyecto.DecimalesImporte);
        scenario.MatrizApu.CostoDirecto = totals.CostoDirectoTotal;
        scenario.Concepto.CostoDirectoUnitario = totals.CostoDirectoTotal;
        scenario.Concepto.CostoDirectoTotal = scenario.Concepto.Cantidad * totals.CostoDirectoTotal;
        scenario.Revolvedora.CantidadCombustible = 10m;
        scenario.Revolvedora.MaterialCombustibleId = scenario.Cemento.Id;
        scenario.Revolvedora.MaterialAceiteId = scenario.Cemento.Id;
        scenario.Revolvedora.ManoDeObraOperadorId = scenario.Oficial.Id;
        context.SaveChanges();

        var result = new ExplosionInsumosService().Calculate(context, scenario.Proyecto.Id, "Todos");

        Assert.AreEqual(5m, result.Materiales[scenario.Cemento.Id].CantidadFisica);
        Assert.AreEqual(300m, result.Materiales[scenario.Cemento.Id].Cantidad);
        Assert.AreEqual(10m, result.ManoObra[scenario.Oficial.Id].CantidadFisica);
        Assert.AreEqual(1, result.Materiales.Count);
        Assert.AreEqual(1, result.ManoObra.Count);
        Assert.AreEqual(1, result.Maquinaria.Count);
        Assert.IsFalse(result.Materiales.Values.Any(i => i.Cantidad == 0m));
        Assert.IsFalse(result.Maquinaria.Values.Any(i => i.Cantidad == 0m));
        Assert.IsFalse(result.Herramientas.Values.Any(i => i.Cantidad == 0m));
    }

    [TestMethod]
    public void Calculate_DebeReconciliarExplosionContraCostoDirectoDelPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var service = new ExplosionInsumosService();
        var result = service.Calculate(context, scenario.Proyecto.Id, "Todos");

        Assert.AreEqual(1000m, result.CostoDirectoPresupuesto);
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal);
        Assert.AreEqual(600m, result.Materiales[scenario.Cemento.Id].Cantidad);
        Assert.AreEqual(300m, result.ManoObra[scenario.Oficial.Id].Cantidad);
        Assert.AreEqual(30m, result.ManoObra[scenario.CaboPorcentajeMo.Id].Cantidad);
        Assert.AreEqual(30m, result.Herramientas[scenario.HerramientaMenorPorcentajeMo.Id].Cantidad);
        Assert.AreEqual(40m, result.Maquinaria[scenario.Revolvedora.Id].Cantidad);
    }

    [TestMethod]
    public void Calculate_DebeInferirPrecioUnitarioDePorcentajeMO_SinTomarloComoInsumoNormal()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var service = new ExplosionInsumosService();
        var result = service.Calculate(context, scenario.Proyecto.Id, "Mano de obra");

        var cabo = result.ManoObra[scenario.CaboPorcentajeMo.Id];

        Assert.IsTrue(cabo.EsPorcentual);
        Assert.AreEqual("%MO", cabo.Unidad);
        Assert.AreEqual(1.00m, cabo.CantidadFisica);
        Assert.AreEqual(300.00m, cabo.PrecioUnitario);
        Assert.AreEqual(30.00m, cabo.Cantidad);
    }

    [TestMethod]
    public void Calculate_CostoDirectoConProductoNoVisible_ConservaParidadConFachada()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        scenario.Concepto.Cantidad = 3m;
        scenario.Concepto.CostoDirectoUnitario = 0.005m;
        context.SaveChanges();

        var esperado = new MotorCalculoSopro(scenario.Proyecto).SumarCostoDirecto(
            context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == scenario.Proyecto.Id)
                .ToList());
        var result = new ExplosionInsumosService().Calculate(context, scenario.Proyecto.Id, "Todos");

        Assert.AreEqual(0.03m, esperado);
        Assert.AreEqual(esperado, result.CostoDirectoPresupuesto);
    }

    [TestMethod]
    public void Calculate_FilasConservanFormatoLegacy()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var result = new ExplosionInsumosService().Calculate(context, scenario.Proyecto.Id, "Todos");
        var formatter = new MotorCalculoSopro(scenario.Proyecto);

        var total = result.Rows.Single(r => r.Descripcion == "TOTAL DEL REPORTE");
        var referencia = result.Rows.Single(r => r.Descripcion == "Costo Directo (Presupuesto)");

        Assert.AreEqual(formatter.FormatImporte(result.CostoDirectoTotal), total.ImporteTexto);
        Assert.AreEqual(formatter.FormatImporte(result.CostoDirectoPresupuesto), referencia.ImporteTexto);
    }
}
