using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Sidebar;

/// <summary>
/// Contrato del sidebar real de FormProyecto (issue #193, Fase 1):
/// - owner-draw total sin líneas ni glifos +/- nativos, con doble buffer;
/// - modo colapsado a 60px lógicos con botón de colapso pequeño arriba-derecha;
/// - las secciones siguen alternando expansión con un clic y el estado
///   persiste en <c>NodosMenuExpandidos</c>/<c>BarraLateralColapsada</c>;
/// - el pintado por formulario distingue activo con y sin foco.
/// </summary>
[STATestClass]
[DoNotParallelize] // Crea el formulario real del shell (controles WinForms y SQLite temporal).
public class SidebarFormularioTests
{
    private const BindingFlags PrivadoDeInstancia = BindingFlags.Instance | BindingFlags.NonPublic;

    private string _root = string.Empty;
    private SOPROContext? _context;
    private FormProyecto? _form;

    [TestInitialize]
    public void Preparar()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_sidebar_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _context = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
        _context.Database.EnsureCreated();
        _form = new FormProyecto(_context, CrearProyecto());
        _ = _form.Handle; // Necesario para Bounds de nodos y validaciones de handle.
    }

    [TestCleanup]
    public void Limpiar()
    {
        _form?.Dispose();
        _context?.Dispose();
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
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

    [TestMethod]
    public void Arbol_OwnerDrawTotal_SinLineasNiGlifosNativosYConDobleBuffer()
    {
        var tree = _form!.treeMenu;

        Assert.AreEqual(TreeViewDrawMode.OwnerDrawAll, tree.DrawMode,
            "El sidebar debe dibujarse por completo (Fase 1 del issue #193).");
        Assert.IsFalse(tree.ShowLines, "Sin líneas nativas.");
        Assert.IsFalse(tree.ShowRootLines, "Sin líneas de raíz nativas.");
        Assert.IsFalse(tree.ShowPlusMinus, "Sin glifos +/- nativos: el chevron es propio.");
        Assert.IsTrue(LeerDoubleBuffered(tree), "El árbol debe tener doble buffer para evitar flicker.");
        Assert.IsFalse(tree.BorderStyle != BorderStyle.None, "El árbol no debe añadir borde propio.");
    }

    [TestMethod]
    public void ModoExpandido_FilasYHeaderEscaladas_YBotonPequenoArribaDerecha()
    {
        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: false);
        var m = SidebarMetrics.Crear(_form!.DeviceDpi);

        var panel = LeerCampo<Panel>(_form, "panelSidebarHeader");
        var boton = _form.btnToggleSidebar;

        Assert.AreEqual(m.AltoHeader, panel.Height, "El header debe salir de las métricas DPI.");
        Assert.AreEqual(m.AltoFila, _form.treeMenu.ItemHeight, "La fila debe salir de las métricas DPI.");
        Assert.AreEqual(m.LadoBotonColapso, boton.Width, "El botón de colapso debe ser pequeño, no banda completa.");
        Assert.AreEqual("«", boton.Text, "En modo expandido el botón invita a colapsar.");
        Assert.IsTrue(boton.Right <= panel.Width && boton.Top >= 0 && boton.Bottom <= panel.Height,
            "El botón debe quedar anclado dentro de la esquina superior derecha del header.");
        Assert.IsTrue(panel.Width - boton.Right <= m.MargenBotonColapso + 2,
            "El botón debe pegarse al borde derecho (más su margen).");
    }

    [TestMethod]
    public void ModoColapsado_Ancho60FilasCompactasYPersiste()
    {
        int anchoEsperado = SidebarMetrics.Escalar(60, _form!.DeviceDpi);
        InvocarAplicarEstadoBarraLateral(colapsada: true, persistir: true);
        var m = SidebarMetrics.Crear(_form.DeviceDpi);

        Assert.AreEqual(anchoEsperado, _form.panelLeft.Width,
            "El ancho colapsado sigue siendo el contrato de 60px lógicos escalados.");
        Assert.AreEqual(m.AltoFilaColapsada, _form.treeMenu.ItemHeight,
            "En modo colapsado la fila debe ser más compacta.");
        Assert.AreEqual("»", _form.btnToggleSidebar.Text, "En modo colapsado el botón invita a expandir.");
        Assert.IsTrue(_form.SidebarColapsado, "El estado colapsado debe reflejarse en el formulario.");
        Assert.IsTrue(LeerProyecto().BarraLateralColapsada,
            "El estado colapsado debe persistirse en la entidad del proyecto.");

        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: true);
        Assert.IsFalse(LeerProyecto().BarraLateralColapsada, "El modo expandido también persiste.");
    }

    [TestMethod]
    public void ClicEnSeccion_AlternaExpansionYPersisteEnNodosMenuExpandidos()
    {
        var tree = _form!.treeMenu;
        var raiz = tree.Nodes["nodeReportes"]!;
        raiz.Collapse();
        Assert.IsFalse(raiz.IsExpanded);

        InvocarToggleRaiz(raiz);
        Assert.IsTrue(raiz.IsExpanded, "Un clic sobre la sección debe expandirla (chevron propio).");

        InvocarToggleRaiz(raiz);
        Assert.IsFalse(raiz.IsExpanded, "Un segundo clic debe colapsarla.");

        // La persistencia formal ocurre al aplicar el modo de la barra: el
        // snapshot de nodos expandidos se guarda en la entidad.
        raiz.Expand();
        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: true);
        Assert.IsTrue(LeerProyecto().NodosMenuExpandidos.Split('|').Contains("nodeReportes"),
            "Los nodos expandidos deben persistir en la entidad (contrato existente).");
    }

    [TestMethod]
    public void ClicEnHoja_NoAlternaExpansion()
    {
        var tree = _form!.treeMenu;
        var raiz = tree.Nodes["nodePresupuesto"]!;
        raiz.Expand();
        var hoja = raiz.Nodes["nodeHojaPresupuesto"]!;

        InvocarToggleRaiz(hoja);

        Assert.IsTrue(raiz.IsExpanded, "El toggle de clic solo aplica a secciones (raíces).");
    }

    [TestMethod]
    public void PintadoPorFormulario_DistingueActivoConFocoDeActivoSinFoco()
    {
        var tree = _form!.treeMenu;
        var hoja = tree.Nodes["nodePresupuesto"]!.Nodes["nodeHojaPresupuesto"]!;
        tree.SelectedNode = hoja;

        using var sinFoco = RenderizarNodo(hoja, TreeNodeStates.Selected);
        using var conFoco = RenderizarNodo(hoja, TreeNodeStates.Selected | TreeNodeStates.Focused);

        Assert.IsTrue(AlgunaDiferencia(sinFoco, conFoco),
            "El pintado del formulario debe diferenciar el activo con foco del activo sin foco.");
    }

    [TestMethod]
    public void PintadoPorFormulario_HojaSeleccionada_UsaElTinteAzulDeLaPaleta()
    {
        var tree = _form!.treeMenu;
        var hoja = tree.Nodes["nodePresupuesto"]!.Nodes["nodeHojaPresupuesto"]!;
        tree.SelectedNode = hoja;

        using var bitmap = RenderizarNodo(hoja, TreeNodeStates.Selected);
        var m = SidebarMetrics.Crear(_form.DeviceDpi);
        var pildora = SidebarRenderer.RectPildora(new Rectangle(0, 0, tree.ClientSize.Width, tree.ItemHeight), m, 1, colapsado: false);
        var punto = new Point(pildora.Right - 5, pildora.Top + pildora.Height / 2);

        Assert.AreEqual(SidebarPaleta.PildoraActiva, bitmap.GetPixel(punto.X, punto.Y),
            "La selección del árbol debe pintar la píldora del tinte azul del tema.");
    }

    [TestMethod]
    public void ModoColapsado_EstadoVisualInmediatoAunqueNoPersista()
    {
        InvocarAplicarEstadoBarraLateral(colapsada: true, persistir: false);
        var m = SidebarMetrics.Crear(_form!.DeviceDpi);

        Assert.IsTrue(_form.SidebarColapsado,
            "El estado visual debe fijarse de inmediato: cualquier pintado durante el toggle ya ve el modo pedido.");
        Assert.IsFalse(LeerProyecto().BarraLateralColapsada,
            "Con persistir:false la entidad no debe tocarse (el estado visual no depende de ella).");
        Assert.AreEqual(m.AltoFilaColapsada, _form.treeMenu.ItemHeight,
            "El pintado inmediato tras el toggle ya debe usar la fila del modo nuevo.");

        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: false);
        Assert.IsFalse(_form.SidebarColapsado, "La vuelta a expandido también es inmediata.");
        Assert.AreEqual(m.AltoFila, _form.treeMenu.ItemHeight);
    }

    [TestMethod]
    public void GeometriaSidebar_LaRutinaUnicaAplicaTodasLasMetricasDeFormaCoherente()
    {
        var tree = _form!.treeMenu;
        var panel = LeerCampo<Panel>(_form, "panelSidebarHeader");

        foreach (bool colapsada in new[] { false, true, false })
        {
            InvocarAplicarGeometriaSidebar(colapsada);
            var m = SidebarMetrics.Crear(_form.DeviceDpi);
            var boton = _form.btnToggleSidebar;

            Assert.AreEqual(colapsada ? m.AltoFilaColapsada : m.AltoFila, tree.ItemHeight,
                $"Fila según modo (colapsada={colapsada}).");
            Assert.AreEqual(m.AltoHeader, panel.Height, $"Header según métricas (colapsada={colapsada}).");
            Assert.AreEqual(m.LadoBotonColapso, boton.Width, $"Botón pequeño (colapsada={colapsada}).");
            Assert.AreEqual(m.SangriaNivel, tree.Indent, $"Indent acorde a métricas (colapsada={colapsada}).");
            Assert.IsTrue(boton.Right <= panel.Width, $"Botón dentro del header (colapsada={colapsada}).");

            if (colapsada)
                Assert.AreEqual(SidebarMetrics.Escalar(60, _form.DeviceDpi), _form.panelLeft.Width,
                    "El ancho colapsado sale de la misma rutina.");
            else
                Assert.IsTrue(_form.panelLeft.Width >= SidebarMetrics.Escalar(250, _form.DeviceDpi)
                           && _form.panelLeft.Width <= SidebarMetrics.Escalar(420, _form.DeviceDpi),
                    "El ancho expandido queda entre el base y el máximo.");
        }
    }

    [TestMethod]
    public void BotonColapso_SuperficieDelTema_SinFondoNativoDelSistema()
    {
        var boton = _form!.btnToggleSidebar;

        Assert.IsFalse(boton.UseVisualStyleBackColor,
            "El fondo nativo del sistema rompería la superficie fría del lateral.");
        Assert.AreEqual(SidebarPaleta.Fondo, boton.BackColor);
    }

    [TestMethod]
    public void ClicEnSeccion_ConservaLaHojaSeleccionada()
    {
        var tree = _form!.treeMenu;
        var raiz = tree.Nodes["nodePresupuesto"]!;
        var hoja = raiz.Nodes["nodeHojaPresupuesto"]!;

        // Selección real: el evento AfterSelect registra la hoja.
        tree.SelectedNode = hoja;
        raiz.Collapse();

        InvocarToggleRaiz(raiz);

        Assert.IsTrue(raiz.IsExpanded, "La sección debe alternar expansión igualmente.");
        Assert.AreEqual(hoja, tree.SelectedNode,
            "El clic nativo sobre la raíz no debe borrar la píldora de la hoja activa.");
    }

    [TestMethod]
    public void ClicEnSeccion_SinHojaPreviaSeleccionada_NoFalla()
    {
        var tree = _form!.treeMenu;
        var raiz = tree.Nodes["nodeReportes"]!;
        tree.SelectedNode = null;

        InvocarToggleRaiz(raiz);

        Assert.IsTrue(raiz.IsExpanded, "La sección debe alternar expansión igualmente.");
        Assert.IsNull(tree.SelectedNode,
            "Sin hoja previa seleccionada, el toggle no inventa una selección (en pantalla la raíz queda seleccionada de forma nativa).");
    }

    // ═══════════════════════ Infraestructura ═══════════════════════

    private Bitmap RenderizarNodo(TreeNode nodo, TreeNodeStates estado)
    {
        var tree = _form!.treeMenu;
        // Ancho ampliado para que el punto de muestreo (interior derecho de la
        // píldora) quede siempre más allá del texto, en cualquier DPI.
        tree.Width = 600;

        var bitmap = new Bitmap(tree.ClientSize.Width, tree.ItemHeight);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var argumentos = new DrawTreeNodeEventArgs(
                graphics, nodo, new Rectangle(0, 0, bitmap.Width, tree.ItemHeight), estado);
            ResolverMetodo("treeMenu_DrawNode", typeof(object), typeof(DrawTreeNodeEventArgs))
                .Invoke(_form, new object?[] { tree, argumentos });
        }
        return bitmap;
    }

    private void InvocarAplicarEstadoBarraLateral(bool colapsada, bool persistir)
        => ResolverMetodo("AplicarEstadoBarraLateral", typeof(bool), typeof(bool))
            .Invoke(_form, new object?[] { colapsada, persistir });

    private void InvocarAplicarGeometriaSidebar(bool colapsada)
        => ResolverMetodo("AplicarGeometriaSidebar", typeof(bool))
            .Invoke(_form, new object?[] { colapsada });

    private void InvocarToggleRaiz(TreeNode nodo)
        => ResolverMetodo("treeMenu_NodeMouseClick_ToggleRaiz", typeof(object), typeof(TreeNodeMouseClickEventArgs))
            .Invoke(_form, new object?[]
            {
                nodo.TreeView ?? _form!.treeMenu,
                new TreeNodeMouseClickEventArgs(nodo, MouseButtons.Left, 1, 10, 10)
            });

    private Proyecto LeerProyecto()
        => (Proyecto)(typeof(FormProyecto).GetField("_proyecto", PrivadoDeInstancia)?.GetValue(_form)
            ?? throw new InvalidOperationException("No se encontró el campo _proyecto."));

    private static Proyecto CrearProyecto() => new()
    {
        Nombre = "Proyecto sidebar",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4,
        FechaCreacion = new DateTime(2026, 1, 1),
        FechaModificacion = new DateTime(2026, 1, 1),
    };

    private static bool AlgunaDiferencia(Bitmap a, Bitmap b)
    {
        if (a.Size != b.Size) return true;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    return true;
        return false;
    }

    private static MethodInfo ResolverMetodo(string nombre, params Type[] tipos)
    {
        var metodo = typeof(FormProyecto).GetMethod(nombre, PrivadoDeInstancia, null, tipos, null);
        Assert.IsNotNull(metodo, $"No se encontró FormProyecto.{nombre}.");
        return metodo;
    }

    private static T LeerCampo<T>(FormProyecto form, string nombre)
    {
        var campo = typeof(FormProyecto).GetField(nombre, PrivadoDeInstancia)
            ?? typeof(FormProyecto).GetField(nombre, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(campo, $"No se encontró el campo {nombre}.");
        return (T)(campo.GetValue(form) ?? throw new InvalidOperationException($"El campo {nombre} es nulo."));
    }

    private static bool LeerDoubleBuffered(TreeView tree)
        => (bool)(typeof(Control)
            .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(tree, null)
            ?? throw new InvalidOperationException("No se pudo leer DoubleBuffered."));
}
