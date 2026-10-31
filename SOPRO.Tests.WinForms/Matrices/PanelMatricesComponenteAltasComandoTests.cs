using System;
using System.Collections.Generic;
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
/// Ruta de comando (ADR-0001) de ALTA EN LOTE de componentes sobre una matriz YA
/// persistida, con el <see cref="FormPresupuesto"/> real, su
/// <see cref="PanelMatricesEmbebido"/> embebido y un SQLite temporal:
/// <list type="bullet">
/// <item>un lote multi-insumo confirma con UNA llamada al comando (contexto
/// nuevo): la base gana TODAS las filas en orden, la matriz, sus dos hojas y el
/// agrupador se recalculan, el compartido no emite SaveChanges y los tags y
/// entidades tracked quedan Unchanged;</item>
/// <item>un INSERT revertido por trigger conserva la base y la vista intactas y
/// avisa que no se guardó nada, reteniendo el mismo comando inmutable;</item>
/// <item>el reintento EXPLÍCICO del lote retenido confirma una sola vez, sin
/// duplicar filas;</item>
/// <item>una cuadrilla y un lote natural mixto de mano de obra + cuadrilla se
/// confirman completos, y un auxiliar sin referencia se rechaza antes de
/// escribir;</item>
/// <item>un tracker compartido sucio ajeno bloquea el despacho sin persistir el
/// sucio y el reintento tras limpiarlo confirma el MISMO lote;</item>
/// <item>una selección vacía es un no-op sin comando.</item>
/// </list>
/// La atomicidad comunicada es SOLO del alta de componentes: cualquier importe
/// de catálogo externo que el selector hubiera confirmado antes no se revierte
/// ni se reivindica. Solo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class PanelMatricesComponenteAltasComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";
    private static readonly Action<string, string, MessageBoxIcon> AvisoPorDefecto =
        (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

    private static readonly Func<bool> ReintentoPorDefecto =
        PanelMatricesEmbebido.ConfirmarReintentoAltasComponente;

    [TestInitialize]
    public void DesactivarSelectores()
    {
        PanelMatricesEmbebido.SeleccionInsumoParaPrueba = null;
    }

    [TestCleanup]
    public void LimpiarSelectores()
    {
        PanelMatricesEmbebido.SeleccionInsumoParaPrueba = null;
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = AvisoPorDefecto;
        PanelMatricesEmbebido.ConfirmarReintentoAltasComponente = ReintentoPorDefecto;
    }

    [TestMethod]
    public void AltasLotePersistido_UnaEscritura_OrdenIdsYSincronizacionCompleta()
    {
        var root = CrearRaiz("altas_ok");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);
            var materialB = ctx.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-B");
            var materialC = ctx.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-C");

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            bool eventoEmitido = false;
            BudgetMatrixComponentAdditionSnapshot? snapshotRecibida = null;
            panel.AltasComponentesConfirmadas += (_, s) => { eventoEmitido = true; snapshotRecibida = s; };

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Lote multi-item capturado como lo entregaría el selector.
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { MatrizId = escenario.MatrizApu.Id, TipoComponente = TipoComponenteMatriz.Material, MaterialId = materialB.Id, Material = materialB, Cantidad = 3m, Notas = "acero columna" },
                new() { MatrizId = escenario.MatrizApu.Id, TipoComponente = TipoComponenteMatriz.Material, MaterialId = materialC.Id, Material = materialC, Cantidad = 5m, Notas = "varilla" },
            };

            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Material);

            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoAltasComponente,
                panel.UltimaRazonAltasSinDespachar);
            Assert.IsNull(panel.AltaPendiente, "Un lote confirmado no queda retenido.");
            Assert.AreEqual(0, guardasCompartidos,
                "El compartido jamás emite SaveChanges: la escritura la hizo el comando en un contexto nuevo.");
            Assert.AreEqual(0, avisos.Count, "La ruta confirmada no muestra avisos.");
            Assert.IsTrue(eventoEmitido, "FormPresupuesto debe recibir la instantánea confirmada.");
            Assert.IsNotNull(snapshotRecibida);
            Assert.AreEqual(2, snapshotRecibida!.AddedComponentIds.Count);
            Assert.IsFalse(snapshotRecibida.AddedComponentIds.Distinct().Count() != 2, "Ids generados distintos.");

            int nuevoId1 = snapshotRecibida.AddedComponentIds[0];
            int nuevoId2 = snapshotRecibida.AddedComponentIds[1];

            using (var verificacion = new SOPROContext(dbPath))
            {
                var nuevos = verificacion.ComponentesMatriz.AsNoTracking()
                    .Where(c => c.MatrizId == escenario.MatrizApu.Id && c.Id != escenario.ComponenteMaterial.Id && c.Id != escenario.ComponenteMO.Id)
                    .OrderBy(c => c.Orden).ThenBy(c => c.Id)
                    .ToList();
                Assert.AreEqual(2, nuevos.Count, "El lote completo entró con UN solo comando.");
                Assert.AreEqual(nuevoId1, nuevos[0].Id, "El orden del lote define el orden persistido.");
                Assert.AreEqual(nuevoId2, nuevos[1].Id);
                Assert.AreEqual(3m, nuevos[0].Cantidad);
                Assert.AreEqual(5m, nuevos[1].Cantidad);
                Assert.AreEqual("acero columna", nuevos[0].Notas, "Las notas del lote se persisten.");
                Assert.AreEqual("varilla", nuevos[1].Notas);

                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id);
                Assert.AreEqual(280m, matrizDb.CostoDirecto, "150 base + 3·10 + 5·20.");

                var hoja1Db = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                var hoja2Db = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja2.Id);
                Assert.AreEqual(280m, hoja1Db.CostoDirectoUnitario);
                Assert.AreEqual(2800m, hoja1Db.ImporteTotal);
                Assert.AreEqual(1400m, hoja2Db.ImporteTotal, "TODAS las hojas que usan la matriz se recalculan.");

                var agrupadorDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id);
                Assert.AreEqual(4200m, agrupadorDb.ImporteTotal);
            }

            // La instantánea trae matrices, componentes (con notas), hojas y agrupadores.
            Assert.AreEqual(1, snapshotRecibida.AffectedMatrices.Count(m => m.MatrixId == escenario.MatrizApu.Id));
            Assert.IsTrue(snapshotRecibida.AffectedComponents.Any(c => c.ComponentId == nuevoId1 && c.Notas == "acero columna"));
            Assert.AreEqual(2, snapshotRecibida.Leaves.Count);
            Assert.AreEqual(1, snapshotRecibida.Aggregators.Count);

            // El compartido queda limpio y las entidades visibles en Unchanged.
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La ruta de comando no deja trabajo pendiente en el compartido.");
            var sucios = ctx.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached)
                .Select(e => $"{e.Entity.GetType().Name}#{e.State}")
                .ToList();
            Assert.IsFalse(sucios.Any(), string.Join("; ", sucios));

            int filaHoja = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry((ConceptoPresupuesto)grid.Rows[filaHoja].Tag!).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry((ConceptoPresupuesto)grid.Rows[filaAgrupador].Tag!).State);
            Assert.AreEqual(280m.ToStringImporte(), BuscarCelda(grid, filaHoja, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(2800m.ToStringImporte(), BuscarCelda(grid, filaHoja, "Importe")!.Value?.ToString());
            Assert.AreEqual(4200m.ToStringImporte(), BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString());

            // El panel muestra los componentes nuevos, repintados desde lo confirmado.
            Assert.IsTrue(BuscarFilaComponente(dgvComp, nuevoId1) >= 0, "Primer alta visible en el panel.");
            Assert.IsTrue(BuscarFilaComponente(dgvComp, nuevoId2) >= 0, "Segunda alta visible en el panel.");
            StringAssert.Contains(panel.LblDirInterno.Text, 280m.ToStringImporte());
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
    public void AltasLotePersistido_RollbackPorTrigger_ConservaVistaYRetieneElComando()
    {
        var root = CrearRaiz("altas_rollback");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);
            var materialB = ctx.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-B");

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            bool eventoEmitido = false;
            panel.AltasComponentesConfirmadas += (_, __) => eventoEmitido = true;

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { TipoComponente = TipoComponenteMatriz.Material, MaterialId = materialB.Id, Material = materialB, Cantidad = 3m, Notas = string.Empty },
            };

            CrearTriggerDeFallo(dbPath);
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Material);
            EliminarTriggerDeFallo(dbPath);

            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoAltasComponente,
                "El INSERT revertido debe clasificarse como fallo del comando.");
            Assert.IsFalse(eventoEmitido, "Un fallo no emite instantánea.");
            Assert.AreEqual(0, guardasCompartidos, "Sin guardado heredado ni desde el comando.");
            Assert.IsNotNull(panel.AltaPendiente,
                "El lote fallido queda RETENIDO como mismo comando inmutable para reintento explícito.");

            // La vista y la base quedan intactas: no hay filas nuevas pintadas ni escritas.
            Assert.AreEqual(2, dgvComp.Rows.Cast<DataGridViewRow>()
                .Count(r => r.Cells["ColId"].Value != null
                    && Convert.ToInt32(r.Cells["ColId"].Value) > 0),
                "Solo los dos componentes originales siguen pintados.");
            StringAssert.Contains(panel.LblDirInterno.Text, 150m.ToStringImporte(),
                "El costo directo mostrado se conserva tras el rollback.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un fallo revertido no deja trabajo pendiente en el compartido.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == escenario.MatrizApu.Id),
                    "El INSERT revertido no ganó ninguna fila.");
                Assert.AreEqual(150m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
            }

            Assert.AreEqual(1, avisos.Count, "El usuario debe ver que no se guardó nada.");
            StringAssert.Contains(avisos[0], "No se guardó nada.");
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
    public void AltasLotePersistido_ReintentoExplicito_ComunicaUnaVezSinDuplicar()
    {
        var root = CrearRaiz("altas_reintento");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);
        PanelMatricesEmbebido.ConfirmarReintentoAltasComponente = () => true;

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);
            var materialB = ctx.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-B");

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            var comandoRetenidoPrevio = false;
            CrearTriggerDeFallo(dbPath);
            AgregarLoteMaterial(panel, materialB, 3m);
            EliminarTriggerDeFallo(dbPath);

            var comandoRetenido = panel.AltaPendiente!;
            Assert.IsNotNull(comandoRetenido);
            Assert.AreSame(panel.AltaPendiente, comandoRetenido);

            // El reintento EXPLÍCITO del MISMO comando (Sí en el seam de confirmación):
            // si el flujo intentara reabrir el selector queda registrado y cancelado.
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ =>
            {
                comandoRetenidoPrevio = true;
                return null;
            };

            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Material);

            Assert.IsFalse(comandoRetenidoPrevio,
                "El reintento nace del lote retenido: NO abre el selector.");
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoAltasComponente,
                "El reintento confirma una sola vez.");
            Assert.AreEqual(0, guardasCompartidos);
            Assert.IsNull(panel.AltaPendiente);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var nuevosDb = verificacion.ComponentesMatriz.AsNoTracking()
                    .Where(c => c.MatrizId == escenario.MatrizApu.Id
                        && c.Id != escenario.ComponenteMaterial.Id
                        && c.Id != escenario.ComponenteMO.Id)
                    .ToList();
                Assert.AreEqual(1, nuevosDb.Count, "El reintento del mismo comando no duplica filas.");
                Assert.AreEqual(3m, nuevosDb[0].Cantidad);
                Assert.AreEqual(180m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto,
                    "150 base + 3·10 del reintento confirmado.");
            }

            Assert.AreEqual(3, dgvComp.Rows.Cast<DataGridViewRow>()
                .Count(r => r.Cells["ColId"].Value is int idConv && (int)idConv > 0),
                "El panel muestra los dos componentes originales y el nuevo.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    private static void AgregarLoteMaterial(PanelMatricesEmbebido panel, Material material, decimal cantidad)
    {
        PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.Material, MaterialId = material.Id, Material = material, Cantidad = cantidad, Notas = string.Empty },
        };
        panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Material);
        PanelMatricesEmbebido.SeleccionInsumoParaPrueba = null;
    }

    [TestMethod]
    public void AltasLotePersistido_CuadrillaYMezclaNatural_ConfirmanTodoElLote()
    {
        var root = CrearRaiz("altas_aux");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);
            ctx.Matrices.Single(m => m.Clave == "CUA-001").CostoDirecto = 50m;
            ctx.SaveChanges();

            var cuadrilla = ctx.Matrices.AsNoTracking().Single(m => m.Clave == "CUA-001");
            var oficial = ctx.ManoDeObra.AsNoTracking().Single(m => m.Clave == "MO-OF");

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            var snapshots = new List<BudgetMatrixComponentAdditionSnapshot>();
            panel.AltasComponentesConfirmadas += (_, s) => snapshots.Add(s);

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Lote de cuadrilla (como lo devuelve el selector de mano de obra).
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = cuadrilla.Id, Auxiliar = cuadrilla, Cantidad = 1m, Notas = string.Empty },
            };
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.ManoDeObra);

            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoAltasComponente,
                panel.UltimaRazonAltasSinDespachar);
            Assert.IsNull(panel.AltaPendiente);
            Assert.AreEqual(0, guardasCompartidos, "El alta usa un contexto nuevo, no el compartido.");
            Assert.AreEqual(1, snapshots.Count);
            Assert.AreEqual(1, snapshots[0].AddedComponentIds.Count);
            Assert.AreEqual(TipoComponenteMatriz.Auxiliar, snapshots[0].AffectedComponents
                .Single(c => c.ComponentId == snapshots[0].AddedComponentIds.Single()).TipoComponente);

            // Lote natural mixto: mano de obra individual + cuadrilla en el MISMO lote.
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObraId = oficial.Id, ManoDeObra = oficial, Cantidad = 1m, Notas = string.Empty },
                new() { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = cuadrilla.Id, Auxiliar = cuadrilla, Cantidad = 1m, Notas = string.Empty },
            };
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.ManoDeObra);

            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoAltasComponente,
                panel.UltimaRazonAltasSinDespachar);
            Assert.IsNull(panel.AltaPendiente);
            Assert.AreEqual(0, guardasCompartidos);
            Assert.AreEqual(2, snapshots.Count);
            Assert.AreEqual(2, snapshots[1].AddedComponentIds.Count);
            CollectionAssert.AreEquivalent(
                new[] { TipoComponenteMatriz.ManoDeObra, TipoComponenteMatriz.Auxiliar },
                snapshots[1].AffectedComponents
                    .Where(c => snapshots[1].AddedComponentIds.Contains(c.ComponentId))
                    .Select(c => c.TipoComponente)
                    .ToArray());
            Assert.AreEqual(0, avisos.Count);

            // Un auxiliar sin referencia sigue rechazándose antes de escribir.
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = 0, Auxiliar = null, Cantidad = 1m, Notas = string.Empty },
            };
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.ManoDeObra);

            Assert.IsNull(panel.UltimoEstadoAltasComponente, "El auxiliar sin referencia no despacha comando.");
            Assert.IsNull(panel.AltaPendiente, "Un lote rechazado no queda retenido.");
            Assert.AreEqual(0, guardasCompartidos);
            Assert.AreEqual(1, avisos.Count);
            StringAssert.Contains(avisos[0], "auxiliar");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(5, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == escenario.MatrizApu.Id),
                    "Solo se persistieron las dos altas confirmadas; el auxiliar inválido no escribió.");
                Assert.AreEqual(2, verificacion.ComponentesMatriz.AsNoTracking()
                    .Count(c => c.MatrizId == escenario.MatrizApu.Id && c.TipoComponente == TipoComponenteMatriz.Auxiliar),
                    "Las dos altas de cuadrilla confirmadas quedaron persistidas.");
            }
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
    public void AltasLotePersistido_TipoIncompatibleConSelector_SeRechazaAntesDeEscribir()
    {
        var root = CrearRaiz("altas_tipo_incompatible");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);
            var basico = new Matriz
            {
                ProyectoId = escenario.Proyecto.Id,
                Clave = "BAS-001",
                Descripcion = "Básico de prueba",
                Unidad = "m3",
                Tipo = TipoMatriz.Basico,
                CostoDirecto = 10m,
                Notas = string.Empty
            };
            ctx.Matrices.Add(basico);
            ctx.SaveChanges();

            var cuadrilla = ctx.Matrices.AsNoTracking().Single(m => m.Clave == "CUA-001");
            basico = ctx.Matrices.AsNoTracking().Single(m => m.Clave == "BAS-001");

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Un básico NO es una cuadrilla: el selector de Mano de Obra no lo admite.
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = basico.Id, Auxiliar = basico, Cantidad = 1m, Notas = string.Empty },
            };
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.ManoDeObra);

            Assert.IsNull(panel.UltimoEstadoAltasComponente);
            Assert.IsNull(panel.AltaPendiente);
            Assert.AreEqual(0, guardasCompartidos);
            Assert.AreEqual(1, avisos.Count);
            StringAssert.Contains(avisos[0], "incompatibles");

            // Una cuadrilla NO es un básico: el selector de Auxiliares no la admite.
            avisos.Clear();
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => new List<ComponenteMatriz>
            {
                new() { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = cuadrilla.Id, Auxiliar = cuadrilla, Cantidad = 1m, Notas = string.Empty },
            };
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Auxiliar);

            Assert.IsNull(panel.UltimoEstadoAltasComponente);
            Assert.IsNull(panel.AltaPendiente);
            Assert.AreEqual(0, guardasCompartidos);
            Assert.AreEqual(1, avisos.Count);
            StringAssert.Contains(avisos[0], "incompatibles");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(2, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == escenario.MatrizApu.Id));
            Assert.AreEqual(150m, verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
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
    public void AltasLotePersistido_TrackerSucioAjeno_BloqueaYElReintentoLimpioConfirmaElMismoLote()
    {
        var root = CrearRaiz("altas_sucio");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        bool escalonReintentoConfirmado = false;
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);
        PanelMatricesEmbebido.ConfirmarReintentoAltasComponente = () =>
        {
            escalonReintentoConfirmado = true;
            return true;
        };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);
            var materialB = ctx.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-B");

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            bool eventoEmitido = false;
            panel.AltasComponentesConfirmadas += (_, __) => eventoEmitido = true;

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Trabajo pendiente ajeno en el contexto compartido (entidad Added).
            var ajena = new ConceptoPresupuesto
            {
                ProyectoId = escenario.Proyecto.Id,
                Clave = "AJENA",
                Descripcion = "Trabajo pendiente ajeno",
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
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            AgregarLoteMaterial(panel, materialB, 3m);

            Assert.IsNull(panel.UltimoEstadoAltasComponente, "El comando no se envió por el sucio ajeno.");
            Assert.IsFalse(eventoEmitido);
            Assert.AreEqual(0, guardasCompartidos, "El bloqueo no persiste el sucio ajeno.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(), "El sucio ajeno se conserva intacto.");
            Assert.AreEqual(EntityState.Added, ctx.Entry(ajena).State);
            Assert.IsNotNull(panel.AltaPendiente, "El comando inmutable queda retenido para reintento.");
            Assert.AreEqual(1, avisos.Count);
            StringAssert.Contains(avisos[0], "no se enviaron a guardar");

            // El usuario limpia/reliquida su trabajo ajeno y REINTEGA explícitamente el MISMO lote.
            ctx.ConceptosPresupuesto.Remove(ajena);
            ctx.ChangeTracker.DetectChanges();
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            Assert.IsNotNull(panel.AltaPendiente);
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ => throw new InvalidOperationException(
                "El reintento usa el comando retenido; no reabre el selector.");
            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Material);

            Assert.IsTrue(escalonReintentoConfirmado, "El envío del lote retenido pide confirmación explícita.");
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoAltasComponente);
            Assert.AreEqual(0, guardasCompartidos);
            Assert.IsTrue(eventoEmitido);
            Assert.IsNull(panel.AltaPendiente);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(3, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == escenario.MatrizApu.Id),
                    "El lote confirmado ganó exactamente las filas propuestas.");
            }
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
    public void AltasLotePersistido_SeleccionVacia_NoOpSinComandoNiAviso()
    {
        var root = CrearRaiz("altas_vacio");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoAltasComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
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

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            bool eventoEmitido = false;
            panel.AltasComponentesConfirmadas += (_, __) => eventoEmitido = true;

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            int llamadasSelector = 0;
            PanelMatricesEmbebido.SeleccionInsumoParaPrueba = _ =>
            {
                llamadasSelector++;
                return new List<ComponenteMatriz>();
            };

            panel.AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz.Material);

            Assert.AreEqual(1, llamadasSelector);
            Assert.IsNull(panel.UltimoEstadoAltasComponente, "Un lote vacío no despacha comando.");
            Assert.IsNull(panel.AltaPendiente, "Un lote vacío no retiene comandos.");
            Assert.AreEqual(0, avisos.Count);
            Assert.IsFalse(eventoEmitido);
            Assert.AreEqual(0, guardasCompartidos);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(2, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == escenario.MatrizApu.Id));
            }
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

    private sealed class EscenarioPanel
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Matriz MatrizApu { get; init; } = null!;
        public ComponenteMatriz ComponenteMaterial { get; init; } = null!;
        public ComponenteMatriz ComponenteMO { get; init; } = null!;
        public ConceptoPresupuesto Hoja1 { get; init; } = null!;
        public ConceptoPresupuesto Hoja2 { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
    }

    private static EscenarioPanel CrearEscenarioDosHojas(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto panel altas",
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

        var materialExistente = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-CEM",
            Descripcion = "Cemento gris",
            Unidad = "kg",
            PrecioUnitario = 60m,
            Notas = string.Empty,
        };
        var materialB = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-B",
            Descripcion = "Acero de refuerzo",
            Unidad = "kg",
            PrecioUnitario = 10m,
            Notas = string.Empty,
        };
        var materialC = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-C",
            Descripcion = "Varilla corrugada",
            Unidad = "kg",
            PrecioUnitario = 20m,
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
        ctx.Materiales.AddRange(materialExistente, materialB, materialC);
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
        var cuadrilla = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "CUA-001",
            Descripcion = "Cuadrilla base",
            Unidad = "jor",
            Tipo = TipoMatriz.Cuadrilla,
            Notas = string.Empty,
        };
        ctx.Matrices.AddRange(matriz, cuadrilla);
        ctx.SaveChanges();

        var componenteMaterial = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materialExistente.Id,
            Material = materialExistente,
            Cantidad = 2m,
            Importe = 120m,
            Orden = 1,
            Notas = string.Empty,
        };
        var componenteMO = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = oficial.Id,
            ManoDeObra = oficial,
            Cantidad = 1m,
            Importe = 30m,
            Orden = 2,
            Notas = string.Empty,
        };
        matriz.Componentes.Add(componenteMaterial);
        matriz.Componentes.Add(componenteMO);
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
            ComponenteMaterial = componenteMaterial,
            ComponenteMO = componenteMO,
            Hoja1 = hoja1,
            Hoja2 = hoja2,
            Agrupador = agrupador,
        };
    }

    private static void CrearTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_componente_alta BEFORE INSERT ON ComponentesMatriz " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

    private static void EliminarTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_componente_alta;");

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

    private static string CrearRaiz(string nombre)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_alta_{nombre}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
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
