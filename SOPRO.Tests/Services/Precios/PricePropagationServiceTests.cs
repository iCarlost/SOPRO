using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
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

    [TestMethod]
    public void PropagarManoDeObra_ConCicloAguasArribaDelOrigen_LanzaDiagnostico()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // B depende del origen A; C depende de B; y B depende de C: el ciclo está aguas
        // arriba del origen sin incluirlo. La propagación lo alcanzaría al subir niveles.
        var b = AgregarBasico(context, scenario, "B-CIC2");
        var c = AgregarBasico(context, scenario, "C-CIC2");
        context.ComponentesMatriz.AddRange(
            new ComponenteMatriz { MatrizId = b.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = scenario.MatrizApu.Id, Cantidad = 1m },
            new ComponenteMatriz { MatrizId = c.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = b.Id, Cantidad = 1m },
            new ComponenteMatriz { MatrizId = b.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = c.Id, Cantidad = 1m });
        context.SaveChanges();

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => PricePropagationService.PropagarManoDeObra(context, scenario.Oficial.Id, scenario.Proyecto.Id));

        StringAssert.Contains(exception.Message, "Ciclo de matrices detectado");
        StringAssert.Contains(exception.Message, $"{b.Id} -> {c.Id} -> {b.Id}");
    }

    [TestMethod]
    public void PropagarManoDeObra_ConDagConvergente_ElPadreConsumeAuxiliaresFrescos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // B depende de A; C depende de B; D depende de A y de C. Por niveles, D se
        // recalculaba con el C obsoleto; el orden topológico exige B → C → D.
        var b = AgregarBasico(context, scenario, "B-DAG");
        var c = AgregarBasico(context, scenario, "C-DAG");
        var d = AgregarBasico(context, scenario, "D-DAG");
        context.ComponentesMatriz.AddRange(
            new ComponenteMatriz { MatrizId = b.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = scenario.MatrizApu.Id, Cantidad = 2m },
            new ComponenteMatriz { MatrizId = c.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = b.Id, Cantidad = 2m },
            new ComponenteMatriz { MatrizId = d.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = scenario.MatrizApu.Id, Cantidad = 1m },
            new ComponenteMatriz { MatrizId = d.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = c.Id, Cantidad = 2m });
        c.CostoDirecto = 999m;
        context.SaveChanges();

        scenario.Oficial.SalarioReal = 40m;
        context.SaveChanges();

        PricePropagationService.PropagarManoDeObra(context, scenario.Oficial.Id, scenario.Proyecto.Id);

        Assert.AreEqual(112.00m, scenario.MatrizApu.CostoDirecto, "A con el nuevo salario (60+40+4+4+4).");
        Assert.AreEqual(224.00m, context.Matrices.Single(m => m.Id == b.Id).CostoDirecto, "B = 2 × A.");
        Assert.AreEqual(448.00m, context.Matrices.Single(m => m.Id == c.Id).CostoDirecto, "C = 2 × B.");
        Assert.AreEqual(1008.00m, context.Matrices.Single(m => m.Id == d.Id).CostoDirecto,
            "D = 1 × A + 2 × C frescos; con propagación por niveles habría consumido el C obsoleto (999).");
    }

    [TestMethod]
    public void PropagarMaterial_ConOrigenesMutuamenteDependientes_PadreConsumeHijoFresco()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // A = MatrizApu (contiene cemento y referenciará a B como auxiliar).
        // B = básico nuevo (contiene cemento). Ambos son orígenes de la propagación
        // y A depende de B: si los orígenes se recalculan en orden de carga (A antes
        // que B, por Id) y quedan excluidos del pase topológico, A consume el costo
        // obsoleto de B (600) en lugar del fresco (700).
        var b = AgregarBasico(context, scenario, "B-MULTI");
        context.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = b.Id,
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = scenario.Cemento.Id,
                Cantidad = 10m,
                Orden = 1
            },
            new ComponenteMatriz
            {
                MatrizId = scenario.MatrizApu.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = b.Id,
                Cantidad = 1m,
                Orden = 6
            });
        b.CostoDirecto = 600m;
        context.SaveChanges();

        scenario.Cemento.PrecioUnitario = 70m;
        context.SaveChanges();

        PricePropagationService.PropagarMaterial(context, scenario.Cemento.Id, scenario.Proyecto.Id);

        Assert.AreEqual(700.00m, context.Matrices.Single(m => m.Id == b.Id).CostoDirecto, "B = 10 × 70.");
        Assert.AreEqual(810.00m, scenario.MatrizApu.CostoDirecto,
            "A = 70 + 40 + 1 × B_fresco(700); con orígenes excluidos del orden topológico habría quedado en 710.");
        var concepto = context.ConceptosPresupuesto.Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(810.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(8100.00m, concepto.CostoDirectoTotal);
    }

    private static Matriz AgregarBasico(SOPROContext context, SoproCalculationScenario scenario, string clave)
    {
        var matriz = new Matriz
        {
            Clave = clave,
            Descripcion = $"Básico {clave}",
            Unidad = "m",
            Tipo = TipoMatriz.Basico,
            ProyectoId = scenario.Proyecto.Id
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
    }
}
