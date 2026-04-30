using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Explosion;

[TestClass]
public class ExplosionInsumosServiceTests
{
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
}
