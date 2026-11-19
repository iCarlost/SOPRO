using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Composición del aviso de privacidad de diagnósticos: cromo SOPRO,
/// jerarquía de la copia y semántica de las banderas Reconocido/Revisar.
/// No se muestra el diálogo de forma modal: se instancia y se dispara el clic
/// por reflexión sobre <see cref="Control.OnClick"/>. Las verificaciones de
/// layout usan el layout REAL del formulario tras PerformLayout.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class FormAvisoPrivacidadDiagnosticosTests
{
    [TestMethod]
    public void Dialogo_UsaElCromoSopro()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        Assert.AreEqual("Registros locales", aviso.Text);
        Assert.AreEqual(FormBorderStyle.FixedDialog, aviso.FormBorderStyle);
        Assert.AreEqual(AutoScaleMode.Dpi, aviso.AutoScaleMode);
        Assert.IsFalse(aviso.ShowInTaskbar);
        Assert.IsFalse(aviso.MaximizeBox);
        Assert.IsFalse(aviso.MinimizeBox);
        Assert.AreEqual(SoproTheme.Colors.CenterBg, aviso.BackColor);
    }

    [TestMethod]
    public void Dialogo_TieneEntendidoPrimarioYRevisarSecundario()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        var entendido = ObtenerBoton(aviso, SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoBotonEntendido);
        var revisar = ObtenerBoton(aviso, SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoBotonRevisar);

        Assert.IsInstanceOfType(entendido, typeof(SoproButton));
        Assert.IsInstanceOfType(revisar, typeof(SoproButton));
        Assert.AreEqual(SoproTheme.Colors.RibbonActive, entendido.BackColor, "Entendido es la acción primaria.");
        Assert.AreEqual(SoproTheme.Colors.Surface, revisar.BackColor, "Revisar registros es neutral secundario.");
        Assert.AreEqual(SoproButtonLayout.Small, ((SoproButton)revisar).SoproLayout, "Icono y texto centrados como un bloque.");
        Assert.AreEqual(2, ObtenerBotones(aviso).Count(), "El aviso solo ofrece Entendido y Revisar registros.");
    }

    [TestMethod]
    public void Dialogo_ResuelveEnterYEscAtravesDeEntendido()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        var entendido = ObtenerBoton(aviso, SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoBotonEntendido);

        Assert.AreSame(entendido, aviso.AcceptButton, "Enter debe reconocer el aviso.");
        Assert.AreSame(entendido, aviso.CancelButton, "Esc también debe cerrar a través de Entendido.");
        Assert.AreEqual(DialogResult.OK, entendido.DialogResult, "Entendido debe cerrar el diálogo igual que antes del cambio visual.");
    }

    [TestMethod]
    public void Entendido_ReconoceSinPedirRevisar()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        DispararClic(ObtenerBoton(aviso, SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoBotonEntendido));

        Assert.IsTrue(aviso.Reconocido);
        Assert.IsFalse(aviso.Revisar);
        Assert.AreEqual(DialogResult.OK, aviso.DialogResult);
    }

    [TestMethod]
    public void Revisar_Registros_PideRevisarYReconoce()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        DispararClic(ObtenerBoton(aviso, SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoBotonRevisar));

        Assert.IsTrue(aviso.Reconocido);
        Assert.IsTrue(aviso.Revisar);
    }

    [TestMethod]
    public void Dialogo_PresentaPrimeroElReconocimientoYDespuesElDetalleTecnico()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        var cuerpo = aviso.Controls.OfType<Panel>().Single(panel => panel.Dock == DockStyle.Fill);
        var etiquetas = cuerpo.Controls.OfType<Label>().ToList();

        Assert.AreEqual(2, etiquetas.Count);
        var reconocimiento = etiquetas.Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoReconocimiento);
        var detalle = etiquetas.Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoDetalle);

        Assert.IsTrue(reconocimiento.Top < detalle.Top, "El reconocimiento de privacidad debe leerse antes del detalle técnico.");
        Assert.AreEqual(SoproTheme.Colors.Ink, reconocimiento.ForeColor);
        Assert.AreEqual(SoproTheme.Colors.Muted, detalle.ForeColor);
        Assert.IsFalse(string.IsNullOrEmpty(reconocimiento.AccessibleName));
        Assert.AreEqual("Información sobre registros locales", detalle.AccessibleName);
    }

    [TestMethod]
    public void Dialogo_EnvuelveAmbosParrafosDentroDelAnchoDelCuerpo()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();
        aviso.PerformLayout();

        var cuerpo = aviso.Controls.OfType<Panel>().Single(panel => panel.Dock == DockStyle.Fill);
        int anchoDisponible = cuerpo.ClientSize.Width - cuerpo.Padding.Horizontal;
        var reconocimiento = cuerpo.Controls.OfType<Label>().Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoReconocimiento);
        var detalle = cuerpo.Controls.OfType<Label>().Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoDetalle);

        Assert.IsFalse(reconocimiento.AutoSize, "AutoSize no envuelve: el ancho desbordaría hacia la derecha.");
        Assert.IsFalse(detalle.AutoSize);
        Assert.AreEqual(anchoDisponible, reconocimiento.Width, "El ancho debe quedar acotado al cuerpo.");
        Assert.AreEqual(anchoDisponible, detalle.Width);
        Assert.IsTrue(detalle.Top >= reconocimiento.Bottom, "Los párrafos no pueden solaparse.");
        Assert.IsTrue(detalle.Bottom <= cuerpo.ClientSize.Height - cuerpo.Padding.Bottom, "El detalle técnico debe caber completo dentro del cuerpo.");
    }

    [TestMethod]
    public void Dialogo_EncabezadoMuestraTituloYSubtituloCompletosSinSolapes()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();
        aviso.PerformLayout();

        var encabezado = aviso.Controls.OfType<TableLayoutPanel>().Single();
        Assert.AreEqual(SoproTheme.Colors.HeaderDark, encabezado.BackColor);
        var titulo = encabezado.Controls.OfType<Label>().Single(l => l.Text == "Registros locales");
        var subtitulo = encabezado.Controls.OfType<Label>().Single(l => l.Text == "Cómo SOPRO guarda los registros de diagnóstico");

        AssertarRotuloMideSuTextoCompleto(titulo);
        AssertarRotuloMideSuTextoCompleto(subtitulo);
        Assert.IsTrue(subtitulo.Top >= titulo.Bottom, "El subtítulo no puede solaparse con el título.");
        Assert.IsTrue(subtitulo.Bottom <= encabezado.Height, "El subtítulo no puede recortarse por abajo.");
    }

    [TestMethod]
    public void Dialogo_ReservaAlturaParaElContenidoMedidoConFuenteAmpliada()
    {
        // Con fuente ampliada (el texto ocupa más líneas con el mismo ancho
        // disponible) el formulario debe crecer hasta contener ambos párrafos;
        // nada puede quedar recortado detrás del pie. Layout REAL: se agranda
        // la fuente de las etiquetas y se deja que el formulario re-mida y
        // reserve; no se simula ningún DPI.
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();
        aviso.PerformLayout();

        var cuerpo = aviso.Controls.OfType<Panel>().Single(panel => panel.Dock == DockStyle.Fill);
        var reconocimiento = cuerpo.Controls.OfType<Label>().Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoReconocimiento);
        var detalle = cuerpo.Controls.OfType<Label>().Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoDetalle);
        int altoDetalleOriginal = detalle.Height;
        int altoVentanaOriginal = aviso.ClientSize.Height;

        var fuenteAmpliada = new Font(reconocimiento.Font.FontFamily, reconocimiento.Font.Size * 1.5F, reconocimiento.Font.Style, reconocimiento.Font.Unit);
        reconocimiento.Font = fuenteAmpliada;
        detalle.Font = fuenteAmpliada;
        // Mismo punto de entrada que usa el constructor: sin handle no se puede
        // diferir con BeginInvoke, así que el ajuste se aplica explícitamente
        // fuera de la transacción del cambio de fuente.
        aviso.AjustarLayout();

        Assert.IsTrue(detalle.Height > altoDetalleOriginal, "Con fuente ampliada el detalle ocupa más líneas.");
        Assert.IsTrue(aviso.ClientSize.Height > altoVentanaOriginal, "El formulario debe reservar altura para el contenido medido.");
        Assert.IsTrue(reconocimiento.Bottom <= detalle.Top, "Los párrafos no pueden solaparse.");
        Assert.IsTrue(reconocimiento.Bottom <= cuerpo.ClientSize.Height - cuerpo.Padding.Bottom, "El reconocimiento debe caber completo.");
        Assert.IsTrue(detalle.Bottom <= cuerpo.ClientSize.Height - cuerpo.Padding.Bottom,
            $"El detalle no puede recortarse detrás del pie (detalle.Bottom={detalle.Bottom}, límite={cuerpo.ClientSize.Height - cuerpo.Padding.Bottom}).");
        Assert.IsTrue(cuerpo.AutoScroll, "Si el área de trabajo no da para más, el cuerpo debe poder desplazarse.");
    }

    [TestMethod]
    public void Dialogo_EnAnchoReducidoElContenidoSigueCabiendoSinTaparse()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();
        aviso.PerformLayout();

        var cuerpo = aviso.Controls.OfType<Panel>().Single(panel => panel.Dock == DockStyle.Fill);
        var detalle = cuerpo.Controls.OfType<Label>().Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoDetalle);
        var reconocimiento = cuerpo.Controls.OfType<Label>().Single(l => l.Text == SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoReconocimiento);
        int altoVentanaOriginal = aviso.ClientSize.Height;

        // Ancho disponible reducido: el detalle envuelve en más líneas. El
        // ajuste de altura se aplica fuera de la transacción del setter,
        // exactamente como lo hace el constructor (layout real, no simulado).
        aviso.ClientSize = new Size(340, aviso.ClientSize.Height);
        aviso.AjustarLayout();

        Assert.IsTrue(aviso.ClientSize.Height > altoVentanaOriginal, "Con menos ancho el formulario debe reservar más altura.");
        Assert.IsTrue(detalle.Bottom <= cuerpo.ClientSize.Height - cuerpo.Padding.Bottom,
            $"El detalle no puede recortarse detrás del pie (detalle.Bottom={detalle.Bottom}, límite={cuerpo.ClientSize.Height - cuerpo.Padding.Bottom}).");
        Assert.IsTrue(reconocimiento.Bottom <= cuerpo.ClientSize.Height - cuerpo.Padding.Bottom, "El reconocimiento debe caber completo.");
        Assert.IsTrue(detalle.Top >= reconocimiento.Bottom, "Los párrafos no pueden solaparse.");
        int anchoDisponible = cuerpo.ClientSize.Width - cuerpo.Padding.Horizontal;
        Assert.AreEqual(anchoDisponible, detalle.Width, "El detalle sigue envolviendo en el ancho real disponible.");
    }

    [TestMethod]
    public void Detalle_ConservaLosDatosTecnicosExactos()
    {
        string detalle = SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos.TextoDetalle;

        StringAssert.Contains(detalle, "%LOCALAPPDATA%\\SOPRO\\logs");
        StringAssert.Contains(detalle, "1 MiB");
        StringAssert.Contains(detalle, "10 archivos");
        StringAssert.Contains(detalle, "7 días");
        StringAssert.Contains(detalle, "Registros locales");
    }

    [TestMethod]
    public void Dialogo_UsaElIconoSOPROEnLaBarraDeTitulo()
    {
        using var aviso = new SOPRO.WinForms.Forms.FormAvisoPrivacidadDiagnosticos();

        Assert.IsNotNull(aviso.Icon, "La barra de titulo nativa debe mostrar el icono SOPRO incrustado.");
    }

    private static void AssertarRotuloMideSuTextoCompleto(Label rotulo)
    {
        // Garantía estructural contra el colapso a un caracter: el rotulo es
        // AutoSize y se renderiza a su tamaño preferido (el texto completo).
        Assert.IsTrue(rotulo.AutoSize, "Un rotulo con ancho fijo puede colapsar el texto a un caracter.");
        Assert.AreEqual(rotulo.PreferredSize.Width, rotulo.Width,
            $"El rotulo debe renderizarse a su tamaño preferido (texto completo) (ancho={rotulo.Width}, preferido={rotulo.PreferredSize.Width}).");
        Assert.IsTrue(rotulo.Height >= rotulo.Font.Height,
            "El alto del rotulo debe cubrir la altura de la fuente.");
    }

    private static Button ObtenerBoton(Control raiz, string texto)
    {
        return ObtenerBotones(raiz).Single(boton => boton.Text == texto);
    }

    private static System.Collections.Generic.IEnumerable<Button> ObtenerBotones(Control raiz)
    {
        foreach (Control hijo in raiz.Controls)
        {
            if (hijo is Button boton)
            {
                yield return boton;
            }

            foreach (var anidado in ObtenerBotones(hijo))
            {
                yield return anidado;
            }
        }
    }

    private static void DispararClic(Button boton)
    {
        var onClick = typeof(Control).GetMethod(
            "OnClick",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(onClick, "Control.OnClick no está disponible para disparar el clic.");
        onClick.Invoke(boton, new object[] { EventArgs.Empty });
    }
}
