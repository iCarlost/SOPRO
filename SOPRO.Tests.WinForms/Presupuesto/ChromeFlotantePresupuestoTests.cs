using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Chrome flotante del presupuesto (issue #171): SOLO panelTop, panelToolbar y
/// workspaceToolbar se hospedan en wrappers con aire alrededor, borde de 1px y
/// sombra sutil. Los grids, el split, el status bar y el cuerpo del panel de
/// matrices conservan padres, Region y geometría.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms y estado estático).
public class ChromeFlotantePresupuestoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Las pruebas que crean FormPresupuesto deben ejecutarse en STA.");
    }

    [TestMethod]
    public void Constructor_HospedaSoloElChrome_ConservaJerarquiaYRegionesDeGridsYStatus()
    {
        using EscenarioPresupuesto escenario = new EscenarioPresupuesto("chrome_hosting");
        FormPresupuesto form = escenario.Form;

        // Solo el chrome queda hospedado: cada superficie vive en su host.
        AssertarHostFlotante(form.PanelTopHost, "panelTopHost", "panelTop", esperadoPadre: form);
        AssertarHostFlotante(form.PanelToolbarHost, "panelToolbarHost", "panelToolbar", esperadoPadre: form);
        AssertarHostFlotante(form.WorkspaceToolbarHost, "workspaceToolbarHost", "workspaceToolbar",
            esperadoPadre: escenario.PanelMatrices);

        // Orden de acople del form intacto: split (Fill) - status (Bottom) -
        // hostToolbar (Top) - hostHeader (Top) procesados en ese orden por el
        // layout (índice z descendente), sin Form.Padding. Los controles
        // flotantes del autocompletado pueden insertarse en índices menores.
        Control.ControlCollection controles = form.Controls;
        Assert.AreEqual(6, controles.Count, "El form gana solo los hosts del chrome de primer nivel.");
        int idxTop = controles.GetChildIndex(form.PanelTopHost!);
        int idxToolbar = controles.GetChildIndex(form.PanelToolbarHost!);
        int idxStatus = controles.GetChildIndex(BuscarPorNombre(form, "statusStrip"));
        int idxSplit = controles.GetChildIndex(form.SplitWorkspace);
        Assert.AreEqual(controles.Count - 1, idxTop, "El host del header se acopla primero (queda arriba de todo).");
        Assert.IsTrue(idxToolbar < idxTop && idxStatus < idxToolbar && idxSplit < idxStatus,
            $"Orden de acople esperado header>toolbar>status>split (top={idxTop}, toolbar={idxToolbar}, status={idxStatus}, split={idxSplit}).");
        Assert.AreEqual(new Padding(0), form.Padding, "No se usa Form.Padding (cambiaría split/grid/status).");

        // Dentro de panelMatrices: host de la toolbar arriba y host operativo en Fill.
        Control panelMatrices = escenario.PanelMatrices;
        Assert.AreEqual(2, panelMatrices.Controls.Count, "panelMatrices contiene solo host operativo + host de toolbar.");
        Assert.AreEqual(1, panelMatrices.Controls.GetChildIndex(form.WorkspaceToolbarHost!));
        Control panelMatricesHost = BuscarPorNombre(form, "panelMatricesHost");
        Assert.AreEqual(DockStyle.Fill, panelMatricesHost.Dock, "El host operativo sigue en Fill.");
        Assert.AreEqual(panelMatrices, panelMatricesHost.Parent);

        // Jerarquía de grids/status intacta.
        Control grid = BuscarPorNombre(form, "dgvPresupuesto");
        Assert.AreSame(form.SplitWorkspace.Panel1, grid.Parent, "El grid sigue en Panel1.");
        Assert.AreSame(form.SplitWorkspace.Panel2, panelMatrices.Parent, "El workspace sigue en Panel2.");
        Assert.AreSame(form, form.SplitWorkspace.Parent);
        Assert.AreSame(form, BuscarPorNombre(form, "statusStrip").Parent);

        // Regiones: el chrome redondea; grids/split/status/panelMatrices no.
        AssertRegionRedondeada(form.PanelTopHost!.Superficie, "panelTop");
        AssertRegionRedondeada(form.PanelToolbarHost!.Superficie, "panelToolbar");
        AssertRegionRedondeada(form.WorkspaceToolbarHost!.Superficie, "workspaceToolbar");
        Assert.IsNull(grid.Region, "dgvPresupuesto no recibe Region.");
        Assert.IsNull(form.SplitWorkspace.Region, "splitContainer no recibe Region.");
        Assert.IsNull(panelMatrices.Region, "panelMatrices no recibe Region.");
        Assert.IsNull(panelMatricesHost.Region, "panelMatricesHost no recibe Region.");
        Assert.IsNull(BuscarPorNombre(form, "statusStrip").Region, "statusStrip no recibe Region.");
    }

    [TestMethod]
    public void Hosts_ReservanAireLogico_EscalarAlDpiYAjustanAlturaAlTamanioDeLaSuperficie()
    {
        using EscenarioPresupuesto escenario = new EscenarioPresupuesto("chrome_aire");
        FormPresupuesto form = escenario.Form;
        int dpi = form.DeviceDpi;

        var aireHeader = new Padding(
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetSuperiorLogico, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi));
        Assert.AreEqual(aireHeader, form.PanelTopHost!.Padding, "Header: 10px a los lados, 8 arriba, 6 de separación abajo.");

        var aireToolbar = new Padding(
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
            0,
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi));
        Assert.AreEqual(aireToolbar, form.PanelToolbarHost!.Padding, "Toolbar: 10px a los lados y 8 de separación abajo.");

        var aireWorkspace = new Padding(
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionWorkspaceLogica, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
            ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionWorkspaceLogica, dpi));
        Assert.AreEqual(aireWorkspace, form.WorkspaceToolbarHost!.Padding, "Toolbar del workspace: 10px a los lados, 6 arriba y abajo.");

        foreach (var host in new[] { form.PanelTopHost!, form.PanelToolbarHost!, form.WorkspaceToolbarHost! })
        {
            Assert.AreEqual(DockStyle.Top, host.Dock, $"{host.Name} debe seguir acoplado arriba.");
            Assert.AreEqual(DockStyle.Top, host.Superficie.Dock, $"La superficie de {host.Name} conserva su acople.");
            Assert.AreEqual(host.Superficie.Height + host.Padding.Vertical, host.Height,
                $"{host.Name}: el alto del host es superficie + aire vertical.");

            // La superficie queda dentro del aire, no superpuesta al borde del host.
            Rectangle boundsSuperficie = host.Superficie.Bounds;
            Assert.IsTrue(boundsSuperficie.X >= host.Padding.Left && boundsSuperficie.Y >= host.Padding.Top,
                $"{host.Name}: la superficie flota dentro del aire del host.");
        }

        // Escalado DPI puro (los handlers re-aplican estas mismas métricas).
        Assert.AreEqual(13, ChromeFlotanteHost.EscalarPixeles(10, 125));
        Assert.AreEqual(10, ChromeFlotanteHost.EscalarPixeles(8, 125));
        Assert.AreEqual(8, ChromeFlotanteHost.EscalarPixeles(6, 125));
        Assert.AreEqual(18, ChromeFlotanteHost.RadioEscalado(SoproUiMetrics.SurfaceRadiusPx, 125));
        Assert.AreEqual(SoproUiMetrics.SurfaceRadiusPx, ChromeFlotanteHost.RadioEscalado(SoproUiMetrics.SurfaceRadiusPx, 96));
    }

    [TestMethod]
    public void SombraYBorde_CumplenContratoSutil_YElHostPintaFondoSuperficieYBorde()
    {
        // Sombra determinista: solo vertical, sutil y neutra (sin halo ni azul).
        int[] alfas = ChromeFlotanteHost.AlfasSombra;
        Assert.HasCount(3, alfas, "Tres pasadas de sombra (sin dependencia de blur).");
        Assert.IsTrue(alfas.Max() <= 24 && alfas.Sum() <= 48,
            $"Sombra sutil: alfa máximo {alfas.Max()}, acumulado {alfas.Sum()}.");
        int[] offsets = ChromeFlotanteHost.DesplazamientosSombra;
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, offsets, "La sombra solo cae hacia abajo (sin halo lateral).");

        Color sombra = ChromeFlotanteHost.ColorSombra;
        Assert.IsTrue(sombra.R <= 0x18 && sombra.G <= 0x1F && sombra.B <= 0x35,
            "Sombra neutra oscura (slate-900 del tema).");
        Assert.AreEqual(SoproTheme.Colors.SurfaceBorder, ChromeFlotanteHost.ColorBorde,
            "Borde 1px con el token de borde de superficies del tema.");

        // Pintado real: fondo alrededor, superficie dentro y borde perimetral.
        using var superficie = new Panel { BackColor = Color.FromArgb(51, 51, 76), Dock = DockStyle.Top, Height = 40 };
        SoproTheme.ApplyRoundedCorners(superficie, SoproUiMetrics.SurfaceRadiusPx);
        using var host = new ChromeFlotanteHost(superficie, SoproUiMetrics.SurfaceRadiusPx,
            "hostPrueba", ChromeFlotanteHost.InsetSuperiorLogico,
            ChromeFlotanteHost.SeparacionInferiorLogica, Color.White);
        using var ventana = new Form { ClientSize = new Size(300, 120), BackColor = Color.White };
        ventana.Controls.Add(host);
        ventana.Show();
        System.Windows.Forms.Application.DoEvents();

        // Pintado real: fondo alrededor, superficie dentro y borde perimetral.
        using var bitmap = new Bitmap(host.Width, host.Height);
        host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, host.Size));

        // Aire del host (fondo claro), centro de la superficie (oscura) y borde.
        Color aire = bitmap.GetPixel(5, host.Height / 2);
        Color centro = bitmap.GetPixel(host.Width / 2, host.Padding.Top + superficie.Height / 2);
        Assert.AreEqual(Color.White.ToArgb(), aire.ToArgb(), "El aire alrededor muestra el fondo existente.");
        Assert.AreEqual(Color.FromArgb(51, 51, 76).ToArgb(), centro.ToArgb(), "La superficie conserva su color dentro del host.");
        Color borde = bitmap.GetPixel(host.Width / 2, host.Padding.Top - 1);
        Assert.IsTrue(borde.R < 255 || borde.G < 255 || borde.B < 255,
            "Sobre la superficie hay un trazo perimetral visible (borde 1px).");

        // Redimensionar no rompe el contrato ni deja altura desfasada.
        int altoAntes = superficie.Height;
        superficie.Height = altoAntes + 20;
        System.Windows.Forms.Application.DoEvents();
        Assert.AreEqual(superficie.Height + host.Padding.Vertical, host.Height,
            "El host reajusta su alto ante cambios de tamaño de la superficie.");
        AssertRegionRedondeada(superficie, "superficie tras resize");
    }

    [TestMethod]
    public void WorkspaceToolbarHost_SigueAcopladoArribaAlCambiarEntreAbajoYDerecha()
    {
        using EscenarioPresupuesto escenario = new EscenarioPresupuesto("chrome_ws");
        FormPresupuesto form = escenario.Form;
        var host = form.WorkspaceToolbarHost!;
        Control panelMatricesHost = BuscarPorNombre(form, "panelMatricesHost");

        foreach (PosicionWorkspace destino in new[] { PosicionWorkspace.Derecha, PosicionWorkspace.Abajo })
        {
            InvocarCambioPosicion(form, destino);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(destino, form.PosicionWorkspaceActual,
                $"La posición debe cambiar a {destino} (la ventana de prueba cabe en ambas).");
            Assert.AreSame(escenario.PanelMatrices, host.Parent,
                "El host de la toolbar del workspace no cambia de contenedor al reorientar.");
            Assert.AreEqual(DockStyle.Top, host.Dock, "El host sigue acoplado arriba en ambas orientaciones.");
            Assert.AreSame(escenario.PanelMatrices, panelMatricesHost.Parent,
                "El host operativo del panel de matrices no se mueve.");
            Assert.AreEqual(DockStyle.Fill, panelMatricesHost.Dock);

            // La toolbar conserva sus comandos clicables dentro del host.
            var toolbar = (ToolStrip)host.Superficie;
            Assert.IsTrue(toolbar.Items.Count >= 5, "La toolbar del workspace conserva sus items.");
            CollectionAssert.AllItemsAreNotNull(toolbar.Items.OfType<ToolStripItem>().ToList());
        }
    }

    private static void AssertarHostFlotante(ChromeFlotanteHost? host, string nombreHost, string nombreSuperficie, Control esperadoPadre)
    {
        Assert.IsNotNull(host, $"Debe existir el host {nombreHost}.");
        Assert.AreEqual(nombreHost, host!.Name);
        Assert.AreSame(esperadoPadre, host.Parent, $"{nombreHost} queda dentro de {esperadoPadre.Name}.");
        Assert.AreEqual(nombreSuperficie, host.Superficie.Name, "El host aloja la superficie correcta.");
        Assert.AreSame(host, host.Superficie.Parent);
        AssertRegionRedondeada(host.Superficie, nombreSuperficie);
    }

    private static void AssertRegionRedondeada(Control control, string nombre)
    {
        Region? region = control.Region;
        Assert.IsNotNull(region, $"{nombre} debe conservar un Region redondeado.");
        Assert.IsFalse(region!.IsVisible(0, 0),
            $"{nombre}: la esquina (0,0) debe quedar fuera del Region (esquina redondeada).");
        Assert.IsTrue(region.IsVisible(control.Width / 2, control.Height / 2),
            $"{nombre}: el centro del control debe seguir visible.");
    }

    private static Control BuscarPorNombre(Control raiz, string nombre)
        => BuscarPorNombreOpcional(raiz, nombre)
           ?? throw new InvalidOperationException($"No se encontro el control {nombre}.");

    private static Control? BuscarPorNombreOpcional(Control raiz, string nombre)
    {
        if (string.Equals(raiz.Name, nombre, StringComparison.Ordinal))
            return raiz;

        foreach (Control hijo in raiz.Controls)
        {
            Control? encontrado = BuscarPorNombreOpcional(hijo, nombre);
            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    /// <summary>
    /// Cambia la posición del workspace por la misma ruta del menú
    /// (CambiarPosicionWorkspace), invocada por reflexión como el resto de las
    /// pruebas del módulo.
    /// </summary>
    private static void InvocarCambioPosicion(FormPresupuesto form, PosicionWorkspace destino)
    {
        MethodInfo metodo = typeof(FormPresupuesto).GetMethod("CambiarPosicionWorkspace",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Debe existir CambiarPosicionWorkspace.");
        metodo.Invoke(form, new object?[] { destino });
    }

    // =====================================================================
    // Escenario compartido (patrón de las demás pruebas de Presupuesto)
    // =====================================================================

    private sealed class EscenarioPresupuesto : IDisposable
    {
        private readonly string _root;
        private readonly Proyecto? _previo;
        private readonly SOPROContext _ctx;

        public EscenarioPresupuesto(string id)
        {
            _root = Path.Combine(Path.GetTempPath(), $"sopro_chrome_flotante_{id}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _previo = LeerProyectoGlobalFormato();
            _ctx = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _ctx.Database.EnsureCreated();
            Proyecto proyecto = CrearProyecto(_ctx);
            var workspace = new ProjectWorkspaceService(
                Path.Combine(_root, "sopro"), Path.Combine(_root, "local"));
            Form = new FormPresupuesto(_ctx, proyecto, workspace);
            Form.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public FormPresupuesto Form { get; }

        public Control PanelMatrices => BuscarPorNombre(Form, "panelMatrices");

        public void Dispose()
        {
            Form.Dispose();
            _ctx.Dispose();
            typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato, BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, _previo);
            LimpiarDirectorio(_root);
        }
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = "Proyecto chrome flotante",
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
        return proyecto;
    }

    private static void LimpiarDirectorio(string ruta)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(ruta))
                    Directory.Delete(ruta, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                Thread.Sleep(200);
            }
        }
    }
}
