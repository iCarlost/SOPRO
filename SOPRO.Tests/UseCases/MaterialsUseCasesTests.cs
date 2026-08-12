using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.UseCases;

/// <summary>
/// Gate N3: los casos de uso de la frontera de Application (slice materiales)
/// se prueban sin formularios, con SQLite real del mismo esquema de producciÃ³n.
/// "Guardado y propagaciÃ³n son una operaciÃ³n lÃ³gica Ãºnica": un solo SaveChanges
/// + propagaciÃ³n inmediata dentro del caso de uso.
/// </summary>
[TestClass]
public class MaterialsUseCasesTests
{
    // ---------- ListMaterials ----------

    [TestMethod]
    public async Task ListMaterials_SinProyecto_DevuelveCatalogoMaestroCompleto()
    {
        using var context = TestDbFactory.CreateContext();

        context.Materiales.Add(new Material
        {
            ProyectoId = null,
            Clave = "B-ARENA",
            Descripcion = "Arena de rio",
            Unidad = "m3",
            PrecioUnitario = 250m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        });
        context.Materiales.Add(new Material
        {
            ProyectoId = null,
            Clave = "A-CEM",
            Descripcion = "Cemento gris",
            Unidad = "kg",
            PrecioUnitario = 60m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        });
        context.SaveChanges();

        var session = ProjectSessionInfo.FromLegacy(context, null);
        var result = await new ListMaterials().Execute(session, new ListMaterialsRequest());

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(2, result.Value.Count);
        Assert.AreEqual("A-CEM", result.Value[0].Clave);
        Assert.AreEqual("B-ARENA", result.Value[1].Clave);
    }

    [TestMethod]
    public async Task ListMaterials_ConProyecto_FiltraPorProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = new Proyecto
        {
            Nombre = "Otro proyecto",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new System.DateTime(2026, 2, 1),
            FechaTermino = new System.DateTime(2026, 2, 28),
            PlazoEjecucion = 28,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(otroProyecto);
        context.SaveChanges();
        context.Materiales.Add(new Material
        {
            ProyectoId = otroProyecto.Id,
            Clave = "MAT-AJENO",
            Descripcion = "Material de otro proyecto",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        });
        context.SaveChanges();

        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);
        var result = await new ListMaterials().Execute(session, new ListMaterialsRequest());

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value.Count);
        Assert.AreEqual(scenario.Cemento.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task ListMaterials_BusquedaPorClaveODescripcion_CaseInsensitive()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var porDescripcion = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(SearchText: "GRIS"))).Value!;
        var porClave = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(SearchText: "mat-ceM"))).Value!;
        var sinResultados = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(SearchText: "no-existe"))).Value!;

        Assert.AreEqual(1, porDescripcion.Count);
        Assert.AreEqual(scenario.Cemento.Id, porDescripcion[0].Id);
        Assert.AreEqual(1, porClave.Count);
        Assert.AreEqual(0, sinResultados.Count);
    }

    [TestMethod]
    public async Task ListMaterials_FiltrosOrigen_ResuelvenPorMarcaDeImportacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        context.Materiales.Add(new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-IMP",
            Descripcion = "Material importado de otro presupuesto",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Notas = "[IMPORTADO DE: PRESUPUESTO ANTERIOR]",
            Origen = OrigenInsumo.Proyecto
        });
        context.SaveChanges();

        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var soloProyecto = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(OnlyProjectItems: true))).Value!;
        var soloMaestro = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(OnlyMasterItems: true))).Value!;

        Assert.IsFalse(soloProyecto.Any(m => m.Clave == "MAT-IMP"));
        Assert.AreEqual(1, soloProyecto.Count);
        Assert.AreEqual(1, soloMaestro.Count);
        Assert.AreEqual("MAT-IMP", soloMaestro[0].Clave);
    }

    // ---------- SaveMaterial ----------

    [TestMethod]
    public async Task SaveMaterial_MaterialNuevoValido_CreaYPersiste()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: "MAT-NUEVO",
            Descripcion: "Material nuevo de prueba",
            Unidad: "pza",
            PrecioUnitario: 42.5m,
            Notas: string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.IsTrue(result.Value.IsNew);
        Assert.IsTrue(result.Value.MaterialId > 0);
        Assert.IsFalse(result.Value.TriggeredRecalculation, "Un material sin uso no debe disparar recÃ¡lculo.");

        var persistido = context.Materiales.Single(m => m.Id == result.Value.MaterialId);
        Assert.AreEqual("MAT-NUEVO", persistido.Clave);
        Assert.AreEqual(42.5m, persistido.PrecioUnitario);
        Assert.AreEqual(scenario.Proyecto.Id, persistido.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, persistido.Origen);
    }

    [TestMethod]
    public async Task SaveMaterial_CamposRequeridos_FallaValidacion()
    {
        using var context = TestDbFactory.CreateContext();
        var session = ProjectSessionInfo.FromLegacy(context, null);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: "   ",
            Descripcion: "Sin clave",
            Unidad: "pza",
            PrecioUnitario: 1m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: null));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Validation, result.Error!.Code);
        Assert.AreEqual(0, context.Materiales.Count());
    }

    [TestMethod]
    public async Task SaveMaterial_ClaveDuplicadaEnProyecto_FallaConflicto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: scenario.Cemento.Clave,
            Descripcion: "Clave repetida",
            Unidad: "kg",
            PrecioUnitario: 1m,
            Notas: string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);
        Assert.AreEqual(1, context.Materiales.Count(m => m.ProyectoId == scenario.Proyecto.Id));
    }

    [TestMethod]
    public async Task SaveMaterial_ActualizaPrecio_GuardaYPropagaEnUnaOperacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: scenario.Cemento.Clave,
            Descripcion: scenario.Cemento.Descripcion,
            Unidad: scenario.Cemento.Unidad,
            PrecioUnitario: 70m,
            Notas: scenario.Cemento.Notas ?? string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.IsFalse(result.Value.IsNew);
        Assert.IsTrue(result.Value.TriggeredRecalculation);

        Assert.AreEqual(110.00m, scenario.MatrizApu.CostoDirecto,
            "Gate N3: el guardado y la propagaciÃ³n del recÃ¡lculo son una sola operaciÃ³n.");
        Assert.AreEqual(110.00m, scenario.Concepto.CostoDirectoUnitario);
        Assert.AreEqual(1100.00m, scenario.Concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task SaveMaterial_GuardarEnMaestro_DesligaDelProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: "MAT-MAESTRO",
            Descripcion: "Material maestro",
            Unidad: "pza",
            PrecioUnitario: 10m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: scenario.Proyecto.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        var persistido = context.Materiales.Single(m => m.Id == result.Value.MaterialId);
        Assert.IsNull(persistido.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Maestro, persistido.Origen);
    }

    [TestMethod]
    public async Task SaveMaterial_MaterialInexistente_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: 9999,
            Clave: "X",
            Descripcion: "X",
            Unidad: "pza",
            PrecioUnitario: 1m,
            Notas: string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
    }

    // ---------- PreviewMaterialDeletion ----------

    [TestMethod]
    public async Task PreviewMaterialDeletion_SinUso_DevuelveVacioSinModificar()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);
        var soltero = new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-SOLO",
            Descripcion = "Sin uso en matrices",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(soltero);
        context.SaveChanges();

        var result = await new PreviewMaterialDeletion().Execute(
            session, new PreviewMaterialDeletionRequest(soltero.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(0, result.Value.ComponentCount);
        Assert.AreEqual(0, result.Value.Matrices.Count);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == soltero.Id),
            "La previsualizaciÃ³n no debe modificar el catÃ¡logo.");
    }

    [TestMethod]
    public async Task PreviewMaterialDeletion_EnUso_ReportaComponentesYMatrices()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new PreviewMaterialDeletion().Execute(
            session, new PreviewMaterialDeletionRequest(scenario.Cemento.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value.ComponentCount);
        Assert.AreEqual(1, result.Value.Matrices.Count);
        Assert.AreEqual("APU-001", result.Value.Matrices[0].Clave);
        Assert.AreEqual("Concreto simple", result.Value.Matrices[0].Descripcion);
    }

    // ---------- DeleteMaterial ----------

    [TestMethod]
    public async Task DeleteMaterial_SinUso_EliminaMaterial()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);
        var soltero = new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-SOLO",
            Descripcion = "Sin uso en matrices",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(soltero);
        context.SaveChanges();

        var result = await new DeleteMaterial().Execute(
            session, new DeleteMaterialRequest(soltero.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(0, result.Value.DeletedComponents);
        Assert.IsFalse(result.Value.TriggeredRecalculation);
        Assert.IsFalse(context.Materiales.Any(m => m.Id == soltero.Id));
    }

    [TestMethod]
    public async Task DeleteMaterial_EnUso_EliminaComponentesYRecalcula()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new DeleteMaterial().Execute(
            session, new DeleteMaterialRequest(scenario.Cemento.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.IsTrue(result.Value!.DeletedComponents == 1);
        Assert.AreEqual(1, result.Value.AffectedMatrixCount);
        Assert.IsTrue(result.Value.TriggeredRecalculation);
        Assert.IsFalse(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MaterialId == scenario.Cemento.Id));

        Assert.AreEqual(40.00m, scenario.MatrizApu.CostoDirecto,
            "Sin el cemento (60) el C.D. de la matriz debe pasar de 100 a 40.");
        Assert.AreEqual(40.00m, scenario.Concepto.CostoDirectoUnitario);
        Assert.AreEqual(400.00m, scenario.Concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task DeleteMaterial_Inexistente_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = ProjectSessionInfo.FromLegacy(context, scenario.Proyecto.Id);

        var result = await new DeleteMaterial().Execute(
            session, new DeleteMaterialRequest(9999));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
    }
}