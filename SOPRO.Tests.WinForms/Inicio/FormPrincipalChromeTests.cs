using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Inicio;

/// <summary>
/// Contrato visual del selector de proyectos (FormPrincipal, issue #201):
/// banda de encabezado CLARA unificada con FormProyecto (#180/#196) —
/// superficie blanca al ras de la ventana con hairline inferior, marca
/// SOPRO en azul de identidad, una sola acción primaria de marca y el resto
/// de botones como secundarios de superficie clara — y tarjeta de recientes
/// limpia (sin el marco nativo del GroupBox) con rótulo de sección centrado,
/// pie con la acción de registros locales dentro de la tarjeta y la rejilla
/// debajo. Sin tocar lógica ni manejadores.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class FormPrincipalChromeTests
{
    [TestMethod]
    public void Header_BandaClaraUnificada_SinRecortesYConMarcaAzul()
    {
        using var escenario = new EscenarioInicio();
        Panel panelTop = escenario.PanelTop;

        // Banda plana: sin Región de esquinas redondeadas (el recorte era del
        // header oscuro anterior).
        Assert.IsNull(panelTop.Region, "La banda clara no debe recortarse con Región.");
        Assert.AreEqual(SoproTheme.Colors.Surface, panelTop.BackColor);

        // Marca destacada en azul de identidad, mismo registro que FormProyecto.
        Assert.AreEqual(SoproTheme.Colors.Brand, escenario.Titulo.ForeColor);
        Assert.AreEqual(SoproTheme.Fonts.Title, escenario.Titulo.Font);

        // Subtítulo apagado pero legible sobre la superficie clara.
        Assert.AreEqual(SoproTheme.Colors.Muted, escenario.Subtitulo.ForeColor);
        Assert.AreEqual(SoproTheme.Fonts.Subtitle, escenario.Subtitulo.Font);
    }

    [TestMethod]
    public void Header_JerarquiaDeBotones_UnaPrimariaDeMarcaYSecundariosDeSuperficie()
    {
        using var escenario = new EscenarioInicio();

        var btnNuevo = escenario.Boton("btnNuevoProyecto");
        var btnAbrir = escenario.Boton("btnAbrirProyecto");
        var btnApoyar = escenario.Boton("btnApoyarKoFi");

        // Única acción primaria: acento de marca con texto claro.
        Assert.AreEqual(SoproTheme.Colors.RibbonActive, btnNuevo.BackColor);
        Assert.AreEqual(SoproTheme.Colors.Surface, btnNuevo.ForeColor);

        // Secundarios: superficie clara con hairline, texto en tinta.
        foreach (var boton in new[] { btnAbrir, btnApoyar })
        {
            Assert.AreEqual(SoproTheme.Colors.Surface, boton.BackColor, boton.Name);
            Assert.AreEqual(SoproTheme.Colors.Ink2, boton.ForeColor, boton.Name);
            Assert.AreEqual(SoproTheme.Colors.SurfaceBorder, boton.FlatAppearance.BorderColor, boton.Name);
            Assert.AreEqual(SoproTheme.Colors.HoverBg, boton.FlatAppearance.MouseOverBackColor, boton.Name);
            Assert.AreEqual(SoproTheme.Colors.SelBg, boton.FlatAppearance.MouseDownBackColor, boton.Name);
        }
    }

    [TestMethod]
    public void Header_VentanaEstrecha_BotonesSinSolapeNiRecorte()
    {
        using var escenario = new EscenarioInicio();
        var form = escenario.Form;
        var clienteOriginal = form.ClientSize;

        try
        {
            foreach (int ancho in new[] { 750, 480 })
            {
                // El mínimo real de la ventana es 750; por debajo se relaja
                // solo en la prueba para ejercitar el fallback de filas.
                form.MinimumSize = Size.Empty;
                form.ClientSize = new Size(ancho, clienteOriginal.Height);
                System.Windows.Forms.Application.DoEvents();

                Panel panelTop = escenario.PanelTop;
                var botones = new[]
                {
                    escenario.Boton("btnNuevoProyecto"),
                    escenario.Boton("btnAbrirProyecto"),
                    escenario.Boton("btnApoyarKoFi")
                };

                foreach (var boton in botones)
                {
                    Assert.IsTrue(boton.Left >= 0 && boton.Right <= panelTop.Width,
                        $"'{boton.Name}' debe caber en el ancho del header (ancho={panelTop.Width}).");
                    Assert.IsTrue(boton.Top >= 0 && boton.Bottom <= panelTop.Height,
                        $"'{boton.Name}' debe caber en el alto del header ({panelTop.Height}).");
                }

                // Ningún par de botones se solapa (fila única o columnas apiladas).
                for (int i = 0; i < botones.Length; i++)
                for (int j = i + 1; j < botones.Length; j++)
                    Assert.IsFalse(botones[i].Bounds.IntersectsWith(botones[j].Bounds),
                        $"'{botones[i].Name}' y '{botones[j].Name}' no deben solaparse con ancho {ancho}.");

                // Los botones no deben tapar el título ni el subtítulo.
                foreach (var rotulo in new[] { escenario.Titulo, escenario.Subtitulo })
                    foreach (var boton in botones)
                        Assert.IsFalse(rotulo.Bounds.IntersectsWith(boton.Bounds),
                            $"'{boton.Name}' no debe solapar '{rotulo.Name}' con ancho {ancho}.");
            }
        }
        finally
        {
            form.MinimumSize = new Size(750, 420);
            form.ClientSize = clienteOriginal;
        }
    }

    [TestMethod]
    public void TarjetaRecientes_TarjetaLimpiaConRotuloYRejillaIntacta()
    {
        using var escenario = new EscenarioInicio();
        DataGridView dgv = escenario.GridRecientes;
        Panel card = escenario.TarjetaRecientes;
        Panel panelCenter = escenario.PanelCenter;

        // Superficie de tarjeta plana: sin Región, borde pintado por el tema.
        Assert.IsNull(card.Region, "La tarjeta no debe redondearse con Región.");
        Assert.AreEqual(SoproTheme.Colors.Surface, card.BackColor);

        // Agrupación accesible equivalente al GroupBox original.
        Assert.AreEqual(AccessibleRole.Grouping, card.AccessibleRole);
        Assert.AreEqual("PROYECTOS RECIENTES", card.AccessibleName);

        // Rótulo de sección de la tarjeta (creado por el diseñador, estilado
        // por el tema): estirado en vertical dentro de su franja para que
        // TextAlign lo centre, no un Dock.Left anclado arriba.
        var captionStrip = card.Controls["panelRecientesCaption"] as Panel;
        Assert.IsNotNull(captionStrip, "La tarjeta debe tener la franja de rótulo.");
        var caption = captionStrip!.Controls["lblRecientesCaption"] as Label;
        Assert.IsNotNull(caption, "La franja debe contener el rótulo de sección.");
        Assert.AreEqual("PROYECTOS RECIENTES", caption!.Text);
        Assert.AreEqual(SoproTheme.Colors.Muted, caption.ForeColor);
        Assert.AreEqual(SoproTheme.Fonts.SidebarSeccion, caption.Font);
        Assert.AreEqual(DockStyle.Fill, caption.Dock, "El rótulo debe estirarse (Dock.Fill) para centrarse en vertical.");
        Assert.AreEqual(captionStrip.ClientSize.Height, caption.Height,
            "El rótulo debe ocupar el alto de la franja; TextAlign lo centra.");
        Assert.AreEqual(SoproTheme.EscalarSegunDpi(card, SoproUiMetrics.CardCaptionHeight), captionStrip.Height,
            "La franja de rótulo debe seguir la convención de escalado por DPI del tema.");

        // La rejilla queda DEBAJO del rótulo y acoplada al resto de la tarjeta.
        Assert.IsTrue(dgv.Dock == DockStyle.Fill, "El grid sigue acoplado a la tarjeta.");
        Assert.IsTrue(dgv.Top >= captionStrip.Bottom,
            $"El grid (Top={dgv.Top}) debe quedar debajo de la franja de rótulo (Bottom={captionStrip.Bottom}).");

        // Geometría/behavior del grid intactos.
        Assert.IsFalse(dgv.AllowUserToAddRows);
        Assert.IsFalse(dgv.AllowUserToDeleteRows);
        Assert.IsTrue(dgv.ReadOnly);
        Assert.AreEqual(DataGridViewSelectionMode.FullRowSelect, dgv.SelectionMode);
        Assert.IsFalse(dgv.MultiSelect);
        Assert.AreEqual(BorderStyle.None, dgv.BorderStyle);
        Assert.IsFalse(dgv.RowHeadersVisible);

        // La tarjeta no participa en recortes del contenedor.
        Assert.IsNull(panelCenter.Region);
    }

    [TestMethod]
    public void TarjetaRecientes_PieConRegistrosLocales_DentroDeLaTarjeta()
    {
        using var escenario = new EscenarioInicio();

        // La acción de registros locales se compone como pie de la tarjeta
        // (alineada a la derecha), no flota bajo la tarjeta.
        var btnRegistros = escenario.BuscarControl("btnRegistrosLocales");
        Assert.IsNotNull(btnRegistros, "El botón de registros locales debe existir.");
        Assert.AreEqual("panelRecientesFooter", btnRegistros!.Parent?.Name);
        Assert.AreEqual(escenario.TarjetaRecientes, btnRegistros.Parent?.Parent);
        Assert.AreEqual(DockStyle.Right, btnRegistros.Dock);
        Assert.AreEqual("Revisar registros locales", btnRegistros.AccessibleName);

        var footer = escenario.TarjetaRecientes.Controls["panelRecientesFooter"] as Panel;
        Assert.IsNotNull(footer, "La tarjeta debe tener franja de pie.");

        // La rejilla termina donde empieza el pie: sin solape ni hueco.
        var dgv2 = escenario.GridRecientes;
        Assert.IsTrue(dgv2.Bottom <= footer!.Top,
            $"El grid (Bottom={dgv2.Bottom}) debe terminar antes del pie (Top={footer.Top}).");
        Assert.IsFalse(dgv2.Bounds.IntersectsWith(footer.Bounds),
            "El grid y el pie no deben solaparse.");
        Assert.IsTrue(dgv2.Bottom > 0 && footer.Bottom <= escenario.TarjetaRecientes.ClientSize.Height,
            "El pie debe quedar dentro de la tarjeta.");
    }

    [TestMethod]
    public void MetricasDpi_LaConvencionDeEscalamientoEsConsistente()
    {
        // Contrato puro de la convención del tema: px lógicos (96 DPI) escalados
        // al DPI dado con redondeo a la unidad; DPI inválido se trata como 96.
        // Cubre la aritmética sin necesitar simulación de monitor.
        Assert.AreEqual(48, SoproTheme.EscalarSegunDpi(96, 48), "A 96 DPI la métrica es identidad.");
        Assert.AreEqual(60, SoproTheme.EscalarSegunDpi(120, 48), "A 120 DPI escala 1.25x.");
        Assert.AreEqual(72, SoproTheme.EscalarSegunDpi(144, 48), "A 144 DPI escala 1.5x.");
        Assert.AreEqual(32, SoproTheme.EscalarSegunDpi(96, 32));
        Assert.AreEqual(132, SoproTheme.EscalarSegunDpi(144, 88));
        Assert.AreEqual(48, SoproTheme.EscalarSegunDpi(0, 48), "DPI inválido se trata como 96.");

        // El control delega en su DeviceDpi real (o 96 si no tiene DPI aún).
        using var control = new Control();
        int dpi = control.DeviceDpi > 0 ? control.DeviceDpi : 96;
        Assert.AreEqual((int)Math.Round(44 * dpi / 96f), SoproTheme.EscalarSegunDpi(control, 44));
        Assert.AreEqual(44, SoproTheme.EscalarSegunDpi(null, 44));
    }

    [TestMethod]
    public void HeaderYPie_MetricasEscaladasPorDpi()
    {
        using var escenario = new EscenarioInicio();

        // Los TRES botones del header comparten la misma altura fija escalada
        // (convención del tema: px lógicos 96 DPI → DPI real del control), y
        // SoproAutoSizeToContent la refleja en su alto actual.
        foreach (var nombre in new[] { "btnNuevoProyecto", "btnAbrirProyecto", "btnApoyarKoFi" })
        {
            var boton = escenario.Boton(nombre);
            Assert.AreEqual(SoproTheme.EscalarSegunDpi(boton, 34), boton.SoproFixedHeight,
                $"SoproFixedHeight de '{nombre}' debe seguir la convención de escalado.");
            Assert.AreEqual(boton.SoproFixedHeight, boton.Height,
                $"El alto actual de '{nombre}' debe igualar su alto fijo escalado.");
        }

        // Pie de la tarjeta: alto y padding escalados; el botón mide su ancho
        // escalado y su ALTO lo determina el área cliente del pie (Dock.Right).
        var form = escenario.Form;
        var footer = (Panel)escenario.TarjetaRecientes.Controls["panelRecientesFooter"]!;
        Assert.AreEqual(SoproTheme.EscalarSegunDpi(form, 48), footer.Height);
        Assert.AreEqual(SoproTheme.EscalarSegunDpi(form, 6), footer.Padding.Top);
        Assert.AreEqual(SoproTheme.EscalarSegunDpi(form, 10), footer.Padding.Right);
        Assert.AreEqual(SoproTheme.EscalarSegunDpi(form, 6), footer.Padding.Bottom);

        var btnRegistros = escenario.BuscarControl("btnRegistrosLocales")!;
        Assert.AreEqual(SoproTheme.EscalarSegunDpi(form, 180), btnRegistros.Width);
        Assert.AreEqual(footer.Height - footer.Padding.Top - footer.Padding.Bottom, btnRegistros.Height,
            "El alto del botón del pie lo determina el área cliente del pie (Dock.Right), no un Size fijo.");
    }

    [TestMethod]
    public void AccionesYEstadoPreservados_RegistrosLocalesVersionYColumnas()
    {
        using var escenario = new EscenarioInicio();

        // Acción de registros locales intacta (creada en el ctor, dentro de la
        // tarjeta de recientes con su nombre accesible).
        var btnRegistros = escenario.BuscarControl("btnRegistrosLocales");
        Assert.IsNotNull(btnRegistros, "El botón de registros locales debe existir.");
        Assert.AreEqual("Revisar registros locales", btnRegistros!.AccessibleName);

        // Versión mostrada en la barra de estado.
        StringAssert.StartsWith(escenario.Form.lblStatus.Text, "Versión actual:");

        // Columnas del grid de recientes creadas por LoadRecentProjects.
        var dgv = escenario.GridRecientes;
        foreach (var nombre in new[] { "colNombre", "colFecha", "colRuta", "colDuplicar", "colEliminar" })
            Assert.IsNotNull(dgv.Columns[nombre], $"Falta la columna '{nombre}'.");

        // Los tres botones de acción del header siguen siendo SoproButton.
        Assert.AreEqual(typeof(SoproButton), escenario.Boton("btnNuevoProyecto").GetType());
        Assert.AreEqual(typeof(SoproButton), escenario.Boton("btnAbrirProyecto").GetType());
        Assert.AreEqual(typeof(SoproButton), escenario.Boton("btnApoyarKoFi").GetType());
    }

    private sealed class EscenarioInicio : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"sopro_inicio_chrome_{Guid.NewGuid():N}");
        private readonly FormPrincipal _form;

        public EscenarioInicio()
        {
            Directory.CreateDirectory(_root);
            var workspace = new ProjectWorkspaceService(
                Path.Combine(_root, "sopro"), Path.Combine(_root, "local"));
            _form = new FormPrincipal(
                workspace,
                new ProjectLifecycleService(workspace),
                initialProjectPath: null)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            _form.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public FormPrincipal Form => _form;

        public Panel PanelTop => _form.panelTop;

        public Panel PanelCenter => _form.panelCenter;

        public Panel TarjetaRecientes => _form.grpRecientes;

        public DataGridView GridRecientes => _form.dgvRecientes;

        public Label Titulo => _form.lblTitle;

        public Label Subtitulo => _form.lblSubtitle;

        public SoproButton Boton(string nombre)
        {
            var boton = BuscarControl(nombre) as SoproButton;
            Assert.IsNotNull(boton, $"Debe existir el botón '{nombre}'.");
            return boton!;
        }

        public Control? BuscarControl(string nombre)
        {
            var pendiente = new Queue<Control>();
            pendiente.Enqueue(_form);
            while (pendiente.Count > 0)
            {
                var actual = pendiente.Dequeue();
                if (actual.Name == nombre) return actual;
                foreach (Control hijo in actual.Controls)
                    pendiente.Enqueue(hijo);
            }

            return null;
        }

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    _form.Dispose();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    Directory.Delete(_root, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
