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
/// Regresión PR #104 sobre <see cref="PanelMatricesEmbebido"/>:
/// <list type="bullet">
/// <item>Escape: en vista normal emite <see cref="PanelMatricesEmbebido.SolicitudCerrarWorkspace"/>
/// exactamente una vez SIN reconstruir el panel; con creación, subedición o borrador
/// de cabecera conserva la salida segura (autoguardado/descarte/vuelta al padre).</item>
/// <item>Transición visual atómica: la supresión de redibujado del subárbol se aplica
/// durante la ráfaga de cambio de concepto y JAMÁS queda activa (carga normal, carga
/// que retorna temprano, sin handle, disposición del control, recreate de handle,
/// bombeo modal/transición anidada dentro de la ráfaga).</item>
/// </list>
/// Escenarios reales sobre <see cref="FormPresupuesto"/> y SQLite temporal.
/// No se abren diálogos interactivos: los seams estáticos del panel se capturan y
/// restauran por prueba (valor vivo dentro del try/finally); la red de seguridad de
/// la suite (NotificadorNoModalSetup/InterceptorDialogoInconsistencias) cubre el resto.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class PanelMatricesEscapeYTransicionTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void Escape_VistaNormal_CierraWorkspaceUnaVez_SinReconstruirPanel()
    {
        var root = CrearRaiz("escape_normal");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            string tituloAntes = panel.TituloCabeceraParaPrueba;
            int filaAntes = panel.FilaConceptoActual;
            int filasComponentesAntes = panel.GridComponentesParaPrueba.Rows.Count;

            int cierres = 0;
            panel.SolicitudCerrarWorkspace += (_, __) => cierres++;

            Assert.IsTrue(panel.EnviarEscapeParaPrueba());
            Assert.AreEqual(1, cierres, "Un Escape en vista normal cierra el workspace UNA vez.");
            Assert.IsTrue(form.SplitWorkspace.Panel2Collapsed,
                "El host real atiende la solicitud y colapsa el workspace.");
            Assert.IsFalse(panel.Visible,
                "El cierre del workspace retira la superficie del panel.");
            Assert.AreEqual(tituloAntes, panel.TituloCabeceraParaPrueba,
                "El Escape de cierre no reconstruye el panel.");
            Assert.AreEqual(filaAntes, panel.FilaConceptoActual,
                "El Escape de cierre no navega ni restaura la selección.");
            Assert.AreEqual(filasComponentesAntes, panel.GridComponentesParaPrueba.Rows.Count,
                "El Escape de cierre no recarga componentes.");

            System.Windows.Forms.Application.DoEvents();
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
                "La limpieza del cierre no deja la supresión activa.");
            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);

            Assert.IsTrue(panel.EnviarEscapeParaPrueba());
            Assert.AreEqual(2, cierres, "Cada pulsación emite la solicitud exactamente una vez.");
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
    public void Escape_CabeceraPersistidaConCambios_Autoguarda_YNoCierraWorkspace()
    {
        var root = CrearRaiz("escape_dirty");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisoPrevio = PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz;
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.EstablecerCabeceraParaPrueba("APU-001X", "Concreto modificado", "m3", TipoMatriz.APU);
            Assert.IsTrue(panel.TieneCambiosPendientesCabeceraParaPrueba);

            int cierres = 0;
            panel.SolicitudCerrarWorkspace += (_, __) => cierres++;

            Assert.IsTrue(panel.EnviarEscapeParaPrueba());
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(0, cierres, "Con borrador de cabecera el Escape NO cierra el workspace.");
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera,
                "La salida segura autoguarda la cabecera persistida.");
            using (var verificacion = new SOPROContext(dbPath))
            {
                var matrizDb = verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.MatrizA.Id);
                Assert.AreEqual("APU-001X", matrizDb.Clave);
                Assert.AreEqual("Concreto modificado", matrizDb.Descripcion);
                Assert.AreEqual("m3", matrizDb.Unidad);
            }
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel?.Id,
                "Tras autoguardar, el panel conserva la matriz vista.");
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void Escape_CreacionConBorrador_DescartaSoloConConfirmacion_YNoCierraWorkspace()
    {
        var root = CrearRaiz("escape_creacion");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisoPrevio = PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz;
        var descartePrevio = PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz;
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = () => false;

            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var panel = form.PanelMatricesInterno!;

            panel.BeginCreateMatrix(TipoMatriz.APU, soloApu: true);
            panel.EstablecerCabeceraParaPrueba("BORRADOR-ESC", "Borrador Escape", "m3", TipoMatriz.APU);

            int cierres = 0;
            panel.SolicitudCerrarWorkspace += (_, __) => cierres++;

            Assert.IsTrue(panel.EnviarEscapeParaPrueba());
            Assert.AreEqual(0, cierres, "Rechazar el descarte mantiene la captura abierta.");
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba,
                "La creación sigue activa tras negar el descarte.");

            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = () => true;
            Assert.IsTrue(panel.EnviarEscapeParaPrueba());
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual(0, cierres, "Descartar la creación NO cierra el workspace.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba,
                "Tras descartar, el panel queda sin selección.");
            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsFalse(verificacion.Matrices.AsNoTracking()
                    .Any(m => m.Clave == "BORRADOR-ESC"),
                    "El descarte de la creación no escribe nada.");
            }
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba);
        }
        finally
        {
            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = descartePrevio;
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void Escape_EnSubedicion_DevuelveAlPadre_YNoCierraWorkspace()
    {
        var root = CrearRaiz("escape_subedicion");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var avisoPrevio = PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz;
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            var (hijo, _) = AgregarHijoAuxiliar(ctx, escenario);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.SelectFilaComponenteParaPrueba(
                ctx.ComponentesMatriz.AsNoTracking()
                    .Single(c => c.MatrizId == escenario.MatrizA.Id && c.AuxiliarId == hijo.Id).Id);
            panel.EntrarSubedicionComponenteParaPrueba();
            Assert.IsTrue(panel.EstaEnSubedicionComponenteParaPrueba);

            int cierres = 0;
            panel.SolicitudCerrarWorkspace += (_, __) => cierres++;

            Assert.IsTrue(panel.EnviarEscapeParaPrueba());
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(0, cierres, "En subedición el Escape NO cierra el workspace.");
            Assert.IsFalse(panel.EstaEnSubedicionComponenteParaPrueba,
                "El Escape debe devolver al padre sin dejar el hijo activo.");
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel?.Id);
            Assert.IsTrue(panel.Visible);
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = avisoPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CambioConcepto_EntreMatrices_SinCambioDeChrome_NoAgendaPases()
    {
        var root = CrearRaiz("transicion_matrices");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba);

            bool resultado = panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaB.Id), false);

            Assert.IsTrue(resultado);
            // Matriz→matriz no cambia el chrome: no hay pase diferido que agende y la
            // supresión se libera al terminar la ráfaga, sin repintado intermedio.
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
                "Sin cambio de chrome no queda trabajo de layout pendiente.");
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);
            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);
            Assert.AreEqual(escenario.MatrizB.Id, panel.MatrizVisibleEnPanel?.Id,
                "El estado destino se aplicó por completo dentro de la ventana suprimida.");

            System.Windows.Forms.Application.DoEvents();
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba);
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);

            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);
            Assert.AreEqual($"Matriz: {escenario.MatrizB.Clave}", panel.TituloCabeceraParaPrueba);
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
    public void CambioConcepto_ConceptoSinMatriz_CargaTempranaLiberaSupresion()
    {
        var root = CrearRaiz("transicion_sin_matriz");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));

            bool resultado = panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id), false);

            Assert.IsFalse(resultado);
            Assert.IsTrue(panel.RedibujoSuprimidoParaPrueba,
                "La ráfaga que termina en MostrarSinSeleccion también suprime mientras agenda pases.");

            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
                "La carga que retorna temprano no deja la supresión activa.");
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);
            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "El concepto sin APU mantiene el acceso Nueva matriz.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
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
    public void CambioConcepto_SinHandle_NoSuprimeYNoDejaEstado()
    {
        var root = CrearRaiz("transicion_sin_handle");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        PanelMatricesEmbebido? panelSuelto = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);

            // Panel NUNCA mostrado: arranca sin handle (la supresión es no-op). El
            // recorrido de carga puede crear el handle (comportamiento histórico);
            // el pase que agende entonces drena limpio al bombear mensajes.
            panelSuelto = new PanelMatricesEmbebido(ctx, escenario.Proyecto.Id);
            Assert.IsFalse(panelSuelto.IsHandleCreated, "pre-conexión sin handle");

            using (var dgv = new DataGridView())
            {
                dgv.Columns.Add("col", "Descripción");
                int filaA = dgv.Rows.Add();
                dgv.Rows[filaA].Tag = ctx.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.HojaA.Id);
                panelSuelto.ConectarPresupuesto(dgv);
                Assert.AreEqual(0, panelSuelto.PasesReprogramacionPendientesParaPrueba, "post-conexión");
                Assert.IsFalse(panelSuelto.IsHandleCreated, "post-conexión sin handle");

                bool resultado = panelSuelto.CargarMatrizDeFilaParaPrueba(filaA, true);

                Assert.IsTrue(resultado);
                Assert.AreEqual(escenario.MatrizA.Id, panelSuelto.MatrizVisibleEnPanel?.Id);
                Assert.IsFalse(panelSuelto.RedibujoSuprimidoParaPrueba,
                    "Sin handle al iniciar, la supresión es no-op.");
                System.Windows.Forms.Application.DoEvents();
                Assert.AreEqual(0, panelSuelto.PasesReprogramacionPendientesParaPrueba,
                    "El pase agendado durante la carga drena limpio al bombear mensajes.");
                Assert.IsFalse(panelSuelto.RedibujoSuprimidoParaPrueba);
            }
        }
        finally
        {
            panelSuelto?.Dispose();
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void Dispose_DuranteVentanaSuprimida_LimpiaEstadoYNoLanza()
    {
        var root = CrearRaiz("transicion_dispose");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));

            panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id), false);
            Assert.IsTrue(panel.RedibujoSuprimidoParaPrueba);

            // Disposición del control con un pase diferido aún pendiente.
            form.Close();
            System.Windows.Forms.Application.DoEvents();

            Assert.IsTrue(panel.IsDisposed);
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
                "La disposición limpia la supresión (el re-habilitado es best-effort).");
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);
            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);
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
    public void TransicionConBombeoAnidado_NoRestauraAntesDeSalirDelBurstExterno()
    {
        var root = CrearRaiz("transicion_anidada");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));

            // Resultado incierto con matriz inexistente: la reconciliación fresca
            // falla DENTRO de la ráfaga suprimida y abre el aviso, que aquí simula
            // el bombeo de un diálogo modal y una transición anidada real (cambio de
            // fila del presupuesto → burst anidado por CurrentCellChanged).
            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.SimularResultadoInciertoCabeceraParaPrueba(999_999);

            int filaB = BuscarFilaPorConcepto(grid, escenario.HojaB.Id);
            int filaC = BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id);
            int llamadasAviso = 0;

            var avisoPrevio = PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz;
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) =>
            {
                llamadasAviso++;
                System.Windows.Forms.Application.DoEvents();
                Assert.IsTrue(panel.RedibujoSuprimidoParaPrueba,
                    $"Aviso #{llamadasAviso}: el bombeo dentro del burst NO debe restaurar el redibujado.");

                if (llamadasAviso == 1)
                {
                    // Transición anidada real dentro del burst externo.
                    grid.CurrentCell = CeldaVisibleParaNavegar(grid, filaC);
                }

                System.Windows.Forms.Application.DoEvents();
                Assert.IsTrue(panel.RedibujoSuprimidoParaPrueba,
                    "Ni el burst anidado ni sus pases drenados restauran antes del burst externo.");
                Assert.IsTrue(panel.ProfundidadSupresionParaPrueba >= 1,
                    "El burst externo mantiene su turno de profundidad.");
            };

            try
            {
                bool resultado = panel.CargarMatrizDeFilaParaPrueba(filaB, false);

                Assert.IsFalse(resultado,
                    "La reconciliación fallida conserva el bloqueo y termina sin matriz.");
                Assert.IsTrue(panel.BloqueoCabeceraActivo);
                Assert.IsNull(panel.MatrizVisibleEnPanel);

                System.Windows.Forms.Application.DoEvents();

                Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
                    "Al salir del burst externo con los pases agotados, la supresión se libera.");
                Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);
                Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);
                Assert.IsTrue(llamadasAviso >= 1, "El aviso interno debió ejecutarse dentro de la ráfaga.");
            }
            finally
            {
                PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = avisoPrevio;
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
    public void CicloHandle_Recreate_SinEstadoStaleNiUnderflow()
    {
        var root = CrearRaiz("transicion_recreate");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));

            bool resultado = panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id), false);
            Assert.IsFalse(resultado);
            Assert.IsTrue(panel.RedibujoSuprimidoParaPrueba);

            // Destroy+create del handle con un pase en cola: la generación vieja no
            // puede heredar supresión ni decrementar contadores de la nueva.
            typeof(Control)
                .GetMethod("RecreateHandle", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .Invoke(panel, Array.Empty<object>());
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
                "El handle recreado no hereda la supresión.");
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba,
                "Ni underflow ni pases stale tras el recreate.");
            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);

            // La siguiente transición sigue operando de forma completa.
            bool regreso = panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaA.Id), false);
            Assert.IsTrue(regreso);
            Assert.IsTrue(panel.RedibujoSuprimidoParaPrueba,
                "La supresión vuelve a aplicarse en la generación nueva.");
            System.Windows.Forms.Application.DoEvents();
            Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba);
            Assert.AreEqual(0, panel.PasesReprogramacionPendientesParaPrueba);
            Assert.AreEqual(0, panel.ProfundidadSupresionParaPrueba);
            Assert.AreEqual(escenario.MatrizA.Id, panel.MatrizVisibleEnPanel?.Id);
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
    public void SetRedrawArbol_CasosBasicos_RoundtripCompleto()
    {
        Assert.IsFalse(GridRedrawHelper.SetRedrawArbol(null!, habilitar: false),
            "Raíz nula: no hay nada que aplicar (fallo declarado).");

        var root = CrearRaiz("helper_basico");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var panel = form.PanelMatricesInterno!;

            Assert.IsTrue(GridRedrawHelper.SetRedrawArbol(panel, habilitar: false),
                "El subárbol completo queda suprimido sin abortar el recorrido.");
            Assert.IsTrue(GridRedrawHelper.SetRedrawArbol(panel, habilitar: true),
                "El subárbol completo queda re-habilitado.");
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
    public void CambioConcepto_ConMatrizASinApu_MuestraSoloAccesoNuevaMatriz()
    {
        var root = CrearRaiz("sin_apu_acceso");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsTrue(panel.ChipsTipoVisiblesParaPrueba);
            Assert.IsFalse(panel.AccesoNuevaMatrizVisible);

            bool resultado = panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id), false);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(resultado);
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba,
                "Sin APU la banda de campos no pertenece a este estado.");
            Assert.IsFalse(panel.ChipsTipoVisiblesParaPrueba,
                "Sin APU los chips de tipo quedan fuera; no deben quedar restos del concepto anterior.");
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "El acceso Nueva matriz es la única acción del estado sin APU.");
            Assert.IsFalse(panel.TipoQuedaFijoApuParaPrueba,
                "El tipo fijo APU solo existe durante la creación por asignación.");
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
    public void CambioConcepto_EntreMatrices_NoOcultaLaBandaDeCampos()
    {
        var root = CrearRaiz("sin_parpadeo_campos");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            int cambiosVisibilidad = 0;
            panel.CabeceraDatosParaPrueba.VisibleChanged += (_, __) => cambiosVisibilidad++;

            bool resultado = panel.CargarMatrizDeFilaParaPrueba(
                BuscarFilaPorConcepto(grid, escenario.HojaB.Id), false);

            Assert.IsTrue(resultado);
            Assert.AreEqual(0, cambiosVisibilidad,
                "Entre matrices la banda de campos permanece visible: no hay ocultar/mostrar intermedio.");
            Assert.AreEqual(escenario.MatrizB.Id, panel.MatrizVisibleEnPanel?.Id);
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsTrue(panel.ChipsTipoVisiblesParaPrueba);
            Assert.IsFalse(panel.AccesoNuevaMatrizVisible);
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
    public void SinApu_EditarMatriz_ReemplazaAccesoPorBandaYMuestraChips()
    {
        var root = CrearRaiz("sin_apu_editar");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id));
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible);
            Assert.IsFalse(panel.ChipsTipoVisiblesParaPrueba);

            panel.BeginEditMatrix(escenario.MatrizA.Id);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(panel.AccesoNuevaMatrizVisible,
                "Con una matriz cargada el acceso Nueva matriz no convive con los chips.");
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsTrue(panel.ChipsTipoVisiblesParaPrueba);
            Assert.IsTrue(panel.ChipApuVisibleParaPrueba && panel.ChipApuHabilitadoParaPrueba,
                "El chip APU reaparece habilitado (sin residuo del tipo fijo de la captura).");
            Assert.IsTrue(panel.ChipBasicoVisibleParaPrueba && panel.ChipCuadrillaVisibleParaPrueba,
                "Básico y Cuadrilla vuelven a mostrarse al editar una matriz existente.");
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
    public void MismaFila_ConceptoPierdeMatriz_MuestraAccesoAunqueQuedeModoEdicion()
    {
        var root = CrearRaiz("sin_apu_misma_fila");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int filaA = BuscarFilaPorConcepto(grid, escenario.HojaA.Id);
            NavegarYDrenar(grid, panel, filaA);
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba);

            // La fila no cambia (misma fila); el estado de edición de la vista
            // anterior sigue activo y antes bloqueaba el acceso "Nueva matriz".
            ((ConceptoPresupuesto)grid.Rows[filaA].Tag!).MatrizId = null;
            bool resultado = panel.CargarMatrizDeFilaParaPrueba(filaA, true);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(resultado);
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsFalse(panel.ChipsTipoVisiblesParaPrueba);
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "Sin matriz el acceso debe mostrarse aunque la vista anterior siguiera en modo edición.");
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
    public void SinApu_CrearMatriz_SustituyeAccesoPorChipsDeCaptura()
    {
        var root = CrearRaiz("sin_apu_crear");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            NavegarYDrenar(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id));
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible);
            Assert.IsFalse(panel.ChipsTipoVisiblesParaPrueba);

            panel.BeginCreateMatrix(
                TipoMatriz.APU,
                soloApu: true,
                cantidad: 1m,
                conceptoOrigenId: escenario.HojaSinMatriz.Id);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(panel.AccesoNuevaMatrizVisible,
                "En captura el acceso Nueva matriz no comparte header con los chips de tipo.");
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsTrue(panel.ChipsTipoVisiblesParaPrueba);
            Assert.IsTrue(panel.TipoQuedaFijoApuParaPrueba,
                "La creación por asignación mantiene el tipo APU fijo.");
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
    public void SinApu_CrearDesdeSelector_NoDejaAccesoBajoElChipApu()
    {
        var root = CrearRaiz("sin_apu_selector");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(Path.Combine(root, "proyecto-temporal.soproproj"));
            var escenario = CrearEscenario(ctx);
            form = CrearFormConWorkspaceExpandido(ctx, escenario, root);
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int fila = BuscarFilaPorConcepto(grid, escenario.HojaSinMatriz.Id);
            NavegarYDrenar(grid, panel, fila);
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible);

            // Ruta real del selector: MostrarPanelMatrizEmbebido + BeginCreateMatrix
            // sobre el mismo concepto sin APU.
            form.IniciarCreacionMatrizDesdeSelectorParaPrueba(
                fila, TipoMatriz.APU, 1m, escenario.HojaSinMatriz.Id);
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(panel.AccesoNuevaMatrizVisible,
                "La captura reemplaza el acceso: no quedan botón y chip APU en el mismo espacio.");
            Assert.IsTrue(panel.ChipsTipoVisiblesParaPrueba);
            Assert.IsTrue(panel.ChipApuVisibleParaPrueba);
            Assert.IsTrue(panel.TipoQuedaFijoApuParaPrueba,
                "La creación desde el selector fija APU como único tipo.");
            Assert.AreEqual("Nueva APU", panel.TituloCabeceraParaPrueba);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ── Infraestructura del escenario ─────────────────────────────

    private static FormPresupuesto CrearFormConWorkspaceExpandido(
        SOPROContext ctx, EscenarioTransicion escenario, string root)
    {
        var form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
            Path.Combine(root, "sopro"), Path.Combine(root, "local")));
        form.Show();
        form.ExpandirWorkspaceParaPrueba();
        System.Windows.Forms.Application.DoEvents();
        return form;
    }

    private static void NavegarYDrenar(DataGridView grid, PanelMatricesEmbebido panel, int fila)
    {
        Assert.AreNotEqual(-1, fila, "La prueba necesita la fila del concepto en el grid.");
        if (grid.CurrentRow?.Index != fila)
            grid.CurrentCell = CeldaVisibleParaNavegar(grid, fila);
        Assert.AreEqual(fila, panel.FilaConceptoActual,
            "La navegación por el grid debe cargar el concepto indicado.");
        System.Windows.Forms.Application.DoEvents();
        Assert.IsFalse(panel.RedibujoSuprimidoParaPrueba,
            "Los pases diferidos del arranque deben drenar antes de actuar.");
    }

    private sealed class EscenarioTransicion
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Material Material { get; init; } = null!;
        public Matriz MatrizA { get; init; } = null!;
        public Matriz MatrizB { get; init; } = null!;
        public ConceptoPresupuesto HojaA { get; init; } = null!;
        public ConceptoPresupuesto HojaB { get; init; } = null!;
        public ConceptoPresupuesto HojaSinMatriz { get; init; } = null!;
    }

    private static EscenarioTransicion CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto escape y transición",
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
        var hojaSinMatriz = CrearHoja(ctx, proyecto.Id, "C-003", null, 3m, 0m, 3);
        ctx.SaveChanges();

        return new EscenarioTransicion
        {
            Proyecto = proyecto,
            Material = material,
            MatrizA = matrizA,
            MatrizB = matrizB,
            HojaA = hojaA,
            HojaB = hojaB,
            HojaSinMatriz = hojaSinMatriz,
        };
    }

    private static (Matriz Hijo, ComponenteMatriz Auxiliar) AgregarHijoAuxiliar(
        SOPROContext ctx, EscenarioTransicion escenario)
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
        int? matrizId,
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
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_escape_{sufijo}_{Guid.NewGuid():N}");
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
