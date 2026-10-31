using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.UseCases;

/// <summary>
/// Issue #58: eliminación en lote del catálogo (materiales, mano de obra,
/// herramientas y maquinaria) como UNA operación lógica: contexto nuevo, una
/// transacción, validación exacta de Ids y alcance, rechazo completo ante
/// referencias externas y recálculo del cierre confirmado en el mismo commit.
/// SQLite real con el esquema de producción.
/// </summary>
[TestClass]
public class DeleteCatalogBatchTests
{
    private static readonly IProjectDbContextFactory Fabrica = new ProjectDbContextFactory();

    // ---------- Materiales ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_MaterialesEnUso_EliminaComponentesYRecalculaPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        // Id repetido: el lote trabaja sobre el conjunto exacto y no duplica impacto.
        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id, scenario.Cemento.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        var impacto = result.Value!;
        CollectionAssert.AreEqual(new[] { scenario.Cemento.Id }, impacto.DeletedIds.ToArray());
        Assert.AreEqual(1, impacto.DeletedComponents);
        Assert.AreEqual(1, impacto.AffectedMatrixCount);
        Assert.AreEqual(1, impacto.RecalculatedMatrixCount);
        Assert.AreEqual(1, impacto.AffectedConceptCount);
        Assert.AreEqual(0, impacto.AffectedAggregatorCount);
        Assert.IsTrue(impacto.TriggeredRecalculation);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(40.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        var concepto = context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(40.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(400.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_MaterialSinUso_EliminaSinRecalcular()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var soltero = NuevoMaterial(scenario.Proyecto.Id, "MAT-SOLO");
        context.Materiales.Add(soltero);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, new[] { soltero.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(0, result.Value!.DeletedComponents);
        Assert.AreEqual(0, result.Value!.AffectedMatrixCount);
        Assert.AreEqual(0, result.Value!.RecalculatedMatrixCount);
        Assert.IsFalse(result.Value!.TriggeredRecalculation);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Materiales.Any(m => m.Id == soltero.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    // ---------- Mano de obra, herramientas y maquinaria ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_ManoDeObraEnUso_EliminaComponenteYRecalcula()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.ManoDeObra, new[] { scenario.Oficial.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.DeletedComponents);
        Assert.AreEqual(1, result.Value!.RecalculatedMatrixCount);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.ManoDeObra.Any(m => m.Id == scenario.Oficial.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.ManoDeObraId == scenario.Oficial.Id));
        // Sin la MO normal, la base de mano de obra es cero: el cabo %MO y la
        // herramienta %MO también quedan en cero. 60 (material) + 4 (maquinaria).
        Assert.AreEqual(64.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HerramientaEnUso_EliminaComponenteYRecalcula()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Herramienta, new[] { scenario.HerramientaMenorPorcentajeMo.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.DeletedComponents);
        Assert.AreEqual(1, result.Value!.RecalculatedMatrixCount);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Herramientas.Any(h => h.Id == scenario.HerramientaMenorPorcentajeMo.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.HerramientaId == scenario.HerramientaMenorPorcentajeMo.Id));
        // Herramienta %MO: 10% de la base de MO (30) = 3. 100 - 3.
        Assert.AreEqual(97.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_MaquinariaEnUso_EliminaComponenteYRecalcula()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Maquinaria, new[] { scenario.Revolvedora.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);
        Assert.AreEqual(1, result.Value!.DeletedComponents);
        Assert.AreEqual(1, result.Value!.RecalculatedMatrixCount);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Maquinaria.Any(m => m.Id == scenario.Revolvedora.Id));
        Assert.IsFalse(context.ComponentesMatriz.Any(c => c.MaquinariaId == scenario.Revolvedora.Id));
        // 100 - 4 (costo horario).
        Assert.AreEqual(96.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    // ---------- Matriz compartida ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_MatrizCompartida_EliminaTodosLosComponentesYRecalculaUnaSolaVez()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        // El cemento aparece una sola vez en APU-001: la matriz se recuenta una vez.
        Assert.AreEqual(1, result.Value!.AffectedMatrixCount);
        Assert.AreEqual(1, result.Value!.RecalculatedMatrixCount);

        var lote = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.ManoDeObra,
                new[] { scenario.Oficial.Id, scenario.CaboPorcentajeMo.Id }));

        if (!lote.IsSuccess) Assert.Fail(lote.Error!.Message);
        Assert.AreEqual(2, lote.Value!.DeletedComponents);
        Assert.AreEqual(1, lote.Value!.AffectedMatrixCount,
            "Dos componentes de la MISMA matriz no deben contarla dos veces.");
        Assert.AreEqual(1, lote.Value!.RecalculatedMatrixCount);

        context.ChangeTracker.Clear();
        var matriz = context.Matrices.AsNoTracking().Single(m => m.Id == scenario.MatrizApu.Id);
        // Sin material ni mano de obra, la base de MO es cero y la herramienta %MO
        // también: solo queda la maquinaria (4).
        Assert.AreEqual(4.00m, matriz.CostoDirecto);
    }

    // ---------- Cierre auxiliar y presupuesto ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_CierreAuxiliar_RecalculaAuxiliaresHojasYAgrupadores()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, new[] { scenario.Cemento.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        var impacto = result.Value!;
        Assert.AreEqual(2, impacto.AffectedMatrixCount,
            "APU-001 y el básico contienen directamente el cemento.");
        Assert.AreEqual(3, impacto.RecalculatedMatrixCount,
            "El cierre incluye los orígenes más el APU ascendiente del básico, sin repetir.");
        Assert.AreEqual(2, impacto.AffectedConceptCount,
            "C-001 (APU-001) y C-AUX (APU-AUX) son hojas afectadas.");
        Assert.AreEqual(1, impacto.AffectedAggregatorCount);

        context.ChangeTracker.Clear();
        Assert.AreEqual(0m, context.Matrices.AsNoTracking().Single(m => m.Id == basico.Id).CostoDirecto);
        Assert.AreEqual(0m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto);
        Assert.AreEqual(40.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);

        var hojaAuxiliarPersistida = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "C-AUX");
        Assert.AreEqual(0m, hojaAuxiliarPersistida.CostoDirectoUnitario);
        Assert.AreEqual(0m, hojaAuxiliarPersistida.ImporteTotal);

        var capituloPersistido = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Clave == "CAP-1");
        Assert.AreEqual(400.00m, capituloPersistido.CostoDirectoTotal,
            "El agrupador debe reflejar solo la hoja restante (C-001 sin cemento).");
        Assert.AreEqual(400.00m, capituloPersistido.ImporteTotal);
    }

    // ---------- Previsualización (solo lectura) ----------

    [TestMethod]
    public async Task PreviewCatalogBatchDeletion_CuentaSeleccionEImpactoAntesDeEliminar()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        // Id repetido: la previsualización informa el conjunto exacto (1 recurso).
        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id, scenario.Cemento.Id }));

        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        var vista = preview.Value!;
        Assert.AreEqual(1, vista.SelectedCount, "La confirmación muestra el número de recursos seleccionados.");
        Assert.AreEqual(1, vista.AffectedComponentCount);
        Assert.AreEqual(1, vista.AffectedMatrixCount);
        Assert.AreEqual(1, vista.RecalculatedMatrixCount);
        Assert.AreEqual(1, vista.AffectedConceptCount);
        Assert.AreEqual(0, vista.AffectedAggregatorCount);
        Assert.IsTrue(vista.IsInUse);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vista.Fingerprint));
        Assert.AreEqual(64, vista.Fingerprint.Length, "La huella es SHA-256 en hexadecimal.");

        // Solo lectura: ni el catálogo, ni los componentes, ni el presupuesto cambian.
        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(100.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, concepto.CostoDirectoTotal);

        // Con la huella vigente, el comando confirma exactamente el impacto previsualizado.
        var execute = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                vista.Fingerprint));

        if (!execute.IsSuccess) Assert.Fail(execute.Error!.Message);
        Assert.AreEqual(vista.SelectedCount, execute.Value!.DeletedIds.Count);
        Assert.AreEqual(vista.AffectedComponentCount, execute.Value!.DeletedComponents);
        Assert.AreEqual(vista.AffectedMatrixCount, execute.Value!.AffectedMatrixCount);
        Assert.AreEqual(vista.RecalculatedMatrixCount, execute.Value!.RecalculatedMatrixCount);
        Assert.AreEqual(vista.AffectedConceptCount, execute.Value!.AffectedConceptCount);
        Assert.AreEqual(vista.AffectedAggregatorCount, execute.Value!.AffectedAggregatorCount);
    }

    [TestMethod]
    public async Task PreviewCatalogBatchDeletion_CierreAuxiliar_CuentaComponentesMatricesYPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));

        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        var vista = preview.Value!;
        Assert.AreEqual(1, vista.SelectedCount);
        Assert.AreEqual(2, vista.AffectedComponentCount,
            "El cemento se usa en APU-001 y en el básico.");
        Assert.AreEqual(2, vista.AffectedMatrixCount);
        Assert.AreEqual(3, vista.RecalculatedMatrixCount,
            "El cierre incluye el APU ascendiente del básico, sin repetir.");
        Assert.AreEqual(2, vista.AffectedConceptCount,
            "C-001 (APU-001) y C-AUX (APU-AUX) son hojas afectadas.");
        Assert.AreEqual(1, vista.AffectedAggregatorCount);

        // Solo lectura: el agrupador conserva su valor de creación (0) y los costos
        // almacenados no se recalculan al previsualizar.
        context.ChangeTracker.Clear();
        Assert.AreEqual(0m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "CAP-1").CostoDirectoTotal);
        Assert.AreEqual(60m, context.Matrices.AsNoTracking().Single(m => m.Id == basico.Id).CostoDirecto);
        Assert.AreEqual(120m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task PreviewCatalogBatchDeletion_IdInexistente_FallaNotFoundSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id, 999_999 }));

        Assert.IsFalse(preview.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, preview.Error!.Code);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
    }

    [TestMethod]
    public async Task PreviewCatalogBatchDeletion_ReferenciaExterna_FallaConflictoSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = NuevoProyecto(context, "proyecto_ajeno");
        var matrizAjena = new Matriz
        {
            ProyectoId = otroProyecto.Id,
            Clave = "APU-P2",
            Descripcion = "Matriz de otro proyecto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizAjena);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizAjena.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));

        Assert.IsFalse(preview.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, preview.Error!.Code);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
    }

    // ---------- Huella de confirmación (previsualización obsoleta) ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorNuevaDependencia_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Cambio concurrente: otra matriz del proyecto empieza a consumir el insumo.
        var matrizNueva = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "APU-NUEVA",
            Descripcion = "Matriz creada después de la confirmación",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizNueva);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizNueva.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id),
            "Una confirmación obsoleta no debe eliminar nada.");
        Assert.AreEqual(2, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id),
            "Los componentes nuevos y el original deben seguir intactos.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(0m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == matrizNueva.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorNuevaHojaDePresupuesto_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Cambio concurrente en el presupuesto: una hoja nueva referencia la matriz
        // afectada, así que el impacto confirmado ya no describe lo que se tocaría.
        var hojaNueva = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-NUEVA",
            Descripcion = "Hoja creada después de la confirmación",
            Unidad = "m3",
            Cantidad = 1m,
            MatrizId = scenario.MatrizApu.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 100m,
            PrecioUnitario = 100m,
            ImporteTotal = 100m,
            Nivel = 5,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(hojaNueva);
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(100.00m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-NUEVA").ImporteTotal);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorAgrupadorSustituido_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        CrearCierreAuxiliar(context, scenario);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);
        Assert.AreEqual(1, preview.Value!.AffectedAggregatorCount);

        // Sustitución concurrente: el proyecto conserva UN agrupador, pero es otra
        // fila. El conteo no cambia; la identidad del estado recalculado sí.
        var capitulo = context.ConceptosPresupuesto.Single(c => c.Clave == "CAP-1");
        context.ConceptosPresupuesto.Remove(capitulo);
        context.ConceptosPresupuesto.Add(NuevoAgrupador(scenario.Proyecto.Id, "CAP-2", nivel: 0, orden: 0));
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(2, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id),
            "Ningún componente (APU-001 ni el básico) debe eliminarse.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(0m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "CAP-2").CostoDirectoTotal,
            "El agrupador sustituto no debe recalcularse.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorHojaSinMatriz_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Hoja manual (sin matriz) agregada al proyecto: no es una hoja "afectada"
        // por el cierre, pero alimenta el total de los agrupadores que la propagación
        // recalcula. La confirmación deja de describir el estado que se tocaría.
        var hojaSinMatriz = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-MANUAL",
            Descripcion = "Hoja manual sin matriz",
            Unidad = "pza",
            Cantidad = 1m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 50m,
            PrecioUnitario = 50m,
            ImporteTotal = 50m,
            Nivel = 5,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(hojaSinMatriz);
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(50.00m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-MANUAL").ImporteTotal);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorHojaReasignadaAMatrizDelCierre_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (_, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Reasignación concurrente: la hoja afectada pasa de APU-001 a otra matriz
        // del MISMO cierre. Los Ids no cambian; el costo que se propagará sí.
        var hoja = context.ConceptosPresupuesto.Single(c => c.Clave == "C-001");
        hoja.MatrizId = apuConAuxiliar.Id;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(2, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(apuConAuxiliar.Id, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-001").MatrizId,
            "La reasignación no debe revertirse ni recalcularse.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(120.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorReordenarBloqueDeAgrupador_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        CrearCierreAuxiliar(context, scenario);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Reordenamiento concurrente dentro del bloque del agrupador: cambia qué
        // hojas suma el agrupador que la propagación recalcula.
        var hojaAuxiliar = context.ConceptosPresupuesto.Single(c => c.Clave == "C-AUX");
        hojaAuxiliar.Orden = 0;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(2, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(0, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-AUX").Orden,
            "El reordenamiento no debe revertirse.");
        Assert.AreEqual(0m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "CAP-1").CostoDirectoTotal,
            "El agrupador no debe recalcularse con el orden nuevo.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorImporteDeHojaSinMatriz_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // Hoja manual (sin matriz) que ya existe al previsualizar: alimenta el total
        // de los agrupadores aunque no sea una hoja "afectada" por el cierre.
        var hojaManual = new ConceptoPresupuesto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "C-MANUAL",
            Descripcion = "Hoja manual sin matriz",
            Unidad = "pza",
            Cantidad = 1m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 50m,
            PrecioUnitario = 50m,
            ImporteTotal = 50m,
            Nivel = 5,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(hojaManual);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Edición concurrente del importe de una fila que no referencia el cierre.
        hojaManual.ImporteTotal = 80m;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(80.00m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Clave == "C-MANUAL").ImporteTotal,
            "El importe editado no debe recalcularse ni revertirse.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorCambioDePorcentajesDelProyecto_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Cambio concurrente de los porcentajes que alimentan el PrecioUnitario
        // recalculado, sin cambiar ninguna fila del presupuesto.
        scenario.Proyecto.PorcentajeUtilidad = 10m;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        var concepto = context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(100.00m, concepto.PrecioUnitario,
            "El precio unitario no debe recalcularse con el porcentaje nuevo.");
        Assert.AreEqual(1000.00m, concepto.ImporteTotal);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorMoverComponenteEntreMatricesAfectadas_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // Dos matrices ya afectadas por el lote: APU-001 con dos componentes del
        // material y APU-002 con uno.
        var apu2 = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "APU-002",
            Descripcion = "Segunda matriz afectada",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 40m,
            Notas = string.Empty
        };
        context.Matrices.Add(apu2);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = apu2.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = scenario.MatrizApu.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 2m,
            Orden = 6,
            Notas = string.Empty
        });
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);
        Assert.AreEqual(3, preview.Value!.AffectedComponentCount);
        Assert.AreEqual(2, preview.Value!.AffectedMatrixCount);

        // Mover un componente seleccionado de APU-001 a APU-002: los conjuntos de Ids
        // de componentes, matrices directas y cierre NO cambian; la asociación sí.
        var movido = context.ComponentesMatriz.Single(c =>
            c.MatrizId == scenario.MatrizApu.Id
            && c.MaterialId == scenario.Cemento.Id
            && c.Cantidad == 2m);
        movido.MatrizId = apu2.Id;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(3, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id),
            "Ningún componente del material debe eliminarse.");
        Assert.AreEqual(apu2.Id, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.Id == movido.Id).MatrizId,
            "El movimiento no debe revertirse ni recalcularse.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(40.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apu2.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorRepuntarAristaAuxiliar_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basico, apuConAuxiliar) = CrearCierreAuxiliar(context, scenario);

        // Segundo básico que también consume el material: ambos quedan en el cierre.
        var basico2 = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "BAS-2",
            Descripcion = "Segundo básico con cemento",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 60m,
            Notas = string.Empty
        };
        context.Matrices.Add(basico2);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = basico2.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);
        Assert.AreEqual(3, preview.Value!.AffectedComponentCount);
        Assert.AreEqual(3, preview.Value!.AffectedMatrixCount);
        Assert.AreEqual(4, preview.Value!.RecalculatedMatrixCount);

        // Repuntar la arista auxiliar de APU-AUX: de BAS-1 a BAS-2. Ambos básicos ya
        // están en el cierre, así que el conjunto de Ids del cierre no cambia.
        var arista = context.ComponentesMatriz.Single(c =>
            c.MatrizId == apuConAuxiliar.Id && c.TipoComponente == TipoComponenteMatriz.Auxiliar);
        arista.AuxiliarId = basico2.Id;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(3, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(basico2.Id, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.Id == arista.Id).AuxiliarId,
            "La arista repuntada no debe revertirse.");
        Assert.AreEqual(60.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == basico.Id).CostoDirecto);
        Assert.AreEqual(60.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == basico2.Id).CostoDirecto);
        Assert.AreEqual(120.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuConAuxiliar.Id).CostoDirecto,
            "El APU ascendiente no debe recalcularse con la arista nueva.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorPrecioDeInsumoEnCierre_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // Cambio de precio del insumo referenciado por un componente del cierre: ni
        // las filas ni las relaciones cambian, pero el recálculo daría otro importe.
        scenario.Cemento.PrecioUnitario = 70m;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.AreEqual(70.00m, context.Materiales.AsNoTracking()
            .Single(m => m.Id == scenario.Cemento.Id).PrecioUnitario,
            "El precio editado no debe recalcularse ni revertirse.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "La matriz no debe recalcularse con el precio nuevo.");
        var concepto = context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(100.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorCostoDeAuxiliarFueraDelCierre_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basicoFuera, componenteAuxiliar) = AgregarAuxiliarFueraDelCierre(context, scenario);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);
        Assert.AreEqual(1, preview.Value!.RecalculatedMatrixCount,
            "La auxiliar referenciada no es un ascendiente: no entra en el cierre.");

        // La auxiliar NO está en el cierre, pero su costo almacenado es entrada directa
        // del cálculo de su padre (hoja precalculada).
        basicoFuera.CostoDirecto = 999m;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(componenteAuxiliar.Id, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.Id == componenteAuxiliar.Id).Id,
            "El componente auxiliar no debe eliminarse.");
        Assert.AreEqual(999.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == basicoFuera.Id).CostoDirecto,
            "El costo editado de la auxiliar no debe recalcularse ni revertirse.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "El padre no debe recalcularse con el costo nuevo.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HuellaObsoletaPorTipoDeAuxiliarFueraDelCierre_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (basicoFuera, componenteAuxiliar) = AgregarAuxiliarFueraDelCierre(context, scenario);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            session, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { scenario.Cemento.Id }));
        if (!preview.IsSuccess) Assert.Fail(preview.Error!.Message);

        // El tipo de la auxiliar decide si su importe entra a la base de mano de obra
        // del padre (Cuadrilla) o a los básicos; cambia el resultado sin cambiar Ids.
        basicoFuera.Tipo = TipoMatriz.Cuadrilla;
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id },
                preview.Value!.Fingerprint));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(TipoMatriz.Cuadrilla, context.Matrices.AsNoTracking()
            .Single(m => m.Id == basicoFuera.Id).Tipo,
            "El tipo editado no debe revertirse.");
        Assert.AreEqual(componenteAuxiliar.Id, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.Id == componenteAuxiliar.Id).Id);
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "El padre no debe recalcularse con el tipo nuevo.");
    }

    // ---------- Referencias externas ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_ReferenciaEnMatrizDeOtroProyecto_RechazaElLoteCompleto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var otroProyecto = NuevoProyecto(context, "proyecto_ajeno");
        var matrizAjena = new Matriz
        {
            ProyectoId = otroProyecto.Id,
            Clave = "APU-P2",
            Descripcion = "Matriz de otro proyecto",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizAjena);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizAjena.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });

        var otroMaterial = NuevoMaterial(scenario.Proyecto.Id, "MAT-OTRO");
        context.Materiales.Add(otroMaterial);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id, otroMaterial.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id),
            "El conflicto externo rechaza el lote completo: ni el insumo ajeno ni el propio se eliminan.");
        Assert.IsTrue(context.Materiales.Any(m => m.Id == otroMaterial.Id));
        Assert.IsTrue(context.ComponentesMatriz.Any(c => c.MatrizId == matrizAjena.Id
            && c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == scenario.MatrizApu.Id && c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "Un lote rechazado no debe recalcular nada.");
    }

    // ---------- Ids faltantes u obsoletos ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_IdInexistente_RechazaElLoteCompleto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id, 999_999 }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id),
            "Un Id faltante rechaza el lote: no se elimina parcialmente.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id));
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_IdObsoleto_RechazaElLoteCompleto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        // El insumo existía al previsualizar, pero otra operación ya lo eliminó.
        var previo = await new DeleteMaterial(Fabrica).Execute(
            session, new DeleteMaterialRequest(scenario.Cemento.Id));
        if (!previo.IsSuccess) Assert.Fail(previo.Error!.Message);

        var otroMaterial = NuevoMaterial(scenario.Proyecto.Id, "MAT-VIVO");
        context.Materiales.Add(otroMaterial);
        context.SaveChanges();

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id, otroMaterial.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotFound, result.Error!.Code);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == otroMaterial.Id),
            "Un Id obsoleto invalida el lote: no se elimina el resto.");
    }

    // ---------- Fallo inyectado y reversión ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_FalloAlPersistir_RevierteCatalogoComponentesYRecalculo()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        // Fallo determinista en la segunda mitad de la transacción: la primera
        // escritura (componentes) ya ocurrió cuando el DELETE del material aborta.
        using (var trigger = new SOPROContext(context.DatabasePath))
        {
            trigger.Database.ExecuteSqlRaw(
                "CREATE TRIGGER trg_fail_material_delete BEFORE DELETE ON Materiales " +
                "BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        }

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, new[] { scenario.Cemento.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id),
            "La reversión debe restaurar el material.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MaterialId == scenario.Cemento.Id),
            "La reversión debe restaurar el componente eliminado antes del fallo.");
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "La reversión debe descartar el recálculo.");
        var concepto = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == scenario.Concepto.Id);
        Assert.AreEqual(100.00m, concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_CopiaImportadaDelMaestro_EliminaSoloLaCopiaLocal()
    {
        var masterDbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            using (var maestro = TestDbFactory.CreateContextAt(masterDbPath))
            {
                maestro.Materiales.Add(new Material
                {
                    ProyectoId = null,
                    Clave = "MAT-IMP",
                    Descripcion = "Fila maestra original",
                    Unidad = "pza",
                    PrecioUnitario = 5m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Maestro
                });
                maestro.SaveChanges();
            }

            using var context = TestDbFactory.CreateContext();
            var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
            var importado = new Material
            {
                ProyectoId = scenario.Proyecto.Id,
                Clave = "MAT-IMP",
                Descripcion = "Copia importada del maestro",
                Unidad = "pza",
                PrecioUnitario = 5m,
                Notas = string.Empty,
                Origen = OrigenInsumo.Maestro,
                MaterialMaestroId = 1
            };
            context.Materiales.Add(importado);
            context.SaveChanges();

            var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath, masterDbPath);
            var result = await new DeleteCatalogBatch(Fabrica).Execute(
                session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, new[] { importado.Id }));

            if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

            context.ChangeTracker.Clear();
            Assert.IsFalse(context.Materiales.Any(m => m.Id == importado.Id),
                "La copia importada pertenece al proyecto y es eliminable desde él.");

            using var maestroVerificacion = new SOPROContext(masterDbPath);
            Assert.AreEqual(1, maestroVerificacion.Materiales.Count(),
                "Eliminar la copia local nunca toca CatalogoMaestro.db.");
        }
        finally
        {
            DeleteQuietly(masterDbPath);
        }
    }

    // ---------- Catálogo maestro ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_SesionMaestro_EliminaSoloFilasMaestras()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = NuevoProyecto(context, "proyecto_con_catalogo");
        var materialProyecto = NuevoMaterial(proyecto.Id, "MAT-PROY");
        context.Materiales.Add(materialProyecto);

        var materialMaestro = new Material
        {
            ProyectoId = null,
            Clave = "MAT-MAESTRO",
            Descripcion = "Material del catálogo maestro",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        };
        context.Materiales.Add(materialMaestro);
        context.SaveChanges();

        var session = CrearSession(null, context.DatabasePath);
        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, new[] { materialMaestro.Id }));

        if (!result.IsSuccess) Assert.Fail(result.Error!.Message);

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Materiales.Any(m => m.Id == materialMaestro.Id));
        Assert.IsTrue(context.Materiales.Any(m => m.Id == materialProyecto.Id),
            "El catálogo maestro no debe tocar filas de proyecto.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_SesionMaestroConCierreHaciaProyecto_RechazaSinTocarMatricesNiPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = NuevoProyecto(context, "proyecto_con_presupuesto");

        var materialMaestro = new Material
        {
            ProyectoId = null,
            Clave = "MAT-MAESTRO",
            Descripcion = "Material del catálogo maestro",
            Unidad = "pza",
            PrecioUnitario = 60m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        };
        context.Materiales.Add(materialMaestro);
        context.SaveChanges();

        // Matriz maestra (sin proyecto) que consume el material maestro.
        var matrizMaestra = new Matriz
        {
            ProyectoId = null,
            Clave = "BAS-MAESTRO",
            Descripcion = "Básico del catálogo maestro",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 60m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizMaestra);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizMaestra.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materialMaestro.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });

        // Matriz de un proyecto que consume la matriz maestra como auxiliar: el
        // cierre del material maestro la alcanza aunque el insumo sea del maestro.
        var matrizProyecto = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-PROY",
            Descripcion = "Matriz del proyecto que usa el básico maestro",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 999m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizProyecto);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizProyecto.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizMaestra.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-PROY",
            Descripcion = "Hoja del proyecto",
            Unidad = "m3",
            Cantidad = 1m,
            MatrizId = matrizProyecto.Id,
            CostoDirectoUnitario = 999m,
            CostoDirectoTotal = 999m,
            PrecioUnitario = 999m,
            ImporteTotal = 999m,
            Nivel = 5,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();

        var masterSession = CrearSession(null, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            masterSession, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { materialMaestro.Id }));

        Assert.IsFalse(preview.IsSuccess,
            "La previsualización maestra no debe confirmar un cierre que alcanza un proyecto.");
        Assert.AreEqual(AppErrorCode.Conflict, preview.Error!.Code);

        var delete = await new DeleteCatalogBatch(Fabrica).Execute(
            masterSession, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material, new[] { materialMaestro.Id }));

        Assert.IsFalse(delete.IsSuccess,
            "El comando debe rechazar el lote completo antes de escribir.");
        Assert.AreEqual(AppErrorCode.Conflict, delete.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == materialMaestro.Id),
            "La fila maestra debe permanecer.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == matrizMaestra.Id));
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == matrizProyecto.Id));
        Assert.AreEqual(60.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == matrizMaestra.Id).CostoDirecto);
        Assert.AreEqual(999.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == matrizProyecto.Id).CostoDirecto,
            "La matriz del proyecto no debe recalcularse desde el catálogo maestro.");
        var conceptoPersistido = context.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == concepto.Id);
        Assert.AreEqual(999.00m, conceptoPersistido.CostoDirectoUnitario);
        Assert.AreEqual(999.00m, conceptoPersistido.CostoDirectoTotal);
        Assert.AreEqual(999.00m, conceptoPersistido.PrecioUnitario);
        Assert.AreEqual(999.00m, conceptoPersistido.ImporteTotal,
            "El presupuesto del proyecto no debe recalcularse desde el catálogo maestro.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_SesionMaestroRecursoEnUsoEnMatrizMaestra_RechazaSinDejarCostosObsoletos()
    {
        using var context = TestDbFactory.CreateContext();

        var materialMaestro = new Material
        {
            ProyectoId = null,
            Clave = "MAT-MAESTRO",
            Descripcion = "Material del catálogo maestro",
            Unidad = "pza",
            PrecioUnitario = 60m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        };
        context.Materiales.Add(materialMaestro);
        context.SaveChanges();

        // Matriz maestra (sin proyecto) que consume el material maestro.
        var matrizMaestra = new Matriz
        {
            ProyectoId = null,
            Clave = "BAS-MAESTRO",
            Descripcion = "Básico del catálogo maestro",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 60m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizMaestra);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizMaestra.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materialMaestro.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });

        // Ascendiente también sin proyecto: la propagación canónica no recalcula
        // matrices sin proyecto, así que eliminarlas dejaría ambos costos obsoletos.
        var apuMaestra = new Matriz
        {
            ProyectoId = null,
            Clave = "APU-MAESTRO",
            Descripcion = "APU del catálogo maestro",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 120m,
            Notas = string.Empty
        };
        context.Matrices.Add(apuMaestra);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = apuMaestra.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizMaestra.Id,
            Cantidad = 2m,
            Orden = 1,
            Notas = string.Empty
        });
        context.SaveChanges();

        var masterSession = CrearSession(null, context.DatabasePath);

        var preview = await new PreviewCatalogBatchDeletion(Fabrica).Execute(
            masterSession, new PreviewCatalogBatchDeletionRequest(
                CatalogItemKind.Material, new[] { materialMaestro.Id }));

        Assert.IsFalse(preview.IsSuccess,
            "El catálogo maestro no puede confirmar la eliminación de un insumo en uso en matrices sin proyecto.");
        Assert.AreEqual(AppErrorCode.Conflict, preview.Error!.Code);

        var delete = await new DeleteCatalogBatch(Fabrica).Execute(
            masterSession, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material, new[] { materialMaestro.Id }));

        Assert.IsFalse(delete.IsSuccess,
            "El comando debe rechazar el lote completo antes de escribir.");
        Assert.AreEqual(AppErrorCode.Conflict, delete.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == materialMaestro.Id),
            "La fila maestra debe permanecer.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == matrizMaestra.Id),
            "El componente de la matriz maestra no debe eliminarse.");
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == apuMaestra.Id),
            "El componente auxiliar de la matriz maestra no debe eliminarse.");
        Assert.AreEqual(60.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == matrizMaestra.Id).CostoDirecto,
            "Sin escritura no puede quedar un costo obsoleto respecto de sus componentes.");
        Assert.AreEqual(120.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == apuMaestra.Id).CostoDirecto,
            "El ascendiente sin proyecto conserva su costo y su componente auxiliar.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_SesionMaestroConFilaDeProyecto_RechazaPorAlcanceMixto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = NuevoProyecto(context, "proyecto_con_catalogo");
        var materialProyecto = NuevoMaterial(proyecto.Id, "MAT-PROY");
        context.Materiales.Add(materialProyecto);
        var materialMaestro = new Material
        {
            ProyectoId = null,
            Clave = "MAT-MAESTRO",
            Descripcion = "Material del catálogo maestro",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        };
        context.Materiales.Add(materialMaestro);
        context.SaveChanges();

        var session = CrearSession(null, context.DatabasePath);
        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { materialMaestro.Id, materialProyecto.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.AreEqual(2, context.Materiales.Count(),
            "El lote mixto se rechaza completo: ni siquiera la fila maestra se elimina.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_ProyectoConFilaMaestra_RechazaPorAlcanceMixto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var materialMaestro = new Material
        {
            ProyectoId = null,
            Clave = "MAT-MAESTRO",
            Descripcion = "Material del catálogo maestro en la base del proyecto",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Maestro
        };
        context.Materiales.Add(materialMaestro);
        context.SaveChanges();

        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(
                CatalogItemKind.Material,
                new[] { scenario.Cemento.Id, materialMaestro.Id }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Conflict, result.Error!.Code);

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.Any(m => m.Id == materialMaestro.Id),
            "Un proyecto no puede eliminar filas del catálogo maestro.");
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id),
            "El rechazo por alcance mixto invalida el lote completo.");
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_HerramientasEnSesionMaestro_NoSoportado()
    {
        using var context = TestDbFactory.CreateContext();
        var session = CrearSession(null, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Herramienta, new[] { 1 }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotSupported, result.Error!.Code);
    }

    // ---------- Validación de entrada ----------

    [TestMethod]
    public async Task DeleteCatalogBatch_LoteVacioOIdInvalido_FallaValidacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);
        var caso = new DeleteCatalogBatch(Fabrica);

        var vacio = await caso.Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, Array.Empty<int>()));
        var invalido = await caso.Execute(
            session, new DeleteCatalogBatchRequest(CatalogItemKind.Material, new[] { 0, scenario.Cemento.Id }));

        Assert.IsFalse(vacio.IsSuccess);
        Assert.AreEqual(AppErrorCode.Validation, vacio.Error!.Code);
        Assert.IsFalse(invalido.IsSuccess);
        Assert.AreEqual(AppErrorCode.Validation, invalido.Error!.Code);
        Assert.IsTrue(context.Materiales.Any(m => m.Id == scenario.Cemento.Id));
    }

    [TestMethod]
    public async Task DeleteCatalogBatch_TipoNoDefinido_NoSoportado()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(scenario.Proyecto.Id, context.DatabasePath);

        var result = await new DeleteCatalogBatch(Fabrica).Execute(
            session, new DeleteCatalogBatchRequest((CatalogItemKind)99, new[] { 1 }));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.NotSupported, result.Error!.Code);
    }

    // ---------- Infraestructura ----------

    private static ProjectSessionInfo CrearSession(
        int? proyectoId,
        string databasePath,
        string? masterDatabasePath = null)
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
                masterDatabasePath,
                proyecto?.DecimalesImporte);
        }

        return ProjectSessionInfo.Create(ProjectRef.Master, databasePath, masterDatabasePath);
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

    private static Material NuevoMaterial(int proyectoId, string clave) => new()
    {
        ProyectoId = proyectoId,
        Clave = clave,
        Descripcion = "Material de prueba",
        Unidad = "pza",
        PrecioUnitario = 1m,
        Notas = string.Empty,
        Origen = OrigenInsumo.Proyecto
    };

    /// <summary>
    /// Agrega un básico que NO forma parte del cierre ascendente (es descendiente de
    /// APU-001, no ascendiente) y lo referencia desde APU-001 como auxiliar.
    /// </summary>
    private static (Matriz Basico, ComponenteMatriz ComponenteAuxiliar) AgregarAuxiliarFueraDelCierre(
        SOPROContext context,
        SoproCalculationScenario scenario)
    {
        var basico = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "BAS-FUERA",
            Descripcion = "Básico fuera del cierre",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 60m,
            Notas = string.Empty
        };
        context.Matrices.Add(basico);
        context.SaveChanges();

        var componente = new ComponenteMatriz
        {
            MatrizId = scenario.MatrizApu.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = basico.Id,
            Cantidad = 1m,
            Orden = 6,
            Notas = string.Empty
        };
        context.ComponentesMatriz.Add(componente);
        context.SaveChanges();

        return (basico, componente);
    }

    /// <summary>
    /// Cierre auxiliar del escenario base: básico con cemento, APU que consume el
    /// básico y una hoja de presupuesto sobre ese APU dentro de un capítulo.
    /// </summary>
    private static (Matriz Basico, Matriz ApuAuxiliar) CrearCierreAuxiliar(
        SOPROContext context,
        SoproCalculationScenario scenario)
    {
        var basico = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "BAS-1",
            Descripcion = "Básico con cemento",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 60m,
            Notas = string.Empty
        };
        context.Matrices.Add(basico);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = basico.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });

        var apuConAuxiliar = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "APU-AUX",
            Descripcion = "APU que consume el básico",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 120m,
            Notas = string.Empty
        };
        context.Matrices.Add(apuConAuxiliar);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = apuConAuxiliar.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = basico.Id,
            Cantidad = 2m,
            Orden = 1,
            Notas = string.Empty
        });

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

    private static void DeleteQuietly(string databasePath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                if (File.Exists(databasePath)) File.Delete(databasePath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
        }
    }
}
