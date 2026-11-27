using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Sidebar;

/// <summary>
/// Contrato de alineación del encabezado (issue #195):
/// - en expandido el arranque del ribbon cae exactamente en el borde derecho
///   real de la barra lateral (panelRibbon.Left == panelLeft.Width), sin
///   constantes hardcodeadas;
/// - el wordmark SOPRO queda centrado en el bloque de marca (entre x=0 y el
///   separador) y centrado en el alto de la banda, con color de marca;
/// - en colapsado el bloque conserva un ancho mínimo que siempre contiene al
///   wordmark con aire propio, aunque el rail mida 60px lógicos;
/// - el mecanismo de overflow del ribbon sigue usable tras la alineación.
/// </summary>
[STATestClass]
[DoNotParallelize] // Crea el formulario real del shell (controles WinForms y SQLite temporal).
public class AlineacionMarcaRibbonTests
{
    private const BindingFlags PrivadoDeInstancia = BindingFlags.Instance | BindingFlags.NonPublic;

    private string _root = string.Empty;
    private SOPROContext? _context;
    private FormProyecto? _form;

    [TestInitialize]
    public void Preparar()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_marca_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _context = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
        _context.Database.EnsureCreated();
        _form = new FormProyecto(_context, CrearProyecto());
        _ = _form.Handle;
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
    public void Expandido_RibbonAlineadoExactoConBordeDerechoDelSidebar()
    {
        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: false);

        var ribbon = Ribbon();
        Assert.AreEqual(_form!.panelLeft.Width, ribbon.Left,
            "En expandido el arranque del ribbon debe coincidir exactamente con panelLeft.Right.");
        Assert.IsTrue(ribbon.Width > 0, "El ribbon debe conservar ancho útil.");
        Assert.IsTrue(ribbon.Right <= _form.panelTop.Width,
            "El ribbon no debe salirse de la banda superior.");

        // El borde derecho del ribbon es invariante al alternar modos: solo
        // se mueve el separador izquierdo.
        int bordeDerechoExpandido = ribbon.Right;
        InvocarAplicarEstadoBarraLateral(colapsada: true, persistir: false);
        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: false);
        Assert.AreEqual(bordeDerechoExpandido, Ribbon().Right,
            "El borde derecho del ribbon no debe moverse al alternar el sidebar.");
    }

    [TestMethod]
    public void Titulo_CentradoEnBloqueMarcaColorBrandYCentradoVertical()
    {
        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: false);

        var titulo = _form!.lblTitulo;
        var ribbon = Ribbon();
        int centroBloque = ribbon.Left / 2;
        int centroTexto = titulo.Left + titulo.PreferredWidth / 2;

        Assert.IsTrue(Math.Abs(centroTexto - centroBloque) <= 2,
            $"El wordmark debe quedar centrado en el bloque de marca (centro texto {centroTexto} vs bloque {centroBloque}).");
        Assert.IsTrue(Math.Abs((titulo.Top * 2 + titulo.PreferredHeight) - _form.panelTop.Height) <= 2,
            "El wordmark debe quedar centrado en el alto de la banda superior.");
        Assert.AreEqual(SoproTheme.Colors.Brand, titulo.ForeColor,
            "El wordmark debe usar el color de marca sobrio del tema.");
        Assert.IsTrue(titulo.Right <= ribbon.Left,
            "El wordmark no debe invadir el área del ribbon.");
    }

    [TestMethod]
    public void Colapsado_BloqueDeMarcaConservaAnchoMinimoQueContieneElWordmark()
    {
        InvocarAplicarEstadoBarraLateral(colapsada: true, persistir: false);

        var titulo = _form!.lblTitulo;
        var ribbon = Ribbon();
        int margen = (int)Math.Ceiling(14 * _form.DeviceDpi / 96d);

        Assert.IsTrue(ribbon.Left > _form.panelLeft.Width,
            "Con el rail colapsado (60px lógicos) el bloque debe conservar el ancho mínimo de marca.");
        Assert.IsTrue(titulo.PreferredWidth + 2 * margen <= ribbon.Left,
            "El bloque colapsado debe contener el wordmark con aire propio a ambos lados.");
        Assert.IsTrue(titulo.Left >= margen && titulo.Right <= ribbon.Left,
            "El wordmark debe seguir centrado y visible dentro del bloque.");
        Assert.IsTrue(ribbon.Width > 0, "El ribbon debe conservar ancho útil en colapsado.");
    }

    [TestMethod]
    public void OverflowRibbon_SigueUtilTrasAlineacionEnVentanaEstrecha()
    {
        InvocarAplicarEstadoBarraLateral(colapsada: false, persistir: false);

        // Ventana angosta: el ancho del ribbon cae por las anclas L|R y el
        // mecanismo de overflow debe reaccionar sin perder el botón "Más".
        _form!.Width = 640;
        _form.PerformLayout();

        var btnMas = Mas();
        var ribbon = Ribbon();
        Assert.AreEqual(ribbon, btnMas.Parent, "El botón de overflow debe vivir dentro del ribbon.");
        Assert.IsTrue(btnMas.Left >= ribbon.Left && btnMas.Right <= ribbon.Right,
            "El botón de overflow debe quedar dentro del ribbon tras el reajuste.");
        Assert.IsTrue(btnMas.Top >= 0 && btnMas.Bottom <= ribbon.Bottom,
            "El botón de overflow debe quedar dentro del alto del ribbon.");
        Assert.IsTrue(_form.lblTitulo.Right <= ribbon.Left,
            "El bloque de marca no debe ser invadido por los comandos del ribbon.");
    }

    private Panel Ribbon()
        => LeerCampo<Panel>(_form!, "panelRibbon");

    private Button Mas()
        => LeerCampo<Button>(_form!, "btnRibbonMas");

    private static T LeerCampo<T>(FormProyecto form, string nombre)
    {
        var campo = typeof(FormProyecto).GetField(nombre, PrivadoDeInstancia)
            ?? typeof(FormProyecto).GetField(nombre, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(campo, $"No se encontró el campo {nombre}.");
        return (T)(campo.GetValue(form) ?? throw new InvalidOperationException($"El campo {nombre} es nulo."));
    }

    private void InvocarAplicarEstadoBarraLateral(bool colapsada, bool persistir)
    {
        var metodo = typeof(FormProyecto).GetMethod("AplicarEstadoBarraLateral", PrivadoDeInstancia, null,
            new[] { typeof(bool), typeof(bool) }, null)
            ?? throw new InvalidOperationException("No se encontró FormProyecto.AplicarEstadoBarraLateral.");
        metodo.Invoke(_form, new object?[] { colapsada, persistir });
    }

    private static Proyecto CrearProyecto() => new()
    {
        Nombre = "Proyecto marca",
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
}
