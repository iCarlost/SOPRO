using System;
using System.IO;
using System.Linq;
using System.Threading;
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
/// Ruta de comando (ADR-0001) de la edición de cabecera de una matriz YA
/// persistida, a través del <see cref="FormPresupuesto"/> real, su
/// <see cref="PanelMatricesEmbebido"/> y un SQLite temporal. Cubre el guardado
/// explícito y el implícito al cambiar de concepto con el MISMO comando
/// síncrono, la sincronización del tracker sin segunda escritura, la
/// conservación del borrador ante conflicto/fallo/tracker sucio, la reentrancia
/// de CurrentCellChanged y la creación preexistente sin regresión.
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class PanelMatricesCabeceraComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";
    private static readonly Action<string, string, MessageBoxIcon> AvisoPorDefecto =
        (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);
    private static readonly Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> ConfirmacionPorDefecto =
        (mensaje, titulo, botones, icono) => MessageBox.Show(mensaje, titulo, botones, icono);

    [TestMethod]
    public void EdicionCabeceraExplicita_Exito_SincronizaTrackedSinSegundaEscritura()
    {
        var root = CrearRaiz("cabecera_exito");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisos = new System.Collections.Generic.List<string>();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, mensaje, _) => avisos.Add(mensaje);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var panel = form.PanelMatricesInterno!;

            Matriz? guardada = null;
            panel.BeginEditMatrix(escenario.MatrizA.Id, m => guardada = m);

            var matrizTracked = ctx.Matrices.Local.Single(m => m.Id == escenario.MatrizA.Id);
            var fechaAnterior = matrizTracked.FechaModificacion;

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            panel.EstablecerCabeceraParaPrueba("  apu-001b  ", "  Concreto actualizado  ", " m2 ", TipoMatriz.APU);
            panel.GuardarCabeceraExplicitaParaPrueba();

            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera);
            Assert.AreEqual(0, guardasCompartidos,
                "El comando escribe en un contexto nuevo: el compartido no hace SaveChanges.");
            Assert.IsNotNull(guardada);
            Assert.AreEqual("APU-001B", guardada!.Clave, "El callback recibe la cabecera confirmada.");
            Assert.AreEqual("Concreto actualizado", guardada.Descripcion);
            Assert.AreEqual("m2", guardada.Unidad);

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(matrizTracked).State);
            Assert.AreEqual("APU-001B", matrizTracked.Clave);
            Assert.AreEqual("Concreto actualizado", matrizTracked.Descripcion);
            Assert.AreEqual("m2", matrizTracked.Unidad);
            Assert.IsTrue(matrizTracked.FechaModificacion > fechaAnterior);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El éxito deja solo la cabecera sincronizada y Unchanged, sin trabajo pendiente.");

            var fechaConfirmada = matrizTracked.FechaModificacion;
            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001B", matrizDb.Clave);
                Assert.AreEqual("Concreto actualizado", matrizDb.Descripcion);
                Assert.AreEqual("m2", matrizDb.Unidad);
                Assert.AreEqual(120m, matrizDb.CostoDirecto, "La cabecera no altera el costo directo.");
                Assert.AreEqual(fechaConfirmada, matrizDb.FechaModificacion);
            }

            // Un SaveChanges ajeno posterior confirma SOLO su trabajo: la cabecera ya
            // quedó Unchanged y no se vuelve a escribir.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior";
            ctx.SaveChanges();
            Assert.AreEqual(1, guardasCompartidos);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001B", matrizDb.Clave);
                Assert.AreEqual(fechaConfirmada, matrizDb.FechaModificacion);
                Assert.AreEqual("cambio ajeno posterior",
                    verificacion.Proyectos.AsNoTracking().Single(p => p.Id == escenario.Proyecto.Id).Descripcion);
            }

            Assert.AreEqual(0, avisos.Count, "La ruta confirmada no muestra avisos.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCabeceraExplicita_Conflicto_ConservaBorradorYSinTocarTracker()
    {
        var root = CrearRaiz("cabecera_conflicto");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            int filaAntes = panel.FilaConceptoActual;

            panel.EstablecerCabeceraParaPrueba(" APU-001B ", "Borrador conservado", "m2", TipoMatriz.APU);

            using (var externa = new SOPROContext(dbPath))
            {
                var matriz = externa.Matrices.Single(m => m.Id == escenario.MatrizA.Id);
                matriz.Clave = "APU-EXTERNA";
                matriz.Descripcion = "Cambio externo";
                externa.SaveChanges();
            }

            panel.GuardarCabeceraExplicitaParaPrueba();

            Assert.AreEqual(BudgetOperationStatus.Conflict, panel.UltimoEstadoEdicionCabecera);
            Assert.IsFalse(panel.BloqueoCabeceraActivo);
            Assert.AreEqual(" APU-001B ", panel.ClaveCabeceraParaPrueba, "El borrador conserva los inputs tal cual.");
            Assert.AreEqual("Borrador conservado", panel.DescripcionCabeceraParaPrueba);
            Assert.AreEqual("m2", panel.UnidadCabeceraParaPrueba);
            Assert.AreEqual(filaAntes, panel.FilaConceptoActual, "El conflicto no navega.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El conflicto de la ruta de comando no toca el tracker compartido.");

            using var verificacion = new SOPROContext(dbPath);
            var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
            Assert.AreEqual("APU-EXTERNA", matrizDb.Clave);
            Assert.AreEqual("Cambio externo", matrizDb.Descripcion);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CambioDeConcepto_Exito_GuardaSincronoYRecienDespuesNavega()
    {
        var root = CrearRaiz("cabecera_nav_ok");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        PanelMatricesEmbebido.ConfirmarCambioContextoParaPrueba =
            (_, _, _, _) => DialogResult.Yes;

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            int filaB = BuscarFilaPorConcepto(grid, escenario.HojaB.Id);
            FijarFilaActualSinBorrador(grid, panel, filaA);

            panel.EstablecerCabeceraParaPrueba(" APU-001B ", "Guardado implícito", "m2", TipoMatriz.APU);
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba, "La cabecera debe estar visible.");
            Assert.IsTrue(panel.TieneCambiosPendientesCabeceraParaPrueba,
                "El borrador debe detectarse como pendiente.");

            // Sin DoEvents ni espera: el guardado implícito debe ser síncrono y la
            // navegación solo ocurrir después del commit.
            grid.CurrentCell = CeldaVisibleParaNavegar(grid, filaB);

            Assert.AreEqual(filaB, grid.CurrentRow?.Index, "La navegación se completó.");
            Assert.AreEqual(escenario.MatrizB.Id, panel.MatrizVisibleEnPanel?.Id,
                "El panel muestra la matriz del concepto destino solo tras el éxito.");
            Assert.AreEqual(filaB, panel.FilaConceptoActual);
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001B", matrizDb.Clave);
                Assert.AreEqual("Guardado implícito", matrizDb.Descripcion);
                Assert.AreEqual("m2", matrizDb.Unidad);
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            PanelMatricesEmbebido.ConfirmarCambioContextoParaPrueba = ConfirmacionPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CambioDeConcepto_Fallo_NoNavegaRestauraSeleccionYConservaBorrador()
    {
        var root = CrearRaiz("cabecera_nav_fail");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        PanelMatricesEmbebido.ConfirmarCambioContextoParaPrueba =
            (_, _, _, _) => DialogResult.Yes;

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            int filaB = BuscarFilaPorConcepto(grid, escenario.HojaB.Id);
            FijarFilaActualSinBorrador(grid, panel, filaA);
            panel.EstablecerCabeceraParaPrueba(" APU-001B ", "Borrador que sobrevive", "m2", TipoMatriz.APU);

            CrearTriggerDeFalloMatrices(dbPath);

            grid.CurrentCell = CeldaVisibleParaNavegar(grid, filaB);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoEdicionCabecera,
                panel.UltimaRazonSinDespacharCabecera);
            Assert.AreEqual(filaA, panel.FilaConceptoActual, "El panel no navegó.");
            Assert.AreEqual(filaA, grid.CurrentRow?.Index,
                $"El fallo restaura la selección (current={grid.CurrentCell?.RowIndex}, filaActual={panel.FilaConceptoActual}).");
            Assert.AreEqual(filaA, panel.FilaConceptoActual);
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel?.Id);
            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoEdicionCabecera);
            Assert.AreEqual(" APU-001B ", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual("Borrador que sobrevive", panel.DescripcionCabeceraParaPrueba);
            Assert.AreEqual("m2", panel.UnidadCabeceraParaPrueba);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001", matrizDb.Clave);
                Assert.AreEqual("Concreto simple", matrizDb.Descripcion);
                Assert.AreEqual("m3", matrizDb.Unidad);
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            PanelMatricesEmbebido.ConfirmarCambioContextoParaPrueba = ConfirmacionPorDefecto;
            EliminarTriggerDeFalloMatrices(dbPath);
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCabecera_TrackerAjeno_BloqueaDespachoYPreservaTrabajo()
    {
        var root = CrearRaiz("cabecera_tracker");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.EstablecerCabeceraParaPrueba(" APU-001B ", "Borrador bloqueado", "m2", TipoMatriz.APU);

            escenario.Proyecto.Descripcion = "trabajo ajeno pendiente";
            ctx.ChangeTracker.DetectChanges();
            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            bool guardado = panel.GuardarCabeceraParaPrueba();

            Assert.IsFalse(guardado);
            Assert.IsNull(panel.UltimoEstadoEdicionCabecera, "No se despachó ningún comando.");
            StringAssert.Contains(panel.UltimaRazonSinDespacharCabecera!, "sucio");
            Assert.AreEqual(" APU-001B ", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual(EntityState.Modified, ctx.Entry(escenario.Proyecto).State,
                "El trabajo ajeno se conserva intacto.");
            Assert.AreEqual(0, guardasCompartidos);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001", matrizDb.Clave);
            }

            // El SaveChanges ajeno posterior confirma solo su trabajo, no la cabecera.
            ctx.SaveChanges();
            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001", matrizDb.Clave);
                Assert.AreEqual("trabajo ajeno pendiente",
                    verificacion.Proyectos.AsNoTracking().Single(p => p.Id == escenario.Proyecto.Id).Descripcion);
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EdicionCabecera_ResultadoIncierto_BloqueaHastaRecargaFresca()
    {
        var root = CrearRaiz("cabecera_incierto");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.EstablecerCabeceraParaPrueba(" APU-001B ", "Borrador incierto", "m2", TipoMatriz.APU);
            panel.SimularResultadoInciertoCabeceraParaPrueba();

            Assert.IsFalse(panel.GuardarCabeceraParaPrueba());
            Assert.IsTrue(panel.BloqueoCabeceraActivo);
            Assert.IsNull(panel.UltimoEstadoEdicionCabecera);
            Assert.AreEqual(" APU-001B ", panel.ClaveCabeceraParaPrueba);
            StringAssert.Contains(panel.UltimaRazonSinDespacharCabecera!, "incierto");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual("APU-001", verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.MatrizA.Id).Clave);
            }

            // Reconciliación: descartar el borrador y recargar la matriz fresca libera
            // el bloqueo; entonces el mismo guardado puede confirmarse.
            panel.EstablecerCabeceraParaPrueba("APU-001", "Concreto simple", "m3", TipoMatriz.APU);
            panel.RecargarMatrizVisibleDesdeBd();
            Assert.IsFalse(panel.BloqueoCabeceraActivo,
                "Una lectura fresca de la matriz reconcilia el resultado incierto.");

            panel.EstablecerCabeceraParaPrueba("APU-001B", "Reintento confirmado", "m2", TipoMatriz.APU);
            Assert.IsTrue(panel.GuardarCabeceraParaPrueba());
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001B", matrizDb.Clave);
                Assert.AreEqual("Reintento confirmado", matrizDb.Descripcion);
            }
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CreacionMatriz_ConservaRutaPreexistenteSinComandoDeCabecera()
    {
        var root = CrearRaiz("cabecera_creacion");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var panel = form.PanelMatricesInterno!;

            Matriz? creada = null;
            panel.BeginCreateMatrix(TipoMatriz.APU, m => creada = m);
            panel.EstablecerCabeceraParaPrueba("NUEVA-001", "Matriz nueva", "m2", TipoMatriz.APU);
            panel.AgregarComponenteTemporalParaPrueba(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = escenario.Material.Id,
                Material = escenario.Material,
                Cantidad = 1m,
                Importe = 60m,
                Orden = 1,
                Notas = string.Empty,
            });

            panel.GuardarCabeceraExplicitaParaPrueba();
            for (int i = 0; i < 200 && creada == null; i++)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.IsNotNull(creada, "La creación sigue usando MatrixApplicationService.SaveAsync.");
            Assert.IsNull(panel.UltimoEstadoEdicionCabecera,
                "La creación no transita la ruta de comando de edición de cabecera.");
            Assert.AreEqual("NUEVA-001", creada!.Clave);

            using var verificacion = new SOPROContext(dbPath);
            var matrizDb = verificacion.Matrices.AsNoTracking()
                .Single(m => m.Clave == "NUEVA-001" && m.ProyectoId == escenario.Proyecto.Id);
            Assert.AreEqual("Matriz nueva", matrizDb.Descripcion);
            Assert.AreEqual(1, verificacion.ComponentesMatriz.AsNoTracking().Count(c => c.MatrizId == matrizDb.Id));
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void SubedicionCabeceraExplicita_NoRecalculaNiEscribeElPadre()
    {
        var root = CrearRaiz("cabecera_subedicion");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            var (hijo, auxiliar) = AgregarHijoAuxiliar(ctx, escenario);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            FijarFilaActualSinBorrador(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));

            panel.SelectFilaComponenteParaPrueba(auxiliar.Id);
            panel.EntrarSubedicionComponenteParaPrueba();
            Assert.IsTrue(panel.EstaEnSubedicionComponenteParaPrueba);

            int guardasCompartidos = 0;
            bool matrizActualizada = false;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;
            panel.MatrizActualizada += (_, __) => matrizActualizada = true;

            panel.EstablecerCabeceraParaPrueba(" CUA-001B ", "Cuadrilla editada", "h", TipoMatriz.Cuadrilla);
            panel.GuardarCabeceraExplicitaParaPrueba();

            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera,
                panel.UltimaRazonSinDespacharCabecera);
            Assert.AreEqual(0, guardasCompartidos,
                "El guardado de cabecera en subedición no puede emitir un SaveChanges compartido.");
            Assert.IsFalse(matrizActualizada,
                "Sin edición real de componentes no debe recalcularse ni notificarse el padre.");
            Assert.IsFalse(panel.EstaEnSubedicionComponenteParaPrueba, "La subedición terminó.");
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel?.Id,
                "El panel vuelve al padre tras el guardado de cabecera.");

            using var verificacion = new SOPROContext(dbPath);
            var hijoDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == hijo.Id);
            Assert.AreEqual("CUA-001B", hijoDb.Clave);
            Assert.AreEqual("Cuadrilla editada", hijoDb.Descripcion);
            Assert.AreEqual("h", hijoDb.Unidad);
            Assert.AreEqual(TipoMatriz.Cuadrilla, hijoDb.Tipo);

            var padreDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
            Assert.AreEqual(150m, padreDb.CostoDirecto,
                "Sin edición de componentes el padre conserva su costo directo.");
            Assert.AreEqual(new DateTime(2026, 1, 1), padreDb.FechaModificacion,
                "El padre no se reescribe en la salida de un guardado solo de cabecera.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void BeginEditMatrix_ReconciliaUnknownConLecturaFrescaYBloqueaReintentoPrevio()
    {
        var root = CrearRaiz("cabecera_reconcile");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));

            var panel = form.PanelMatricesInterno!;
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            var tracked = ctx.Matrices.Local.Single(m => m.Id == escenario.MatrizA.Id);
            Assert.AreEqual("APU-001", tracked.Clave);

            panel.EstablecerCabeceraParaPrueba("BORRADOR", "Borrador incierto", "m2", TipoMatriz.APU);
            panel.SimularResultadoInciertoCabeceraParaPrueba();
            Assert.IsFalse(panel.GuardarCabeceraParaPrueba(), "No debe permitirse reintentar antes de reconciliar.");
            Assert.IsTrue(panel.BloqueoCabeceraActivo);

            // La base cambia por otra vía; el contexto compartido conserva la identidad
            // stale de la matriz. La reapertura debe leer la cabecera fresca, no la vieja.
            using (var externa = new SOPROContext(dbPath))
            {
                var matriz = externa.Matrices.Single(m => m.Id == escenario.MatrizA.Id);
                matriz.Clave = "APU-FRESCA";
                matriz.Descripcion = "Descripción fresca";
                matriz.Unidad = "m3";
                externa.SaveChanges();
            }

            Assert.AreEqual("APU-001", tracked.Clave,
                "La instancia tracked es stale antes de la reconciliación.");

            panel.BeginEditMatrix(escenario.MatrizA.Id);

            Assert.IsFalse(panel.BloqueoCabeceraActivo, "La lectura fresca libera el bloqueo.");
            Assert.AreEqual("APU-FRESCA", panel.ClaveCabeceraParaPrueba,
                "La vista usa la cabecera fresca, no el valor stale del identity map.");
            Assert.AreEqual("Descripción fresca", panel.DescripcionCabeceraParaPrueba);
            Assert.AreEqual("APU-FRESCA", tracked.Clave,
                "La instancia tracked se sincroniza con lo que la vista va a mostrar.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La sincronización de la cabecera no deja trabajo pendiente.");

            panel.EstablecerCabeceraParaPrueba("APU-REINTENTO", "Reintento", "m2", TipoMatriz.APU);
            Assert.IsTrue(panel.GuardarCabeceraParaPrueba());
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual("APU-REINTENTO", verificacion.Matrices.AsNoTracking()
                .Single(m => m.Id == escenario.MatrizA.Id).Clave);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void BeginEditMatrix_LecturaFrescaFallidaOMismatch_MantieneBloqueoSinEdicion()
    {
        var root = CrearRaiz("cabecera_reconcile_fail");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            var otroProyecto = new Proyecto
            {
                Nombre = "Proyecto ajeno",
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
            ctx.Proyectos.Add(otroProyecto);
            ctx.SaveChanges();
            var matrizAjena = new Matriz
            {
                ProyectoId = otroProyecto.Id,
                Clave = "AJENA-001",
                Descripcion = "Matriz ajena",
                Unidad = "m3",
                Tipo = TipoMatriz.APU,
                CostoDirecto = 10m,
                Notas = string.Empty,
            };
            ctx.Matrices.Add(matrizAjena);
            ctx.SaveChanges();

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var panel = form.PanelMatricesInterno!;

            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba();

            panel.BeginEditMatrix(999_999);
            Assert.IsTrue(panel.BloqueoCabeceraActivo,
                "Una lectura fresca inexistente no puede liberar el bloqueo.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba,
                "No se habilita la edición si no se pudo reconciliar.");
            Assert.IsFalse(panel.TieneCambiosPendientesCabeceraParaPrueba);
            Assert.IsFalse(panel.GuardarCabeceraParaPrueba());
            StringAssert.Contains(panel.UltimaRazonSinDespacharCabecera!, "reconciliar");

            panel.BeginEditMatrix(matrizAjena.Id);
            Assert.IsTrue(panel.BloqueoCabeceraActivo,
                "Una matriz de otro proyecto tampoco reconcilia la cabecera.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsFalse(panel.GuardarCabeceraParaPrueba());
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void RecargaNormalConLock_UsaLecturaFrescaYLiberaSoloTrasReconciliar()
    {
        var root = CrearRaiz("cabecera_recarga_lock");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            FijarFilaActualSinBorrador(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            var tracked = ctx.Matrices.Local.Single(m => m.Id == escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba();

            using (var externa = new SOPROContext(dbPath))
            {
                var matriz = externa.Matrices.Single(m => m.Id == escenario.MatrizA.Id);
                matriz.Clave = "APU-FRESCA";
                matriz.Descripcion = "Descripción fresca";
                externa.SaveChanges();
            }

            Assert.AreEqual("APU-001", tracked.Clave, "La instancia tracked sigue stale antes de recargar.");

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            bool recargada = panel.IntentarRecargarMatrizVisibleDesdeBd();

            Assert.IsTrue(recargada);
            Assert.IsFalse(panel.BloqueoCabeceraActivo,
                "La recarga normal no puede liberar sin la lectura fresca por factory.");
            Assert.AreEqual("APU-FRESCA", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual("Descripción fresca", panel.DescripcionCabeceraParaPrueba);
            Assert.AreEqual("APU-FRESCA", tracked.Clave,
                "La cabecera tracked se sincroniza con la lectura fresca.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
            Assert.AreEqual(0, guardasCompartidos);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void RecargaSubedicionConLock_UsaLecturaFrescaDelHijoYLibera()
    {
        var root = CrearRaiz("cabecera_recarga_sub");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            var (hijo, auxiliar) = AgregarHijoAuxiliar(ctx, escenario);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            FijarFilaActualSinBorrador(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.SelectFilaComponenteParaPrueba(auxiliar.Id);
            panel.EntrarSubedicionComponenteParaPrueba();
            Assert.IsTrue(panel.EstaEnSubedicionComponenteParaPrueba);

            panel.SimularResultadoInciertoCabeceraParaPrueba();

            using (var externa = new SOPROContext(dbPath))
            {
                var m = externa.Matrices.Single(x => x.Id == hijo.Id);
                m.Clave = "CUA-FRESCA";
                m.Descripcion = "Cuadrilla fresca";
                externa.SaveChanges();
            }

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            bool recargada = panel.IntentarRecargarMatrizVisibleDesdeBd();

            Assert.IsFalse(recargada, "La subedición conserva el contrato histórico de no reconciliar selección.");
            Assert.IsFalse(panel.BloqueoCabeceraActivo);
            Assert.AreEqual("CUA-FRESCA", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual("Cuadrilla fresca", panel.DescripcionCabeceraParaPrueba);
            Assert.IsTrue(panel.EstaEnSubedicionComponenteParaPrueba,
                "La recarga del hijo no sale de la subedición.");
            Assert.AreEqual(0, guardasCompartidos);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void NavegacionConLock_UsaLecturaFrescaDeBloqueadaYDestino()
    {
        var root = CrearRaiz("cabecera_nav_lock_ok");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            int filaB = BuscarFilaPorConcepto(grid, escenario.HojaB.Id);
            FijarFilaActualSinBorrador(grid, panel, filaA);
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            var trackedA = ctx.Matrices.Local.Single(m => m.Id == escenario.MatrizA.Id);
            var trackedB = ctx.Matrices.Local.Single(m => m.Id == escenario.MatrizB.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba();

            using (var externa = new SOPROContext(dbPath))
            {
                var a = externa.Matrices.Single(m => m.Id == escenario.MatrizA.Id);
                a.Clave = "APU-FRESCA";
                a.Descripcion = "A fresca";
                var b = externa.Matrices.Single(m => m.Id == escenario.MatrizB.Id);
                b.Clave = "APU-B-FRESCA";
                b.Descripcion = "B fresca";
                externa.SaveChanges();
            }

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            grid.CurrentCell = CeldaVisibleParaNavegar(grid, filaB);

            Assert.AreEqual(filaB, grid.CurrentRow?.Index);
            Assert.IsFalse(panel.BloqueoCabeceraActivo,
                "La navegación no puede liberar sin lectura fresca de la bloqueada y del destino.");
            Assert.AreEqual(escenario.MatrizB.Id, panel.MatrizVisibleEnPanel?.Id);
            Assert.AreEqual("APU-B-FRESCA", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual("B fresca", panel.DescripcionCabeceraParaPrueba);
            Assert.AreEqual("APU-FRESCA", trackedA.Clave,
                "La matriz bloqueada tracked también se reconcilia antes de liberar.");
            Assert.AreEqual("APU-B-FRESCA", trackedB.Clave,
                "La matriz destino tracked también se sincroniza con la lectura fresca.");
            Assert.AreEqual(0, guardasCompartidos);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void NavegacionConLockLecturaFrescaFallida_MantieneBloqueoSinEdicion()
    {
        var root = CrearRaiz("cabecera_nav_lock_fail");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            int filaB = BuscarFilaPorConcepto(grid, escenario.HojaB.Id);
            FijarFilaActualSinBorrador(grid, panel, filaA);
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba(999_999);

            grid.CurrentCell = CeldaVisibleParaNavegar(grid, filaB);

            Assert.IsTrue(panel.BloqueoCabeceraActivo,
                "Una lectura fresca fallida no puede liberar el bloqueo.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsNull(panel.MatrizVisibleEnPanel, "No se expone una matriz stale como editable.");
            Assert.IsFalse(panel.GuardarCabeceraParaPrueba());
            StringAssert.Contains(panel.UltimaRazonSinDespacharCabecera!, "reconciliar");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void RecargaConLockLecturaFrescaFallida_MantieneBloqueo()
    {
        var root = CrearRaiz("cabecera_recarga_lock_fail");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            FijarFilaActualSinBorrador(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba(999_999);

            bool recargada = panel.IntentarRecargarMatrizVisibleDesdeBd();

            Assert.IsFalse(recargada);
            Assert.IsTrue(panel.BloqueoCabeceraActivo,
                "La recarga normal no puede liberar con una lectura fresca fallida.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsNull(panel.MatrizVisibleEnPanel);
            Assert.IsFalse(panel.GuardarCabeceraParaPrueba());
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void MismaFilaConLock_FastPathNoEvitaLecturaFrescaYLiberaAlReconciliar()
    {
        var root = CrearRaiz("cabecera_misma_fila_lock_ok");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            Assert.IsTrue(panel.CargarMatrizDeFilaParaPrueba(filaA, forceReload: true));

            panel.BeginEditMatrix(escenario.MatrizA.Id);
            var tracked = ctx.Matrices.Local.Single(m => m.Id == escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba();

            using (var externa = new SOPROContext(dbPath))
            {
                var matriz = externa.Matrices.Single(m => m.Id == escenario.MatrizA.Id);
                matriz.Clave = "APU-FRESCA";
                matriz.Descripcion = "Fresca en la misma fila";
                externa.SaveChanges();
            }

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Misma fila, mismo concepto y misma matriz: antes del ajuste esta llamada
            // caía en el fast path y no reconciliaba. Ahora debe forzar la carga y pasar
            // por la lectura fresca del helper.
            bool cargada = panel.CargarMatrizDeFilaParaPrueba(filaA, forceReload: false);

            Assert.IsTrue(cargada);
            Assert.IsFalse(panel.BloqueoCabeceraActivo,
                "La misma fila con lock no puede saltarse la reconciliación fresca.");
            Assert.AreEqual("APU-FRESCA", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual("Fresca en la misma fila", panel.DescripcionCabeceraParaPrueba);
            Assert.AreEqual("APU-FRESCA", tracked.Clave);
            Assert.AreEqual(0, guardasCompartidos);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void MismaFilaConLockLecturaFrescaFallida_MantieneBloqueo()
    {
        var root = CrearRaiz("cabecera_misma_fila_lock_fail");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            Assert.IsTrue(panel.CargarMatrizDeFilaParaPrueba(filaA, forceReload: true));

            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba(999_999);

            bool cargada = panel.CargarMatrizDeFilaParaPrueba(filaA, forceReload: false);

            Assert.IsFalse(cargada);
            Assert.IsTrue(panel.BloqueoCabeceraActivo,
                "La misma fila no puede liberar el lock si la lectura fresca falla.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsNull(panel.MatrizVisibleEnPanel);
            Assert.IsFalse(panel.GuardarCabeceraParaPrueba());
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void MismaFilaSinLock_ConservaFastPathSinLecturaFresca()
    {
        var root = CrearRaiz("cabecera_misma_fila_fast");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            Assert.IsTrue(panel.CargarMatrizDeFilaParaPrueba(filaA, forceReload: true));

            using (var externa = new SOPROContext(dbPath))
            {
                var matriz = externa.Matrices.Single(m => m.Id == escenario.MatrizA.Id);
                matriz.Clave = "APU-FRESCA";
                externa.SaveChanges();
            }

            // Sin lock se conserva el fast path histórico: la vista no se recarga y
            // mantiene el valor cargado aunque la base haya cambiado por fuera.
            bool cargada = panel.CargarMatrizDeFilaParaPrueba(filaA, forceReload: false);

            Assert.IsTrue(cargada);
            Assert.IsFalse(panel.BloqueoCabeceraActivo);
            Assert.AreEqual("APU-001", panel.ClaveCabeceraParaPrueba);
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel?.Id);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class EscenarioCabecera
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Material Material { get; init; } = null!;
        public Matriz MatrizA { get; init; } = null!;
        public Matriz MatrizB { get; init; } = null!;
        public ConceptoPresupuesto HojaA { get; init; } = null!;
        public ConceptoPresupuesto HojaB { get; init; } = null!;
    }

    private static EscenarioCabecera CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto panel cabecera",
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
        ctx.Materiales.Add(material);
        ctx.SaveChanges();

        var matrizA = CrearMatriz(ctx, proyecto.Id, "APU-001", "Concreto simple", "m3", material, 2m, 120m);
        var matrizB = CrearMatriz(ctx, proyecto.Id, "APU-002", "Acero de refuerzo", "kg", material, 1m, 60m);

        var hojaA = CrearHoja(ctx, proyecto.Id, "C-001", matrizA.Id, 10m, 150m, 1);
        var hojaB = CrearHoja(ctx, proyecto.Id, "C-002", matrizB.Id, 5m, 60m, 2);
        ctx.SaveChanges();

        return new EscenarioCabecera
        {
            Proyecto = proyecto,
            Material = material,
            MatrizA = matrizA,
            MatrizB = matrizB,
            HojaA = hojaA,
            HojaB = hojaB,
        };
    }

    private static (Matriz Hijo, ComponenteMatriz Auxiliar) AgregarHijoAuxiliar(
        SOPROContext ctx, EscenarioCabecera escenario)
    {
        var hijo = new Matriz
        {
            ProyectoId = escenario.Proyecto.Id,
            Clave = "CUA-001",
            Descripcion = "Cuadrilla hija",
            Unidad = "h",
            Tipo = TipoMatriz.Cuadrilla,
            CostoDirecto = 30m,
            Notas = string.Empty,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(hijo);
        ctx.SaveChanges();

        var auxiliar = new ComponenteMatriz
        {
            MatrizId = escenario.MatrizA.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = hijo.Id,
            Auxiliar = hijo,
            Cantidad = 1m,
            Importe = 30m,
            Orden = 2,
            Notas = string.Empty,
        };
        ctx.ComponentesMatriz.Add(auxiliar);
        escenario.MatrizA.CostoDirecto = 150m;
        ctx.SaveChanges();
        return (hijo, auxiliar);
    }

    private static Matriz CrearMatriz(
        SOPROContext ctx,
        int proyectoId,
        string clave,
        string descripcion,
        string unidad,
        Material material,
        decimal cantidad,
        decimal importe)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = unidad,
            Tipo = TipoMatriz.APU,
            CostoDirecto = importe,
            Notas = string.Empty,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        ctx.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = cantidad,
            Importe = importe,
            Orden = 1,
            Notas = string.Empty,
        });
        ctx.SaveChanges();
        return matriz;
    }

    private static ConceptoPresupuesto CrearHoja(
        SOPROContext ctx,
        int proyectoId,
        string clave,
        int matrizId,
        decimal cantidad,
        decimal costo,
        int orden)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = $"Hoja {clave}",
            EsAgrupador = false,
            Nivel = 1,
            Orden = orden,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = cantidad,
            MatrizId = matrizId,
            CostoDirectoUnitario = costo,
            CostoDirectoTotal = cantidad * costo,
            PrecioUnitario = costo,
            ImporteTotal = cantidad * costo,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        return concepto;
    }

    private static string CrearRaiz(string sufijo)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_cabecera_{sufijo}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static SOPROContext CrearContexto(string dbPath)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
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

    private static DataGridViewCell CeldaVisibleParaNavegar(DataGridView grid, int fila)
    {
        foreach (DataGridViewColumn columna in grid.Columns)
        {
            if (columna.Visible)
                return grid.Rows[fila].Cells[columna.Index];
        }

        return grid.Rows[fila].Cells[0];
    }

    private static void FijarFilaActualSinBorrador(DataGridView grid, PanelMatricesEmbebido panel, int fila)
    {
        if (grid.CurrentRow?.Index != fila)
            grid.CurrentCell = CeldaVisibleParaNavegar(grid, fila);

        panel.NotificarFilaCambiada(fila);
        Assert.AreEqual(fila, panel.FilaConceptoActual,
            "La prueba necesita partir del concepto indicado sin borrador pendiente.");
    }

    private static void CrearTriggerDeFalloMatrices(string dbPath)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(
            "CREATE TRIGGER trg_fail_matriz_cab BEFORE UPDATE ON Matrices " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_cabecera'); END;");
    }

    private static void EliminarTriggerDeFalloMatrices(string dbPath)
    {
        try
        {
            using var admin = new SOPROContext(dbPath);
            admin.Database.ExecuteSqlRaw("DROP TRIGGER IF EXISTS trg_fail_matriz_cab;");
        }
        catch
        {
            // La base temporal puede haberse eliminado ya; no es parte del contrato.
        }
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
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (intento < 4)
            {
                Thread.Sleep(200);
            }
        }
    }
}
