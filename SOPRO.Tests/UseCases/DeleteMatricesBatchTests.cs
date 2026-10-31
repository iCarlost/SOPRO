using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.UseCases.Matrices;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.UseCases;

/// <summary>
/// Issue #58: eliminación de matrices como UNA operación lógica. La selección se
/// valida completa antes de escribir; las aristas auxiliares externas se eliminan
/// con el lote y sus padres se recalculan, mientras una fila de presupuesto que
/// referencia directamente una matriz seleccionada sigue bloqueando la operación.
/// SQLite real con el esquema de producción.
/// </summary>
[TestClass]
public class DeleteMatricesBatchTests
{
    private static readonly IProjectDbContextFactory Fabrica = new ProjectDbContextFactory();

    // ---------- Eliminación sin dependencias ----------

    [TestMethod]
    public async Task DeleteMatricesBatch_MatrizSinUso_EliminaMatrizYComponentes()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var basico = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE", TipoMatriz.Basico);
        context.Matrices.Add(basico);
        context.SaveChanges();
        context.ComponentesMatriz.Add(NuevoComponenteMaterial(basico.Id, scenario.Cemento.Id));
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        CollectionAssert.AreEqual(new[] { basico.Id }, result.Value!.DeletedIds.ToArray());
        Assert.AreEqual(1, result.Value!.DeletedComponents);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Id == basico.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MatrizId == basico.Id),
            "Los componentes de la matriz eliminada no deben quedar huérfanos.");
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id),
            "Eliminar una matriz no debe tocar el catálogo de insumos.");
        Assert.IsTrue(context.Matrices.Any(m => m.Id == scenario.MatrizApu.Id));
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_LoteSinUso_EliminaTodasLasMatricesYComponentes()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var basico1 = NuevaMatriz(scenario.Proyecto.Id, "BAS-LOTE-1", TipoMatriz.Basico);
        var basico2 = NuevaMatriz(scenario.Proyecto.Id, "BAS-LOTE-2", TipoMatriz.Cuadrilla);
        context.Matrices.AddRange(basico1, basico2);
        context.SaveChanges();
        context.ComponentesMatriz.AddRange(
            NuevoComponenteMaterial(basico1.Id, scenario.Cemento.Id, orden: 1),
            NuevoComponenteMaterial(basico2.Id, scenario.Cemento.Id, orden: 1));
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico1.Id, basico2.Id, basico1.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        CollectionAssert.AreEqual(
            new[] { basico1.Id, basico2.Id }.OrderBy(id => id).ToArray(),
            result.Value!.DeletedIds.ToArray());
        Assert.AreEqual(2, result.Value!.DeletedComponents);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Id == basico1.Id || m.Id == basico2.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c =>
            c.MatrizId == basico1.Id || c.MatrizId == basico2.Id));
        Assert.AreEqual(5, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == scenario.MatrizApu.Id),
            "La matriz fuera del lote conserva todos sus componentes.");
    }

    // ---------- Referencia como auxiliar ----------

    [TestMethod]
    public async Task DeleteMatricesBatch_ReferenciadaComoAuxiliarFueraDelLote_EliminaAristaYRecalculaPadre()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        var impacto = result.Value!;
        CollectionAssert.AreEqual(new[] { basico.Id }, impacto.DeletedIds.ToArray());
        Assert.AreEqual(1, impacto.DeletedComponents);
        Assert.AreEqual(1, impacto.RemovedExternalAuxiliarComponents,
            "La arista auxiliar externa se elimina con el lote.");
        Assert.AreEqual(1, impacto.RecalculatedMatrixCount);
        Assert.AreEqual(1, impacto.AffectedConceptCount);
        Assert.AreEqual(1, impacto.AffectedAggregatorCount);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Id == basico.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MatrizId == basico.Id));
        Assert.IsTrue(context.Matrices.Any(m => m.Id == apuConAuxiliar.Id),
            "La matriz padre permanece; solo pierde la arista auxiliar.");
        Assert.IsFalse(context.ComponentesMatriz.Any(c =>
                c.MatrizId == apuConAuxiliar.Id && c.TipoComponente == TipoComponenteMatriz.Auxiliar),
            "No debe quedar una referencia auxiliar colgante.");
        Assert.AreEqual(0m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto,
            "El padre se recalcula sin el auxiliar eliminado.");
        Assert.AreEqual(0m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-AUX").CostoDirectoUnitario);
        Assert.AreEqual(1000m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "CAP-1").ImporteTotal,
            "El agrupador conserva el importe de la hoja C-001, que no depende del auxiliar eliminado.");
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_LoteConLibreYUsadaComoAuxiliar_EliminaAmbasYQuitaAristaExterna()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);

        var libre = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE", TipoMatriz.Basico);
        context.Matrices.Add(libre);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico.Id, libre.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        CollectionAssert.AreEqual(
            new[] { basico.Id, libre.Id }.OrderBy(id => id).ToArray(),
            result.Value!.DeletedIds.ToArray());
        Assert.AreEqual(1, result.Value!.RemovedExternalAuxiliarComponents);
        Assert.AreEqual(1, result.Value!.RecalculatedMatrixCount);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Id == basico.Id || m.Id == libre.Id));
        Assert.IsTrue(context.Matrices.Any(m => m.Id == apuConAuxiliar.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c =>
            c.MatrizId == apuConAuxiliar.Id && c.TipoComponente == TipoComponenteMatriz.Auxiliar));
        Assert.AreEqual(0m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_MatrizYDependienteSeleccionados_EliminaAmbas()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearAuxiliar(context, scenario);

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico.Id, apuConAuxiliar.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        Assert.AreEqual(2, result.Value!.DeletedComponents,
            "Se eliminan el componente del básico y la arista auxiliar del APU.");

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Id == basico.Id || m.Id == apuConAuxiliar.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c =>
            c.MatrizId == basico.Id || c.MatrizId == apuConAuxiliar.Id));
        Assert.AreEqual(5, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == scenario.MatrizApu.Id));
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_AuxiliarConAncestroYHoja_RecalculaCierreSinReferenciasColgantes()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuPadre) = CrearCierreAuxiliar(context, scenario);

        var apuAbuelo = NuevaMatriz(scenario.Proyecto.Id, "APU-ABUELO", TipoMatriz.APU);
        apuAbuelo.CostoDirecto = 120m;
        context.Matrices.Add(apuAbuelo);
        context.SaveChanges();

        var aristaAbuelo = NuevoComponenteAuxiliar(apuAbuelo.Id, apuPadre.Id);
        context.ComponentesMatriz.Add(aristaAbuelo);
        context.ConceptosPresupuesto.Add(new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-ABUELO",
            Descripcion = "Concepto sobre el APU abuelo",
            Unidad = "m3",
            Cantidad = 1m,
            MatrizId = apuAbuelo.Id,
            CostoDirectoUnitario = 120m,
            CostoDirectoTotal = 120m,
            PrecioUnitario = 120m,
            ImporteTotal = 120m,
            Nivel = 5,
            Orden = 3,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        });
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        Assert.AreEqual(1, result.Value!.RemovedExternalAuxiliarComponents);
        Assert.AreEqual(2, result.Value!.RecalculatedMatrixCount,
            "El cierre incluye al padre y a su ascendiente auxiliar.");
        Assert.AreEqual(2, result.Value!.AffectedConceptCount);
        Assert.AreEqual(1, result.Value!.AffectedAggregatorCount);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Id == basico.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MatrizId == apuPadre.Id),
            "El padre perdió su única arista auxiliar.");
        Assert.IsTrue(context.Matrices.Any(m => m.Id == apuAbuelo.Id));
        Assert.AreEqual(apuPadre.Id, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.MatrizId == apuAbuelo.Id).AuxiliarId,
            "La arista del abuelo sigue apuntando a una matriz existente.");
        Assert.AreEqual(0m, context.Matrices.AsNoTracking().Single(m => m.Id == apuPadre.Id).CostoDirecto);
        Assert.AreEqual(0m, context.Matrices.AsNoTracking().Single(m => m.Id == apuAbuelo.Id).CostoDirecto);
        Assert.AreEqual(0m, context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "C-AUX").ImporteTotal);
        Assert.AreEqual(0m, context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "C-ABUELO").ImporteTotal);
        Assert.AreEqual(1000m, context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "CAP-1").ImporteTotal,
            "El agrupador conserva la hoja C-001; las hojas del cierre quedan en cero.");
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_FalloEnRecalculo_RevierteBorradoYAristas()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);

        using (var trigger = new SOPROContext(context.DatabasePath))
        {
            trigger.Database.ExecuteSqlRaw(
                "CREATE TRIGGER trg_fail_matriz_update BEFORE UPDATE ON Matrices " +
                "BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        }

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { basico.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.Any(m => m.Id == basico.Id),
            "El rollback restaura la matriz seleccionada.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == basico.Id));
        Assert.AreEqual(basico.Id, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.MatrizId == apuConAuxiliar.Id && c.TipoComponente == TipoComponenteMatriz.Auxiliar)
            .AuxiliarId, "El rollback restaura la arista auxiliar externa.");
        Assert.AreEqual(120m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto,
            "El rollback descarta el recálculo fallido.");
        Assert.AreEqual(120m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-AUX").CostoDirectoUnitario);
    }

    // ---------- Previsualización ----------

    [TestMethod]
    public async Task PreviewMatricesBatchDeletion_AuxiliarExterno_DescribeAristasYRecalculo()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var previewResult = await new PreviewMatricesBatchDeletion(Fabrica).Execute(
            session, new PreviewMatricesBatchDeletionRequest(new[] { basico.Id }));

        if (!previewResult.IsSuccess) Assert.Fail(previewResult.Error!.Message);

        var preview = previewResult.Value!;
        Assert.IsFalse(preview.IsBlocked);
        Assert.AreEqual(1, preview.SelectedCount);
        Assert.AreEqual(1, preview.ExternalAuxiliarComponentCount);
        Assert.AreEqual(1, preview.ExternalAuxiliarMatrixCount);
        CollectionAssert.Contains(preview.RecalculatedMatrixIds.ToArray(), apuConAuxiliar.Id);
        Assert.AreEqual(1, preview.AffectedConceptCount);
        Assert.AreEqual(1, preview.AffectedAggregatorCount);

        var mensaje = preview.BuildConfirmationMessage();
        StringAssert.Contains(mensaje, "componente(s) auxiliar(es)");
        StringAssert.Contains(mensaje, "recalcularán");
    }

    [TestMethod]
    public async Task PreviewMatricesBatchDeletion_ConceptoBloqueante_SeInformaAntesDeConfirmar()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var previewResult = await new PreviewMatricesBatchDeletion(Fabrica).Execute(
            session, new PreviewMatricesBatchDeletionRequest(new[] { scenario.MatrizApu.Id }));

        if (!previewResult.IsSuccess) Assert.Fail(previewResult.Error!.Message);

        var preview = previewResult.Value!;
        Assert.IsTrue(preview.IsBlocked);
        Assert.IsTrue(preview.BlockingConceptIds.Count > 0);

        var mensaje = preview.BuildBlockingMessage();
        StringAssert.Contains(mensaje, "No se pueden eliminar");
        StringAssert.Contains(mensaje, "C-001");
    }

    [TestMethod]
    public async Task PreviewMatricesBatchDeletion_SeleccionMultiple_InformaCantidadSeleccionada()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var libre1 = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE-1", TipoMatriz.Basico);
        var libre2 = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE-2", TipoMatriz.Basico);
        context.Matrices.AddRange(libre1, libre2);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var previewResult = await new PreviewMatricesBatchDeletion(Fabrica).Execute(
            session, new PreviewMatricesBatchDeletionRequest(new[] { libre1.Id, libre2.Id }));

        if (!previewResult.IsSuccess) Assert.Fail(previewResult.Error!.Message);

        var preview = previewResult.Value!;
        Assert.AreEqual(2, preview.SelectedCount);
        Assert.AreEqual(0, preview.ExternalAuxiliarComponentCount);
        StringAssert.Contains(preview.BuildConfirmationMessage(), "2 matrices seleccionadas");
        StringAssert.Contains(preview.BuildConfirmationMessage(), "No se requiere recálculo");
    }

    // ---------- Referencia desde el presupuesto ----------

    [TestMethod]
    public async Task DeleteMatricesBatch_ReferenciadaPorConceptoDePresupuesto_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { scenario.MatrizApu.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);
        StringAssert.Contains(result.Error!.Message, "C-001",
            "El rechazo debe nombrar el concepto que bloquea la eliminación.");

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.Any(m => m.Id == scenario.MatrizApu.Id));
        Assert.AreEqual(5, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == scenario.MatrizApu.Id));
        var concepto = context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(scenario.MatrizApu.Id, concepto.MatrizId,
            "La fila del presupuesto no debe quedar apuntando a una matriz inexistente.");
        Assert.AreEqual(100.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_LoteConUnaReferenciadaPorPresupuesto_RechazaTodoElLote()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var libre = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE", TipoMatriz.Basico);
        context.Matrices.Add(libre);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { libre.Id, scenario.MatrizApu.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.Any(m => m.Id == libre.Id),
            "Una fila del presupuesto rechaza el lote completo: no se elimina la matriz libre.");
        Assert.IsTrue(context.Matrices.Any(m => m.Id == scenario.MatrizApu.Id));
    }

    // ---------- Ids faltantes o de otro proyecto ----------

    [TestMethod]
    public async Task DeleteMatricesBatch_IdInexistente_RechazaElLoteCompleto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var libre = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE", TipoMatriz.Basico);
        context.Matrices.Add(libre);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { libre.Id, 999_999 }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.Any(m => m.Id == libre.Id),
            "Un Id faltante rechaza el lote: no se elimina parcialmente.");
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_MatrizDeOtroProyecto_RechazaElLoteCompleto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var otroProyecto = NuevoProyecto(context, "proyecto_ajeno");
        var ajena = NuevaMatriz(otroProyecto.Id, "BAS-AJENA", TipoMatriz.Basico);
        context.Matrices.Add(ajena);

        var libre = NuevaMatriz(scenario.Proyecto.Id, "BAS-LIBRE", TipoMatriz.Basico);
        context.Matrices.Add(libre);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { libre.Id, ajena.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code,
            "Una matriz de otro proyecto se rechaza igual que un Id inexistente.");

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.Any(m => m.Id == libre.Id),
            "El rechazo por alcance no elimina la matriz propia del lote.");
        Assert.IsTrue(context.Matrices.Any(m => m.Id == ajena.Id));
    }

    // ---------- Validación de entrada ----------

    [TestMethod]
    public async Task DeleteMatricesBatch_LoteVacioOIdInvalido_FallaValidacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var caso = new DeleteMatricesBatch(Fabrica);

        var vacio = await caso.Execute(
            session, new DeleteMatricesBatchRequest(Array.Empty<int>()));
        var invalido = await caso.Execute(
            session, new DeleteMatricesBatchRequest(new[] { 0, scenario.MatrizApu.Id }));

        Assert.IsFalse(vacio.IsSuccess);
        Assert.AreEqual(AppErrorCode.Validation, vacio.Error!.Code);
        Assert.IsFalse(invalido.IsSuccess);
        Assert.AreEqual(AppErrorCode.Validation, invalido.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.Any(m => m.Id == scenario.MatrizApu.Id));
    }

    [TestMethod]
    public async Task DeleteMatricesBatch_SesionMaestro_NoSoportado()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(null, context.DatabasePath);

        var result = await new DeleteMatricesBatch(Fabrica).Execute(
            session, new DeleteMatricesBatchRequest(new[] { scenario.MatrizApu.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotSupported, result.Error!.Code);
    }

    // ---------- Infraestructura ----------

    /// <summary>
    /// Básico con un componente de material y un APU que lo consume como auxiliar.
    /// Ninguno está referenciado por el presupuesto del escenario base.
    /// </summary>
    private static (Matriz Basico, Matriz ApuConAuxiliar) CrearAuxiliar(
        SOPROContext context,
        SoproCalculationScenario scenario)
    {
        var basico = NuevaMatriz(scenario.Proyecto.Id, "BAS-USADO", TipoMatriz.Basico);
        var apuConAuxiliar = NuevaMatriz(scenario.Proyecto.Id, "APU-CON-AUX", TipoMatriz.APU);
        context.Matrices.AddRange(basico, apuConAuxiliar);
        context.SaveChanges();

        context.ComponentesMatriz.Add(NuevoComponenteMaterial(basico.Id, scenario.Cemento.Id));
        context.ComponentesMatriz.Add(NuevoComponenteAuxiliar(apuConAuxiliar.Id, basico.Id));
        context.SaveChanges();

        return (basico, apuConAuxiliar);
    }

    /// <summary>
    /// Cierre auxiliar con costos y presupuesto: básico con cemento, APU que lo
    /// consume como auxiliar, hoja C-AUX y agrupador CAP-1.
    /// </summary>
    private static (Matriz Basico, Matriz ApuConAuxiliar) CrearCierreAuxiliar(
        SOPROContext context,
        SoproCalculationScenario scenario)
    {
        var basico = NuevaMatriz(scenario.Proyecto.Id, "BAS-1", TipoMatriz.Basico);
        basico.CostoDirecto = 60m;
        var apuConAuxiliar = NuevaMatriz(scenario.Proyecto.Id, "APU-AUX", TipoMatriz.APU);
        apuConAuxiliar.CostoDirecto = 120m;
        context.Matrices.AddRange(basico, apuConAuxiliar);
        context.SaveChanges();

        context.ComponentesMatriz.Add(NuevoComponenteMaterial(basico.Id, scenario.Cemento.Id));
        var arista = NuevoComponenteAuxiliar(apuConAuxiliar.Id, basico.Id);
        arista.Cantidad = 2m;
        context.ComponentesMatriz.Add(arista);
        context.ConceptosPresupuesto.Add(NuevoAgrupador(scenario.Proyecto.Id, "CAP-1", nivel: 0, orden: 0));
        context.ConceptosPresupuesto.Add(new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-AUX",
            Descripcion = "Concepto sobre el APU con básico",
            Unidad = "m3",
            Cantidad = 1m,
            MatrizId = apuConAuxiliar.Id,
            CostoDirectoUnitario = 120m,
            CostoDirectoTotal = 120m,
            PrecioUnitario = 120m,
            ImporteTotal = 120m,
            Nivel = 5,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        });
        context.SaveChanges();

        return (basico, apuConAuxiliar);
    }

    private static ConceptoPresupuesto NuevoAgrupador(int proyectoId, string clave, int nivel, int orden) => new()
    {
        ProyectoId = proyectoId,
        Clave = clave,
        Descripcion = "Agrupador de prueba",
        Unidad = string.Empty,
        Cantidad = 1m,
        MatrizId = null,
        CostoDirectoUnitario = 0m,
        CostoDirectoTotal = 0m,
        PrecioUnitario = 0m,
        ImporteTotal = 0m,
        Nivel = nivel,
        Orden = orden,
        EsAgrupador = true,
        ColumnasPersonalizadasJSON = string.Empty,
        Notas = string.Empty
    };

    private static Matriz NuevaMatriz(int proyectoId, string clave, TipoMatriz tipo) => new()
    {
        ProyectoId = proyectoId,
        Clave = clave,
        Descripcion = "Matriz de prueba",
        Unidad = "m3",
        Tipo = tipo,
        CostoDirecto = 0m,
        Notas = string.Empty
    };

    private static ComponenteMatriz NuevoComponenteMaterial(int matrizId, int materialId, int orden = 1) => new()
    {
        MatrizId = matrizId,
        TipoComponente = TipoComponenteMatriz.Material,
        MaterialId = materialId,
        Cantidad = 1m,
        Orden = orden,
        Notas = string.Empty
    };

    private static ComponenteMatriz NuevoComponenteAuxiliar(int matrizId, int auxiliarId, int orden = 1) => new()
    {
        MatrizId = matrizId,
        TipoComponente = TipoComponenteMatriz.Auxiliar,
        AuxiliarId = auxiliarId,
        Cantidad = 1m,
        Orden = orden,
        Notas = string.Empty
    };

    private static ProjectSessionInfo CrearSession(int? proyectoId, string databasePath)
    {
        if (proyectoId.HasValue)
        {
            using var lectura = new SOPROContext(databasePath);
            var proyecto = lectura.Proyectos.Find(proyectoId.Value);
            return ProjectSessionInfo.Create(
                proyecto != null
                    ? ProjectRef.FromEntity(proyecto)
                    : new ProjectRef(proyectoId.Value, string.Empty),
                databasePath,
                decimalesImporte: proyecto?.DecimalesImporte);
        }

        return ProjectSessionInfo.Create(ProjectRef.Master, databasePath);
    }

    private static Proyecto NuevoProyecto(SOPROContext context, string nombre)
    {
        var proyecto = new Proyecto
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 360,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }
}
