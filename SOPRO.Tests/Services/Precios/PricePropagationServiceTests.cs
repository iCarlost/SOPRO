using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Precios;

[TestClass]
public class PricePropagationServiceTests
{
    [TestMethod]
    public void PropagarMaterial_DebeActualizarMatrizYConceptoConMotorDelProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        scenario.Cemento.PrecioUnitario = 70m;
        context.SaveChanges();

        PricePropagationService.PropagarMaterial(context, scenario.Cemento.Id);

        var matriz = context.Matrices.Include(m => m.Componentes).Single(m => m.Id == scenario.MatrizApu.Id);
        var concepto = context.ConceptosPresupuesto.Single(c => c.Id == scenario.Concepto.Id);

        Assert.AreEqual(110.00m, matriz.CostoDirecto, "60+10 por el nuevo precio del cemento.");
        Assert.AreEqual(110.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1100.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public void PropagarManoDeObra_DebeRepercutirEnPorcentajeMoYMaquinariaYConcepto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        scenario.Oficial.SalarioReal = 40m;
        context.SaveChanges();

        PricePropagationService.PropagarManoDeObra(context, scenario.Oficial.Id);

        var matriz = context.Matrices.Include(m => m.Componentes).Single(m => m.Id == scenario.MatrizApu.Id);
        var concepto = context.ConceptosPresupuesto.Single(c => c.Id == scenario.Concepto.Id);

        Assert.AreEqual(112.00m, matriz.CostoDirecto, "60+40+%MO(4)+%MO(4)+maquinaria(4).");
        Assert.AreEqual(112.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1120.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public void PropagarEliminacion_AlQuitarUnComponente_ReajustaMatrizYConcepto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var maquinaria = context.ComponentesMatriz.Single(c => c.MaquinariaId == scenario.Revolvedora.Id);
        context.ComponentesMatriz.Remove(maquinaria);
        context.SaveChanges();

        PricePropagationService.PropagarEliminacion(context, new List<int> { scenario.MatrizApu.Id });

        var matriz = context.Matrices.Single(m => m.Id == scenario.MatrizApu.Id);
        var concepto = context.ConceptosPresupuesto.Single(c => c.Id == scenario.Concepto.Id);

        Assert.AreEqual(96.00m, matriz.CostoDirecto, "100 - 4 de la revolvedora eliminada.");
        Assert.AreEqual(96.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(960.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public void PropagarEliminacion_ConListaVacia_NoLanzaNiModifica()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        PricePropagationService.PropagarEliminacion(context, new List<int>());

        Assert.AreEqual(100.00m, scenario.MatrizApu.CostoDirecto);
        Assert.AreEqual(1000.00m, scenario.Concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public void PropagarMaterial_ConCicloDeAuxiliares_LanzaDiagnostico()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var basicoCiclico = new Matriz
        {
            Clave = "B-CIC",
            Descripcion = "Básico cíclico",
            Unidad = "m",
            Tipo = TipoMatriz.Basico,
            ProyectoId = scenario.Proyecto.Id
        };
        context.Matrices.Add(basicoCiclico);
        context.SaveChanges();

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = scenario.MatrizApu.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = basicoCiclico.Id,
            Cantidad = 1m
        });
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = basicoCiclico.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = scenario.MatrizApu.Id,
            Cantidad = 1m
        });
        context.SaveChanges();

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => PricePropagationService.PropagarMaterial(context, scenario.Cemento.Id, scenario.Proyecto.Id));

        StringAssert.Contains(exception.Message, "Ciclo de matrices detectado");
        StringAssert.Contains(exception.Message, $"{scenario.MatrizApu.Id} -> {basicoCiclico.Id} -> {scenario.MatrizApu.Id}");
    }
}
