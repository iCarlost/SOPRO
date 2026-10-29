using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Asignación de una matriz APU a un concepto YA existente por edición de Clave en el
/// <see cref="FormPresupuesto"/> real (ADR-0001), sobre SQLite y workspace temporales:
/// <list type="bullet">
/// <item>un UPDATE rechazado por la base no escribe ni ensucia el tracker compartido,
/// de modo que un guardado ajeno posterior no puede confirmarlo;</item>
/// <item>el borrador de la clave permanece visible y el reintento del mismo intento
/// confirma y sincroniza Tag y celdas sin marcarlos <c>Modified</c>.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoAsignacionMatrizComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void AsignacionMatrizComando_ClaveDeConceptoExistente_ConfirmaSincronizaTagYCeldas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_asignacion_ok_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            var claveCell = BuscarCelda(grid, fila, "Clave");
            Assert.IsNotNull(claveCell, "Debe existir la columna Clave.");
            claveCell!.Value = "APU-002";

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz,
                "La asignación de matriz debe confirmarse como comando.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", persistido.Clave);
            Assert.AreEqual(escenario.MatrizDestino.Id, persistido.MatrizId);
            Assert.AreEqual("Acero de refuerzo", persistido.Descripcion);
            Assert.AreEqual("kg", persistido.Unidad);
            Assert.AreEqual(10m, persistido.Cantidad);
            Assert.AreEqual(50m, persistido.CostoDirectoUnitario);
            Assert.AreEqual(500m, persistido.CostoDirectoTotal);
            Assert.AreEqual(50m, persistido.PrecioUnitario);
            Assert.AreEqual(500m, persistido.ImporteTotal);

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual("APU-002", tag.Clave, "El Tag se sincroniza con la clave confirmada.");
            Assert.AreEqual(escenario.MatrizDestino.Id, tag.MatrizId);
            Assert.AreEqual("Acero de refuerzo", tag.Descripcion);
            Assert.AreEqual("kg", tag.Unidad);
            Assert.AreEqual(500m, tag.ImporteTotal);
            Assert.AreEqual(persistido.FechaModificacion, tag.FechaModificacion,
                "El Tag queda con la marca confirmada en la base.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");

            Assert.AreEqual("APU-002", BuscarCelda(grid, fila, "Clave")!.Value?.ToString());
            Assert.AreEqual("Acero de refuerzo", BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual("kg", BuscarCelda(grid, fila, "Unidad")!.Value?.ToString());
            StringAssert.Contains(BuscarCelda(grid, fila, "PrecioUnitario")!.Value?.ToString() ?? string.Empty, "50");
            StringAssert.Contains(BuscarCelda(grid, fila, "Importe")!.Value?.ToString() ?? string.Empty, "500");

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La asignación confirmada no deja trabajo pendiente en el contexto compartido.");

            // Un guardado ajeno posterior al éxito no puede volver a escribir la fila.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", trasGuardadoAjeno.Clave);
            Assert.AreEqual(escenario.MatrizDestino.Id, trasGuardadoAjeno.MatrizId);
            Assert.AreEqual(persistido.FechaModificacion, trasGuardadoAjeno.FechaModificacion,
                "El contexto compartido no debe reescribir una fila ya confirmada.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
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
    public void AsignacionMatrizComando_FallaElUpdate_ConservaElBorradorYElReintentoConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_asignacion_fail_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            CrearTriggerDeFallo(dbPath);

            var claveCell = BuscarCelda(grid, fila, "Clave");
            Assert.IsNotNull(claveCell, "Debe existir la columna Clave.");
            claveCell!.Value = "APU-002";

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoAsignacionMatriz,
                "El UPDATE rechazado debe clasificarse como fallo revertido por el comando.");

            // El borrador sigue visible, pero nada se dio por guardado ni se tocó el Tag.
            Assert.AreEqual("APU-002", claveCell.Value?.ToString(),
                "La celda conserva el borrador del usuario tras el fallo.");
            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual("1.1", tag.Clave,
                "La ruta de comando no muta el Tag compartido antes de confirmar.");
            Assert.AreEqual(escenario.MatrizOrigen.Id, tag.MatrizId);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "La entidad compartida no queda Modified por un comando fallido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            Assert.AreEqual("1.1", LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Clave,
                "El UPDATE rechazado no puede quedar en la base.");

            // Se retira el trigger y se hace un guardado ajeno: no puede arrastrar el
            // borrador fallido porque la ruta heredada no se ejecutó para ese evento.
            EliminarTriggerDeFallo(dbPath);
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
            ctx.SaveChanges();

            Assert.AreEqual("1.1", LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Clave,
                "Un guardado ajeno no puede confirmar el UPDATE abortado.");

            // Reintento del MISMO intento: la celda aún tiene el borrador, así que se
            // invoca el manejador que consume el evento (asignar el mismo valor no
            // vuelve a disparar CellValueChanged).
            Assert.IsTrue(form.TryApplyBudgetConceptAssignmentByKey(fila),
                "El reintento debe consumirse en la ruta de asignación.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz,
                "El reintento sin trigger debe confirmarse.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", persistido.Clave);
            Assert.AreEqual(escenario.MatrizDestino.Id, persistido.MatrizId);
            Assert.AreEqual(500m, persistido.ImporteTotal);

            Assert.AreEqual("APU-002", tag.Clave, "El Tag se sincroniza tras el reintento.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
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
    public void AsignacionMatrizSeleccionada_ConceptoExistente_ConfirmaSincronizaTagYCeldas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_ok_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    fila,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: true),
                "La fila persistida debe consumirse en la ruta de comando.");

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz,
                "La asignación de la matriz seleccionada debe confirmarse como comando.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("1.1", persistido.Clave, "La clave actual se conserva.");
            Assert.AreEqual(escenario.MatrizDestino.Id, persistido.MatrizId);
            Assert.AreEqual("Concreto simple en obra", persistido.Descripcion,
                "La descripción elaborada del usuario se conserva.");
            Assert.AreEqual("m3", persistido.Unidad, "La unidad actual se conserva.");
            Assert.AreEqual(7m, persistido.Cantidad);
            Assert.AreEqual(50m, persistido.CostoDirectoUnitario);
            Assert.AreEqual(350m, persistido.CostoDirectoTotal);
            Assert.AreEqual(50m, persistido.PrecioUnitario);
            Assert.AreEqual(350m, persistido.ImporteTotal);

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual("1.1", tag.Clave);
            Assert.AreEqual(escenario.MatrizDestino.Id, tag.MatrizId);
            Assert.AreEqual("Concreto simple en obra", tag.Descripcion);
            Assert.AreEqual("m3", tag.Unidad);
            Assert.AreEqual(7m, tag.Cantidad);
            Assert.AreEqual(350m, tag.ImporteTotal);
            Assert.AreEqual(persistido.FechaModificacion, tag.FechaModificacion,
                "El Tag queda con la marca confirmada en la base.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");

            Assert.AreEqual("1.1", BuscarCelda(grid, fila, "Clave")!.Value?.ToString());
            Assert.AreEqual("Concreto simple en obra",
                BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual("m3", BuscarCelda(grid, fila, "Unidad")!.Value?.ToString());
            Assert.AreEqual(7m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value));
            StringAssert.Contains(BuscarCelda(grid, fila, "PrecioUnitario")!.Value?.ToString() ?? string.Empty, "50");
            StringAssert.Contains(BuscarCelda(grid, fila, "Importe")!.Value?.ToString() ?? string.Empty, "350");

            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0, "El agrupador debe estar en el grid cargado.");
            Assert.AreEqual(350m, escenario.Agrupador.ImporteTotal,
                "El agrupador compartido se sincroniza con el total confirmado.");
            StringAssert.Contains(
                BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString() ?? string.Empty,
                "350",
                "La celda del agrupador refleja el total confirmado.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Agrupador).State);

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La asignación confirmada no deja trabajo pendiente en el contexto compartido.");

            // Un guardado ajeno posterior al éxito no puede volver a escribir la fila.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(escenario.MatrizDestino.Id, trasGuardadoAjeno.MatrizId);
            Assert.AreEqual(7m, trasGuardadoAjeno.Cantidad);
            Assert.AreEqual(persistido.FechaModificacion, trasGuardadoAjeno.FechaModificacion,
                "El contexto compartido no debe reescribir una fila ya confirmada.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
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
    public void AsignacionMatrizSeleccionada_FallaElUpdate_ConservaElBorradorYElReintentoConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_fail_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            CrearTriggerDeFallo(dbPath);

            Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                    fila,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: true),
                "Un comando no confirmado no se reporta como asignación aplicada.");

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoAsignacionMatriz,
                "El UPDATE rechazado debe clasificarse como fallo revertido por el comando.");
            Assert.IsNotNull(form.UltimoDiagnosticoAsignacionMatriz,
                "El intento no confirmado debe dejar diagnóstico.");

            // Los valores visibles ORIGINALES se conservan: no se pinta el borrador de
            // la selección (la cantidad propuesta 7 no se escribe en la grilla).
            Assert.AreEqual("1.1", BuscarCelda(grid, fila, "Clave")!.Value?.ToString());
            Assert.AreEqual("Concreto simple en obra",
                BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual("m3", BuscarCelda(grid, fila, "Unidad")!.Value?.ToString());
            Assert.AreEqual(10m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value),
                "La fila conserva la cantidad visible original.");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(escenario.MatrizOrigen.Id, tag.MatrizId,
                "La ruta de comando no muta el Tag compartido antes de confirmar.");
            Assert.AreEqual(10m, tag.Cantidad);
            Assert.AreEqual(1000m, tag.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "La entidad compartida no queda Modified por un comando fallido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            var enBaseTrasFallo = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(escenario.MatrizOrigen.Id, enBaseTrasFallo.MatrizId,
                "El UPDATE rechazado no puede quedar en la base.");
            Assert.AreEqual(10m, enBaseTrasFallo.Cantidad);

            // Un guardado ajeno posterior al fallo no puede arrastrar el borrador.
            EliminarTriggerDeFallo(dbPath);
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
            ctx.SaveChanges();

            Assert.AreEqual(escenario.MatrizOrigen.Id,
                LeerConceptoEnBase(dbPath, escenario.Concepto.Id).MatrizId,
                "Un guardado ajeno no puede confirmar el UPDATE abortado.");

            // Reintento del mismo intento: se retira el trigger y se vuelve a ejecutar la selección.
            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    fila,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: true),
                "El reintento debe consumirse en la ruta de asignación.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz,
                "El reintento sin trigger debe confirmarse.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(escenario.MatrizDestino.Id, persistido.MatrizId);
            Assert.AreEqual(7m, persistido.Cantidad);
            Assert.AreEqual(350m, persistido.ImporteTotal);

            Assert.AreEqual(escenario.MatrizDestino.Id, tag.MatrizId,
                "El Tag se sincroniza tras el reintento.");
            Assert.AreEqual(350m, tag.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
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
    public void AsignacionMatrizSeleccionada_CambiosPendientesEnContextoCompartido_ConservaBorradorSinEscribir()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_pending_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            // Trabajo ajeno pendiente en el contexto compartido: el comando no debe
            // enviarse, pero tampoco ejecutarse el guardado heredado.
            escenario.Proyecto.Descripcion = "cambio pendiente ajeno";

            Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                    fila,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: true),
                "Sin despacho no hay asignación aplicada: la fila conserva lo visible.");

            Assert.IsNull(form.UltimoEstadoAsignacionMatriz,
                "Sin despacho no hay estado de comando; la distinción queda explícita.");
            StringAssert.Contains(form.UltimoDiagnosticoAsignacionMatriz ?? string.Empty,
                "no enviada",
                "El intento no enviado debe registrar su diagnóstico.");

            Assert.AreEqual("1.1", BuscarCelda(grid, fila, "Clave")!.Value?.ToString());
            Assert.AreEqual("Concreto simple en obra",
                BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual("m3", BuscarCelda(grid, fila, "Unidad")!.Value?.ToString());
            Assert.AreEqual(10m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value),
                "Los valores visibles originales no se sobrescriben con el borrador.");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(escenario.MatrizOrigen.Id, tag.MatrizId,
                "El Tag compartido no se muta antes de un commit.");
            Assert.AreEqual(10m, tag.Cantidad);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El cambio ajeno pendiente no debe descartarse ni guardarse aquí.");

            Assert.AreEqual(string.Empty,
                LeerProyectoEnBase(dbPath, escenario.Proyecto.Id).Descripcion,
                "No se escribe en disco el trabajo pendiente del contexto compartido.");

            var conceptoEnBase = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(escenario.MatrizOrigen.Id, conceptoEnBase.MatrizId,
                "No se escribe la asignación cuando el comando no se envió.");
            Assert.AreEqual(10m, conceptoEnBase.Cantidad);
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
    public void AsignacionMatrizSeleccionada_DescripcionEnEdicion_UsaTextoVisible()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_edit_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            var descCell = BuscarCelda(grid, fila, "Descripcion");
            Assert.IsNotNull(descCell);
            grid.CurrentCell = descCell;
            form.CreateControl();
            if (!grid.IsHandleCreated)
                grid.CreateControl();

            Assert.IsTrue(grid.BeginEdit(true),
                "El harness debe permitir simular una Descripción en edición activa.");
            var editor = grid.EditingControl as TextBox;
            Assert.IsNotNull(editor, "La celda Descripción debe editarse con un TextBox.");
            editor!.Text = "Descripción en edición activa";

            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    fila,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: true));

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz);
            Assert.AreEqual("Descripción en edición activa",
                LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Descripcion,
                "El EndEdit previo debe confirmar el texto visible de la celda, no el valor viejo.");
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
    public void AsignacionMatrizSeleccionada_FilaNueva_ConfirmaAdjuntaTagConIdYSincronizaCeldas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_new_ok_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);
            int conceptosIniciales = ContarConceptosEnBase(dbPath, escenario.Proyecto.Id);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaNueva = BuscarFilaVacia(grid);
            Assert.IsTrue(filaNueva >= 0, "Debe existir una fila vacía para el alta.");
            Assert.IsNull(grid.Rows[filaNueva].Tag,
                "La fila nueva debe ser genuinamente no persistida (Tag null).");

            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    filaNueva,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: false),
                "La fila nueva debe consumirse en la ruta de comando.");

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz,
                "El alta de la fila debe confirmarse como comando.");

            var tag = (ConceptoPresupuesto)grid.Rows[filaNueva].Tag!;
            Assert.IsTrue(tag.Id > 0, "El Tag se adjunta con el Id generado en el comando.");
            Assert.AreEqual(escenario.MatrizDestino.Id, tag.MatrizId);
            Assert.AreEqual("APU-002", tag.Clave);
            Assert.AreEqual(7m, tag.Cantidad);
            Assert.AreEqual(350m, tag.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag adjuntado queda Unchanged para no insertarse de nuevo.");

            Assert.AreEqual(conceptosIniciales + 1, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id),
                "El alta crea exactamente una fila.");

            var persistido = LeerConceptoEnBase(dbPath, tag.Id);
            Assert.AreEqual(2, persistido.Orden, "La fila nueva se agrega al final del rowset.");
            Assert.AreEqual(5, persistido.Nivel);
            Assert.IsFalse(persistido.EsAgrupador);
            Assert.AreEqual(escenario.MatrizDestino.Id, persistido.MatrizId);
            Assert.AreEqual(350m, persistido.ImporteTotal);

            Assert.AreEqual("APU-002", BuscarCelda(grid, filaNueva, "Clave")!.Value?.ToString());
            Assert.AreEqual("Acero de refuerzo",
                BuscarCelda(grid, filaNueva, "Descripcion")!.Value?.ToString());
            StringAssert.Contains(
                BuscarCelda(grid, filaNueva, "Importe")!.Value?.ToString() ?? string.Empty, "350");

            Assert.AreEqual(1, LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Orden,
                "La fila existente conserva su Orden.");
            Assert.AreEqual(1350m, escenario.Agrupador.ImporteTotal,
                "El agrupador compartido se sincroniza con el total confirmado.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Agrupador).State);

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El alta confirmada no deja trabajo pendiente en el contexto compartido.");
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
    public void AsignacionMatrizSeleccionada_FilaNuevaFallaElInsert_ConservaBorradorYElReintentoInsertaUnaSolaVez()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_new_fail_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);
            int conceptosIniciales = ContarConceptosEnBase(dbPath, escenario.Proyecto.Id);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaNueva = BuscarFilaVacia(grid);
            Assert.IsTrue(filaNueva >= 0, "Debe existir una fila vacía para el alta.");

            CrearTriggerDeFalloInsercion(dbPath);

            Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                    filaNueva,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: false),
                "Un alta no confirmada no se reporta como asignación aplicada.");

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoAsignacionMatriz,
                "El INSERT rechazado debe clasificarse como fallo revertido.");
            Assert.IsNotNull(form.UltimoDiagnosticoAsignacionMatriz,
                "El intento no confirmado debe dejar diagnóstico.");

            Assert.IsNull(grid.Rows[filaNueva].Tag,
                "Un alta fallida conserva la fila como no persistida (Tag null).");
            Assert.IsTrue(string.IsNullOrEmpty(BuscarCelda(grid, filaNueva, "Clave")!.Value?.ToString()),
                "Un alta fallida no pinta un borrador ficticio: la fila conserva sus valores visibles.");
            Assert.IsTrue(string.IsNullOrEmpty(BuscarCelda(grid, filaNueva, "Descripcion")!.Value?.ToString()));
            Assert.IsNull(BuscarCelda(grid, filaNueva, "Cantidad")!.Value,
                "La cantidad visible original de la fila vacía no se sobrescribe.");
            Assert.AreEqual(conceptosIniciales, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id),
                "El INSERT rechazado no deja la fila nueva.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un alta fallida no deja trabajo pendiente en el contexto compartido.");

            EliminarTriggerDeFalloInsercion(dbPath);

            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    filaNueva,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: false),
                "El reintento debe consumirse en la ruta de asignación.");

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz,
                "El reintento sin trigger debe confirmarse.");

            var tag = (ConceptoPresupuesto)grid.Rows[filaNueva].Tag!;
            Assert.IsTrue(tag.Id > 0);
            Assert.AreEqual(conceptosIniciales + 1, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id),
                "El reintento inserta una sola fila.");
            Assert.AreEqual(tag.Id, LeerConceptoEnBase(dbPath, tag.Id).Id);
            Assert.AreEqual(350m, tag.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
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
    public void AsignacionMatrizSeleccionada_FilaCreadaPorCambioTipo_ActualizaLaExistenteSinDuplicar()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_tipo_luego_matriz_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);
            int conceptosIniciales = ContarConceptosEnBase(dbPath, escenario.Proyecto.Id);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaNueva = BuscarFilaVacia(grid);
            Assert.IsTrue(filaNueva >= 0, "Debe existir una fila vacía para el alta por Tipo.");

            // El cambio de Tipo persiste la fila con Id generado por el comando.
            var tipoCell = BuscarCelda(grid, filaNueva, "Tipo");
            Assert.IsNotNull(tipoCell);
            tipoCell!.Value = "Concepto";

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoCambioTipo,
                "El cambio de tipo debe crear la fila persistida como comando.");
            var tagCreado = (ConceptoPresupuesto)grid.Rows[filaNueva].Tag!;
            Assert.IsTrue(tagCreado.Id > 0, "La fila creada por Tipo debe tener Id.");
            int idCreado = tagCreado.Id;
            Assert.AreEqual(conceptosIniciales + 1, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id));

            // Asignar la matriz debe ACTUALIZAR esa fila existente, nunca insertar otra.
            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    filaNueva,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: false));

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz);
            Assert.AreEqual(conceptosIniciales + 1, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id),
                "La asignación de una fila creada por Tipo no inserta un duplicado.");

            var tagAsignado = (ConceptoPresupuesto)grid.Rows[filaNueva].Tag!;
            Assert.AreEqual(idCreado, tagAsignado.Id,
                "El Tag conserva el Id de la fila creada por el cambio de tipo.");
            Assert.AreEqual(escenario.MatrizDestino.Id, tagAsignado.MatrizId);
            Assert.AreEqual(350m, tagAsignado.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagAsignado).State);

            var persistido = LeerConceptoEnBase(dbPath, idCreado);
            Assert.AreEqual(idCreado, persistido.Id);
            Assert.AreEqual(escenario.MatrizDestino.Id, persistido.MatrizId);
            Assert.AreEqual(350m, persistido.ImporteTotal);
            Assert.AreEqual(2, persistido.Orden,
                "La actualización conserva la posición que le dio el alta por Tipo.");
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
    public void AsignacionMatrizSeleccionada_AltaConResultadoIncierto_BloqueaElReintentoYNoLoReportaComoTrackerSucio()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_selector_new_unknown_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenario(ctx);
            int conceptosIniciales = ContarConceptosEnBase(dbPath, escenario.Proyecto.Id);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaNueva = BuscarFilaVacia(grid);
            Assert.IsTrue(filaNueva >= 0, "Debe existir una fila vacía para el alta.");

            // 1er intento: el comando confirma el INSERT y el cierre del contexto falla
            // -> OutcomeUnknown real, sin tocar datos reales.
            var factoryPrevia = SustituirFactory(form, new DisposeThrowingFactory(dbPath));
            try
            {
                Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                        filaNueva,
                        escenario.MatrizDestino,
                        7m,
                        preserveCurrentTexts: false),
                    "Un resultado incierto no se reporta como asignación aplicada.");
            }
            finally
            {
                SustituirFactory(form, factoryPrevia);
            }

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, form.UltimoEstadoAsignacionMatriz,
                "El alta quedó con resultado incierto.");
            Assert.AreEqual(conceptosIniciales + 1, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id),
                "El INSERT se confirmó antes de que fallara el cierre del contexto.");
            Assert.IsNull(grid.Rows[filaNueva].Tag,
                "La vista no se pinta con un resultado incierto.");

            // 2º intento sobre la misma fila nueva: la causa del bloqueo es la
            // incertidumbre previa, no trabajo pendiente en el contexto compartido.
            Assert.IsFalse(form.TryApplySelectedMatrixAssignment(
                    filaNueva,
                    escenario.MatrizDestino,
                    7m,
                    preserveCurrentTexts: false),
                "La alta debe seguir bloqueada hasta reconciliar con la base.");

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, form.UltimoEstadoAsignacionMatriz,
                "El estado debe seguir reflejando la incertidumbre, no un tracker sucio.");

            string diagnostico = form.UltimoDiagnosticoAsignacionMatriz ?? string.Empty;
            StringAssert.Contains(diagnostico, "incierto",
                "El diagnóstico debe decir que la alta anterior tuvo resultado incierto.");
            StringAssert.Contains(diagnostico, "Reconcilie",
                "El diagnóstico debe pedir reconciliar antes de reintentar.");
            Assert.IsFalse(diagnostico.Contains("cambios pendientes", StringComparison.OrdinalIgnoreCase),
                "El bloqueo NO es por trabajo pendiente en el contexto compartido.");

            Assert.AreEqual(conceptosIniciales + 1, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id),
                "El reintento bloqueado no envía otra operación ni inserta una segunda fila.");
            Assert.IsNull(grid.Rows[filaNueva].Tag,
                "Un intento bloqueado no cambia ni pinta la fila.");

            // Recarga segura: libera el bloqueo (misma ruta que reconciliación de la vista).
            form.RecargarTrasDialogoColumnas();

            int filaVaciaTrasRecarga = BuscarFilaVacia(grid);
            Assert.IsTrue(filaVaciaTrasRecarga >= 0,
                "Tras la recarga debe quedar una fila vacía disponible para el alta.");

            Assert.IsTrue(form.TryApplySelectedMatrixAssignment(
                    filaVaciaTrasRecarga,
                    escenario.MatrizDestino,
                    2m,
                    preserveCurrentTexts: false),
                "Tras la recarga segura el alta vuelve a estar disponible.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoAsignacionMatriz);
            Assert.AreEqual(conceptosIniciales + 2, ContarConceptosEnBase(dbPath, escenario.Proyecto.Id));
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

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Matriz MatrizOrigen { get; init; } = null!;
        public Matriz MatrizDestino { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto asignación matriz",
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
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        var matrizOrigen = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m,
            Notas = string.Empty,
        };

        var matrizDestino = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-002",
            Descripcion = "Acero de refuerzo",
            Unidad = "kg",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 50m,
            Notas = string.Empty,
        };

        ctx.Matrices.AddRange(matrizOrigen, matrizDestino);
        ctx.SaveChanges();

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
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 0m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concreto simple en obra",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 10m,
            MatrizId = matrizOrigen.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.AddRange(agrupador, concepto);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            MatrizOrigen = matrizOrigen,
            MatrizDestino = matrizDestino,
            Agrupador = agrupador,
            Concepto = concepto,
        };
    }

    private static void CrearTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

    private static void EliminarTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_concepto;");

    private static void CrearTriggerDeFalloInsercion(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_insert_concepto BEFORE INSERT ON ConceptosPresupuesto " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_insert'); END;");

    private static void EliminarTriggerDeFalloInsercion(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_insert_concepto;");

    private static int ContarConceptosEnBase(string dbPath, int proyectoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Count(c => c.ProyectoId == proyectoId);
    }

    private static int BuscarFilaVacia(DataGridView grid)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is not null)
                continue;

            var tipo = BuscarCelda(grid, i, "Tipo")?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(tipo))
                return i;
        }

        return -1;
    }

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

    private static ConceptoPresupuesto LeerConceptoEnBase(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Single(c => c.Id == conceptoId);
    }

    private static Proyecto LeerProyectoEnBase(string dbPath, int proyectoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.Proyectos
            .AsNoTracking()
            .Single(p => p.Id == proyectoId);
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

    private static System.Reflection.FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");

    /// <summary>
    /// Fábrica de prueba por instancia: el contexto confirma y luego falla al cerrarse,
    /// lo que produce un OutcomeUnknown REAL (la escritura ya quedó en la base).
    /// </summary>
    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath) => new DisposeThrowingContext(_dbPath);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        public DisposeThrowingContext(string dbPath)
            : base(dbPath)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("dispose_simulado_asignacion");
        }
    }

    /// <summary>
    /// Sustituye (y devuelve el previo) la fábrica del formulario para inducir fallos de
    /// cierre postcommit. Se restaura siempre en el llamador.
    /// </summary>
    private static IProjectDbContextFactory SustituirFactory(
        FormPresupuesto form, IProjectDbContextFactory factory)
    {
        var campo = typeof(FormPresupuesto).GetField(
            "_factory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró el campo _factory.");

        var previo = (IProjectDbContextFactory)campo.GetValue(form)!;
        campo.SetValue(form, factory);
        return previo;
    }

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
