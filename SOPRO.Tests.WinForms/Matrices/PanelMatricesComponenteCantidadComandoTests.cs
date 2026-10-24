using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Ruta de comando (ADR-0001) de la celda Cantidad de un componente YA
/// persistido, con el <see cref="FormPresupuesto"/> real, su
/// <see cref="PanelMatricesEmbebido"/> embebido y un SQLite temporal:
/// <list type="bullet">
/// <item>la edición confirma con UNA sola escritura del comando (contexto nuevo),
/// sincroniza panel, hojas y agrupadores, y deja el compartido Unchanged sin
/// SaveChanges propio;</item>
/// <item>un fallo revertido conserva el borrador visible sin escribir la base ni
/// el tracker, y el reintento del mismo intento confirma una sola vez;</item>
/// <item>un tracker compartido sucio bloquea el despacho conservando el
/// pendiente y el borrador;</item>
/// <item>abrir/renderizar el panel no ensucia el tracker;</item>
/// <item>una matriz hija recalcula la ascendencia completa y la hoja del
/// presupuesto;</item>
/// <item>la ruta nueva nunca dispara <c>RefreshBudgetPrices</c>.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class PanelMatricesComponenteCantidadComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";
    private static readonly Action<string, string, MessageBoxIcon> AvisoPorDefecto =
        (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

    [TestMethod]
    public void EdicionCantidadComponente_DosHojasYAggrupador_SincronizaUnaEscrituraYTagsUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_cantidad_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisos = new System.Collections.Generic.List<string>();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente =
            (_, mensaje, _) => avisos.Add(mensaje);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            int filaHoja = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            Assert.IsTrue(filaHoja >= 0);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Antes de editar el tracker compartido debe estar limpio.");

            panel.NotificarFilaCambiada(filaHoja);

            var comp = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaComp = BuscarFilaComponente(dgvComp, comp.Id);
            Assert.IsTrue(filaComp >= 0, "El componente material debe estar pintado en el panel.");

            Assert.AreEqual(2m.ToStringCantidad(),
                dgvComp.Rows[filaComp].Cells["ColCantidad"].Value?.ToString());
            Assert.AreEqual(120m.ToStringImporte(),
                dgvComp.Rows[filaComp].Cells["ColImporte"].Value?.ToString());

            bool eventoEmitido = false;
            panel.CantidadComponenteConfirmada += (_, __) => eventoEmitido = true;

            bool consumida = panel.ProcesarEdicionCantidadComponenteCelda(filaComp, "3");
            Assert.IsTrue(consumida, "La celda Cantidad persistida debe consumirse en la ruta de comando.");
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente,
                panel.UltimaRazonSinDespachar);
            Assert.IsTrue(eventoEmitido, "El formulario debe recibir la instantánea confirmada.");
            Assert.IsNull(panel.IntentoCantidadPendiente, "Sin pendiente tras el commit.");

            // Exactamente NINGUNA escritura desde el contexto compartido: sin
            // trabajo pendiente y las entidades compartidas Unchanged.
            var sucios = ctx.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached)
                .Select(e => $"{e.Entity.GetType().Name}#{(e.Entity as Matriz)?.Id}{(e.Entity as ComponenteMatriz)?.Id}#{e.State}")
                .ToList();
            Assert.IsFalse(sucios.Any(),
                $"La ruta de comando no deja trabajo pendiente en el compartido. Sucios: {string.Join("; ", sucios)}");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(comp).State,
                "La entidad del componente editado nunca queda Modified: el borrador no muta el tracker.");
            Assert.AreEqual(3m, comp.Cantidad,
                "Tras el commit la entidad compartida queda sincronizada con lo confirmado (y Unchanged).");

            // Estado persistido: una sola escritura del comando.
            using (var verificacion = new SOPROContext(dbPath))
            {
                var compDb = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id);
                Assert.AreEqual(3m, compDb.Cantidad);
                Assert.AreEqual(180m, compDb.Importe);

                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id);
                Assert.AreEqual(210m, matrizDb.CostoDirecto);

                var hoja1Db = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                var hoja2Db = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja2.Id);
                Assert.AreEqual(210m, hoja1Db.CostoDirectoUnitario);
                Assert.AreEqual(210m, hoja1Db.PrecioUnitario);
                Assert.AreEqual(2100m, hoja1Db.ImporteTotal);
                Assert.AreEqual(210m, hoja2Db.CostoDirectoUnitario, "TODAS las hojas con la matriz se recalculan.");
                Assert.AreEqual(1050m, hoja2Db.ImporteTotal);

                var agrupadorDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id);
                Assert.AreEqual(3150m, agrupadorDb.ImporteTotal);
                Assert.AreEqual(3150m, agrupadorDb.CostoDirectoTotal);
            }

            // Sincronización visible del panel desde la instantánea.
            Assert.AreEqual(3m.ToStringCantidad(),
                dgvComp.Rows[filaComp].Cells["ColCantidad"].Value?.ToString());
            Assert.AreEqual(180m.ToStringImporte(),
                dgvComp.Rows[filaComp].Cells["ColImporte"].Value?.ToString());
            StringAssert.Contains(panel.LblDirInterno.Text, 210m.ToStringImporte(),
                "El costo directo del panel refleja la instantánea confirmada.");

            // Hojas y agrupador visibles en el grid del presupuesto, aplicadas por Id.
            int filaHoja2 = BuscarFilaPorConcepto(grid, escenario.Hoja2.Id);
            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.AreEqual(210m.ToStringImporte(), BuscarCelda(grid, filaHoja, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(2100m.ToStringImporte(), BuscarCelda(grid, filaHoja, "Importe")!.Value?.ToString());
            Assert.AreEqual(1050m.ToStringImporte(), BuscarCelda(grid, filaHoja2, "Importe")!.Value?.ToString());
            Assert.AreEqual(3150m.ToStringImporte(), BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString());

            // Tags sincronizados y Unchanged: ningún guardado heredado los reescribe.
            var tagHoja1 = (ConceptoPresupuesto)grid.Rows[filaHoja].Tag!;
            var tagHoja2 = (ConceptoPresupuesto)grid.Rows[filaHoja2].Tag!;
            var tagAgrupador = (ConceptoPresupuesto)grid.Rows[filaAgrupador].Tag!;
            Assert.AreEqual(2100m, tagHoja1.ImporteTotal);
            Assert.AreEqual(1050m, tagHoja2.ImporteTotal);
            Assert.AreEqual(3150m, tagAgrupador.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagHoja1).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagHoja2).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagAgrupador).State);

            // Un guardado ajeno posterior no reescribe fila confirmada.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();
            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2100m, verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            // La ruta nueva jamás dispara el refresco de precios (segunda escritura).
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios,
                "RefreshBudgetPrices NO debe ejecutarse desde la ruta de comando.");
            Assert.AreEqual(0, avisos.Count, "La ruta confirmada no muestra avisos.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCantidadComponente_FalloRevertido_ConservaBorradorYElReintentoConfirmaUnaVez()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_rollback_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));
            var comp = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaComp = BuscarFilaComponente(dgvComp, comp.Id);

            // El borrador vive en la celda: el usuario ya escribió 3.
            dgvComp.Rows[filaComp].Cells["ColCantidad"].Value = "3";
            dgvComp.CurrentCell = dgvComp.Rows[filaComp].Cells["ColCantidad"];

            CrearTriggerDeFallo(dbPath);

            bool consumida = panel.ProcesarEdicionCantidadComponenteCelda(filaComp, "3");
            Assert.IsTrue(consumida);
            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoEdicionCantidadComponente,
                "El intento debe clasificarse como fallo revertido por el comando. " + panel.UltimaRazonSinDespachar);

            // Borrador visible, base y entidades intactas, tracker limpio.
            Assert.AreEqual("3", dgvComp.Rows[filaComp].Cells["ColCantidad"].Value?.ToString(),
                "La celda conserva el borrador del usuario tras el fallo.");
            Assert.AreEqual(2m, comp.Cantidad,
                "La entidad compartida no se muta en la ruta de comando.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(comp).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un fallo revertido no deja trabajo pendiente en el compartido.");
            Assert.IsNotNull(panel.IntentoCantidadPendiente,
                "El intento queda pendiente para el reintento del mismo propósito.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2m, verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id).Cantidad,
                    "El UPDATE rechazado no puede quedar en la base.");
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            EliminarTriggerDeFallo(dbPath);

            // Reintento del MISMO intento: UN solo commit.
            bool consumidaReintento = panel.ProcesarEdicionCantidadComponenteCelda(filaComp, "3");
            Assert.IsTrue(consumidaReintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente,
                panel.UltimaRazonSinDespachar);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var compDb = verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id);
                Assert.AreEqual(3m, compDb.Cantidad);
                Assert.AreEqual(210m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                Assert.AreEqual(2100m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            Assert.IsNull(panel.IntentoCantidadPendiente, "Tras el commit no queda intento pendiente.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCantidadComponente_ComponenteYaModificado_SeConsumeSinRutaHeredadaConservaBorradorYPendienteSucio()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_comp_mod_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisos = new System.Collections.Generic.List<string>();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente =
            (_, mensaje, _) => avisos.Add(mensaje);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));
            var comp = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaComp = BuscarFilaComponente(dgvComp, comp.Id);
            dgvComp.Rows[filaComp].Cells["ColCantidad"].Value = "5";
            dgvComp.CurrentCell = dgvComp.Rows[filaComp].Cells["ColCantidad"];

            // El componente ya esta MODIFICADO en el compartido (borrador ajeno sin bases).
            comp.Cantidad = 7m;
            ctx.ChangeTracker.DetectChanges();
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            bool consumida = panel.ProcesarEdicionCantidadComponenteCelda(filaComp, "5");
            Assert.IsTrue(consumida,
                "Un componente persistido reconocido NUNCA cae a la ruta heredada.");
            Assert.IsNull(panel.UltimoEstadoEdicionCantidadComponente,
                "El comando no llegó a construirse por el componente ya modificado.");

            // El borrador (celda) queda visible, el sucio ajeno se conserva y nada se salvó.
            Assert.AreEqual("5", dgvComp.Rows[filaComp].Cells["ColCantidad"].Value?.ToString(),
                "El borrador queda visible sin afirmar que se guardó.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "Aviso emitido; el trabajo pendiente ajeno no se salva ni se confirma.");
            Assert.AreEqual(1, avisos.Count, "Debe avisarse al usuario que no se guardó.");
            Assert.AreEqual(7m, comp.Cantidad, "El borrador tracked ajeno no se altera.");
            Assert.IsNull(panel.IntentoCantidadPendiente,
                "Sin bases capturables no hay intento mintiendo pendiente.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2m, verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id).Cantidad,
                    "Nada se escribió en la base (no guardado heredado ni comando).");
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            StringAssert.Contains(avisos[0], "no se guardó");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCantidadComponente_TrackerSucioAjeno_BloqueaDespachoYConservaPendienteYBorrador()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_sucio_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));
            var comp = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaComp = BuscarFilaComponente(dgvComp, comp.Id);
            dgvComp.Rows[filaComp].Cells["ColCantidad"].Value = "3";

            // Trabajo pendiente ajeno en el contexto compartido.
            escenario.Agrupador.Notas = "ajena pendiente";
            ctx.ChangeTracker.DetectChanges();
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            bool consumida = panel.ProcesarEdicionCantidadComponenteCelda(filaComp, "3");
            Assert.IsTrue(consumida, "La edición es de la ruta de comando pero NO se despacha.");
            Assert.IsNull(panel.UltimoEstadoEdicionCantidadComponente,
                "El comando no llegó a enviarse por el trabajo ajeno pendiente.");
            Assert.IsNotNull(panel.IntentoCantidadPendiente,
                "El intento se conserva pendiente junto con el borrador.");

            // El pendiente ajeno sigue sucio en el compartido y nada se salvó.
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El bloqueo no salva ni toca el trabajo ajeno; conserva sucio el pendiente.");
            Assert.AreEqual("3", dgvComp.Rows[filaComp].Cells["ColCantidad"].Value?.ToString(),
                "El borrador sigue visible.");
            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2m, verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id).Cantidad,
                    "Nada se escribió en la base.");
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal,
                    "La hoja conserva su estado previo.");
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void AperturaYRenderDelPanel_NoEnsucianElTrackerCompartido()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_render_{Guid.NewGuid():N}");
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

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            int filaHoja = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);

            panel.NotificarFilaCambiada(filaHoja);

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Abrir y renderizar la matriz del panel no debe ensuciar el tracker compartido.");
            Assert.IsTrue(ctx.ChangeTracker.Entries<Matriz>()
                .All(e => e.State == EntityState.Unchanged || e.State == EntityState.Detached));

            // Re-render (repintado) tampoco ensucia.
            panel.NotificarFilaCambiada(filaHoja);
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
    public void UndoRedoCantidadComponentePersistida_RepiteLaMismaIntencionComoComandoUnico_SinEscrituraHeredada()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_undo_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisos = new System.Collections.Generic.List<string>();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente =
            (_, mensaje, _) => avisos.Add(mensaje);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            int filaHoja = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            panel.NotificarFilaCambiada(filaHoja);

            var comp = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaComp = BuscarFilaComponente(dgvComp, comp.Id);

            // Edición viva por la ruta de comando (2 → 3) para sembrar el undo.
            dgvComp.CurrentCell = dgvComp.Rows[filaComp].Cells["ColCantidad"];
            Assert.IsTrue(panel.SembrarEdicionCantidadParaPrueba(filaComp, "3"));
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente);

            // UNDO: debe repetir la intención previa (cantidad 2) como comando.
            Assert.IsTrue(panel.TryUndo(), "El undo debe deshacer la edición de cantidad.");
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente,
                "El undo de cantidad persistida debe viajar COMO comando único." + panel.UltimaRazonSinDespachar);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios,
                "El undo no debe disparar RefreshBudgetPrices (segunda escritura).");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2m, verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id).Cantidad);
                Assert.AreEqual(150m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El comando de undo no deja trabajo pendiente en el compartido.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(comp).State);
            Assert.AreEqual(2m.ToStringCantidad(),
                dgvComp.Rows[filaComp].Cells["ColCantidad"].Value?.ToString());

            // Fallo del undo por trigger: sin escritura ni fallback heredado.
            dgvComp.Rows[filaComp].Cells["ColCantidad"].Value = "3";
            dgvComp.CurrentCell = dgvComp.Rows[filaComp].Cells["ColCantidad"];

            // Redo: vuelve a 3 (comando) antes del escenario de fallo.
            Assert.IsTrue(panel.TryRedo());
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente);

            CrearTriggerDeFallo(dbPath);

            // Undo con trigger: el comando falla y se revierte SIN fallback.
            Assert.IsTrue(panel.TryUndo());
            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoEdicionCantidadComponente,
                "El undo con fallo de base debe clasificarse como fallo revertido." + panel.UltimaRazonSinDespachar);
            Assert.IsNotNull(panel.IntentoCantidadPendiente,
                "El intento de undo queda pendiente tras el fallo revertido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(3m, verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id).Cantidad,
                    "El fallo del undo no puede alterar la base.");
                Assert.AreEqual(210m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                Assert.AreEqual(2100m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
                Assert.AreEqual(3150m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id).ImporteTotal);
            }

            Assert.AreEqual(180m.ToStringImporte(),
                dgvComp.Rows[filaComp].Cells["ColImporte"].Value?.ToString(),
                "El panel conserva el estado confirmado tras el fallo del undo.");

            EliminarTriggerDeFallo(dbPath);

            // Reintento del MISMO intento de undo (aplicar 2): UN commit.
            dgvComp.CurrentCell = dgvComp.Rows[filaComp].Cells["ColCantidad"];
            Assert.IsTrue(panel.SembrarEdicionCantidadParaPrueba(filaComp, "2"));
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente,
                panel.UltimaRazonSinDespachar);
            Assert.IsNull(panel.IntentoCantidadPendiente);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2m, verificacion.ComponentesMatriz.AsNoTracking().Single(c => c.Id == comp.Id).Cantidad);
                Assert.AreEqual(150m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
                Assert.AreEqual(2250m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id).ImporteTotal);
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCantidadComponente_MatrizHija_RecalculaAscendenciaYHojasDelPresupuesto()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_anidada_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioAnidado(ctx);
            var hijoId = escenario.Hijo!.Value;

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            int filaHoja = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            panel.NotificarFilaCambiada(filaHoja);

            // Subedición real del panel sobre la matriz hija (cuadrilla).
            panel.BeginEditMatrix(hijoId);
            int filaMaq = BuscarFilaComponente(dgvComp, escenario.ComponenteMaquinariaId!.Value);
            Assert.IsTrue(filaMaq >= 0, "El componente maquinaria de la hija debe estar pintado.");

            bool consumida = panel.ProcesarEdicionCantidadComponenteDialogo(filaMaq, 4m);
            Assert.IsTrue(consumida);
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente,
                panel.UltimaRazonSinDespachar);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var maq = verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == escenario.ComponenteMaquinariaId!.Value);
                Assert.AreEqual(4m, maq.Cantidad);
                Assert.AreEqual(0.25m, maq.Rendimiento, "La hija aplica la semántica canónica del diálogo.");
                Assert.AreEqual(16m, maq.Importe, "Costo horario 4 x 4 h.");

                var hija = verificacion.Matrices.AsNoTracking().Single(m => m.Id == hijoId);
                Assert.AreEqual(16m, hija.CostoDirecto, "La hija se recalcula primero.");

                var padre = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id);
                Assert.AreEqual(166m, padre.CostoDirecto,
                    "120 material + 30 MO + 16 cuadrilla: el ascendiente APU consume el costo fresco de la hija.");

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                Assert.AreEqual(166m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(166m, hoja.PrecioUnitario);
                Assert.AreEqual(1660m, hoja.ImporteTotal,
                    "La hoja del presupuesto con el ancestro se recalcula.");
            }

            int filaHojaPosible = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            Assert.AreEqual(166m.ToStringImporte(), BuscarCelda(grid, filaHojaPosible, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(1660m.ToStringImporte(), BuscarCelda(grid, filaHojaPosible, "Importe")!.Value?.ToString());
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void SubedicionCantidadComando_SalirNoRecalculaNiGuarda_CierreAtomicoYaConfirmado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_anidada_salida_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioAnidado(ctx);
            var hijoId = escenario.Hijo!.Value;
            var hijoAuxiliar = escenario.MatrizApu.Componentes
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Entrada REAL al hijo anidado por la ruta del panel, con el contexto
            // del grid sobre el componente auxiliar pintado del padre.
            panel.SelectFilaComponenteParaPrueba(hijoAuxiliar.Id);
            panel.EntrarSubedicionComponenteParaPrueba();
            Assert.IsTrue(panel.EstaEnSubedicionComponenteParaPrueba,
                "La subedición del panel debe haber arrancado.");
            Assert.IsFalse(panel.SubedicionSoloCantidadConfirmada,
                "Sin ediciones aún: el cierre atómico no está marcado.");

            int filaMaq = BuscarFilaComponente(dgvComp, escenario.ComponenteMaquinariaId!.Value);
            Assert.IsTrue(filaMaq >= 0);

            bool consumida = panel.ProcesarEdicionCantidadComponenteDialogo(filaMaq, 4m);
            Assert.IsTrue(consumida);
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente,
                panel.UltimaRazonSinDespachar);
            Assert.IsTrue(panel.SubedicionSoloCantidadConfirmada,
                "El cierre atómico confirmado queda marcado para la salida.");

            // Trabajo ajeno visible justo antes de salir: la salida con cierre
            // atómico confirmado NO debe recalcular el padre ni emitir SaveChanges.
            var ajenaAntesDeSalir = AgregarConceptoAjeno(ctx, escenario);
            int guardasAntesDeSalir = guardasCompartidos;
            bool eventoMatrizActualizada = false;
            panel.MatrizActualizada += (_, __) => eventoMatrizActualizada = true;

            // Salida por Cancelar de la subedición: el cierre atómico confirmado
            // recalculó hijo → ascendientes → hojas/agrupadores; la salida NO
            // repite el recálculo heredado NI emite SaveChanges compartido.
            panel.FinalizarSubedicionComponenteParaPrueba(refrescarPadre: false);

            Assert.AreEqual(EntityState.Added, ctx.Entry(ajenaAntesDeSalir).State,
                "La salida con cierre confirmado conserva el ajeno pendiente sin tocarlo.");
            Assert.AreEqual(guardasAntesDeSalir, guardasCompartidos,
                "La salida con cierre atómico confirmado NO emite SaveChanges del compartido.");
            Assert.IsFalse(eventoMatrizActualizada,
                "Con el cierre confirmado la salida no repite la propagación heredada.");
            Assert.IsFalse(panel.EstaEnSubedicionComponenteParaPrueba, "La subedición terminó.");
            Assert.IsFalse(panel.SubedicionSoloCantidadConfirmada, "El flag se reinicia al salir.");
            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsFalse(verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Any(c => c.Clave == "AJENA-SUB"),
                    "La salida con cierre confirmado no persiste trabajo ajeno.");
            }

            // La base conserva exactamente el estado del cierre atómico, sin reescritura.
            using (var verificacion = new SOPROContext(dbPath))
            {
                var padre = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id);
                Assert.AreEqual(166m, padre.CostoDirecto,
                    "120 material + 30 MO + 16 cuadrilla recalculados por el comando.");

                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                Assert.AreEqual(166m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(1660m, hoja.ImporteTotal);

                var agrupadorDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id);
                Assert.AreEqual(2490m, agrupadorDb.ImporteTotal,
                    "El cierre atómico recalculó también los agrupadores: 166×10 + 166×5.");
            }

            // La vista del panel regresa al padre con lo confirmado (AsNoTracking).
            int filaAuxiliarPadre = BuscarFilaComponente(dgvComp, hijoAuxiliar.Id);
            Assert.IsTrue(filaAuxiliarPadre >= 0, "El padre (con su componente auxiliar) vuelve a pintarse.");
            StringAssert.Contains(panel.LblDirInterno.Text, 166m.ToStringImporte());

            int filaHojaFinal = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            Assert.AreEqual(166m.ToStringImporte(), BuscarCelda(grid, filaHojaFinal, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(1660m.ToStringImporte(), BuscarCelda(grid, filaHojaFinal, "Importe")!.Value?.ToString());
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void SubedicionCantidadComando_SalidaPorGuardar_SinRecalculoHeredadoNiPropagacion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_anidada_guardar_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioAnidado(ctx);
            var hijoAuxiliar = escenario.MatrizApu.Componentes
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            panel.SelectFilaComponenteParaPrueba(hijoAuxiliar.Id);
            panel.EntrarSubedicionComponenteParaPrueba();
            Assert.IsTrue(panel.EstaEnSubedicionComponenteParaPrueba);
            Assert.IsFalse(panel.SubedicionSoloCantidadConfirmada);

            // 1) Comando de cantidad confirmado (cierre atómico hijo→padre→hojas).
            int filaMaq = BuscarFilaComponente(panel.DgvComponentesInterno, escenario.ComponenteMaquinariaId!.Value);
            Assert.IsTrue(panel.ProcesarEdicionCantidadComponenteDialogo(filaMaq, 4m));
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente);
            Assert.IsTrue(panel.SubedicionSoloCantidadConfirmada);

            // Trabajo ajeno visible justo antes de salir: la salida POR GUARDAR con
            // cierre atómico confirmado tampoco debe preciso tocarlo.
            var ajenaAntesDeSalir = AgregarConceptoAjeno(ctx, escenario);
            int guardasAntesDeSalir = guardasCompartidos;
            bool eventoMatrizActualizada = false;
            panel.MatrizActualizada += (_, __) => eventoMatrizActualizada = true;

            // Salida POR GUARDAR (como el botón Guardar de la subedición) que invoca
            // onSaved → refrescarPadre=true: el cierre confirmado omite el recálculo
            // heredado (sin SaveChanges extra ni MatrizActualizada).
            panel.FinalizarSubedicionComponenteParaPrueba(refrescarPadre: true);

            Assert.IsFalse(panel.EstaEnSubedicionComponenteParaPrueba, "La subedición terminó.");
            Assert.IsFalse(panel.SubedicionSoloCantidadConfirmada, "El flag se reinicia al salir.");
            Assert.AreEqual(guardasAntesDeSalir, guardasCompartidos,
                "La salida por Guardar con cierre confirmado NO emite SaveChanges del compartido.");
            Assert.IsFalse(eventoMatrizActualizada,
                "La salida por Guardar con cierre confirmado no repite MatrizActualizada heredada.");
            Assert.AreEqual(EntityState.Added, ctx.Entry(ajenaAntesDeSalir).State,
                "El ajeno visible se conserva pendiente sin onda en la salida.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(4m, verificacion.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.Id == escenario.ComponenteMaquinariaId!.Value).Cantidad);
                Assert.AreEqual(166m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                Assert.AreEqual(166m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(1660m, hoja.ImporteTotal);
            }

            int filaAuxiliar = BuscarFilaComponente(panel.DgvComponentesInterno, hijoAuxiliar.Id);
            Assert.IsTrue(filaAuxiliar >= 0, "El padre vuelve a pintarse tras la salida por Guardar.");
            StringAssert.Contains(panel.LblDirInterno.Text, 166m.ToStringImporte());
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void SubedicionEdicionHeredadaDespuesDelComando_SalidaEjecutaRecalculoHeredado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_anidada_legado_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCantidadComponente = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioAnidado(ctx);
            var hijoAuxiliar = escenario.MatrizApu.Componentes
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            panel.SelectFilaComponenteParaPrueba(hijoAuxiliar.Id);
            panel.EntrarSubedicionComponenteParaPrueba();

            // 1) Comando de cantidad confirmado (cierre atómico del hijo→padre).
            int filaMaq = BuscarFilaComponente(dgvComp, escenario.ComponenteMaquinariaId!.Value);
            Assert.IsTrue(panel.ProcesarEdicionCantidadComponenteDialogo(filaMaq, 4m));
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCantidadComponente);
            Assert.IsTrue(panel.SubedicionSoloCantidadConfirmada);

            // 2) Una edición heredada durante la subedición (reproducción del
            //    bookkeeping legado de celda persistida: SaveChanges + registro de
            //    edición + GuardarYPropagar) limpia el flag atómico: su salida
            //    DEBE volver a recalcular.
            panel.SimularEdicionLegadaSubedicionParaPrueba();
            Assert.IsFalse(panel.SubedicionSoloCantidadConfirmada,
                "Una edición heredada en subedición limpia el cierre atómico confirmado.");

            // 3) Salir por Cancelar: con edición heredada registrada, el recálculo
            //    heredado de salida SE ejecuta y persiste el trabajo ajeno visible.
            AgregarConceptoAjeno(ctx, escenario);
            int guardasAntesDeSalir = guardasCompartidos;
            panel.FinalizarSubedicionComponenteParaPrueba(refrescarPadre: false);

            Assert.IsFalse(panel.EstaEnSubedicionComponenteParaPrueba);
            Assert.IsFalse(panel.SubedicionSoloCantidadConfirmada);
            Assert.IsTrue(guardasCompartidos > guardasAntesDeSalir,
                "La salida con edición heredada persistida ejecuta el recálculo heredado.");
            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsTrue(verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Any(c => c.Clave == "AJENA-SUB"),
                    "El recálculo heredado de salida persiste el trabajo ajeno (protección activa).");
            }

            using (var verificacion = new SOPROContext(dbPath))
            {
                var hoja = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                Assert.AreEqual(166m, hoja.CostoDirectoUnitario);
                Assert.AreEqual(1660m, hoja.ImporteTotal);
                var padre = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id);
                Assert.AreEqual(166m, padre.CostoDirecto);
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCantidadComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class EscenarioPanel
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Matriz MatrizApu { get; init; } = null!;
        public ConceptoPresupuesto Hoja1 { get; init; } = null!;
        public ConceptoPresupuesto Hoja2 { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public int? Hijo { get; init; }
        public int? ComponenteMaquinariaId { get; init; }
    }

    private static EscenarioPanel CrearEscenarioDosHojas(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto panel componente",
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

        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-CEM",
            Descripcion = "Cemento gris",
            Unidad = "kg",
            PrecioUnitario = 60m,
            Notas = string.Empty,
        };
        var oficial = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-OF",
            Descripcion = "Oficial albañil",
            Unidad = "jor",
            SalarioBase = 30m,
            FactorSalarioReal = 1m,
            SalarioReal = 30m,
            Notas = string.Empty,
        };
        ctx.Materiales.Add(material);
        ctx.ManoDeObra.Add(oficial);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = 2m,
            Importe = 120m,
            Orden = 1,
            Notas = string.Empty,
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = oficial.Id,
            ManoDeObra = oficial,
            Cantidad = 1m,
            Importe = 30m,
            Orden = 2,
            Notas = string.Empty,
        });
        matriz.CostoDirecto = 150m;
        ctx.SaveChanges();

        ConceptoPresupuesto CrearHoja(string clave, decimal cantidad, decimal costo, int orden) => new()
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = $"Hoja {clave}",
            EsAgrupador = false,
            Nivel = 1,
            Orden = orden,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = cantidad,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = costo,
            CostoDirectoTotal = cantidad * costo,
            PrecioUnitario = costo,
            ImporteTotal = cantidad * costo,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var hoja1 = CrearHoja("C-001", 10m, 150m, 1);
        var hoja2 = CrearHoja("C-002", 5m, 150m, 2);
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
            CostoDirectoTotal = 2250m,
            PrecioUnitario = 0m,
            ImporteTotal = 2250m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.AddRange(hoja1, hoja2, agrupador);
        ctx.SaveChanges();

        return new EscenarioPanel
        {
            Proyecto = proyecto,
            MatrizApu = matriz,
            Hoja1 = hoja1,
            Hoja2 = hoja2,
            Agrupador = agrupador,
        };
    }

    private static EscenarioPanel CrearEscenarioAnidado(SOPROContext ctx)
    {
        var base1 = CrearEscenarioDosHojas(ctx);

        var revolvedora = new Maquinaria
        {
            ProyectoId = base1.Proyecto.Id,
            Clave = "MAQ-REV",
            Descripcion = "Revolvedora",
            CostoHorario = 4m,
            Notas = string.Empty,
        };
        ctx.Maquinaria.Add(revolvedora);
        ctx.SaveChanges();

        var hijo = new Matriz
        {
            ProyectoId = base1.Proyecto.Id,
            Clave = "CUA-001",
            Descripcion = "Cuadrilla con revolvedora",
            Unidad = "h",
            Tipo = TipoMatriz.Cuadrilla,
            Notas = string.Empty,
        };
        ctx.Matrices.Add(hijo);
        ctx.SaveChanges();
        var compMaquinaria = new ComponenteMatriz
        {
            MatrizId = hijo.Id,
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = revolvedora.Id,
            Maquinaria = revolvedora,
            Cantidad = 2m,
            Rendimiento = 0.5m,
            Importe = 8m,
            Orden = 1,
            Notas = string.Empty,
        };
        ctx.ComponentesMatriz.Add(compMaquinaria);
        hijo.CostoDirecto = 8m;
        ctx.SaveChanges();

        // El APU padre consume la cuadrilla como componente auxiliar.
        var auxiliar = new ComponenteMatriz
        {
            MatrizId = base1.MatrizApu.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = hijo.Id,
            Auxiliar = hijo,
            Cantidad = 1m,
            Importe = 8m,
            Orden = 3,
            Notas = string.Empty,
        };
        ctx.ComponentesMatriz.Add(auxiliar);
        base1.MatrizApu.CostoDirecto = 158m;
        ctx.SaveChanges();

        // Las hojas reflejan el precio previo del padre (lo que la vista cargó).
        foreach (var hoja in new[] { base1.Hoja1, base1.Hoja2 })
        {
            hoja.CostoDirectoUnitario = 158m;
            hoja.PrecioUnitario = 158m;
            hoja.CostoDirectoTotal = hoja.Cantidad * 158m;
            hoja.ImporteTotal = hoja.Cantidad * 158m;
        }
        ctx.SaveChanges();

        return new EscenarioPanel
        {
            Proyecto = base1.Proyecto,
            MatrizApu = base1.MatrizApu,
            Hoja1 = base1.Hoja1,
            Hoja2 = base1.Hoja2,
            Agrupador = base1.Agrupador,
            Hijo = hijo.Id,
            ComponenteMaquinariaId = compMaquinaria.Id,
        };
    }

    private static ConceptoPresupuesto AgregarConceptoAjeno(
        SOPROContext ctx, EscenarioPanel escenario)
    {
        var ajena = new ConceptoPresupuesto
        {
            ProyectoId = escenario.Proyecto.Id,
            Clave = "AJENA-SUB",
            Descripcion = "Trabajo ajeno en el compartido",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 99,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(ajena);
        ctx.ChangeTracker.DetectChanges();
        return ajena;
    }

    private static void CrearTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_componente_p BEFORE UPDATE ON ComponentesMatriz " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

    private static void EliminarTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_componente_p;");

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

    private static int BuscarFilaComponente(DataGridView dgv, int componenteId)
    {
        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            var cell = dgv.Rows[i].Cells["ColId"];
            if (cell?.Value != null && Convert.ToInt32(cell.Value) == componenteId)
                return i;
        }

        return -1;
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
