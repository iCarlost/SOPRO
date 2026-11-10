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
/// CANDADOS de comportamiento del header compacto del panel embebido de matrices
/// (issue #103, Concepto A):
/// <list type="bullet">
/// <item>el cambio de tipo de matriz capturado desde los chips autoguarda por el
/// comando único de cabecera (BudgetPersistenceService.ExecuteMatrixHeaderEditCommand)
/// exactamente una vez;</item>
/// <item>una matriz referenciada por conceptos (o como auxiliar) rechaza el cambio
/// de tipo sin escribir: la vista conserva el tipo y la base no se toca;</item>
/// <item>la creación desde el presupuesto es tipo fijo APU y VISUALMENTE solo muestra
/// el chip APU (deshabilitado, con tooltip/descripción de tipo fijo); Básico/Cuadrilla
/// NO aparecen; al editar una matriz existente los tres chips reaparecen habilitados;</item>
/// <item>sin matriz en estado (sin selección/agrupador/concepto sin APU) los chips no
/// se muestran: el header se reserva para "Nueva matriz";</item>
/// <item>el tránsito de una matriz editada hacia un concepto sin APU muestra el acceso
/// "Nueva matriz" (sin filtrar el estado de edición).</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class PanelMatricesCabeceraCompactaSemanticaTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";
    private static readonly Action<string, string, MessageBoxIcon> AvisoPorDefecto =
        (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

    /// <summary>
    /// Patrón de aislamiento del repo (FormPresupuestoValidacionNoModalTests): captura
    /// el interceptor previo, instala uno silencioso ANTES de Show y devuelve el
    /// valor previo para restaurarlo en finally.
    /// </summary>
    private static Action<System.Collections.Generic.IReadOnlyList<PresupuestoValidationService.ValidationIssue>, string, bool>? InstalarInterceptorInconsistencias()
    {
        var interceptorPrevio = FormPresupuesto.InterceptorDialogoInconsistencias;
        FormPresupuesto.InterceptorDialogoInconsistencias = (_, _, _) => { };
        return interceptorPrevio;
    }

    [TestMethod]
    public void ConfirmarTipoDesdeCabecera_AutoguardaUnaVezPorComandoYSincroniza()
    {
        var root = CrearRaiz("compacta_tipo_ok");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            var matrizLibre = CrearMatrizSinReferencia(ctx, escenario.Proyecto.Id, "BAS-LIBRE", "Insumo compuesto libre", "kg", TipoMatriz.Basico, 40m);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var panel = form.PanelMatricesInterno!;

            panel.BeginEditMatrix(matrizLibre.Id);

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            // Gestión de usuario equivalente al chip: cambiar Basico -> APU.
            panel.CambiarTipoCabeceraDesdeChipParaPrueba(TipoMatriz.APU);

            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEdicionCabecera,
                panel.UltimaRazonSinDespacharCabecera);
            Assert.AreEqual(0, guardasCompartidos,
                "El comando escribe con contexto nuevo: el compartido no hace SaveChanges.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La cabecera confirmada queda Unchanged sin trabajo pendiente.");

            using var verificacion = new SOPROContext(dbPath);
            var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == matrizLibre.Id);
            Assert.AreEqual(TipoMatriz.APU, matrizDb.Tipo,
                "El tipo confirmado por el usuario se persistió vía comando.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CambioTipoProgramaticoEnCarga_NoDespachaComando()
    {
        var root = CrearRaiz("compacta_tipo_programatico");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

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

            panel.CargarMatrizDeFilaParaPrueba(BuscarFilaPorConcepto(grid, escenario.HojaA.Id), forceReload: true);
            Assert.IsNull(panel.UltimoEstadoEdicionCabecera,
                "La carga normal no debe transitar la ruta de guardado de cabecera.");
            Assert.IsFalse(panel.TieneCambiosPendientesCabeceraParaPrueba);
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CambioTipoMatrizReferenciada_Conflicto_PreservaVistaYTipoPersistido()
    {
        var root = CrearRaiz("compacta_tipo_referenciada");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx); // MatrizA referenciada por HojaA
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var panel = form.PanelMatricesInterno!;

            panel.BeginEditMatrix(escenario.MatrizA.Id);
            panel.CambiarTipoCabeceraDesdeChipParaPrueba(TipoMatriz.Basico);

            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoEdicionCabecera,
                panel.UltimaRazonSinDespacharCabecera);
            Assert.IsFalse(panel.BloqueoCabeceraActivo,
                "El rechazo por matriz referenciada es pre-despacho: no hay resultado incierto.");
            Assert.AreEqual(TipoMatriz.Basico, panel.TipoCabeceraParaPrueba,
                "La vista conserva la selección del usuario como borrador no confirmado.");
            Assert.IsTrue(panel.TieneCambiosPendientesCabeceraParaPrueba);

            using var verificacion = new SOPROContext(dbPath);
            var matrizDb = verificacion.Matrices.AsNoTracking().Single(m => m.Id == escenario.MatrizA.Id);
            Assert.AreEqual(TipoMatriz.APU, matrizDb.Tipo,
                "La base conserva el tipo original de la matriz referenciada.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CreacionDesdePresupuesto_FijaApuYNoPermiteOtrosTipos()
    {
        var root = CrearRaiz("compacta_creacion_fija");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var panel = form.PanelMatricesInterno!;

            panel.BeginCreateMatrix(TipoMatriz.APU, soloApu: true);

            Assert.IsTrue(panel.TipoQuedaFijoApuParaPrueba,
                "La creación por asignación debe reportar tipo fijo APU.");
            Assert.AreEqual(TipoMatriz.APU, panel.TipoCabeceraParaPrueba);

            bool cambioAceptado = panel.CambiarTipoCabeceraDesdeChipParaPrueba(TipoMatriz.Basico);
            Assert.IsFalse(cambioAceptado, "Un tipo fijo no acepta la selección de Básico.");
            Assert.AreEqual(TipoMatriz.APU, panel.TipoCabeceraParaPrueba,
                "El tipo permanece APU tras el intento de cambio.");
            Assert.IsNull(panel.UltimoEstadoEdicionCabecera,
                "Un tipo fijo no despacha ningún comando.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void TransitoDeMatrizAConceptoSinMatriz_MuestraNuevaMatriz_SinFiltrarEdicion()
    {
        var root = CrearRaiz("compacta_transito_nueva");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            var conceptoSinMatriz = CrearHoja(ctx, escenario.Proyecto.Id, "C-003", matrizId: null, 2m, 5m, 3);
            ctx.SaveChanges();

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            FijarFilaActualSinBorrador(grid, panel, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            panel.BeginEditMatrix(escenario.MatrizA.Id);

            // Tránsito hacia el concepto sin matriz (sin cambios pendientes).
            grid.CurrentCell = CeldaVisibleParaNavegar(grid, BuscarFilaPorConcepto(grid, conceptoSinMatriz.Id));

            Assert.AreEqual("Concepto sin APU asignada", panel.TituloCabeceraParaPrueba);
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "Tras editar una matriz, llegar a un concepto sin APU debe reexponer Nueva matriz.");
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba,
                "El tránsito no debe dejar los campos de una matriz fantasma activos.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void CreacionForzada_MuestraSoloChipApuFijo_YLosOtrosNoSeMuestran()
    {
        var root = CrearRaiz("compacta_fija_visual");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

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
            var panel = form.PanelMatricesInterno!;

            panel.BeginCreateMatrix(TipoMatriz.APU, soloApu: true);

            // Presentación: SOLO el chip APU visible, marcado y NO editable.
            Assert.IsTrue(panel.ChipBasicoVisibleParaPrueba == panel.ChipCuadrillaVisibleParaPrueba,
                "Estado consistente del grupo de chips.");
            Assert.IsFalse(panel.ChipBasicoVisibleParaPrueba,
                "En creación por asignación el chip Básico no debe mostrarse.");
            Assert.IsFalse(panel.ChipCuadrillaVisibleParaPrueba,
                "En creación por asignación el chip Cuadrilla no debe mostrarse.");
            Assert.IsTrue(panel.ChipApuVisibleParaPrueba, "El chip APU permanece visible.");
            Assert.IsFalse(panel.ChipApuHabilitadoParaPrueba,
                "El chip APU fijo no es editable (deshabilitado).");

            bool cambioAceptado = panel.CambiarTipoCabeceraDesdeChipParaPrueba(TipoMatriz.Basico);
            Assert.IsFalse(cambioAceptado, "Un tipo fijo no acepta la selección de Básico.");
            Assert.IsNull(panel.UltimoEstadoEdicionCabecera, "Un tipo fijo no despacha ningún comando.");

            // Editar una matriz EXISTENTE re-muestra y habilita los tres chips.
            panel.BeginEditMatrix(escenario.MatrizB.Id);
            Assert.IsTrue(panel.ChipBasicoVisibleParaPrueba, "En edición los tres chips reaparecen.");
            Assert.IsTrue(panel.ChipCuadrillaVisibleParaPrueba);
            Assert.IsTrue(panel.ChipApuVisibleParaPrueba && panel.ChipApuHabilitadoParaPrueba,
                "El chip APU vuelve a ser editable en edición.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EstilosSinMatriz_OcultanChips_YElHeaderSeReservaParaNuevaMatriz()
    {
        var root = CrearRaiz("compacta_chips_sin_matriz");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = (_, _, _) => { };
        var interceptorPrevio = InstalarInterceptorInconsistencias();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = CrearContexto(dbPath);
            var escenario = CrearEscenario(ctx);
            var conceptoSinMatriz = CrearHoja(ctx, escenario.Proyecto.Id, "C-003", matrizId: null, 2m, 5m, 3);
            ctx.SaveChanges();

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            // Concepto sin APU: los chips no se muestran. Navegación por el grid del
            // presupuesto (ruta real de usuario al hacer clic en otra fila): por diseño
            // NotificarFilaCambiada se ignora mientras el panel edita una cabecera.
            grid.CurrentCell = CeldaVisibleParaNavegar(grid, BuscarFilaPorConcepto(grid, conceptoSinMatriz.Id));
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual("Concepto sin APU asignada", panel.TituloCabeceraParaPrueba);
            Assert.IsFalse(panel.ChipsTipoVisiblesParaPrueba,
                "Los chips de tipo no pertenecen a estados sin matriz.");
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "El header del estado sin APU se reserva para Nueva matriz.");

            // Con matriz cargada los chips vuelven.
            grid.CurrentCell = CeldaVisibleParaNavegar(grid, BuscarFilaPorConcepto(grid, escenario.HojaA.Id));
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(panel.ChipsTipoVisiblesParaPrueba,
                "Con matriz cargada los chips de tipo reaparecen.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void DescarteYReconciliacion_ConservanContratosTrasTransito()
    {
        var root = CrearRaiz("compacta_descarte_transito");
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

            panel.BeginCreateMatrix(TipoMatriz.APU, soloApu: true);
            panel.EstablecerCabeceraParaPrueba("BORRADOR-C", "Borrador", "m2", TipoMatriz.APU);
            panel.SimularResultadoInciertoCabeceraParaPrueba();
            Assert.IsTrue(panel.BloqueoCabeceraActivo);

            // Descarte del borrador: ConfirmarDescartarNuevaMatriz ? no escribe nada.
            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz = () => true;
            bool salio = panel.IntentarSalirDeCabeceraActual();
            Assert.IsTrue(salio, "El descarte confirmado permite salir.");
            using var verificacion = new SOPROContext(dbPath);
            Assert.IsFalse(verificacion.Matrices.AsNoTracking().Any(m => m.Clave == "BORRADOR-C"),
                "Descartar no escribe nada.");
        }
        finally
        {
            PanelMatricesEmbebido.ConfirmarDescartarNuevaMatriz =
                () => MessageBox.Show("¿Descartar?", "Descartar", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes;
            PanelMatricesEmbebido.MostrarAvisoCabeceraMatriz = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class EscenarioCompacta
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Material Material { get; init; } = null!;
        public Matriz MatrizA { get; init; } = null!;
        public Matriz MatrizB { get; init; } = null!;
        public ConceptoPresupuesto HojaA { get; init; } = null!;
        public ConceptoPresupuesto HojaB { get; init; } = null!;
    }

    private static EscenarioCompacta CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto cabecera compacta",
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

        var matrizA = CrearMatrizConInsumo(ctx, proyecto.Id, "APU-001", "Concreto simple", "m3", material, 2m, 120m);
        var matrizB = CrearMatrizConInsumo(ctx, proyecto.Id, "APU-002", "Acero de refuerzo", "kg", material, 1m, 60m);

        var hojaA = CrearHoja(ctx, proyecto.Id, "C-001", matrizA.Id, 10m, 150m, 1);
        var hojaB = CrearHoja(ctx, proyecto.Id, "C-002", matrizB.Id, 5m, 60m, 2);
        ctx.SaveChanges();

        return new EscenarioCompacta
        {
            Proyecto = proyecto,
            Material = material,
            MatrizA = matrizA,
            MatrizB = matrizB,
            HojaA = hojaA,
            HojaB = hojaB,
        };
    }

    private static Matriz CrearMatrizSinReferencia(
        SOPROContext ctx, int proyectoId, string clave, string descripcion, string unidad, TipoMatriz tipo, decimal costo)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = unidad,
            Tipo = tipo,
            CostoDirecto = costo,
            Notas = string.Empty,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        return matriz;
    }

    private static Matriz CrearMatrizConInsumo(
        SOPROContext ctx, int proyectoId, string clave, string descripcion, string unidad,
        Material material, decimal cantidad, decimal importe)
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
        SOPROContext ctx, int proyectoId, string clave, int? matrizId,
        decimal cantidad, decimal costo, int orden)
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
        var root = Path.Combine(Path.GetTempPath(), $"sopro_panel_compacta_{sufijo}_{Guid.NewGuid():N}");
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
        Assert.AreEqual(fila, panel.FilaConceptoActual);
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper)
            .GetField(CampoProyectoGlobalFormato,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper)
            .GetField(CampoProyectoGlobalFormato,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, previo);

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
