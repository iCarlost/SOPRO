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
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Pegado atómico en el <see cref="FormPresupuesto"/> real (ADR-0001): el bloque se
/// procesa como un único comando, la vista no muta nada antes del commit y el contexto
/// compartido no escribe (nada de <c>GuardarCambios</c>/ApplyAutoSaveChanges por celda).
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoPegadoComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void PegadoComando_DosFilasConOffsetYOverrides_ConfirmaSincronizaGridYSinEscribirElContextoCompartido()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_ok_{Guid.NewGuid():N}");
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

            int escriturasContextoCompartido = 0;
            ctx.SavedChanges += (_, __) => escriturasContextoCompartido++;

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            int filaDos = BuscarFilaPorConcepto(grid, escenario.ConceptoDos.Id);
            Assert.AreEqual(fila + 1, filaDos, "El escenario debe dejar las dos filas contiguas para el offset.");

            var claveCell = BuscarCelda(grid, fila, "Clave");
            Assert.IsNotNull(claveCell);
            grid.CurrentCell = claveCell;

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7\nAPU-001\t\t\t0",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.Committed, outcome.Kind, outcome.Message);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoPegado);
            Assert.AreEqual(1, outcome.SkippedCells, "La cantidad 0 de la segunda fila se omite.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", persistido.Clave);
            Assert.AreEqual("Descripción pegada", persistido.Descripcion);
            Assert.AreEqual("kg", persistido.Unidad);
            Assert.AreEqual(7m, persistido.Cantidad);
            Assert.AreEqual(50m, persistido.CostoDirectoUnitario);
            Assert.AreEqual(350m, persistido.CostoDirectoTotal);
            Assert.AreEqual(350m, persistido.ImporteTotal);

            var persistidoDos = LeerConceptoEnBase(dbPath, escenario.ConceptoDos.Id);
            Assert.AreEqual("APU-001", persistidoDos.Clave);
            Assert.AreEqual(string.Empty, persistidoDos.Descripcion,
                "El texto vacío pegado limpia la descripción, como en la ruta heredada.");
            Assert.AreEqual(string.Empty, persistidoDos.Unidad);
            Assert.AreEqual(3m, persistidoDos.Cantidad, "La cantidad inválida no pisa la existente.");
            Assert.AreEqual(300m, persistidoDos.ImporteTotal);

            var agrupador = LeerConceptoEnBase(dbPath, escenario.Agrupador.Id);
            Assert.AreEqual(650m, agrupador.ImporteTotal,
                "Los totales de agrupadores se confirman en la misma operación.");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual("APU-002", tag.Clave);
            Assert.AreEqual("Descripción pegada", tag.Descripcion);
            Assert.AreEqual(7m, tag.Cantidad);
            Assert.AreEqual(350m, tag.ImporteTotal);
            Assert.AreEqual(persistido.FechaModificacion, tag.FechaModificacion);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag sincronizado queda Unchanged.");

            Assert.AreEqual("APU-002", BuscarCelda(grid, fila, "Clave")!.Value?.ToString());
            Assert.AreEqual("Descripción pegada", BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual("kg", BuscarCelda(grid, fila, "Unidad")!.Value?.ToString());
            Assert.AreEqual(7m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value));
            StringAssert.Contains(BuscarCelda(grid, fila, "Importe")!.Value?.ToString() ?? string.Empty, "350");

            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            StringAssert.Contains(
                BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString() ?? string.Empty,
                "650");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Agrupador).State);

            Assert.AreEqual(0, escriturasContextoCompartido,
                "El pegado no puede guardar por la ruta heredada (ApplyAutoSaveChanges).");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El contexto compartido queda sin trabajo pendiente.");
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
    public void PegadoComando_FallaElUpdateDeLaSegundaFila_NoEscribeNiMutaElGridYElReintentoConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_fail_{Guid.NewGuid():N}");
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
            int filaDos = BuscarFilaPorConcepto(grid, escenario.ConceptoDos.Id);
            int filasGridPrevias = grid.Rows.Count;
            int filasBasePrevias = ContarConceptos(dbPath);

            var claveCell = BuscarCelda(grid, fila, "Clave");
            grid.CurrentCell = claveCell;
            string claveOriginal = claveCell!.Value?.ToString() ?? string.Empty;

            CrearTriggerDeFallo(dbPath, escenario.ConceptoDos.Id);

            const string bloque = "APU-002\tPrimera pegada\tkg\t7\nAPU-001\tSegunda pegada\tkg\t2";

            var fallido = form.ProcesarPegadoPresupuesto(bloque, _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.Failed, fallido.Kind, fallido.Message);
            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoPegado);

            // La vista conserva TODO: ni celdas ni Tags se mutaron antes del commit.
            Assert.AreEqual(claveOriginal, claveCell.Value?.ToString());
            Assert.AreEqual("C-001", ((ConceptoPresupuesto)grid.Rows[fila].Tag!).Clave);
            Assert.AreEqual("C-002", ((ConceptoPresupuesto)grid.Rows[filaDos].Tag!).Clave);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(grid.Rows[fila].Tag!).State);
            Assert.AreEqual(filasGridPrevias, grid.Rows.Count, "El pegado no agrega filas al grid.");

            var conceptoEnBase = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("C-001", conceptoEnBase.Clave, "La primera fila del lote no puede quedar escrita.");
            Assert.AreEqual(1000m, conceptoEnBase.ImporteTotal);

            var agrupador = LeerConceptoEnBase(dbPath, escenario.Agrupador.Id);
            Assert.AreEqual(1300m, agrupador.ImporteTotal,
                "Los agrupadores tampoco pueden quedar a medias.");
            Assert.AreEqual(filasBasePrevias, ContarConceptos(dbPath));

            EliminarTriggerDeFallo(dbPath);

            var reintento = form.ProcesarPegadoPresupuesto(bloque, _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.Committed, reintento.Kind, reintento.Message);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoPegado);
            Assert.AreEqual(filasBasePrevias, ContarConceptos(dbPath),
                "El reintento del mismo bloque no duplica filas.");

            Assert.AreEqual("APU-002", LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Clave);
            Assert.AreEqual(350m, LeerConceptoEnBase(dbPath, escenario.Concepto.Id).ImporteTotal);
            Assert.AreEqual(200m, LeerConceptoEnBase(dbPath, escenario.ConceptoDos.Id).ImporteTotal);
            Assert.AreEqual(550m, LeerConceptoEnBase(dbPath, escenario.Agrupador.Id).ImporteTotal);
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
    public void PegadoComando_CopiaDesdeFilaOrigen_ConfirmaOCancelaSinCambiosParciales()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_copy_{Guid.NewGuid():N}");
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
            var claveCell = BuscarCelda(grid, fila, "Clave");
            grid.CurrentCell = claveCell;

            int confirmaciones = 0;
            var cancelado = form.ProcesarPegadoPresupuesto("C-002", _ =>
            {
                confirmaciones++;
                return false;
            });

            Assert.AreEqual(BudgetPasteOutcomeKind.Cancelled, cancelado.Kind, cancelado.Message);
            Assert.AreEqual(1, confirmaciones, "La copia desde otra fila exige confirmación.");
            Assert.IsNull(form.UltimoEstadoPegado, "Cancelar no llega a enviar el comando.");
            Assert.AreEqual("C-001", claveCell!.Value?.ToString(), "La celda conserva la clave original.");
            Assert.AreEqual("C-001", LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Clave);

            var confirmado = form.ProcesarPegadoPresupuesto("C-002", _ =>
            {
                confirmaciones++;
                return true;
            });

            Assert.AreEqual(BudgetPasteOutcomeKind.Committed, confirmado.Kind, confirmado.Message);
            Assert.AreEqual(2, confirmaciones);

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("C-002", persistido.Clave);
            Assert.AreEqual("Acero en obra", persistido.Descripcion,
                "La copia toma los textos del concepto de origen.");
            Assert.AreEqual(3m, persistido.Cantidad);
            Assert.AreEqual(300m, persistido.ImporteTotal);

            var origen = LeerConceptoEnBase(dbPath, escenario.ConceptoDos.Id);
            Assert.AreEqual("C-002", origen.Clave);
            Assert.AreEqual(300m, origen.ImporteTotal, "La fila de origen no se modifica.");

            Assert.AreEqual("C-002", BuscarCelda(grid, fila, "Clave")!.Value?.ToString());
            Assert.AreEqual("Acero en obra", BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(grid.Rows[fila].Tag!).State);
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
    public void PegadoComando_DestinoSinConceptoPersistido_RechazaAntesDeEscribirNiAgregarFilas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_reject_{Guid.NewGuid():N}");
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

            int escriturasContextoCompartido = 0;
            ctx.SavedChanges += (_, __) => escriturasContextoCompartido++;

            var grid = form.GridPrincipal;
            int filaVacia = BuscarFilaVacia(grid);
            Assert.IsTrue(filaVacia >= 0, "El grid debe tener filas vacías de captura.");

            int filasPrevias = grid.Rows.Count;
            int filasBasePrevias = ContarConceptos(dbPath);

            grid.CurrentCell = BuscarCelda(grid, filaVacia, "Clave");

            var outcome = form.ProcesarPegadoPresupuesto("APU-002", _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.Rejected, outcome.Kind);
            StringAssert.Contains(outcome.Message, "No se modificó nada");
            StringAssert.Contains(outcome.Message, "fila " + (filaVacia + 1) + " del presupuesto");
            Assert.IsNull(form.UltimoEstadoPegado, "El lote se rechaza antes de enviar el comando.");
            Assert.AreEqual(filasPrevias, grid.Rows.Count, "No se agregan filas vacías como hacía la ruta heredada.");
            Assert.AreEqual(filasBasePrevias, ContarConceptos(dbPath));
            Assert.AreEqual(0, escriturasContextoCompartido);
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
    public void PegadoComando_FilaCambioEnLaBase_ConflictoSinActualizarElGrid()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_conflict_{Guid.NewGuid():N}");
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

            using (var ajeno = new SOPROContext(dbPath))
            {
                var concepto = ajeno.ConceptosPresupuesto.Single(c => c.Id == escenario.Concepto.Id);
                concepto.Descripcion = "Descripción cambiada en la base";
                ajeno.SaveChanges();
            }

            var claveCell = BuscarCelda(grid, fila, "Clave");
            grid.CurrentCell = claveCell;

            string descripcionGrid = BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString() ?? string.Empty;
            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            DateTime fechaTag = tag.FechaModificacion;

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.Failed, outcome.Kind);
            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoPegado);
            StringAssert.Contains(outcome.Message, "recargarse");

            Assert.AreEqual(descripcionGrid, BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString(),
                "El grid no se actualiza cuando el comando no se confirma.");
            Assert.AreEqual("C-001", claveCell!.Value?.ToString());
            Assert.AreEqual("C-001", tag.Clave);
            Assert.AreEqual(fechaTag, tag.FechaModificacion);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("Descripción cambiada en la base", persistido.Descripcion);
            Assert.AreEqual("C-001", persistido.Clave);
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
    public void PegadoComando_TodasLasCeldasInvalidas_NoIntentaComandoNiEscribeNiSellaFecha()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_noop_{Guid.NewGuid():N}");
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

            int escriturasContextoCompartido = 0;
            ctx.SavedChanges += (_, __) => escriturasContextoCompartido++;

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            var cantidadCell = BuscarCelda(grid, fila, "Cantidad");
            Assert.IsNotNull(cantidadCell);
            grid.CurrentCell = cantidadCell;

            var antes = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            int filasPrevias = grid.Rows.Count;

            // Bloque válido para el parser, pero la única celda se rechaza por valor
            // inválido (cantidad <= 0): equivale a la ruta heredada con hadChanges=false.
            var outcome = form.ProcesarPegadoPresupuesto("0", _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.NoChanges, outcome.Kind);
            Assert.AreEqual(1, outcome.SkippedCells,
                "Se conserva la notificación de celdas omitidas.");
            Assert.IsNull(form.UltimoEstadoPegado,
                "No debe intentarse ningún comando cuando no hay operaciones.");
            Assert.AreEqual(filasPrevias, grid.Rows.Count);
            Assert.AreEqual(antes.Cantidad, Convert.ToDecimal(cantidadCell.Value),
                "El grid conserva la cantidad original.");

            var despues = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(antes.Cantidad, despues.Cantidad);
            Assert.AreEqual(antes.ImporteTotal, despues.ImporteTotal);
            Assert.AreEqual(antes.FechaModificacion, despues.FechaModificacion,
                "Un pegado sin celdas aceptadas no puede sellar FechaModificacion.");

            Assert.AreEqual(0, escriturasContextoCompartido);
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
    public void ClasificarResultadoPegado_TodosLosEstados_NoConfundeInciertoConFalloRevertido()
    {
        foreach (var status in Enum.GetValues<BudgetOperationStatus>())
        {
            var outcome = FormPresupuesto.ClasificarResultadoPegado(status, "mensaje de la base");

            switch (status)
            {
                case BudgetOperationStatus.Committed:
                    Assert.AreEqual(BudgetPasteOutcomeKind.Committed, outcome.Kind, status.ToString());
                    break;

                case BudgetOperationStatus.Conflict:
                case BudgetOperationStatus.Failed:
                    Assert.AreEqual(BudgetPasteOutcomeKind.Failed, outcome.Kind, status.ToString());
                    Assert.AreEqual("mensaje de la base", outcome.Message);
                    break;

                default:
                    Assert.AreEqual(BudgetPasteOutcomeKind.OutcomeUnknown, outcome.Kind, status.ToString());
                    StringAssert.Contains(outcome.Message, "antes de reintentar", status.ToString());
                    StringAssert.Contains(outcome.Message, "No se reintentó automáticamente", status.ToString());
                    Assert.IsFalse(
                        outcome.Message.Contains("no se modificó nada", StringComparison.OrdinalIgnoreCase),
                        $"{status}: un commit incierto no puede afirmar que la base quedó sin cambios.");
                    break;
            }
        }

        var incierto = FormPresupuesto.ClasificarResultadoPegado(BudgetOperationStatus.OutcomeUnknown, null);
        Assert.AreEqual(BudgetPasteOutcomeKind.OutcomeUnknown, incierto.Kind);
        Assert.AreEqual("Pegado sin confirmar", incierto.Title);
        StringAssert.Contains(incierto.Message, "concilie con la base");
    }

    [TestMethod]
    public void PegadoComando_VistaFallaDespuesDelCommit_RecargaDesdeBaseYSinDecirQueNoSeModifico()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_vistareload_{Guid.NewGuid():N}");
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

            int escriturasContextoCompartido = 0;
            ctx.SavedChanges += (_, __) => escriturasContextoCompartido++;

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            var claveCell = BuscarCelda(grid, fila, "Clave");
            grid.CurrentCell = claveCell;

            // Conector de prueba: la vista falla DESPUÉS del commit y DESPUÉS de aplicar
            // la primera fila del bloque de dos, dejando la vista parcialmente aplicada.
            form.PruebaFilasAplicadasAntesDeFallo = 1;

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7\nAPU-001\tSegunda pegada\tkg\t2",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.CommittedViewNeedsReload, outcome.Kind);
            Assert.AreEqual("Pegado guardado", outcome.Title);
            StringAssert.Contains(outcome.Message, "guardado en la base",
                "El mensaje debe afirma que la base quedó guardada.");
            Assert.IsFalse(
                outcome.Message.Contains("no se modificó nada", StringComparison.OrdinalIgnoreCase),
                "Tras un commit no se puede decir que no se modificó nada.");

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoPegado,
                "El commit de la base sí ocurrió.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", persistido.Clave);
            Assert.AreEqual("Descripción pegada", persistido.Descripcion);
            Assert.AreEqual(7m, persistido.Cantidad);
            Assert.AreEqual(350m, persistido.ImporteTotal);

            var persistidoDos = LeerConceptoEnBase(dbPath, escenario.ConceptoDos.Id);
            Assert.AreEqual("APU-001", persistidoDos.Clave);
            Assert.AreEqual("Segunda pegada", persistidoDos.Descripcion);
            Assert.AreEqual(2m, persistidoDos.Cantidad);
            Assert.AreEqual(200m, persistidoDos.ImporteTotal);

            var agrupadorTrasRecarga = LeerConceptoEnBase(dbPath, escenario.Agrupador.Id);
            Assert.AreEqual(550m, agrupadorTrasRecarga.ImporteTotal);

            // La vista se recargó desde la base (las filas del grid se reconstruyeron):
            // se releen las celdas por índice, no la referencia previa a la recarga.
            Assert.AreEqual("APU-002", BuscarCelda(grid, fila, "Clave")!.Value?.ToString(),
                "La recarga desde la base muestra el valor confirmado.");
            Assert.AreEqual("Descripción pegada",
                BuscarCelda(grid, fila, "Descripcion")!.Value?.ToString());
            Assert.AreEqual(7m, Convert.ToDecimal(BuscarCelda(grid, fila, "Cantidad")!.Value));

            int filaDos = BuscarFilaPorConcepto(grid, escenario.ConceptoDos.Id);
            Assert.AreEqual("APU-001", BuscarCelda(grid, filaDos, "Clave")!.Value?.ToString(),
                "La recarga reconstruyó la fila parcialmente aplicada con el valor de la base.");
            Assert.AreEqual("Segunda pegada",
                BuscarCelda(grid, filaDos, "Descripcion")!.Value?.ToString());
            Assert.AreEqual(2m, Convert.ToDecimal(BuscarCelda(grid, filaDos, "Cantidad")!.Value));

            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            StringAssert.Contains(
                BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString() ?? string.Empty,
                "550",
                "La recarga recalculó los totales de agrupadores desde la base.");

            Assert.AreEqual(0, escriturasContextoCompartido,
                "La recuperación no puede guardar por la ruta heredada.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El contexto compartido queda sin trabajo pendiente.");

            // Recarga exitosa: no queda bloqueado un pegado posterior.
            form.PruebaFilasAplicadasAntesDeFallo = 0;

            // La recarga reconstruyó filas y columnas: se repunta CurrentCell.
            grid.CurrentCell = BuscarCelda(grid, fila, "Clave");

            var reintento = form.ProcesarPegadoPresupuesto(
                "APU-001\tReintento\tkg\t2",
                _ => true);
            Assert.AreEqual(BudgetPasteOutcomeKind.Committed, reintento.Kind, reintento.Message);
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
    public void PegadoComando_RecargaFallaTrasCommit_BloqueaPegadoHastaReapertura()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_reloadfail_{Guid.NewGuid():N}");
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

            int escriturasContextoCompartido = 0;
            ctx.SavedChanges += (_, __) => escriturasContextoCompartido++;

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            grid.CurrentCell = BuscarCelda(grid, fila, "Clave");

            // La vista falla tras aplicar la única fila del bloque y el preflight de la
            // recarga desde la base también falla: no hay diálogos en la ruta de prueba.
            form.PruebaFilasAplicadasAntesDeFallo = 1;
            form.PruebaForzarFalloDeRecarga = true;

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.CommittedViewNeedsReload, outcome.Kind,
                "El commit ocurrió: nunca se afirma que la vista quedó bien ni que no hubo cambios.");
            Assert.AreEqual("Pegado guardado", outcome.Title);
            StringAssert.Contains(outcome.Message, "no se pudo recargar");
            StringAssert.Contains(outcome.Message, "Cierre y vuelva a abrir el presupuesto");
            Assert.IsFalse(
                outcome.Message.Contains("no se modificó nada", StringComparison.OrdinalIgnoreCase),
                "Tras un commit no se puede decir que no se modificó nada.");

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoPegado,
                "El commit de la base sí ocurrió aunque la vista quedó desincronizada.");

            // La base quedó guardada pese al fallo de vista+recarga.
            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", persistido.Clave);
            Assert.AreEqual(7m, persistido.Cantidad);
            Assert.AreEqual(350m, persistido.ImporteTotal);

            Assert.AreEqual(0, escriturasContextoCompartido,
                "La recuperación no puede guardar por la ruta heredada.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El contexto compartido queda sin trabajo pendiente.");

            // El siguiente pegado se rechaza: la vista quedó desincronizada y la recarga
            // no pudo reconstruirla; hay que reabrir el presupuesto. El rechazo ocurre
            // antes de tocar el contexto, sin diálogos.
            var rechazado = form.ProcesarPegadoPresupuesto("APU-001", _ => true);
            Assert.AreEqual(BudgetPasteOutcomeKind.Rejected, rechazado.Kind);
            Assert.IsNull(form.UltimoEstadoPegado,
                "El pegado bloqueado no debe llegar a enviar ningún comando.");
            StringAssert.Contains(rechazado.Message, "vuelva a abrir el presupuesto");

            // En la base sigue sin haber cambios del intento bloqueado.
            var sinCambios = LeerConceptoEnBase(dbPath, escenario.ConceptoDos.Id);
            Assert.AreEqual(300m, sinCambios.ImporteTotal,
                "El pegado bloqueado no puede escribir nada más en la base.");
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
    public void PegadoComando_RecargaPreservaEdicionEnMemoriaDeConceptoAjenoAlLote()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pegado_ajeno_{Guid.NewGuid():N}");
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

            // Tercer concepto ajeno al lote: nunca entra al bloque pegado.
            var conceptoAjeno = new ConceptoPresupuesto
            {
                ProyectoId = escenario.Proyecto.Id,
                Clave = "C-003",
                Descripcion = "Concepto ajeno original",
                EsAgrupador = false,
                Nivel = 1,
                Orden = 3,
                Unidad = "m2",
                ColumnasPersonalizadasJSON = string.Empty,
                Notas = string.Empty,
                Cantidad = 0m,
                CostoDirectoUnitario = 0m,
                CostoDirectoTotal = 0m,
                PrecioUnitario = 0m,
                ImporteTotal = 0m,
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            ctx.ConceptosPresupuesto.Add(conceptoAjeno);
            ctx.SaveChanges();

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));

            int escriturasContextoCompartido = 0;
            ctx.SavedChanges += (_, __) => escriturasContextoCompartido++;

            var grid = form.GridPrincipal;
            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            grid.CurrentCell = BuscarCelda(grid, fila, "Clave");

            DateTime fechaAjenaOriginal = conceptoAjeno.FechaModificacion;
            const string descripcionEditada = "Edición manual sin guardar";

            // La vista falla tras aplicar la única fila del lote y, antes de lanzar el
            // fallo, el contexto compartido ya arrastra una edición Manual de un concepto
            // ajeno al pegado (así queda el tracker al entrar a la recuperación).
            form.PruebaFilasAplicadasAntesDeFallo = 1;
            form.PruebaAccionAntesDelFalloDeVista = () =>
            {
                conceptoAjeno.Descripcion = descripcionEditada;
                ctx.Entry(conceptoAjeno).State = EntityState.Modified;
            };

            var outcome = form.ProcesarPegadoPresupuesto(
                "APU-002\tDescripción pegada\tkg\t7",
                _ => true);

            Assert.AreEqual(BudgetPasteOutcomeKind.CommittedViewNeedsReload, outcome.Kind,
                "El commit ocurrió y la recarga desde la base debe funcionar sin borrar la edición ajena.");
            StringAssert.Contains(outcome.Message, "recargó desde la base",
                "La recuperación con la recarga selectiva debe reportar éxito.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoPegado);

            // El lote confirmado quedó en la base y la vista lo recupera.
            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", persistido.Clave);
            Assert.AreEqual("Descripción pegada", persistido.Descripcion);
            Assert.AreEqual(7m, persistido.Cantidad);

            int filaRecargada = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.AreEqual("APU-002", BuscarCelda(grid, filaRecargada, "Clave")!.Value?.ToString(),
                "La fila pegada se recupera desde la base pese a la edición ajena en el tracker.");
            Assert.AreEqual("Descripción pegada",
                BuscarCelda(grid, filaRecargada, "Descripcion")!.Value?.ToString());
            Assert.AreEqual(7m, Convert.ToDecimal(BuscarCelda(grid, filaRecargada, "Cantidad")!.Value));

            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            StringAssert.Contains(
                BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString() ?? string.Empty,
                "650",
                "La recarga recalculó los totales de agrupadores desde la base.");

            // El concepto ajeno conserva su edición en memoria: sigue Modified, la
            // descripción editada no se descartó y la base no la recibió.
            Assert.AreEqual(EntityState.Modified, ctx.Entry(conceptoAjeno).State,
                "La recuperación no puede TOCAR la entidad ajena al lote.");
            Assert.AreEqual(descripcionEditada, conceptoAjeno.Descripcion,
                "La edición en memoria del concepto ajeno se preserva.");

            var ajenoEnBase = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            var ajenoReal = LeerConceptoEnBase(dbPath, conceptoAjeno.Id);
            Assert.AreEqual("Concepto ajeno original", ajenoReal.Descripcion,
                "La base conserva la versión persistida del concepto ajeno.");
            Assert.AreEqual(fechaAjenaOriginal, ajenoReal.FechaModificacion,
                "La recuperación no sella FechaModificacion del concepto ajeno.");
            Assert.AreEqual("APU-002", ajenoEnBase.Clave);

            Assert.AreEqual(0, escriturasContextoCompartido,
                "La recuperación no puede guardar por la ruta heredada ni reconciliar a mano.");

            int filaAjena = BuscarFilaPorConcepto(grid, conceptoAjeno.Id);
            Assert.AreEqual(descripcionEditada,
                BuscarCelda(grid, filaAjena, "Descripcion")!.Value?.ToString(),
                "El grid reconstruido materializa la fila ajena desde el valor rastreado en memoria.");
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
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
        public ConceptoPresupuesto ConceptoDos { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto pegado atómico",
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
            Clave = "CAP-1",
            Descripcion = "Capítulo 1",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 1300m,
            PrecioUnitario = 0m,
            ImporteTotal = 1300m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
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

        var conceptoDos = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-002",
            Descripcion = "Acero en obra",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 2,
            Unidad = "kg",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 3m,
            MatrizId = matrizOrigen.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 300m,
            PrecioUnitario = 100m,
            ImporteTotal = 300m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        ctx.ConceptosPresupuesto.AddRange(agrupador, concepto, conceptoDos);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            Agrupador = agrupador,
            Concepto = concepto,
            ConceptoDos = conceptoDos,
        };
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

    private static int BuscarFilaVacia(DataGridView grid)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is null)
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

    private static void CrearTriggerDeFallo(string dbPath, int conceptoId)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(
            "CREATE TRIGGER trg_fail_pegado BEFORE UPDATE ON ConceptosPresupuesto " +
            $"WHEN OLD.Id = {conceptoId} BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");
    }

    private static void EliminarTriggerDeFallo(string dbPath)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw("DROP TRIGGER trg_fail_pegado;");
    }

    private static ConceptoPresupuesto LeerConceptoEnBase(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Single(c => c.Id == conceptoId);
    }

    private static int ContarConceptos(string dbPath)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto.Count();
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
