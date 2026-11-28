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
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Indirectos;

/// <summary>
/// Tema de Indirectos sobre el FORMULARIO VIVO (issue #205): construye
/// FormIndirectos real contra un proyecto SQLite temporal con cada preset
/// activo y verifica el chrome de rejilla DESPUÉS del helper genérico
/// (AplicarEstiloSOPRO pisa fondo/líneas/encabezados) y el color capturado por
/// ChromeFlotanteHost (debe construirse con el BackColor ya tematizado).
///
/// La paleta es GLOBAL al proceso: la clase está marcada [DoNotParallelize] y
/// cada test restaura en el Dispose el preset previo y el proyecto global de
/// FormatoHelper (mismo patrón que ChromeFlotantePresupuestoTests). Los IDs de
/// preset van como cadena y se resuelven con TryParsePreset porque el enum es
/// internal y DataRow exige tipos accesibles.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class FormIndirectosTemaFormularioTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Las pruebas que crean FormIndirectos deben ejecutarse en STA.");
    }

    [DataTestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void Formulario_ConCadaPreset_GridChromeSobreviveAlHelperGenerico(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out var preset));
        using EscenarioIndirectos escenario = new(preset);
        FormIndirectos form = escenario.Form;
        var paleta = SoproTheme.GetPalette(preset);

        foreach (var nombre in new[] { "dgvOficinaCentral", "dgvCampo" })
        {
            var dgv = (DataGridView)escenario.Buscar(nombre);

            // El helper genérico fija blanco/#E6E6E6: si estos asserts fallan,
            // el repasado del tema quedó antes del helper en el constructor.
            Assert.AreEqual(paleta.Surface, dgv.BackgroundColor, $"{preset}: fondo de {nombre}");
            Assert.AreEqual(paleta.GridLine, dgv.GridColor, $"{preset}: líneas de {nombre}");

            var encabezado = dgv.ColumnHeadersDefaultCellStyle;
            Assert.AreEqual(paleta.HeaderBg, encabezado.BackColor, $"{preset}: fondo de encabezado de {nombre}");
            Assert.AreEqual(paleta.Ink, encabezado.ForeColor, $"{preset}: texto de encabezado de {nombre}");
        }
    }

    [DataTestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void Formulario_ConCadaPreset_HostFlotanteCapturaElFondoTematizado(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out var preset));
        using EscenarioIndirectos escenario = new(preset);
        FormIndirectos form = escenario.Form;
        var paleta = SoproTheme.GetPalette(preset);

        Assert.AreEqual(paleta.CenterBg, form.BackColor, $"{preset}: fondo del formulario");
        Assert.AreEqual(paleta.CenterBg, escenario.HostHeader.BackColor,
            $"{preset}: ChromeFlotanteHost del header captura el BackColor tematizado");
        Assert.AreEqual(paleta.CenterBg, escenario.HostToolbar.BackColor,
            $"{preset}: ChromeFlotanteHost de la toolbar captura el BackColor tematizado");

        Assert.AreEqual(paleta.HeaderDark, escenario.Buscar("panelTop").BackColor, $"{preset}: banda de header");

        // El color del título lo gestiona EditableReportTitleHelper con el
        // color configurado por el usuario (default #FFFFFF): es DATO del
        // usuario, no un rol del tema. Se aserta legibilidad sobre el header.
        Color tituloReal = escenario.Buscar("lblTitulo").ForeColor;
        Assert.IsTrue(Contraste(tituloReal, paleta.HeaderDark) >= 4.5,
            $"{preset}: título (configurado por el usuario) legible sobre HeaderDark, {Contraste(tituloReal, paleta.HeaderDark):F2}:1");
    }

    [DataTestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void Formulario_ConCadaPreset_TabsConBackColorTematizadoSinVisualStyles(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out var preset));
        using EscenarioIndirectos escenario = new(preset);
        FormIndirectos form = escenario.Form;
        var paleta = SoproTheme.GetPalette(preset);

        foreach (var nombre in new[] { "tabOficinaCentral", "tabCampo", "tabConfiguracion" })
        {
            var page = (TabPage)escenario.Buscar(nombre);
            Assert.IsFalse(page.UseVisualStyleBackColor, $"{preset}: UseVisualStyleBackColor debe ser false con BackColor tematizado");
            Assert.AreEqual(paleta.Surface, page.BackColor, $"{preset}: cuerpo de {nombre}");
        }

        // La BANDA de pestañas queda NATIVA por decisión de diseño (#205): el
        // TabControl no expone pintado de la franja sin owner-draw (descartado
        // por fragilidad en revisión). No se aserta el color de la franja.
        var tabControl = (TabControl)escenario.Buscar("tabControl");
        Assert.AreEqual(TabDrawMode.Normal, tabControl.DrawMode,
            "Sin owner-draw: el strip y el cue de foco los pinta el nativo.");
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static double Contraste(Color a, Color b)
    {
        double Luminancia(Color color)
        {
            static double Canal(int valor)
            {
                double s = valor / 255d;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Canal(color.R) + 0.7152 * Canal(color.G) + 0.0722 * Canal(color.B);
        }

        double la = Luminancia(a);
        double lb = Luminancia(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // =====================================================================
    // Escenario compartido (patrón ChromeFlotantePresupuestoTests)
    // =====================================================================

    private sealed class EscenarioIndirectos : IDisposable
    {
        private readonly string _root;
        private readonly SoproTemaPreset _presetAnterior;
        private readonly Proyecto? _proyectoAnterior;
        private readonly SOPROContext _ctx;

        public EscenarioIndirectos(SoproTemaPreset preset)
        {
            _root = Path.Combine(Path.GetTempPath(), $"sopro_indirectos_tema_{preset}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);

            _presetAnterior = SoproTheme.CurrentPalette.Preset;
            _proyectoAnterior = LeerProyectoGlobalFormato();
            SoproTheme.InitializePalette(preset);

            _ctx = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _ctx.Database.EnsureCreated();
            Proyecto proyecto = CrearProyecto(_ctx);
            Form = new FormIndirectos(_ctx, proyecto);
            Form.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public FormIndirectos Form { get; }

        public Control HostHeader => Buscar("panelTopHost");
        public Control HostToolbar => Buscar("panelToolbarHost");

        public Control Buscar(string nombre)
            => Form.Controls.Find(nombre, true).FirstOrDefault()
               ?? throw new AssertFailedException($"No se encontró el control '{nombre}'.");

        public void Dispose()
        {
            Form.Dispose();
            _ctx.Dispose();
            SoproTheme.InitializePalette(_presetAnterior);
            typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato, BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, _proyectoAnterior);

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
                    Thread.Sleep(200);
                }
            }
        }

        private static Proyecto? LeerProyectoGlobalFormato()
            => (Proyecto?)typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
                   BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

        private static Proyecto CrearProyecto(SOPROContext ctx)
        {
            Proyecto proyecto = new Proyecto
            {
                Nombre = "Proyecto tema indirectos",
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
