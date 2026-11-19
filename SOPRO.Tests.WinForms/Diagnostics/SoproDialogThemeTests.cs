using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Cromo compartido de dialogos: funciones puras de layout, botones del tema y
/// composición del encabezado con TableLayoutPanel (rotulos que miden su
/// texto completo con la fuente real). No abren formularios modales.
/// </summary>
[TestClass]
public class SoproDialogThemeTests
{
    [TestMethod]
    public void CalcularXBotonPie_AlineaElPrimarioAlMargenDerecho()
    {
        int x = SoproDialogTheme.CalcularXBotonPie(400, new[] { 120 }, 0);

        Assert.AreEqual(400 - SoproDialogTheme.MargenDerechoPie - 120, x);
    }

    [TestMethod]
    public void CalcularXBotonPie_EspaciaLosBotonesDeDerechaAIzquierda()
    {
        int xPrimario = SoproDialogTheme.CalcularXBotonPie(400, new[] { 120, 100 }, 0);
        int xSecundario = SoproDialogTheme.CalcularXBotonPie(400, new[] { 120, 100 }, 1);

        Assert.AreEqual(xPrimario - 100 - SoproDialogTheme.SeparacionBotones, xSecundario);
        Assert.IsTrue(xSecundario >= 0, "El botón más a la izquierda debe caber dentro del pie.");
    }

    [TestMethod]
    public void CalcularYBotonPie_CentraVerticalmenteYSinValoresNegativos()
    {
        Assert.AreEqual(12, SoproDialogTheme.CalcularYBotonPie(58, 34));
        Assert.AreEqual(0, SoproDialogTheme.CalcularYBotonPie(20, 34));
    }

    [TestMethod]
    public void CalcularAltoItemLista_NoBajaDelMinimoYCreceConElTexto()
    {
        Assert.AreEqual(SoproDialogTheme.AltoMinimoItemLista, SoproDialogTheme.CalcularAltoItemLista(10));
        Assert.AreEqual(20 + SoproDialogTheme.MargenVerticalItemLista, SoproDialogTheme.CalcularAltoItemLista(20));
    }

    [TestMethod]
    public void CalcularAltoItemLista_ConControlMideConSuFuente()
    {
        using var lista = new ListBox { Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point) };

        int alto = SoproDialogTheme.CalcularAltoItemLista(lista);

        Assert.IsTrue(alto >= SoproDialogTheme.AltoMinimoItemLista, "El alto del item nunca baja del mínimo.");
    }

    [TestMethod]
    public void CalcularAltoEtiqueta_EnvuelveElTextoEnElAnchoDisponible()
    {
        using var fuente = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        string texto = "SOPRO guarda registros de diagnóstico locales en %LOCALAPPDATA%\\SOPRO\\logs. Cada archivo tiene un límite aproximado de 1 MiB; se conservan como máximo 10 archivos durante 7 días.";

        int altoAncho = SoproDialogTheme.CalcularAltoEtiqueta(texto, fuente, 600);
        int altoEstrecho = SoproDialogTheme.CalcularAltoEtiqueta(texto, fuente, 200);

        Assert.IsTrue(altoAncho > 0);
        Assert.IsTrue(altoEstrecho > altoAncho, "Con menos ancho el texto necesita más líneas.");
    }

    [TestMethod]
    public void CalcularAltoEtiqueta_CasosDeBorde()
    {
        using var fuente = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

        Assert.AreEqual(0, SoproDialogTheme.CalcularAltoEtiqueta(string.Empty, fuente, 300));
        Assert.AreEqual(0, SoproDialogTheme.CalcularAltoEtiqueta("texto", fuente, 0));
        Assert.AreEqual(0, SoproDialogTheme.CalcularAltoEtiqueta("texto", null!, 300));
    }

    [TestMethod]
    public void CrearBotonPrimario_UsaElAcentoDelTemaYLayoutSmall()
    {
        using var boton = SoproDialogTheme.CrearBotonPrimario("Entendido");

        Assert.AreEqual(SoproTheme.Colors.RibbonActive, boton.BackColor);
        Assert.AreEqual(SoproTheme.Colors.Surface, boton.ForeColor);
        Assert.AreEqual(0, boton.FlatAppearance.BorderSize);
        Assert.AreEqual(SoproButtonLayout.Small, boton.SoproLayout, "Icono y texto se dibujan como un bloque centrado.");
        AssertarAnchoAlContenido(boton);
    }

    [TestMethod]
    public void CrearBotonSecundario_EsNeutralConFilete()
    {
        using var boton = SoproDialogTheme.CrearBotonSecundario("Revisar registros", SoproIconType.Carpeta);

        Assert.AreEqual(SoproTheme.Colors.Surface, boton.BackColor);
        Assert.AreEqual(SoproTheme.Colors.Ink2, boton.ForeColor);
        Assert.AreEqual(1, boton.FlatAppearance.BorderSize);
        Assert.AreEqual(SoproTheme.Colors.SurfaceBorder, boton.FlatAppearance.BorderColor);
        Assert.IsNotNull(boton.Image, "El icono indicado debe rasterizarse en el botón.");
        Assert.AreEqual(SoproButtonLayout.Small, boton.SoproLayout);
        AssertarAnchoAlContenido(boton);
    }

    private static void AssertarAnchoAlContenido(SoproButton boton)
    {
        int anchoTexto = TextRenderer.MeasureText(boton.Text, boton.Font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        int anchoIcono = boton.SoproIcon.HasValue ? Math.Max(12, boton.SoproIconSize) : 0;
        int espaciado = anchoIcono > 0 && anchoTexto > 0 ? boton.SoproIconTextSpacing : 0;
        int esperado = Math.Max(boton.SoproMinimumAutoWidth,
            boton.SoproContentPadding.Left + anchoIcono + espaciado + anchoTexto + boton.SoproContentPadding.Right);

        Assert.AreEqual(esperado, boton.Width, "El ancho del botón debe cubrir icono + texto + padding completos.");
        Assert.AreEqual(boton.SoproFixedHeight, boton.Height);
    }

    [TestMethod]
    public void AlinearBotonesPie_ColocaLosBotonesSegunLaFuncionPura()
    {
        using var pie = new Panel { Width = 400, Height = SoproDialogTheme.AltoPie };
        using var primario = new Button { Width = 120, Height = 34 };
        using var secundario = new Button { Width = 100, Height = 34 };

        SoproDialogTheme.AlinearBotonesPie(pie, primario, secundario);

        Assert.AreEqual(400 - SoproDialogTheme.MargenDerechoPie - 120, primario.Left);
        Assert.AreEqual(SoproDialogTheme.CalcularYBotonPie(SoproDialogTheme.AltoPie, 34), primario.Top);
        Assert.AreEqual(primario.Left - 100 - SoproDialogTheme.SeparacionBotones, secundario.Left);
    }

    [TestMethod]
    public void CrearEncabezado_ComponeTituloYSubtituloDelTemaConTextoCompleto()
    {
        using var encabezado = SoproDialogTheme.CrearEncabezado("Registros locales", "Subtítulo de prueba");
        encabezado.PerformLayout();

        Assert.IsInstanceOfType(encabezado, typeof(TableLayoutPanel));
        Assert.AreEqual(SoproTheme.Colors.HeaderDark, encabezado.BackColor);
        Assert.AreEqual(2, encabezado.Controls.Count);
        var titulo = encabezado.Controls.OfType<Label>().Single(l => l.Text == "Registros locales");
        var subtitulo = encabezado.Controls.OfType<Label>().Single(l => l.Text == "Subtítulo de prueba");
        Assert.AreEqual(SoproTheme.Fonts.DialogTitle, titulo.Font);
        Assert.AreEqual(SoproTheme.Colors.Surface, titulo.ForeColor);
        Assert.AreEqual(SoproTheme.Fonts.Subtitle, subtitulo.Font);
        Assert.AreEqual(SoproTheme.Colors.SubtitleText, subtitulo.ForeColor);
        AssertarRotuloMideSuTextoCompleto(titulo);
        AssertarRotuloMideSuTextoCompleto(subtitulo);
        Assert.IsTrue(subtitulo.Top >= titulo.Bottom, "El subtítulo no puede solaparse con el título.");
        Assert.IsTrue(subtitulo.Bottom <= encabezado.Height, "El subtítulo debe caber dentro de la banda.");
        Assert.IsTrue(encabezado.Height >= SoproDialogTheme.AltoEncabezado, "La banda nunca baja del alto del tema.");
    }

    [TestMethod]
    public void CrearEncabezado_SinSubtituloSoloIncluyeElTitulo()
    {
        using var encabezado = SoproDialogTheme.CrearEncabezado("Registros locales", null);
        encabezado.PerformLayout();

        Assert.AreEqual(1, encabezado.Controls.Count);
        var titulo = encabezado.Controls.OfType<Label>().Single();
        AssertarRotuloMideSuTextoCompleto(titulo);
        Assert.IsTrue(titulo.Bottom <= encabezado.Height);
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

    [TestMethod]
    public void CrearTarjeta_TieneFondoDeSuperficieYBordeReservado()
    {
        using var tarjeta = SoproDialogTheme.CrearTarjeta();

        Assert.AreEqual(SoproTheme.Colors.Surface, tarjeta.BackColor);
        Assert.AreEqual(1, tarjeta.Padding.Left);
        Assert.AreEqual(1, tarjeta.Padding.Top);
    }
}
