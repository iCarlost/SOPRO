using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.UseCases;

/// <summary>
/// Gate N3: los casos de uso de la frontera de Application (slice materiales)
/// se prueban sin formularios, con SQLite real del mismo esquema de producción.
/// "Guardado y propagación son una operación lógica única": una transacción
/// única + propagación inmediata dentro del caso de uso, con rollback completo
/// ante cualquier fallo.
/// </summary>
[TestClass]
public class MaterialsUseCasesTests
{
    private static ProjectSessionInfo CrearSession(int? proyectoId, string databasePath, string? masterDatabasePath = null)
        => ProjectSessionInfo.Create(proyectoId, databasePath, masterDatabasePath);

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

        using var session = CrearSession(null, context.DatabasePath, TestDbFactory.CreateTempDbPath());
        var result = await new ListMaterials().Execute(session, new ListMaterialsRequest());

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(2, result.Value!.Count);
        Assert.AreEqual("A-CEM", result.Value[0].Clave);
        Assert.AreEqual("B-ARENA", result.Value[1].Clave);
    }

    [TestMethod]
    public async Task ListMaterials_ConProyecto_FiltraPorProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
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

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new ListMaterials().Execute(session, new ListMaterialsRequest());

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.Count);
        Assert.AreEqual(scenario.Cemento.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task ListMaterials_BusquedaPorClaveODescripcion_CaseInsensitive()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var soloProyecto = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(OnlyProjectItems: true))).Value!;
        var soloMaestro = (await new ListMaterials().Execute(
            session, new ListMaterialsRequest(OnlyMasterItems: true))).Value!;

        Assert.IsFalse(soloProyecto.Any(m => m.Clave == "MAT-IMP"));
        Assert.AreEqual(1, soloProyecto.Count);
        Assert.AreEqual(1, soloMaestro.Count);
        Assert.AreEqual("MAT-IMP", soloMaestro[0].Clave);
    }

    // ---------- FindMaterialByKey ----------

    [TestMethod]
    public async Task FindMaterialByKey_ClaveExacta_CaseInsensitiveDevuelveElMaterial()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var encontrado = (await new FindMaterialByKey().Execute(
            session, new FindMaterialByKeyRequest("mat-cEm"))).Value!;
        var inexistente = (await new FindMaterialByKey().Execute(
            session, new FindMaterialByKeyRequest("NO-EXISTE"))).Value!;

        Assert.IsNotNull(encontrado);
        Assert.AreEqual(scenario.Cemento.Id, encontrado.Id);
        Assert.IsNull(inexistente);
    }

    [TestMethod]
    public async Task FindMaterialByKey_SesionMaestro_DevuelveNulo()
    {
        using var context = TestDbFactory.CreateContext();
        context.Materiales.Add(new Material
        {
            ProyectoId = null,
            Clave = "MAESTRO-01",
            Descripcion = "Maestro",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        });
        context.SaveChanges();

        using var session = CrearSession(null, context.DatabasePath, TestDbFactory.CreateTempDbPath());
        var resultado = await new FindMaterialByKey().Execute(
            session, new FindMaterialByKeyRequest("MAESTRO-01"));

        Assert.IsTrue(resultado.IsSuccess);
        Assert.IsNull(resultado.Value);
    }

    // ---------- SaveMaterial ----------

    [TestMethod]
    public async Task SaveMaterial_MaterialNuevoValido_CreaYPersiste()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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
        Assert.IsTrue(result.Value!.IsNew);
        Assert.IsTrue(result.Value!.MaterialId > 0);
        Assert.IsFalse(result.Value!.TriggeredRecalculation, "Un material sin uso no debe disparar recálculo.");

        var persistido = context.Materiales.AsNoTracking().Single(m => m.Id == result.Value!.MaterialId);
        Assert.AreEqual("MAT-NUEVO", persistido.Clave);
        Assert.AreEqual(42.5m, persistido.PrecioUnitario);
        Assert.AreEqual(scenario.Proyecto.Id, persistido.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, persistido.Origen);
    }

    [TestMethod]
    public async Task SaveMaterial_CamposRequeridos_FallaValidacion()
    {
        using var context = TestDbFactory.CreateContext();
        using var session = CrearSession(null, context.DatabasePath, TestDbFactory.CreateTempDbPath());

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
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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
    public async Task SaveMaterial_ClaveDuplicadaAlEditar_FallaConflictoTipado()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        context.Materiales.Add(new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-EXTRA",
            Descripcion = "Segundo material del mismo proyecto",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        });
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: "MAT-EXTRA",
            Descripcion: scenario.Cemento.Descripcion,
            Unidad: scenario.Cemento.Unidad,
            PrecioUnitario: 70m,
            Notas: scenario.Cemento.Notas ?? string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);
        Assert.AreEqual(60m, context.Materiales.AsNoTracking().Single(m => m.Id == scenario.Cemento.Id).PrecioUnitario,
            "La edición rechazada no debe modificar el material.");
    }

    [TestMethod]
    public async Task SaveMaterial_ActualizaPrecio_GuardaYPropagaEnUnaOperacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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
        Assert.IsFalse(result.Value!.IsNew);
        Assert.IsTrue(result.Value!.TriggeredRecalculation);

        context.ChangeTracker.Clear();
        Assert.AreEqual(110.00m, context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "Gate N3: el guardado y la propagación del recálculo son una sola operación.");
        var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(110.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1100.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task SaveMaterial_PropagacionFallida_RollbackCompleto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: scenario.Cemento.Clave,
            Descripcion: scenario.Cemento.Descripcion,
            Unidad: scenario.Cemento.Unidad,
            PrecioUnitario: decimal.MaxValue,
            Notas: scenario.Cemento.Notas ?? string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.AreEqual(60m, context.Materiales.AsNoTracking().Single(m => m.Id == scenario.Cemento.Id).PrecioUnitario,
            "El rollback debe revertir el precio del material.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "El rollback debe revertir el recálculo de la matriz.");
        var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(100.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task SaveMaterial_CanceladoAntesDePersistir_NoGuardaNada()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            new SaveMaterial().Execute(session, new SaveMaterialRequest(
                MaterialId: null,
                Clave: "MAT-NUEVO",
                Descripcion: "Nunca debe persistir",
                Unidad: "pza",
                PrecioUnitario: 1m,
                Notas: string.Empty,
                SaveToMaster: false,
                ProjectId: scenario.Proyecto.Id), cts.Token));

        Assert.AreEqual(1, context.Materiales.Count(m => m.ProyectoId == scenario.Proyecto.Id),
            "La cancelación no debe dejar persistencia parcial.");
        Assert.AreEqual(60m, context.Materiales.AsNoTracking().Single(m => m.Id == scenario.Cemento.Id).PrecioUnitario);
    }

    [TestMethod]
    public async Task SaveMaterial_ReaperturaSqlite_PersistePrecioUnitarioEImporteTotal()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        using (var context = TestDbFactory.CreateContextAt(dbPath))
        {
            var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
            using var session = CrearSession(scenario.Proyecto.Id, dbPath);

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
        }

        using var reabierta = new SOPRO.Data.Context.SOPROContext(dbPath);
        var matriz = reabierta.Matrices.AsNoTracking().Single(m => m.Clave == "APU-001");
        var concepto = reabierta.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "C-001");
        Assert.AreEqual(70m, reabierta.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-CEM").PrecioUnitario);
        Assert.AreEqual(110.00m, matriz.CostoDirecto);
        Assert.AreEqual(110.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(110.00m, concepto.PrecioUnitario,
            "La propagación headless también actualiza el PrecioUnitario del concepto.");
        Assert.AreEqual(1100.00m, concepto.ImporteTotal,
            "La propagación headless también actualiza el ImporteTotal del concepto.");
        Assert.AreEqual(1100.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task SaveMaterial_GuardarEnMaestro_EscribeEnCatalogoMaestroSinTocarProyecto()
    {
        var masterDbPath = TestDbFactory.CreateTempDbPath();
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath, masterDbPath);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: "  mat-maestro  ",
            Descripcion: "Material maestro",
            Unidad: "  pza  ",
            PrecioUnitario: 10m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: scenario.Proyecto.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        using var maestro = new SOPRO.Data.Context.SOPROContext(masterDbPath);
        var fila = maestro.Materiales.Single(m => m.Id == result.Value!.MaterialId);
        Assert.AreEqual("MAT-MAESTRO", fila.Clave);
        Assert.AreEqual("pza", fila.Unidad);
        Assert.IsNull(fila.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Maestro, fila.Origen);
        Assert.IsFalse(fila.ProyectoId.HasValue);

        Assert.IsFalse(context.Materiales.Any(m => m.Clave == "MAT-MAESTRO"),
            "La fila maestra vive en CatalogoMaestro.db, no en la base del proyecto.");
        Assert.AreEqual(1, context.Materiales.Count(m => m.ProyectoId == scenario.Proyecto.Id));
    }

    [TestMethod]
    public async Task SaveMaterial_MaterialInexistente_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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

    [TestMethod]
    public async Task SaveMaterial_MaterialDeOtroProyecto_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var ajeno = new Material
        {
            ProyectoId = otroProyecto.Id,
            Clave = "MAT-AJENO",
            Descripcion = "Material de otro proyecto",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(ajeno);
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: ajeno.Id,
            Clave: "MAT-AJENO",
            Descripcion: "Intento de edición ajena",
            Unidad: "pza",
            PrecioUnitario: 999m,
            Notas: string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
        Assert.AreEqual(1m, context.Materiales.AsNoTracking().Single(m => m.Id == ajeno.Id).PrecioUnitario);
    }

    // ---------- PreviewMaterialDeletion ----------

    [TestMethod]
    public async Task PreviewMaterialDeletion_SinUso_DevuelveVacioSinModificar()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
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
        Assert.AreEqual(0, result.Value!.ComponentCount);
        Assert.AreEqual(0, result.Value!.Matrices.Count);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == soltero.Id),
            "La previsualización no debe modificar el catálogo.");
    }

    [TestMethod]
    public async Task PreviewMaterialDeletion_EnUso_ReportaComponentesYMatrices()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new PreviewMaterialDeletion().Execute(
            session, new PreviewMaterialDeletionRequest(scenario.Cemento.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.ComponentCount);
        Assert.AreEqual(1, result.Value!.Matrices.Count);
        Assert.AreEqual("APU-001", result.Value!.Matrices[0].Clave);
        Assert.AreEqual("Concreto simple", result.Value!.Matrices[0].Descripcion);
    }

    [TestMethod]
    public async Task PreviewMaterialDeletion_MaterialDeOtroProyecto_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var ajeno = new Material
        {
            ProyectoId = otroProyecto.Id,
            Clave = "MAT-AJENO",
            Descripcion = "Material de otro proyecto",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(ajeno);
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new PreviewMaterialDeletion().Execute(
            session, new PreviewMaterialDeletionRequest(ajeno.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == ajeno.Id));
    }

    // ---------- DeleteMaterial ----------

    [TestMethod]
    public async Task DeleteMaterial_SinUso_EliminaMaterial()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
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
        Assert.AreEqual(0, result.Value!.DeletedComponents);
        Assert.IsFalse(result.Value!.TriggeredRecalculation);
        Assert.IsFalse(context.Materiales.Any(m => m.Id == soltero.Id));
    }

    [TestMethod]
    public async Task DeleteMaterial_EnUso_EliminaComponentesYRecalcula()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteMaterial().Execute(
            session, new DeleteMaterialRequest(scenario.Cemento.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.IsTrue(result.Value!.DeletedComponents == 1);
        Assert.AreEqual(1, result.Value!.AffectedMatrixCount);
        Assert.IsTrue(result.Value!.TriggeredRecalculation);
        Assert.IsFalse(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MaterialId == scenario.Cemento.Id));

        context.ChangeTracker.Clear();
        Assert.AreEqual(40.00m, context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "Sin el cemento (60) el C.D. de la matriz debe pasar de 100 a 40.");
        var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(40.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(400.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task DeleteMaterial_Inexistente_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteMaterial().Execute(
            session, new DeleteMaterialRequest(9999));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
    }

    [TestMethod]
    public async Task DeleteMaterial_MaterialDeOtroProyecto_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var ajeno = new Material
        {
            ProyectoId = otroProyecto.Id,
            Clave = "MAT-AJENO",
            Descripcion = "Material de otro proyecto",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(ajeno);
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteMaterial().Execute(
            session, new DeleteMaterialRequest(ajeno.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == ajeno.Id),
            "La eliminación rechazada no debe tocar materiales ajenos.");
    }

    [TestMethod]
    public async Task SaveMaterial_GuardarEnMaestroDesdeMaterialDeProyecto_NoSobrescribeFilaMaestra()
    {
        // Colisión numérica de Ids entre bases: el material del proyecto y la
        // fila maestra comparten el Id 1. Guardar "en maestro" desde el proyecto
        // debe crear una fila nueva y no tocar la fila maestra 1.
        var masterDbPath = TestDbFactory.CreateTempDbPath();
        using (var maestroDb = TestDbFactory.CreateContextAt(masterDbPath))
        {
            maestroDb.Materiales.Add(new Material
            {
                ProyectoId = null,
                Clave = "OTRO-MAESTRO",
                Descripcion = "Fila maestra ajena",
                Unidad = "pza",
                PrecioUnitario = 999m,
                Notas = string.Empty,
                Origen = OrigenInsumo.Maestro
            });
            maestroDb.SaveChanges();
        }

        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath, masterDbPath);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: "MAT-MAESTRO",
            Descripcion: "Material guardado en maestro",
            Unidad: "pza",
            PrecioUnitario: 10m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: scenario.Proyecto.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.IsTrue(result.Value!.IsNew);

        using var maestro = new SOPRO.Data.Context.SOPROContext(masterDbPath);
        var ajena = maestro.Materiales.Single(m => m.Id == 1);
        Assert.AreEqual("OTRO-MAESTRO", ajena.Clave);
        Assert.AreEqual(999m, ajena.PrecioUnitario, "La fila maestra ajena no debe sobrescribirse.");
        var nueva = maestro.Materiales.Single(m => m.Clave == "MAT-MAESTRO");
        Assert.AreNotEqual(1, nueva.Id);
        Assert.IsNull(nueva.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Maestro, nueva.Origen);
    }

    [TestMethod]
    public async Task SaveMaterial_GuardarEnMaestroDesdeImportado_EditaSuFilaMaestra()
    {
        // Copia importada del maestro: el Id local (2) no existe en CatalogoMaestro.db
        // como el mismo registro; la identidad real es MaterialMaestroId (2 → fila 2).
        var masterDbPath = TestDbFactory.CreateTempDbPath();
        using (var maestroDb = TestDbFactory.CreateContextAt(masterDbPath))
        {
            maestroDb.Materiales.Add(new Material
            {
                ProyectoId = null,
                Clave = "MAESTRO-1",
                Descripcion = "Fila maestra 1",
                Unidad = "pza",
                PrecioUnitario = 1m,
                Notas = string.Empty,
                Origen = OrigenInsumo.Maestro
            });
            maestroDb.Materiales.Add(new Material
            {
                ProyectoId = null,
                Clave = "MAESTRO-2",
                Descripcion = "Fila maestra 2 (la importada)",
                Unidad = "pza",
                PrecioUnitario = 2m,
                Notas = string.Empty,
                Origen = OrigenInsumo.Maestro
            });
            maestroDb.SaveChanges();
        }

        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var importado = new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-IMP",
            Descripcion = "Copia importada del maestro",
            Unidad = "pza",
            PrecioUnitario = 2m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro,
            MaterialMaestroId = 2
        };
        context.Materiales.Add(importado);
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath, masterDbPath);

        var result = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: importado.Id,
            Clave: "MAT-IMP-EDITADA",
            Descripcion: "Importado actualizado en el maestro",
            Unidad: "pza",
            PrecioUnitario: 25m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: scenario.Proyecto.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.IsFalse(result.Value!.IsNew);

        using var maestro = new SOPRO.Data.Context.SOPROContext(masterDbPath);
        Assert.AreEqual(2, maestro.Materiales.Count());
        var fila2 = maestro.Materiales.Single(m => m.Id == 2);
        Assert.AreEqual("MAT-IMP-EDITADA", fila2.Clave);
        Assert.AreEqual(25m, fila2.PrecioUnitario);
        Assert.AreEqual("MAESTRO-1", maestro.Materiales.Single(m => m.Id == 1).Clave,
            "La otra fila maestra no debe tocarse.");
    }

    [TestMethod]
    public async Task SaveMaterial_ClaveMaestraExistenteEnMinusculas_FallaConflicto()
    {
        var masterDbPath = TestDbFactory.CreateTempDbPath();
        using (var maestroDb = TestDbFactory.CreateContextAt(masterDbPath))
        {
            maestroDb.Materiales.Add(new Material
            {
                ProyectoId = null,
                Clave = "mat-old",
                Descripcion = "Clave legada en minúsculas",
                Unidad = "pza",
                PrecioUnitario = 1m,
                Notas = string.Empty,
                Origen = OrigenInsumo.Maestro
            });
            maestroDb.SaveChanges();
        }

        using var context = TestDbFactory.CreateContext();
        using var session = CrearSession(null, context.DatabasePath, masterDbPath);

        var duplicada = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: "MAT-OLD",
            Descripcion: "Equivalente en mayúsculas",
            Unidad: "pza",
            PrecioUnitario: 5m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: null));

        Assert.IsFalse(duplicada.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, duplicada.Error!.Code);

        var edicionMismaClave = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: 1,
            Clave: "mat-old",
            Descripcion: "Edición de la misma fila",
            Unidad: "pza",
            PrecioUnitario: 7m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: null));

        if (!edicionMismaClave.IsSuccess) Assert.Fail(edicionMismaClave.Error!.Message);
        using var maestro = new SOPRO.Data.Context.SOPROContext(masterDbPath);
        Assert.AreEqual(1, maestro.Materiales.Count());
        Assert.AreEqual("MAT-OLD", maestro.Materiales.Single().Clave);
    }

    [TestMethod]
    public async Task SaveMaterial_DespuesDeRollback_NoPersisteValoresFallidos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var fallido = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: scenario.Cemento.Clave,
            Descripcion: scenario.Cemento.Descripcion,
            Unidad: scenario.Cemento.Unidad,
            PrecioUnitario: decimal.MaxValue,
            Notas: scenario.Cemento.Notas ?? string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));
        Assert.IsFalse(fallido.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, fallido.Error!.Code);

        // Operación posterior sobre el mismo contexto compartido: el tracker no
        // debe conservar las entidades mutadas por la operación fallida.
        var limpio = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: null,
            Clave: "MAT-LIMPIO",
            Descripcion: "Operación posterior",
            Unidad: "pza",
            PrecioUnitario: 5m,
            Notas: string.Empty,
            SaveToMaster: false,
            ProjectId: scenario.Proyecto.Id));
        if (!limpio.IsSuccess) Assert.Fail(limpio.Error!.Message);

        context.ChangeTracker.Clear();
        Assert.AreEqual(60m, context.Materiales.AsNoTracking().Single(m => m.Id == scenario.Cemento.Id).PrecioUnitario,
            "El contexto compartido no debe arrastrar el precio fallido a la base.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task SaveMaterial_ActualizaAgrupadoresPorOrdenYNivel_TrasPropagacion()
    {
        // La jerarquía legacy se infiere por Orden/Nivel (la persistencia no
        // establece PadreId): Capitulo (Nivel 0) contiene a Subcapitulo (Nivel 1)
        // y a sus hojas. Los totales previos de un agrupador deben sustituirse
        // por la suma real de su bloque — nunca ponerse en cero por no tener
        // PadreId, ni conservarse obsoletos.
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        scenario.Concepto.Nivel = 5;  // hoja legacy ("Concepto")
        scenario.Concepto.Orden = 2;
        context.SaveChanges();

        var capitulo = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "CAP-1",
            Descripcion = "Capítulo 1",
            Unidad = string.Empty,
            Cantidad = 1m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 999999.99m,
            PrecioUnitario = 0m,
            ImporteTotal = 999999.99m,
            Nivel = 0,
            Orden = 0,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(capitulo);

        var subcapitulo = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "SUB-1",
            Descripcion = "Subcapítulo 1",
            Unidad = string.Empty,
            Cantidad = 1m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 500m,
            PrecioUnitario = 0m,
            ImporteTotal = 500m,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(subcapitulo);

        var sinHijos = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "CAP-VACIO",
            Descripcion = "Capítulo sin hojas",
            Unidad = string.Empty,
            Cantidad = 1m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 500m,
            PrecioUnitario = 0m,
            ImporteTotal = 500m,
            Nivel = 0,
            Orden = 3,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(sinHijos);
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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

        context.ChangeTracker.Clear();
        var capituloPersistido = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "CAP-1");
        Assert.AreEqual(1100.00m, capituloPersistido.CostoDirectoTotal,
            "El capítulo debe sumar los totales de las hojas de su bloque (nunca quedar en cero).");
        Assert.AreEqual(1100.00m, capituloPersistido.ImporteTotal);

        var subPersistido = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "SUB-1");
        Assert.AreEqual(1100.00m, subPersistido.CostoDirectoTotal,
            "El subcapítulo debe sumar las hojas de su bloque, aunque el capítulo lo preceda.");
        Assert.AreEqual(1100.00m, subPersistido.ImporteTotal);

        var vacio = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "CAP-VACIO");
        Assert.AreEqual(0m, vacio.CostoDirectoTotal,
            "Un agrupador sin hojas debe quedar en cero, no con totales obsoletos.");
        Assert.AreEqual(0m, vacio.ImporteTotal);
    }

    [TestMethod]
    public async Task SaveMaterial_PropagacionNoCruzaProyectos()
    {
        // Un material de P1 referenciado por una matriz de P2 (colisión de Ids
        // entre bases): guardar el material solo debe recalcular matrices del
        // proyecto de la sesión, nunca las de otro proyecto.
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var matrizP2 = new Matriz
        {
            ProyectoId = otroProyecto.Id,
            Clave = "APU-P2",
            Descripcion = "Matriz de otro proyecto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
            CostoDirecto = 0m
        };
        context.Matrices.Add(matrizP2);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizP2.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

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

        context.ChangeTracker.Clear();
        Assert.AreEqual(110.00m,
            context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "La matriz del proyecto de la sesión debe recalcularse.");
        Assert.AreEqual(0m,
            context.Matrices.AsNoTracking().Single(m => m.Id == matrizP2.Id).CostoDirecto,
            "La matriz de otro proyecto no debe recalcularse.");
        Assert.AreEqual(1,
            context.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == matrizP2.Id),
            "El componente de la matriz ajena no debe modificarse.");
    }

    [TestMethod]
    public async Task DeleteMaterial_NoEliminaComponentesDeOtroProyecto()
    {
        // El material de P1 está referenciado por una matriz de P2: eliminar el
        // material sin tocar datos ajenos es imposible (FK Restrict + aislamiento
        // por proyecto), así que la operación se rechaza con Conflict sin
        // modificar NADA: ni el componente ajeno, ni el propio, ni las matrices.
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var matrizP2 = new Matriz
        {
            ProyectoId = otroProyecto.Id,
            Clave = "APU-P2",
            Descripcion = "Matriz de otro proyecto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
            CostoDirecto = 0m
        };
        context.Matrices.Add(matrizP2);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizP2.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteMaterial().Execute(session, new DeleteMaterialRequest(scenario.Cemento.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.AreEqual(1,
            context.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == matrizP2.Id),
            "El componente de la matriz de otro proyecto debe sobrevivir.");
        Assert.AreEqual(5,
            context.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == scenario.MatrizApu.Id),
            "El componente del proyecto de la sesión también debe sobrevivir (rollback).");
        Assert.IsNotNull(context.Materiales.AsNoTracking().SingleOrDefault(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(100.00m,
            context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "Ninguna matriz debe recalcularse.");
    }

    [TestMethod]
    public async Task PreviewMaterialDeletion_NoCuentaComponentesDeOtroProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var matrizP2 = new Matriz
        {
            ProyectoId = otroProyecto.Id,
            Clave = "APU-P2",
            Descripcion = "Matriz de otro proyecto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
            CostoDirecto = 0m
        };
        context.Matrices.Add(matrizP2);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizP2.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new PreviewMaterialDeletion().Execute(
            session, new PreviewMaterialDeletionRequest(scenario.Cemento.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.ComponentCount,
            "El preview debe reflejar el alcance real de la eliminación (solo el proyecto de la sesión).");
        Assert.AreEqual(1, result.Value.Matrices.Count);
        Assert.AreEqual(scenario.MatrizApu.Id, result.Value.Matrices[0].MatrizId);
        Assert.AreEqual(1, result.Value.CrossProjectReferenceCount,
            "El preview debe advertir las referencias externas que bloquean la eliminación.");
    }

    [TestMethod]
    public async Task SaveMaterial_GuardarEnMaestroDosVeces_ReutilizaLaMismaFilaMaestra()
    {
        // Pregunta abierta del dictamen: al crear una fila maestra nueva desde un
        // material de proyecto se persiste la asociación (MaterialMaestroId), de
        // modo que un segundo guardado con otra clave edita la MISMA fila en
        // lugar de crear una copia independiente.
        var masterDbPath = TestDbFactory.CreateTempDbPath();

        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath, masterDbPath);

        var primero = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: "MAT-MAESTRO-1",
            Descripcion: "Primer guardado en maestro",
            Unidad: "pza",
            PrecioUnitario: 10m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: scenario.Proyecto.Id));

        if (!primero.IsSuccess) Assert.Fail(primero.Error!.Message);
        Assert.IsTrue(primero.Value!.IsNew);

        var segundo = await new SaveMaterial().Execute(session, new SaveMaterialRequest(
            MaterialId: scenario.Cemento.Id,
            Clave: "MAT-MAESTRO-2",
            Descripcion: "Segundo guardado en maestro",
            Unidad: "pza",
            PrecioUnitario: 20m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: scenario.Proyecto.Id));

        if (!segundo.IsSuccess) Assert.Fail(segundo.Error!.Message);
        Assert.IsFalse(segundo.Value!.IsNew);
        Assert.AreEqual(primero.Value.MaterialId, segundo.Value.MaterialId,
            "El segundo guardado debe editar la fila maestra creada por el primero.");

        context.ChangeTracker.Clear();
        Assert.AreEqual(primero.Value.MaterialId,
            context.Materiales.AsNoTracking().Single(m => m.Id == scenario.Cemento.Id).MaterialMaestroId,
            "La asociación maestra debe persistirse en el material local.");

        using var maestro = new SOPRO.Data.Context.SOPROContext(masterDbPath);
        Assert.AreEqual(1, maestro.Materiales.Count(),
            "No debe crearse una fila maestra por cada guardado.");
        Assert.AreEqual("MAT-MAESTRO-2", maestro.Materiales.Single().Clave);
    }

    // ---------- FindMatricesUsingMaterial ----------

    [TestMethod]
    public async Task FindMatricesUsingMaterial_ImportadoEnUso_DevuelveMatricesDelProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // Copia importada del maestro: conserva Origen=Maestro pero pertenece al
        // proyecto (visible y usable desde él).
        var importado = new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-IMP",
            Descripcion = "Copia importada del maestro",
            Unidad = "pza",
            PrecioUnitario = 2m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro,
            MaterialMaestroId = 1
        };
        context.Materiales.Add(importado);
        context.SaveChanges();

        scenario.MatrizApu.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = scenario.MatrizApu.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = importado.Id,
            Cantidad = 1m,
            Orden = 99,
            Notas = string.Empty
        });
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new FindMatricesUsingMaterial().Execute(
            session, new FindMatricesUsingMaterialRequest(importado.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.Count);
        Assert.AreEqual(scenario.MatrizApu.Id, result.Value[0].MatrizId);
        Assert.AreEqual("APU-001", result.Value[0].Clave);
    }

    [TestMethod]
    public async Task FindMatricesUsingMaterial_SinUso_DevuelveVacio()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new FindMatricesUsingMaterial().Execute(
            session, new FindMatricesUsingMaterialRequest(9999));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
    }

    [TestMethod]
    public async Task FindMatricesUsingMaterial_MaterialDeOtroProyecto_FallaNotFound()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var ajeno = new Material
        {
            ProyectoId = otroProyecto.Id,
            Clave = "MAT-AJENO",
            Descripcion = "Material de otro proyecto",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(ajeno);
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new FindMatricesUsingMaterial().Execute(
            session, new FindMatricesUsingMaterialRequest(ajeno.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
    }

    [TestMethod]
    public async Task FindMatricesUsingMaterial_MatrizDeOtroProyecto_NoSeFiltraHaciaAdentro()
    {
        // El esquema no garantiza que un Id de matriz referencie el mismo
        // proyecto: la consulta debe restringir las matrices a la sesión.
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = CrearOtroProyecto(context);
        var materialLocal = new Material
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "MAT-LOCAL",
            Descripcion = "Material del proyecto",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.Materiales.Add(materialLocal);
        context.SaveChanges();

        var matrizAjeno = new Matriz
        {
            ProyectoId = otroProyecto.Id,
            Clave = "APU-AJENO",
            Descripcion = "Matriz de otro proyecto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizAjeno);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizAjeno.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materialLocal.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        using var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new FindMatricesUsingMaterial().Execute(
            session, new FindMatricesUsingMaterialRequest(materialLocal.Id));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(0, result.Value!.Count,
            "Las matrices de otro proyecto no deben aparecer por colisión de Ids.");
    }

    // ---------- MaterialCatalogExportResolver (PDF headless) ----------

    [TestMethod]
    public void MaterialCatalogExportResolver_ResuelveValoresParaElPdf()
    {
        var material = new MaterialListItem(
            1,
            "MAT-CEM",
            "Cemento gris",
            "kg",
            60m,
            string.Empty,
            OrigenInsumo.Proyecto);

        var columnaClave = new ColumnaMaterial { ProyectoId = 1, Nombre = "Clave", NombreInterno = "Clave", NombreFuente = "Clave" };
        var columnaPrecio = new ColumnaMaterial { ProyectoId = 1, Nombre = "Precio", NombreInterno = "PrecioUnitario", NombreFuente = "PrecioUnitario" };
        var columnaOrigen = new ColumnaMaterial { ProyectoId = 1, Nombre = "Origen", NombreInterno = "Origen", NombreFuente = "Origen" };

        Assert.AreEqual("MAT-CEM", MaterialCatalogExportResolver.ResolveValue(material, columnaClave));
        Assert.AreEqual("Cemento gris", MaterialCatalogExportResolver.ResolveValue(material, new ColumnaMaterial { ProyectoId = 1, Nombre = "D", NombreInterno = "Descripcion", NombreFuente = "D" }));
        Assert.AreEqual("kg", MaterialCatalogExportResolver.ResolveValue(material, new ColumnaMaterial { ProyectoId = 1, Nombre = "U", NombreInterno = "Unidad", NombreFuente = "U" }));
        Assert.AreEqual("60.0000", MaterialCatalogExportResolver.ResolveValue(material, columnaPrecio));
        Assert.AreEqual("Proyecto", MaterialCatalogExportResolver.ResolveValue(material, columnaOrigen));

        var maestro = new MaterialListItem(2, "M-1", "Maestro", "pza", 1m, string.Empty, OrigenInsumo.Maestro);
        Assert.AreEqual("Maestro", MaterialCatalogExportResolver.ResolveOrigin(maestro));
    }

    private static Proyecto CrearOtroProyecto(SOPRO.Data.Context.SOPROContext context)
    {
        var otroProyecto = new Proyecto
        {
            Nombre = "Otro proyecto",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 2, 1),
            FechaTermino = new DateTime(2026, 2, 28),
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
        return otroProyecto;
    }
}