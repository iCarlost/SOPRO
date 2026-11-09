using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

[TestClass]
public sealed class DepuracionServiceTests
{
    [TestMethod]
    public void Depuracion_PreviewYResultadoMismoPlan_ProtegeVinculosDeMaquinariaRetenidaYLimpiaCandidata()
    {
        using var context = TestDbFactory.CreateContext();
        SoproCalculationScenario scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        Material retainedFuel = NewMaterial(scenario.Proyecto.Id, "MAT-RET-FUEL");
        Material removedFuel = NewMaterial(scenario.Proyecto.Id, "MAT-DEL-FUEL");
        ManoDeObra retainedOperator = NewLabor(scenario.Proyecto.Id, "MO-RET");
        ManoDeObra removedOperator = NewLabor(scenario.Proyecto.Id, "MO-DEL");
        context.Materiales.AddRange(retainedFuel, removedFuel);
        context.ManoDeObra.AddRange(retainedOperator, removedOperator);
        context.SaveChanges();

        Maquinaria retainedMachine = NewMachine(scenario.Proyecto.Id, "EQ-RET");
        retainedMachine.MaterialCombustibleId = retainedFuel.Id;
        retainedMachine.ManoDeObraOperadorId = retainedOperator.Id;
        Maquinaria candidateMachine = NewMachine(scenario.Proyecto.Id, "EQ-DEL");
        candidateMachine.MaterialCombustibleId = removedFuel.Id;
        candidateMachine.ManoDeObraOperadorId = removedOperator.Id;
        context.Maquinaria.AddRange(retainedMachine, candidateMachine);
        context.SaveChanges();
        Matriz candidateMatrix = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "APU-DEL",
            Descripcion = "Matriz candidata",
            Unidad = "u",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        context.Matrices.Add(candidateMatrix);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = candidateMatrix.Id,
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = candidateMachine.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = scenario.MatrizApu.Id,
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = retainedMachine.Id,
            Cantidad = 1m,
            Orden = 99,
            Notas = string.Empty
        });
        context.SaveChanges();

        DepuracionService.DepuracionPreview preview = DepuracionService.ObtenerPreview(context, scenario.Proyecto.Id);
        DepuracionService.DepuracionResultado result = DepuracionService.Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(preview.MatricesApuCandidatas, result.MatricesApuEliminadas);
        Assert.AreEqual(preview.MatricesBasicasCandidatas, result.MatricesBasicasEliminadas);
        Assert.AreEqual(preview.CuadrillasCandidatas, result.CuadrillasEliminadas);
        Assert.AreEqual(preview.MaterialesCandidatos, result.MaterialesEliminados);
        Assert.AreEqual(preview.ManoDeObraCandidata, result.ManoDeObraEliminada);
        Assert.AreEqual(preview.MaquinariaCandidata, result.MaquinariaEliminada);
        Assert.IsTrue(context.Maquinaria.Any(m => m.Id == retainedMachine.Id));
        Assert.IsFalse(context.Maquinaria.Any(m => m.Id == candidateMachine.Id));
        Assert.IsFalse(context.Matrices.Any(m => m.Id == candidateMatrix.Id));
        Assert.IsTrue(context.Materiales.Any(m => m.Id == retainedFuel.Id));
        Assert.IsTrue(context.ManoDeObra.Any(m => m.Id == retainedOperator.Id));
        Assert.IsFalse(context.Materiales.Any(m => m.Id == removedFuel.Id));
        Assert.IsFalse(context.ManoDeObra.Any(m => m.Id == removedOperator.Id));
    }

    private static Material NewMaterial(int projectId, string key) => new()
    {
        ProyectoId = projectId, Clave = key, Descripcion = key, Unidad = "L", PrecioUnitario = 1m,
        Notas = string.Empty, Origen = OrigenInsumo.Proyecto
    };

    private static ManoDeObra NewLabor(int projectId, string key) => new()
    {
        ProyectoId = projectId, Clave = key, Descripcion = key, Unidad = "jor", SalarioReal = 1m,
        Notas = string.Empty, Origen = OrigenInsumo.Proyecto
    };

    private static Maquinaria NewMachine(int projectId, string key) => new()
    {
        ProyectoId = projectId, Clave = key, Descripcion = key, Notas = string.Empty,
        CostoHorario = 1m, EsCostoCalculado = false, Origen = OrigenInsumo.Proyecto
    };
}
