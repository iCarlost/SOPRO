using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Reajuste de costo de UNA matriz desde un concepto hoja a través de la ruta de
/// comando (ADR-0001) con el <see cref="FormPresupuesto"/> real sobre un SQLite y
/// un workspace temporales:
/// <list type="bullet">
/// <item>el objetivo P.U. y el objetivo importe se aplican con la semántica del
/// backend, sin que la UI calcule el costo directo objetivo;</item>
/// <item>la instantánea confirmada sincroniza proyecto, filas, matrices y
/// componentes rastreados y los deja <c>Unchanged</c>, sin un segundo
/// <c>SaveChanges</c> compartido ni un refresco de precios;</item>
/// <item>las hojas que comparten matriz se actualizan juntas con su agrupador;</item>
/// <item>el reajuste de un agrupador usa el comando grupal, captura sus bases antes
/// del diálogo y aplica la instantánea sin un segundo guardado;</item>
/// <item>trabajo pendiente, un fallo revertido y un resultado incierto bloquean el
/// reajuste sin descartar ni confirmar nada.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoReajusteCostoComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";
    private const string CampoBloqueoReajuste = "_reajusteCostoBloqueadoPorResultadoIncierto";

    [TestMethod]
    public void ReajusteCostoComando_ObjetivoPU_ConfirmaYAplicaSnapshotSinSegundoGuardado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_pu_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            // Matriz y componente rastreados por el contexto compartido: la
            // instantánea debe volcarlos y dejarlos Unchanged.
            var matrizRastreada = ctx.Matrices
                .Include(m => m.Componentes)
                .Single(m => m.Id == escenario.Matriz.Id);
            var componenteRastreado = matrizRastreada.Componentes.Single();
            ctx.ResetSaveChangesCount();

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0, "El concepto hoja debe estar en el grid cargado.");
            SeleccionarFila(form.GridPrincipal, fila);
            Assert.AreEqual(escenario.Matriz.Id, form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id,
                "El panel debe tener cargada la matriz que la vista muestra.");

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado, "La ruta de comando debe consumir la intención.");
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto);
            Assert.IsNull(form.UltimoDiagnosticoReajusteCosto);

            // Ni SaveChanges compartido ni RefreshBudgetPrices desde esta ruta.
            Assert.AreEqual(0, ctx.SaveChangesCount,
                "La ruta de comando no debe guardar desde el contexto compartido.");
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios,
                "La ruta de comando no debe emitir un refresco de precios.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La instantánea aplicada no deja trabajo pendiente en el contexto compartido.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.Matriz.Id);
                Assert.AreEqual(120m, matriz.CostoDirecto);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja.Id);
                Assert.AreEqual(120m, hoja.PrecioUnitario);
                Assert.AreEqual(600m, hoja.ImporteTotal);
                Assert.AreEqual(5m, hoja.Cantidad, "La cantidad del concepto no se modifica.");

                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Agrupador.Id);
                Assert.AreEqual(600m, agrupador.ImporteTotal);
            }

            // Matriz y componente rastreados sincronizados y marcados Unchanged.
            Assert.AreEqual(120m, matrizRastreada.CostoDirecto);
            Assert.AreEqual(12m, componenteRastreado.Cantidad,
                "El componente material se escala con el factor del reajuste.");
            Assert.AreEqual(120m, componenteRastreado.Importe);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(matrizRastreada).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(componenteRastreado).State);

            var tagHoja = (ConceptoPresupuesto)form.GridPrincipal.Rows[fila].Tag!;
            Assert.AreEqual(120m, tagHoja.PrecioUnitario);
            Assert.AreEqual(600m, tagHoja.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagHoja).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");
            Assert.AreEqual(120m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, fila, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(600m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, fila, "Importe")!.Value?.ToString());

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0);
            Assert.AreEqual(600m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, filaAgrupador, "Importe")!.Value?.ToString());
            Assert.AreEqual(EntityState.Unchanged,
                ctx.Entry((ConceptoPresupuesto)form.GridPrincipal.Rows[filaAgrupador].Tag!).State);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_ObjetivoImporte_UsaCantidadDelConcepto()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_importe_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);
            ctx.ResetSaveChangesCount();

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);

            // Importe objetivo 750 con cantidad 5 => P.U. 150 con factor 1.
            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.Importe,
                750m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.AreEqual(BudgetMatrixCostAdjustmentTargetKind.Importe, resultado.Confirmed!.TargetKind);
            Assert.AreEqual(750m, resultado.Confirmed.RequestedTargetValue);
            Assert.AreEqual(150m, resultado.Confirmed.TargetCost);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.Matriz.Id);
                Assert.AreEqual(150m, matriz.CostoDirecto);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja.Id);
                Assert.AreEqual(150m, hoja.PrecioUnitario);
                Assert.AreEqual(750m, hoja.ImporteTotal);
                Assert.AreEqual(5m, hoja.Cantidad);
            }

            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_MatrizCompartidaPorDosHojas_ActualizaAmbasYElAgrupador()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_compartida_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 2);
            form = CrearFormulario(ctx, escenario, root);
            ctx.ResetSaveChangesCount();

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.AreEqual(2, resultado.Confirmed!.Leaves.Count,
                "La instantánea debe incluir ambas hojas que comparten la matriz.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var primera = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Hoja.Id);
                var segunda = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.SegundaHoja!.Id);
                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Agrupador.Id);

                Assert.AreEqual(120m, primera.PrecioUnitario);
                Assert.AreEqual(600m, primera.ImporteTotal);
                Assert.AreEqual(120m, segunda.PrecioUnitario);
                Assert.AreEqual(240m, segunda.ImporteTotal);
                Assert.AreEqual(840m, agrupador.ImporteTotal,
                    "El agrupador se recalcula con todas las hojas afectadas.");
            }

            int filaSegunda = BuscarFilaPorConcepto(form.GridPrincipal, escenario.SegundaHoja!.Id);
            Assert.IsTrue(filaSegunda >= 0);
            var tagSegunda = (ConceptoPresupuesto)form.GridPrincipal.Rows[filaSegunda].Tag!;
            Assert.AreEqual(120m, tagSegunda.PrecioUnitario);
            Assert.AreEqual(240m, tagSegunda.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagSegunda).State);

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.AreEqual(840m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, filaAgrupador, "Importe")!.Value?.ToString());
            Assert.AreEqual(0, ctx.SaveChangesCount);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_Guards_TrackerSucioYResultadoInciertoBloqueanSinEscribir()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_guards_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);
            ctx.ResetSaveChangesCount();

            // Guard 1: trabajo ajeno pendiente en el contexto compartido.
            escenario.Proyecto.Descripcion = "borrador ajeno sin guardar";
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            var bloqueadoPorTrabajo = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNull(bloqueadoPorTrabajo, "Con trabajo pendiente no se envía el comando.");
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual("borrador ajeno sin guardar", escenario.Proyecto.Descripcion,
                "El guard no descarta el trabajo del usuario.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "El guard no escribe en la base.");

            ctx.Entry(escenario.Proyecto).OriginalValues.SetValues(ctx.Entry(escenario.Proyecto).CurrentValues);
            ctx.Entry(escenario.Proyecto).State = EntityState.Unchanged;
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            // Guard 2: resultado incierto previo (el comando no se reintenta).
            EstablecerBloqueoReajusteIncierto(form, bloqueado: true);
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorResultadoIncierto);

            var bloqueadoPorIncierto = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNull(bloqueadoPorIncierto,
                "Un resultado incierto bloquea el siguiente reajuste hasta una recarga segura.");
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id));
            Assert.AreEqual(0, ctx.SaveChangesCount);

            // La recarga segura (activación sin cambios pendientes) libera el bloqueo
            // sólo cuando la reconciliación confirma que aplicó la instantánea.
            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto());
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);

            var reintento = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(reintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, reintento!.Status, reintento.Message);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id));
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_FalloRevertido_NoAplicaNiReintentaAutomaticamente()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_fallo_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);
            ctx.ResetSaveChangesCount();

            // El trigger se crea después del constructor: la carga de la vista no falla.
            EjecutarSql(dbPath,
                "CREATE TRIGGER trg_fail_reajuste BEFORE UPDATE ON Matrices " +
                "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Failed, resultado!.Status,
                "El UPDATE rechazado se clasifica como fallo revertido.");
            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoReajusteCosto);

            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "El fallo no deja la matriz modificada.");
            var hoja = LeerConceptoEnBase(dbPath, escenario.Hoja.Id);
            Assert.AreEqual(100m, hoja.PrecioUnitario);
            Assert.AreEqual(500m, hoja.ImporteTotal);

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Hoja).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un fallo no deja trabajo pendiente en el contexto compartido.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.AreEqual(100m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, fila, "PrecioUnitario")!.Value?.ToString(),
                "La vista conserva lo que mostraba antes del fallo.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_CambioExternoTrasCargarLaVista_DevuelveConflictoSinEscribir()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_conflicto_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);

            // La vista ya tiene cargada la matriz con el precio que el usuario ve.
            Assert.AreEqual(escenario.Matriz.Id, form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id);
            Assert.AreEqual(10m,
                form.PanelMatricesInterno!.MatrizVisibleEnPanel!.Componentes.Single().Material!.PrecioUnitario);

            ctx.ResetSaveChangesCount();

            // Cambio externo DESPUÉS de cargar la vista: el comando debe revalidar
            // contra lo que la pantalla mostraba, no contra una lectura fresca.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                var material = otroModulo.Materiales.Single(m => m.Id == escenario.Material.Id);
                material.PrecioUnitario = 11m;
                otroModulo.SaveChanges();
            }

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado!.Status,
                "La base esperada debe ser la de la vista, no una lectura fresca de SQLite.");
            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoReajusteCosto);

            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "Un conflicto no escribe la matriz.");
            var hoja = LeerConceptoEnBase(dbPath, escenario.Hoja.Id);
            Assert.AreEqual(100m, hoja.PrecioUnitario);
            Assert.AreEqual(500m, hoja.ImporteTotal);

            var tagHoja = (ConceptoPresupuesto)form.GridPrincipal.Rows[fila].Tag!;
            Assert.AreEqual(100m, tagHoja.PrecioUnitario,
                "La vista conserva lo que mostraba; no se aplica ninguna instantánea.");
            Assert.AreEqual(500m, tagHoja.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagHoja).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_CierreAnidado_RecalculaAncestroAuxiliar()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_anidado_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1, incluirAncestroAuxiliar: true);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);
            ctx.ResetSaveChangesCount();

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.AreEqual(2, resultado.Confirmed!.AffectedMatrices.Count,
                "El cierre confirmado debe incluir la matriz reajustada y su ancestro auxiliar.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var origen = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.Matriz.Id);
                var ancestro = verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.MatrizAncestro!.Id);
                var componenteAncestro = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == escenario.ComponenteAncestro!.Id);

                Assert.AreEqual(120m, origen.CostoDirecto);
                Assert.AreEqual(240m, ancestro.CostoDirecto,
                    "El ancestro auxiliar consume el nuevo costo del origen.");
                Assert.AreEqual(2m, componenteAncestro.Cantidad);
                Assert.AreEqual(240m, componenteAncestro.Importe);

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Hoja.Id);
                Assert.AreEqual(120m, hoja.PrecioUnitario);
                Assert.AreEqual(600m, hoja.ImporteTotal);
            }

            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_CambioCoherenteEnAncestroEntreCapturaYComando_DevuelveConflicto()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_ancestro_conflicto_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1, incluirAncestroAuxiliar: true);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);
            ctx.ResetSaveChangesCount();

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales,
                () =>
                {
                    // Cambio COHERENTE del ancestro fuera de la vista: su componente
                    // auxiliar y su costo directo se actualizan juntos.
                    using var otroModulo = new SOPROContext(dbPath);
                    var componente = otroModulo.ComponentesMatriz
                        .Single(c => c.Id == escenario.ComponenteAncestro!.Id);
                    componente.Cantidad = 3m;
                    componente.Importe = 300m;

                    var ancestro = otroModulo.Matrices
                        .Single(m => m.Id == escenario.MatrizAncestro!.Id);
                    ancestro.CostoDirecto = 300m;
                    ancestro.FechaModificacion = new DateTime(2026, 2, 1);
                    otroModulo.SaveChanges();
                });

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado!.Status,
                "El cierre esperado debe incluir el ancestro tal como lo vio la vista.");
            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoReajusteCosto);

            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "El conflicto no ajusta la matriz origen.");
            var hoja = LeerConceptoEnBase(dbPath, escenario.Hoja.Id);
            Assert.AreEqual(100m, hoja.PrecioUnitario);
            Assert.AreEqual(500m, hoja.ImporteTotal);
            Assert.AreEqual(300m, LeerCostoDirectoEnBase(dbPath, escenario.MatrizAncestro!.Id),
                "El cambio externo del ancestro no se revierte desde la vista.");

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Hoja).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReconciliacionResultadoIncierto_RecargaNoAplicada_MantieneBloqueoYNoEscribe()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reconciliacion_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);
            ctx.ResetSaveChangesCount();

            EstablecerBloqueoReajusteIncierto(form, bloqueado: true);

            // Recarga NO aplicable: el contexto compartido tiene trabajo ajeno.
            escenario.Proyecto.Descripcion = "borrador ajeno sin guardar";
            Assert.IsFalse(form.NotificarRecargaSeguraReajusteCosto(),
                "Un tracker sucio no puede liberar el bloqueo: no hubo recarga aplicada.");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "La reconciliación rechazada no descarta el borrador ajeno.");

            var bloqueado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNull(bloqueado, "Sin reconciliación exitosa el comando sigue bloqueado.");
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "El bloqueo no permite escribir en la base.");
            Assert.AreEqual(0, ctx.SaveChangesCount);

            ctx.Entry(escenario.Proyecto).OriginalValues.SetValues(
                ctx.Entry(escenario.Proyecto).CurrentValues);
            ctx.Entry(escenario.Proyecto).State = EntityState.Unchanged;
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            // Recarga NO aplicable: edición activa en el presupuesto.
            var grid = form.GridPrincipal;
            var descCell = BuscarCelda(grid, fila, "Descripcion");
            Assert.IsNotNull(descCell);
            grid.CurrentCell = descCell;
            form.CreateControl();
            if (!grid.IsHandleCreated)
                grid.CreateControl();
            Assert.IsTrue(grid.BeginEdit(true),
                "El harness debe permitir simular una edición activa del presupuesto.");

            Assert.IsFalse(form.NotificarRecargaSeguraReajusteCosto(),
                "Con edición activa la recarga no se aplica y el bloqueo se conserva.");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorResultadoIncierto);

            grid.EndEdit();
            Assert.IsFalse(grid.IsCurrentCellInEditMode);
            ctx.ResetSaveChangesCount();

            // Cambio externo ya guardado: ahora sí hay una recarga real que aplicar.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                var matriz = otroModulo.Matrices.Single(m => m.Id == escenario.Matriz.Id);
                matriz.CostoDirecto = 130m;
                matriz.FechaModificacion = new DateTime(2026, 3, 1);

                int componenteId = ObtenerComponenteId(otroModulo, escenario.Matriz.Id);
                var componente = otroModulo.ComponentesMatriz.Single(c => c.Id == componenteId);
                componente.Cantidad = 13m;
                componente.Importe = 130m;

                var hoja = otroModulo.ConceptosPresupuesto.Single(c => c.Id == escenario.Hoja.Id);
                hoja.CostoDirectoUnitario = 130m;
                hoja.CostoDirectoTotal = 650m;
                hoja.PrecioUnitario = 130m;
                hoja.ImporteTotal = 650m;

                var agrupador = otroModulo.ConceptosPresupuesto.Single(c => c.Id == escenario.Agrupador.Id);
                agrupador.ImporteTotal = 650m;

                otroModulo.SaveChanges();
            }

            var panel = form.PanelMatricesInterno!;
            Assert.IsTrue(panel.IntentarRecargarMatrizVisibleDesdeBd(),
                "La carga normal debe reportar la matriz fresca del concepto seleccionado.");
            Assert.AreEqual(130m, panel.MatrizVisibleEnPanel!.CostoDirecto,
                "La matriz seleccionada quedó reemplazada por la instancia fresca.");

            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto(),
                "La reconciliación sólo debe devolver éxito después de aplicar la recarga.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto,
                "El bloqueo se libera únicamente tras la recarga real.");

            var tagHoja = (ConceptoPresupuesto)form.GridPrincipal.Rows[fila].Tag!;
            Assert.AreEqual(130m, tagHoja.PrecioUnitario,
                "La recarga aplicó la instantánea de la fila antes de liberar el bloqueo.");
            Assert.AreEqual(130m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, fila, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(130m, form.PanelMatricesInterno!.MatrizVisibleEnPanel!.CostoDirecto,
                "La recarga aplicó también la matriz visible del panel.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
            Assert.AreEqual(130m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id));
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReconciliacionResultadoIncierto_ModoCreacionVacio_MantieneBloqueoYReajusteBloqueado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reconciliacion_creacion_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);
            ctx.ResetSaveChangesCount();

            // Modo creación vacío en el panel: no hay matriz que recargar.
            var panel = form.PanelMatricesInterno!;
            panel.BeginCreateMatrix(TipoMatriz.APU);
            Assert.IsNull(panel.MatrizVisibleEnPanel,
                "El panel está capturando una matriz nueva, no una matriz persistida.");
            Assert.IsFalse(panel.IntentarRecargarMatrizVisibleDesdeBd(),
                "El modo creación no puede reportar una recarga aplicada.");
            Assert.IsFalse(form.IntentarRecargarPreciosDesdeBase(),
                "La recarga del formulario no puede reportar éxito si el panel no aplicó la matriz.");

            EstablecerBloqueoReajusteIncierto(form, bloqueado: true);

            Assert.IsFalse(form.NotificarRecargaSeguraReajusteCosto(),
                "Con el panel en modo creación la reconciliación no puede liberar el bloqueo.");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.IsNull(panel.MatrizVisibleEnPanel,
                "La reconciliación rechazada no reemplaza el modo creación por una matriz.");

            var bloqueado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNull(bloqueado, "El reajuste sigue bloqueado tras la reconciliación fallida.");
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "La reconciliación rechazada no escribe en la base.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReconciliacionResultadoIncierto_SinSeleccionVisible_MantieneBloqueoYReajusteBloqueado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reconciliacion_sin_seleccion_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Hoja.Id);
            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.IsTrue(fila >= 0);
            Assert.IsTrue(filaAgrupador >= 0);

            var panel = form.PanelMatricesInterno!;

            // Fila sin matriz (agrupador): no hay matriz persistida que reconciliar.
            SeleccionarFila(grid, filaAgrupador);
            Assert.IsFalse(panel.IntentarRecargarMatrizVisibleDesdeBd(),
                "Una fila sin matriz no puede reportar una recarga aplicada.");

            // Sin selección/fila visible: tampoco es un éxito reconciliable.
            panel.SimularSinSeleccionParaPrueba();
            Assert.IsNull(panel.MatrizVisibleEnPanel);
            Assert.IsFalse(panel.IntentarRecargarMatrizVisibleDesdeBd(),
                "Sin fila visible no hay recarga segura.");

            ctx.ResetSaveChangesCount();

            Assert.IsFalse(form.IntentarRecargarPreciosDesdeBase(),
                "La recarga del formulario exige que el panel confirme su matriz visible.");

            EstablecerBloqueoReajusteIncierto(form, bloqueado: true);

            Assert.IsFalse(form.NotificarRecargaSeguraReajusteCosto(),
                "El bloqueo por OutcomeUnknown no se libera con 'nada que reconciliar'.");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorResultadoIncierto);

            var bloqueado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNull(bloqueado, "El reajuste sigue bloqueado tras la reconciliación fallida.");
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "La reconciliación rechazada no escribe en la base.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_CeldaComponenteEnEdicionEnPanel_BloqueaSinDespacharYNoEscribe()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_panel_edit_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int fila = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(fila >= 0);
            SeleccionarFila(form.GridPrincipal, fila);

            var panel = form.PanelMatricesInterno!;
            Assert.AreEqual(escenario.Matriz.Id, panel.MatrizVisibleEnPanel?.Id);

            Assert.IsTrue(panel.DgvComponentesInterno.Rows.Count > 0,
                "El panel debe haber pintado los componentes.");

            // Seam determinista del ensamblado: la edición real del grid en un
            // formulario sin bucle de mensajes no puede cerrarse sin despachar
            // BeginInvoke, así que se fuerza el mismo estado que consulta el guard.
            panel.SimularCeldaComponenteEnEdicionParaPrueba(true);
            Assert.IsTrue(panel.TieneCeldaComponenteEnEdicionEnPanel);

            ctx.ResetSaveChangesCount();

            var bloqueado = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNull(bloqueado,
                "Con una celda de componentes en edición no se captura ni se despacha el comando.");
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "El guard no escribe en la base.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            panel.SimularCeldaComponenteEnEdicionParaPrueba(false);
            Assert.IsFalse(panel.TieneCeldaComponenteEnEdicionEnPanel);

            var reintento = form.EjecutarReajusteCostoDesdeFila(
                fila,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(reintento, "Al finalizar la edición el comando vuelve a despacharse.");
            Assert.AreEqual(BudgetOperationStatus.Committed, reintento!.Status, reintento.Message);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id));
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoGrupal_GuardAntesDeRamificar_NoCapturaNiDespacha()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_grupal_guard_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0, "El agrupador debe estar en el grid cargado.");
            SeleccionarFila(form.GridPrincipal, filaAgrupador);

            bool capturo = false;
            void EntreCapturaYComando() => capturo = true;

            ctx.ResetSaveChangesCount();

            // Guard 1: trabajo ajeno pendiente en el contexto compartido.
            escenario.Proyecto.Descripcion = "borrador ajeno sin guardar";
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            var bloqueadoPorTrabajo = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 600m, MatrixAdjustmentScopes.Materiales, EntreCapturaYComando);

            Assert.IsNull(bloqueadoPorTrabajo,
                "Con trabajo pendiente no se captura ni se despacha el comando grupal.");
            Assert.IsFalse(capturo,
                "El guard debe rechazar antes de capturar y ramificar al agrupador.");
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual("borrador ajeno sin guardar", escenario.Proyecto.Descripcion,
                "El guard no descarta el trabajo del usuario.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id),
                "El guard no escribe en la base.");

            ctx.Entry(escenario.Proyecto).OriginalValues.SetValues(
                ctx.Entry(escenario.Proyecto).CurrentValues);
            ctx.Entry(escenario.Proyecto).State = EntityState.Unchanged;
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            // Guard 2: resultado incierto previo (el comando grupal no se reintenta).
            EstablecerBloqueoReajusteIncierto(form, bloqueado: true);
            capturo = false;

            var bloqueadoPorIncierto = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 600m, MatrixAdjustmentScopes.Materiales, EntreCapturaYComando);

            Assert.IsNull(bloqueadoPorIncierto,
                "Un resultado incierto bloquea también el reajuste de agrupadores.");
            Assert.IsFalse(capturo);
            Assert.IsNull(form.UltimoEstadoReajusteCosto);
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.AreEqual(100m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id));
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoGrupal_ExitoAplicaSnapshotSinSegundoGuardado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_grupal_ok_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 2);
            form = CrearFormulario(ctx, escenario, root);

            var matrizRastreada = ctx.Matrices
                .Include(m => m.Componentes)
                .Single(m => m.Id == escenario.Matriz.Id);
            var componenteRastreado = matrizRastreada.Componentes.Single();

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0);
            SeleccionarFila(form.GridPrincipal, filaAgrupador);
            ctx.ResetSaveChangesCount();

            bool capturo = false;
            var resultado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador,
                840m,
                MatrixAdjustmentScopes.Materiales,
                () => capturo = true);

            Assert.IsTrue(capturo, "La captura PRE-diálogo debe ocurrir antes del comando.");
            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto);
            Assert.IsNull(form.UltimoDiagnosticoReajusteCosto);
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);

            var snapshot = resultado.Confirmed;
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(escenario.Proyecto.Id, snapshot!.ProjectId);
            Assert.AreEqual(escenario.Agrupador.Id, snapshot.Group.ConceptId);
            Assert.AreEqual(700m, snapshot.CurrentAmount);
            Assert.AreEqual(840m, snapshot.RequestedTargetAmount);
            Assert.AreEqual(840m, snapshot.AchievedAmount, 0.01m);
            Assert.AreEqual(1.2m, snapshot.FactorApplied, 0.001m);
            Assert.AreEqual(2, snapshot.AffectedLeaves.Count,
                "La instantánea incluye ambas hojas que comparten la matriz.");
            Assert.AreEqual(1, snapshot.AffectedMatrices.Count);
            Assert.AreEqual(1, snapshot.AffectedComponents.Count);

            // El comando se confirmó en un contexto NUEVO; el contexto compartido no
            // guardó nada y la instantánea lo dejó limpio.
            Assert.AreEqual(0, ctx.SaveChangesCount,
                "La ruta grupal no debe guardar desde el contexto compartido.");
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios,
                "La ruta grupal no debe emitir un refresco de precios heredado.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La instantánea aplicada no deja trabajo pendiente.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matriz = verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.Matriz.Id);
                Assert.AreEqual(120m, matriz.CostoDirecto, 0.01m);

                var componente = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == componenteRastreado.Id);
                Assert.AreEqual(12m, componente.Cantidad, 0.01m);
                Assert.AreEqual(120m, componente.Importe, 0.01m);

                var primera = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Hoja.Id);
                var segunda = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.SegundaHoja!.Id);
                var agrupador = verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Agrupador.Id);

                Assert.AreEqual(120m, primera.PrecioUnitario, 0.01m);
                Assert.AreEqual(600m, primera.ImporteTotal, 0.01m);
                Assert.AreEqual(120m, segunda.PrecioUnitario, 0.01m);
                Assert.AreEqual(240m, segunda.ImporteTotal, 0.01m);
                Assert.AreEqual(840m, agrupador.ImporteTotal, 0.01m);
            }

            // Matriz y componente rastreados sincronizados y marcados Unchanged.
            Assert.AreEqual(120m, matrizRastreada.CostoDirecto, 0.01m);
            Assert.AreEqual(12m, componenteRastreado.Cantidad, 0.01m);
            Assert.AreEqual(120m, componenteRastreado.Importe, 0.01m);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(matrizRastreada).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(componenteRastreado).State);

            foreach (var hoja in new[] { escenario.Hoja, escenario.SegundaHoja! })
            {
                int fila = BuscarFilaPorConcepto(form.GridPrincipal, hoja.Id);
                Assert.IsTrue(fila >= 0);
                Assert.AreEqual(120m, hoja.PrecioUnitario, 0.01m);
                Assert.AreEqual(120m * hoja.Cantidad, hoja.ImporteTotal, 0.01m);
                Assert.AreEqual(EntityState.Unchanged, ctx.Entry(hoja).State);
                Assert.AreEqual(120m.ToStringImporte(),
                    BuscarCelda(form.GridPrincipal, fila, "PrecioUnitario")!.Value?.ToString());
            }

            Assert.AreEqual(840m, escenario.Agrupador.ImporteTotal, 0.01m);
            Assert.AreEqual(840m.ToStringImporte(),
                BuscarCelda(form.GridPrincipal, filaAgrupador, "Importe")!.Value?.ToString());
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Agrupador).State);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoGrupal_FalloPostcommitDeSincronizacion_BloqueaHastaRecargaSegura()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_grupal_sync_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0);
            SeleccionarFila(form.GridPrincipal, filaAgrupador);
            ctx.ResetSaveChangesCount();

            // El comando se confirma en otro contexto y, justo antes de volcar su
            // instantánea, aparece trabajo ajeno en la vista: el volcado se rechaza.
            form.AntesDeAplicarReajusteCostoGrupalConfirmadoParaPrueba = () =>
            {
                form.AntesDeAplicarReajusteCostoGrupalConfirmadoParaPrueba = null;
                escenario.Proyecto.Descripcion = "cambio posterior al commit";
            };

            var resultado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador,
                600m,
                MatrixAdjustmentScopes.Materiales);

            // Un commit confirmado se reporta como commit: nunca como fallo ni como
            // reintento automático.
            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.IsTrue(resultado.IsCommitted);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto);
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente,
                "Un fallo postcommit de sincronización bloquea el siguiente reajuste.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto,
                "El commit es cierto: el bloqueo no es por resultado incierto.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El trabajo ajeno que impidió sincronizar se conserva.");
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m,
                "El comando sí se confirmó en la base.");
            Assert.AreEqual(100m, escenario.Hoja.PrecioUnitario,
                "La vista no se pisó porque el volcado se rechazó completo.");

            // Mientras dure el bloqueo no se despacha otro comando: un objetivo
            // distinto no debe llegar a la base.
            var bloqueado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 700m, MatrixAdjustmentScopes.Materiales);
            Assert.IsNull(bloqueado);
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m,
                "El bloqueo impide reenviar el reajuste sobre una vista desfasada.");
            Assert.AreEqual(0, ctx.SaveChangesCount);

            // Reconciliación segura: se descarta el borrador ajeno y se selecciona una
            // hoja para que el panel pueda recargar su matriz desde la base.
            ctx.Entry(escenario.Proyecto).OriginalValues.SetValues(
                ctx.Entry(escenario.Proyecto).CurrentValues);
            ctx.Entry(escenario.Proyecto).State = EntityState.Unchanged;
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            int filaHoja = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(filaHoja >= 0);
            SeleccionarFila(form.GridPrincipal, filaHoja);
            Assert.AreEqual(escenario.Matriz.Id,
                form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id,
                "La recarga segura exige una matriz persistida visible en el panel.");

            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto(),
                "La recarga aplicada libera el bloqueo postcommit.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
            Assert.AreEqual(120m,
                ((ConceptoPresupuesto)form.GridPrincipal.Rows[filaHoja].Tag!).PrecioUnitario,
                0.01m,
                "La recarga aplicó la instantánea confirmada antes de liberar el bloqueo.");

            var reintento = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 700m, MatrixAdjustmentScopes.Materiales);
            Assert.IsNotNull(reintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, reintento!.Status, reintento.Message);
            Assert.AreEqual(140m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m,
                "Tras la recarga segura el reajuste grupal vuelve a estar disponible.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoGrupal_ExcepcionPostcommit_BloqueaSinPresentarFallo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_grupal_sync_ex_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0);
            SeleccionarFila(form.GridPrincipal, filaAgrupador);
            ctx.ResetSaveChangesCount();

            form.AntesDeAplicarReajusteCostoGrupalConfirmadoParaPrueba = () =>
            {
                form.AntesDeAplicarReajusteCostoGrupalConfirmadoParaPrueba = null;
                throw new InvalidOperationException("fallo_simulado_sync_grupal");
            };

            var resultado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 600m, MatrixAdjustmentScopes.Materiales);

            // La excepción de sincronización no convierte el commit en fallo: la
            // operación sigue siendo Committed y se informa la recarga requerida.
            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.IsTrue(resultado.IsCommitted);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto,
                "Una excepción postcommit no puede presentar la operación como fallo.");
            StringAssert.Contains(form.UltimoDiagnosticoReajusteCosto,
                "fallo_simulado_sync_grupal");
            Assert.IsNotNull(form.UltimoMensajeReajusteCosto);
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "se guardó");
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "Recargue");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente,
                "La excepción postcommit debe marcar sincronización pendiente.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m,
                "El comando ya se confirmó en la base.");
            Assert.AreEqual(100m, escenario.Hoja.PrecioUnitario,
                "La vista no se pisó: el volcado se rechazó antes de tocar las filas.");

            // El bloqueo impide reenviar aunque el usuario pulse de nuevo.
            var bloqueado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 700m, MatrixAdjustmentScopes.Materiales);
            Assert.IsNull(bloqueado);
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);

            // Recarga segura: libera el bloqueo y reconcilia la vista con la base.
            int filaHoja = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(filaHoja >= 0);
            SeleccionarFila(form.GridPrincipal, filaHoja);
            Assert.AreEqual(escenario.Matriz.Id,
                form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id);
            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto());
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.AreEqual(120m,
                ((ConceptoPresupuesto)form.GridPrincipal.Rows[filaHoja].Tag!).PrecioUnitario,
                0.01m);

            var reintento = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 700m, MatrixAdjustmentScopes.Materiales);
            Assert.IsNotNull(reintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, reintento!.Status, reintento.Message);
            Assert.AreEqual(140m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_ExcepcionPostcommit_BloqueaSinPresentarFallo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_sync_ex_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int filaHoja = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(filaHoja >= 0);
            SeleccionarFila(form.GridPrincipal, filaHoja);
            Assert.AreEqual(escenario.Matriz.Id,
                form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id);
            ctx.ResetSaveChangesCount();

            form.AntesDeAplicarReajusteCostoConfirmadoParaPrueba = () =>
            {
                form.AntesDeAplicarReajusteCostoConfirmadoParaPrueba = null;
                throw new InvalidOperationException("fallo_simulado_sync_individual");
            };

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                filaHoja,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.IsTrue(resultado.IsCommitted);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto,
                "Una excepción postcommit no puede presentar la operación como fallo.");
            StringAssert.Contains(form.UltimoDiagnosticoReajusteCosto,
                "fallo_simulado_sync_individual");
            Assert.IsNotNull(form.UltimoMensajeReajusteCosto);
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "se guardó");
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "Recargue");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(100m, escenario.Hoja.PrecioUnitario,
                "La vista no se pisó: el volcado se rechazó antes de tocar las filas.");

            var bloqueado = form.EjecutarReajusteCostoDesdeFila(
                filaHoja,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                150m,
                MatrixAdjustmentScopes.Materiales);
            Assert.IsNull(bloqueado);
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);

            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto());
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.AreEqual(120m,
                ((ConceptoPresupuesto)form.GridPrincipal.Rows[filaHoja].Tag!).PrecioUnitario,
                0.01m);

            var reintento = form.EjecutarReajusteCostoDesdeFila(
                filaHoja,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                150m,
                MatrixAdjustmentScopes.Materiales);
            Assert.IsNotNull(reintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, reintento!.Status, reintento.Message);
            Assert.AreEqual(150m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoComando_RecargaDelPanelNoAplicada_BloqueaPorSincronizacionPendiente()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_sync_panel_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            int filaHoja = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(filaHoja >= 0);
            SeleccionarFila(form.GridPrincipal, filaHoja);
            Assert.AreEqual(escenario.Matriz.Id,
                form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id,
                "El panel debe mostrar la matriz afectada por el reajuste.");
            ctx.ResetSaveChangesCount();

            // La matriz visible pertenece al cierre afectado y su recarga no se aplica.
            int? matrizVistaEnRecarga = null;
            form.IntentarRecargarMatrizVisibleDesdeBdParaPrueba = () =>
            {
                matrizVistaEnRecarga = form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id;
                return false;
            };

            var resultado = form.EjecutarReajusteCostoDesdeFila(
                filaHoja,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                120m,
                MatrixAdjustmentScopes.Materiales);

            Assert.AreEqual(escenario.Matriz.Id, matrizVistaEnRecarga,
                "La recarga sólo se exige sobre la matriz visible afectada.");
            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message,
                "El commit ya ocurrió: una recarga del panel no aplicada no es un fallo de operación.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto);
            Assert.IsNull(form.UltimoDiagnosticoReajusteCosto,
                "La recarga no aplicada no es una excepción técnica.");
            Assert.IsNotNull(form.UltimoMensajeReajusteCosto);
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "se guardó");
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "Recargue");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente,
                "Una matriz visible afectada sin reconciliar bloquea el siguiente reajuste.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto,
                "El commit es cierto: no es un resultado incierto.");
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m,
                "El comando sí se confirmó en la base.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);

            var bloqueado = form.EjecutarReajusteCostoDesdeFila(
                filaHoja,
                BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario,
                150m,
                MatrixAdjustmentScopes.Materiales);
            Assert.IsNull(bloqueado,
                "El bloqueo por sincronización pendiente impide reenviar el reajuste.");
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);

            // Recarga segura con el panel operativo: libera el bloqueo y reconcilia.
            form.IntentarRecargarMatrizVisibleDesdeBdParaPrueba = null;
            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto(),
                "La recarga segura debe desbloquear la sincronización pendiente.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.AreEqual(120m,
                ((ConceptoPresupuesto)form.GridPrincipal.Rows[filaHoja].Tag!).PrecioUnitario,
                0.01m);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ReajusteCostoGrupal_RecargaDelPanelNoAplicada_BloqueaPorSincronizacionPendiente()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reajuste_grupal_panel_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        CountingContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new CountingContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx, hojas: 1);
            form = CrearFormulario(ctx, escenario, root);

            // La hoja queda seleccionada para que el panel muestre la matriz afectada;
            // el comando grupal se despacha por la fila del agrupador.
            int filaHoja = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Hoja.Id);
            Assert.IsTrue(filaHoja >= 0);
            SeleccionarFila(form.GridPrincipal, filaHoja);
            Assert.AreEqual(escenario.Matriz.Id,
                form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id);

            int filaAgrupador = BuscarFilaPorConcepto(form.GridPrincipal, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0);
            ctx.ResetSaveChangesCount();

            int? matrizVistaEnRecarga = null;
            form.IntentarRecargarMatrizVisibleDesdeBdParaPrueba = () =>
            {
                matrizVistaEnRecarga = form.PanelMatricesInterno?.MatrizVisibleEnPanel?.Id;
                return false;
            };

            var resultado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 600m, MatrixAdjustmentScopes.Materiales);

            Assert.AreEqual(escenario.Matriz.Id, matrizVistaEnRecarga,
                "La recarga sólo se exige sobre la matriz visible afectada.");
            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message,
                "El commit ya ocurrió: una recarga del panel no aplicada no es un fallo de operación.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReajusteCosto);
            Assert.IsNull(form.UltimoDiagnosticoReajusteCosto,
                "La recarga no aplicada no es una excepción técnica.");
            Assert.IsNotNull(form.UltimoMensajeReajusteCosto);
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "se guardó");
            StringAssert.Contains(form.UltimoMensajeReajusteCosto, "Recargue");
            Assert.IsTrue(form.ReajusteCostoBloqueadoPorSincronizacionPendiente,
                "Una matriz visible afectada sin reconciliar bloquea el siguiente reajuste.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto,
                "El commit es cierto: no es un resultado incierto.");
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m,
                "El comando sí se confirmó en la base.");
            Assert.AreEqual(0, ctx.SaveChangesCount);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios);

            var bloqueado = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 700m, MatrixAdjustmentScopes.Materiales);
            Assert.IsNull(bloqueado,
                "El bloqueo por sincronización pendiente impide reenviar el reajuste grupal.");
            Assert.AreEqual(120m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);

            form.IntentarRecargarMatrizVisibleDesdeBdParaPrueba = null;
            Assert.IsTrue(form.NotificarRecargaSeguraReajusteCosto(),
                "La recarga segura debe desbloquear la sincronización pendiente.");
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorSincronizacionPendiente);
            Assert.IsFalse(form.ReajusteCostoBloqueadoPorResultadoIncierto);
            Assert.AreEqual(120m,
                ((ConceptoPresupuesto)form.GridPrincipal.Rows[filaHoja].Tag!).PrecioUnitario,
                0.01m);

            var reintento = form.EjecutarReajusteCostoGrupalDesdeFila(
                filaAgrupador, 700m, MatrixAdjustmentScopes.Materiales);
            Assert.IsNotNull(reintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, reintento!.Status, reintento.Message);
            Assert.AreEqual(140m, LeerCostoDirectoEnBase(dbPath, escenario.Matriz.Id), 0.01m);
            Assert.AreEqual(0, ctx.SaveChangesCount);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static int ObtenerComponenteId(SOPROContext ctx, int matrizId)
        => ctx.ComponentesMatriz.AsNoTracking()
            .Where(c => c.MatrizId == matrizId)
            .Select(c => c.Id)
            .First();

    private static void SeleccionarFila(DataGridView grid, int rowIndex)
    {
        var cell = BuscarCelda(grid, rowIndex, "PrecioUnitario")
            ?? (grid.Rows[rowIndex].Cells.Count > 0 ? grid.Rows[rowIndex].Cells[0] : null);

        if (cell != null)
            grid.CurrentCell = cell;
    }

    private sealed class CountingContext : SOPROContext
    {
        public CountingContext(string databasePath)
            : base(databasePath)
        {
        }

        public int SaveChangesCount { get; private set; }

        public void ResetSaveChangesCount() => SaveChangesCount = 0;

        public override int SaveChanges()
        {
            SaveChangesCount++;
            return base.SaveChanges();
        }
    }

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Matriz Matriz { get; init; } = null!;
        public Material Material { get; init; } = null!;
        public Matriz? MatrizAncestro { get; init; }
        public ComponenteMatriz? ComponenteAncestro { get; init; }
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Hoja { get; init; } = null!;
        public ConceptoPresupuesto? SegundaHoja { get; init; }
    }

    private static FormPresupuesto CrearFormulario(
        CountingContext ctx, Escenario escenario, string root)
        => new(ctx, escenario.Proyecto, new ProjectWorkspaceService(
            Path.Combine(root, "sopro"),
            Path.Combine(root, "local")));

    private static Escenario CrearEscenario(
        SOPROContext ctx, int hojas, bool incluirAncestroAuxiliar = false)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto comando reajuste",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            PorcentajeIVA = 0m,
            ModoCalculoPorcentajes = "Acumulables",
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-001",
            Descripcion = "Material de prueba",
            Unidad = "kg",
            PrecioUnitario = 10m,
            Notas = string.Empty,
        };
        ctx.Materiales.Add(material);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Matriz de prueba",
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        var componente = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 10m,
            Rendimiento = 1m,
            Importe = 100m,
            Orden = 1,
            Notas = string.Empty,
        };
        ctx.ComponentesMatriz.Add(componente);
        ctx.SaveChanges();

        Matriz? matrizAncestro = null;
        ComponenteMatriz? componenteAncestro = null;
        if (incluirAncestroAuxiliar)
        {
            matrizAncestro = new Matriz
            {
                ProyectoId = proyecto.Id,
                Clave = "BAS-001",
                Descripcion = "Matriz ancestro que consume la matriz reajustada",
                Unidad = "pza",
                Tipo = TipoMatriz.Basico,
                CostoDirecto = 200m,
                Notas = string.Empty,
                FechaCreacion = new DateTime(2026, 1, 1),
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            ctx.Matrices.Add(matrizAncestro);
            ctx.SaveChanges();

            componenteAncestro = new ComponenteMatriz
            {
                MatrizId = matrizAncestro.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = matriz.Id,
                Cantidad = 2m,
                Rendimiento = 1m,
                Importe = 200m,
                Orden = 1,
                Notas = string.Empty,
            };
            ctx.ComponentesMatriz.Add(componenteAncestro);
            ctx.SaveChanges();
        }

        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1",
            Descripcion = "Capítulo 1",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = hojas == 1 ? 500m : 700m,
            PrecioUnitario = 0m,
            ImporteTotal = hojas == 1 ? 500m : 700m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(agrupador);
        ctx.SaveChanges();

        var hoja = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concepto hoja",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "pza",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 5m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 500m,
            PrecioUnitario = 100m,
            ImporteTotal = 500m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(hoja);
        ctx.SaveChanges();

        ConceptoPresupuesto? segundaHoja = null;
        if (hojas > 1)
        {
            segundaHoja = new ConceptoPresupuesto
            {
                ProyectoId = proyecto.Id,
                Clave = "1.2",
                Descripcion = "Segundo concepto con la misma matriz",
                EsAgrupador = false,
                Nivel = 1,
                Orden = 2,
                Unidad = "pza",
                ColumnasPersonalizadasJSON = string.Empty,
                Notas = string.Empty,
                Cantidad = 2m,
                MatrizId = matriz.Id,
                CostoDirectoUnitario = 100m,
                CostoDirectoTotal = 200m,
                PrecioUnitario = 100m,
                ImporteTotal = 200m,
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            ctx.ConceptosPresupuesto.Add(segundaHoja);
            ctx.SaveChanges();
        }

        return new Escenario
        {
            Proyecto = proyecto,
            Matriz = matriz,
            Material = material,
            MatrizAncestro = matrizAncestro,
            ComponenteAncestro = componenteAncestro,
            Agrupador = agrupador,
            Hoja = hoja,
            SegundaHoja = segundaHoja,
        };
    }

    private static void EstablecerBloqueoReajusteIncierto(FormPresupuesto form, bool bloqueado)
    {
        var campo = typeof(FormPresupuesto).GetField(
            CampoBloqueoReajuste, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"No se encontró el campo {CampoBloqueoReajuste}.");

        campo.SetValue(form, bloqueado);
    }

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

    private static decimal LeerCostoDirectoEnBase(string dbPath, int matrizId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizId).CostoDirecto;
    }

    private static ConceptoPresupuesto LeerConceptoEnBase(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId);
    }

    private static int BuscarFilaPorConcepto(DataGridView grid, int conceptoId)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is ConceptoPresupuesto concepto && concepto.Id == conceptoId)
                return i;
        }

        return -1;
    }

    private static DataGridViewCell? BuscarCelda(DataGridView grid, int rowIndex, string nombreInterno)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;

        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.Tag is ColumnaPersonalizada definicion
                && string.Equals(definicion.NombreInterno, nombreInterno, StringComparison.OrdinalIgnoreCase))
            {
                return grid.Rows[rowIndex].Cells[col.Index];
            }
        }

        return null;
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");

    private static void EliminarDirectorio(string root)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
        }
    }
}
