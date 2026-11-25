using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Chrome;

/// <summary>
/// Chrome flotante extendido a los módulos (issue #171): cada formulario hospeda
/// SOLO su chrome declarado (header, toolbar, banda de filtros/búsqueda y, en
/// FSR, la barra inferior de acciones). Los grids, splits, TabControls, cuerpos
/// de contenido y statusStrips conservan padre, acople y Region intactos.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye formularios reales de módulo (controles WinForms y estado estático).
public class ChromeFlotanteModulosTests
{
    [TestMethod]
    public void ExplosionInsumos_HospedaSoloElHeader_YElGridQuedaIntacto()
    {
        using Escenario escenario = new("explosion", (ctx, proyecto) => new FormExplosionInsumos(ctx, proyecto.Id));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarBandaIntacta(escenario, "dgvExplosion", DockStyle.Fill, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 1);
    }

    [TestMethod]
    public void Porcentajes_HospedaSoloElHeader_YLosGroupBoxesQuedanIntactos()
    {
        using Escenario escenario = new("porcentajes", (ctx, proyecto) => new FormPorcentajes(ctx, proyecto));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        foreach (string nombre in new[] { "grpReferencia", "grpPorcentajes", "grpModoCalculo", "grpPreview" })
            AssertarBandaIntacta(escenario, nombre, Dock: null, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 1);
    }

    [TestMethod]
    public void Indirectos_HospedaHeaderYToolbar_YTabControlGridsYStatusQuedanIntactos()
    {
        using Escenario escenario = new("indirectos", (ctx, proyecto) => new FormIndirectos(ctx, proyecto));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarHost(escenario, "panelToolbarHost", "panelToolbar", DockStyle.Top, aireExteriorLogico: 0);
        AssertarOrdenDeAcople(escenario, "panelTopHost", "panelToolbarHost", "statusStrip", "tabControl");
        AssertarBandaIntacta(escenario, "tabControl", DockStyle.Fill, contenedor: escenario.Form);
        AssertarBandaIntacta(escenario, "statusStrip", DockStyle.Bottom, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 2);
    }

    [TestMethod]
    public void FSR_HospedaHeaderYBarraInferior_YElCuerpoQuedaIntacto()
    {
        using Escenario escenario = new("fsr", (ctx, proyecto) => new FormFSR(ctx, proyecto));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarHost(escenario, "panelBottomHost", "panelBottom", DockStyle.Bottom);

        // Aire espejado en la barra inferior: separación arriba y aire exterior abajo.
        var hostBottom = (ChromeFlotanteHost)Buscar(escenario.Form, "panelBottomHost");
        int dpi = hostBottom.DeviceDpi;
        Assert.AreEqual(
            new Padding(
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi),
                ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetSuperiorLogico, dpi)),
            hostBottom.Padding,
            "La barra de acciones respira arriba (6) y hacia el borde inferior (8).");

        // La botonera sigue funcional dentro del host.
        Assert.IsNotNull(Buscar(escenario.Form, "btnAplicar"), "La barra inferior conserva sus acciones.");
        Assert.IsNotNull(Buscar(escenario.Form, "btnCerrar"), "La barra inferior conserva su cierre.");

        AssertarBandaIntacta(escenario, "panelMain", DockStyle.Fill, contenedor: escenario.Form);
        AssertarOrdenDeAcople(escenario, "panelTopHost", "panelBottomHost", "panelMain");
        AssertarSinMasHosts(escenario, hosts: 2);
    }

    [TestMethod]
    public void Matrices_HospedaHeaderToolbarYFiltros_YGridYStatusQuedanIntactos()
    {
        using Escenario escenario = new("matrices", (ctx, proyecto) => new FormMatrices(ctx, proyecto.Id));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarHost(escenario, "panelToolbarHost", "panelToolbar", DockStyle.Top, aireExteriorLogico: 0);
        AssertarHost(escenario, "panelFiltrosHost", "panelFiltros", DockStyle.Top, aireExteriorLogico: 0);
        AssertarOrdenDeAcople(escenario, "panelTopHost", "panelToolbarHost", "panelFiltrosHost", "statusStrip", "dgvMatrices");
        AssertarBandaIntacta(escenario, "dgvMatrices", DockStyle.Fill, contenedor: escenario.Form);
        AssertarBandaIntacta(escenario, "statusStrip", DockStyle.Bottom, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 3);
    }

    [TestMethod]
    public void CatalogoMateriales_HospedaHeaderToolbarYFiltros_YGridYStatusQuedanIntactos()
    {
        using Escenario escenario = new("catMateriales", (ctx, proyecto) => new FormCatalogoMateriales(ctx, proyecto.Id));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarHost(escenario, "panelToolbarHost", "panelToolbar", DockStyle.Top, aireExteriorLogico: 0);
        AssertarHost(escenario, "panelFiltrosHost", "panelFiltros", DockStyle.Top, aireExteriorLogico: 0);
        AssertarBandaIntacta(escenario, "dgvMateriales", DockStyle.Fill, contenedor: escenario.Form);
        AssertarBandaIntacta(escenario, "statusStrip", DockStyle.Bottom, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 3);
    }

    [TestMethod]
    public void CatalogoHerramientas_HospedaHeaderBotonesYBusqueda_YGridYStatusQuedanIntactos()
    {
        using Escenario escenario = new("catHerramientas", (ctx, proyecto) => new FormCatalogoHerramientas(ctx, proyecto.Id));

        AssertarHost(escenario, "panelHeaderHost", "panelHeader", DockStyle.Top);
        AssertarHost(escenario, "panelBotonesHost", "panelBotones", DockStyle.Top, aireExteriorLogico: 0);
        AssertarHost(escenario, "panelBusquedaHost", "panelBusqueda", DockStyle.Top, aireExteriorLogico: 0);
        AssertarBandaIntacta(escenario, "dgvHerramientas", DockStyle.Fill, contenedor: escenario.Form);
        AssertarBandaIntacta(escenario, "statusStrip", DockStyle.Bottom, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 3);
    }

    [TestMethod]
    public void ProgramaObra_HospedaHeaderYRibbon_YSplitYEstadoQuedanIntactos()
    {
        using Escenario escenario = new("programaObra", (ctx, proyecto) => new FormProgramaObra(ctx, proyecto));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarHost(escenario, "toolProgramaHost", "toolPrograma", DockStyle.Top, aireExteriorLogico: 0);
        AssertarBandaIntacta(escenario, "splitPrincipal", DockStyle.Fill, contenedor: escenario.Form);
        AssertarBandaIntacta(escenario, "lblEstado", DockStyle.Bottom, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 2);
    }

    [TestMethod]
    public void ProgramaInsumos_HospedaHeaderYRibbon_YSplitYStatusQuedanIntactos()
    {
        using Escenario escenario = new("programaInsumos", (ctx, proyecto) => new FormProgramaInsumos(ctx, proyecto));

        AssertarHost(escenario, "panelTopHost", "panelTop", DockStyle.Top);
        AssertarHost(escenario, "toolStripTopHost", "toolStripTop", DockStyle.Top, aireExteriorLogico: 0);
        AssertarBandaIntacta(escenario, "splitPrincipal", DockStyle.Fill, contenedor: escenario.Form);
        AssertarBandaIntacta(escenario, "statusStrip1", DockStyle.Bottom, contenedor: escenario.Form);
        AssertarSinMasHosts(escenario, hosts: 2);
    }

    // ═════════════════════════════ Aserciones compartidas ═════════════════════════════

    private static void AssertarHost(Escenario escenario, string nombreHost, string nombreSuperficie, DockStyle dockEsperado, int aireExteriorLogico = ChromeFlotanteHost.InsetSuperiorLogico)
    {
        var host = (ChromeFlotanteHost)Buscar(escenario.Form, nombreHost);
        Assert.AreSame(escenario.Form, host.Parent, $"{nombreHost} vive en el formulario.");
        Assert.AreEqual(nombreSuperficie, host.Superficie.Name, "El host aloja la superficie correcta.");
        Assert.AreSame(host, host.Superficie.Parent, "La superficie vive dentro del host.");
        Assert.AreEqual(dockEsperado, host.Dock, $"{nombreHost}: acople del host.");
        Assert.AreEqual(dockEsperado, host.Superficie.Dock, $"{nombreHost}: acople original de la superficie.");

        Region? region = host.Superficie.Region;
        Assert.IsNotNull(region, $"{nombreSuperficie} conserva su Region redondeada.");
        Assert.IsFalse(region!.IsVisible(0, 0), $"{nombreSuperficie}: esquina redondeada.");
        Assert.IsTrue(region.IsVisible(host.Superficie.Width / 2, host.Superficie.Height / 2), $"{nombreSuperficie}: centro visible.");

        int dpi = host.DeviceDpi;
        int lados = ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.InsetHorizontalLogico, dpi);
        int exterior = ChromeFlotanteHost.EscalarPixeles(aireExteriorLogico, dpi);
        int interior = ChromeFlotanteHost.EscalarPixeles(ChromeFlotanteHost.SeparacionInferiorLogica, dpi);
        int arriba = dockEsperado == DockStyle.Top ? exterior : interior;
        int abajo = dockEsperado == DockStyle.Top ? interior : exterior;
        Assert.AreEqual(
            new Padding(lados, arriba, lados, abajo),
            host.Padding,
            $"{nombreHost}: aire lógico (10 lateral, {aireExteriorLogico} exterior, 6 interior) escalado a {dpi} DPI.");
        Assert.AreEqual(ChromeFlotanteHost.EscalarPixeles(aireExteriorLogico, dpi), exterior,
            $"{nombreHost}: aire exterior lógico de {aireExteriorLogico}px.");

        Assert.AreEqual(host.Superficie.Height + host.Padding.Vertical, host.Height,
            $"{nombreHost}: alto = superficie + aire vertical.");
    }

    private static void AssertarBandaIntacta(Escenario escenario, string nombre, DockStyle? Dock, Control contenedor)
    {
        Control banda = Buscar(escenario.Form, nombre);
        Assert.AreSame(contenedor, banda.Parent, $"{nombre}: conserva su contenedor (sin reparenteo).");
        Assert.IsNull(banda.Region, $"{nombre}: sin recorte redondeado (queda fuera del criterio flotante).");
        if (Dock.HasValue)
            Assert.AreEqual(Dock.Value, banda.Dock, $"{nombre}: conserva su acople.");
    }

    private static void AssertarOrdenDeAcople(Escenario escenario, params string[] ordenProcesado)
    {
        Control.ControlCollection controles = escenario.Form.Controls;
        var indices = controles.Cast<Control>().ToDictionary(c => c.Name, controles.GetChildIndex);

        for (int i = 0; i < ordenProcesado.Length - 1; i++)
            Assert.IsTrue(indices[ordenProcesado[i]] > indices[ordenProcesado[i + 1]],
                $"{ordenProcesado[i]} debe acoplarse antes (quedar más arriba/afuera) que {ordenProcesado[i + 1]}.");
    }

    private static void AssertarSinMasHosts(Escenario escenario, int hosts)
    {
        int encontrados = ContarHosts(escenario.Form);
        Assert.AreEqual(hosts, encontrados,
            $"El módulo gana exactamente {hosts} host(s) de primer nivel; nada más se reparenta.");
    }

    private static int ContarHosts(Control raiz)
        => (raiz is ChromeFlotanteHost ? 1 : 0)
           + raiz.Controls.Cast<Control>().Sum(ContarHosts);

    private static Control Buscar(Control raiz, string nombre)
    {
        if (string.Equals(raiz.Name, nombre, StringComparison.Ordinal))
            return raiz;

        foreach (Control hijo in raiz.Controls)
        {
            try { return Buscar(hijo, nombre); }
            catch (InvalidOperationException) { }
        }

        throw new InvalidOperationException($"No se encontró el control '{nombre}'.");
    }

    // ═════════════════════════════ Escenario compartido ═════════════════════════════

    private sealed class Escenario : IDisposable
    {
        private readonly string _root;
        private readonly SOPROContext _ctx;

        public Escenario(string id, Func<SOPROContext, Proyecto, Form> crearFormulario)
        {
            _root = Path.Combine(Path.GetTempPath(), $"sopro_chrome_modulos_{id}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _ctx = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _ctx.Database.EnsureCreated();
            Proyecto = CrearProyecto(_ctx);
            Form = crearFormulario(_ctx, Proyecto);
            // Sin Show(): el Load de los módulos carga datos y puede abrir
            // diálogos modales (p. ej. Explosion sin conceptos). El layout con
            // handle basta para validar acople, aire, altos y Regiones.
            Form.CreateControl();
            Form.PerformLayout();
            System.Windows.Forms.Application.DoEvents();
        }

        public Form Form { get; }

        public Proyecto Proyecto { get; }

        public void Dispose()
        {
            if (Form.Visible)
                Form.Close();
            Form.Dispose();
            _ctx.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static Proyecto CrearProyecto(SOPROContext ctx)
        {
            Proyecto proyecto = new()
            {
                Nombre = "Proyecto chrome flotante módulos",
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
    }
}
