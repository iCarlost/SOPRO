using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Indirectos;

/// <summary>
/// Tema del módulo de Indirectos (issue #205): los roles de la paleta que
/// consume el formulario (header, toolbar, cuerpo de pestañas, encabezados de
/// grid, status) deben ser legibles en los TRES presets.
///
/// Los tests NO mutan la paleta activa del proceso (nunca llaman a
/// InitializePalette): resuelven paletas por preset con SoproTheme.GetPalette,
/// de modo que no dependen del orden de ejecución ni del preset del equipo.
/// Limitación declarada: el pintado real (renderer de toolbar) requiere un
/// formulario vivo y no se cubre aquí; la BANDA de pestañas queda nativa
/// (decisión #205 tras revisión), así que sus colores no son roles del tema.
/// </summary>
[TestClass]
public class FormIndirectosTemaTests
{
    private static readonly SoproTemaPreset[] Presets =
    {
        SoproTemaPreset.ClaroAzul,
        SoproTemaPreset.Grafito,
        SoproTemaPreset.ClaroVerde,
    };

    // =====================================================================
    // Legibilidad de los roles consumidos por el formulario, por preset
    // =====================================================================

    [TestMethod]
    public void Tema_TituloYSubtituloLegiblesSobreElHeaderEnLosTresPresets()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(p.OnHeader, p.HeaderDark, 4.5, $"{preset}: título sobre header");
            AssertContraste(p.SubtitleText, p.HeaderDark, 4.5, $"{preset}: subtítulo de proyecto sobre header");
        }
    }

    [TestMethod]
    public void Tema_ToolbarLegibleEnReposoHoverYPresionado()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(p.Ink2, p.SecondaryBg, 4.5, $"{preset}: comando sobre toolbar");
            AssertContraste(p.Ink2, p.HoverBg, 4.5, $"{preset}: comando en hover");
            AssertContraste(p.Ink2, p.RibbonPressed, 4.5, $"{preset}: comando en presionado");
            AssertContraste(p.Muted, p.SecondaryBg, 4.5, $"{preset}: Cerrar sobre toolbar");
            AssertContraste(p.Muted, p.HoverBg, 4.5, $"{preset}: Cerrar en hover");
            AssertContraste(p.Muted, p.RibbonPressed, 4.5, $"{preset}: Cerrar en presionado");

            // Affordance: los rellenos de hover/presionado contrastan ~1:1 con
            // la banda, así que el contorno de marca aporta el ≥3:1 no textual.
            AssertContraste(p.BrandMd, p.SecondaryBg, 3.0, $"{preset}: contorno de affordance sobre toolbar");
        }
    }

    /// <summary>
    /// El CUERPO de cada página toma la superficie del tema (la banda de
    /// pestañas queda nativa, sin roles del tema).
    /// </summary>
    [TestMethod]
    public void Tema_CuerpoDePaginaLegibleSobreLaSuperficie()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(p.Ink, p.Surface, 4.5, $"{preset}: contenido de página");
        }
    }

    [TestMethod]
    public void Tema_EncabezadosDeGridYStatusLegibles()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(p.Ink, p.HeaderBg, 4.5, $"{preset}: encabezado de grid");
            AssertContraste(p.OnSelection, p.SelBg, 4.5, $"{preset}: texto sobre selección");
            AssertContraste(p.Muted, p.HeaderBg, 4.5, $"{preset}: status sobre statusStrip");
            AssertContraste(p.Ink2, p.Surface, 4.5, $"{preset}: cuerpo de configuración sobre superficie");
        }
    }

    [TestMethod]
    public void Tema_AccionPrimariaLegibleEnSusEstados()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(p.OnBrand, p.RibbonActive, 4.5, $"{preset}: primario en reposo");
            AssertContraste(p.OnBrand, p.BrandHover, 4.5, $"{preset}: primario en hover");
            AssertContraste(p.OnBrand, p.RibbonDown, 4.5, $"{preset}: primario en presionado");
        }
    }

    /// <summary>
    /// DgvCeldaHelper (helper global, no modificable desde este lane) impone
    /// SelectionForeColor = Color.Black sobre el SelBg que el formulario le
    /// pasa. Verificar por preset que ese negro impuesto sigue siendo legible
    /// con los tokens reales de la paleta activa (los tres SelBg son claros).
    /// </summary>
    [TestMethod]
    public void Tema_SeleccionLegibleConElNegroImpuestoPorDgvCeldaHelper()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(Color.Black, p.SelBg, 4.5, $"{preset}: texto seleccionado negro sobre SelBg");
            AssertContraste(p.OnSelection, p.SelBg, 4.5, $"{preset}: token OnSelection sobre SelBg");
        }
    }

    /// <summary>
    /// Los tres porcentajes del resumen conservan los colores semánticos FIJOS
    /// del diseñador (no son configurables por el usuario). Contra la superficie
    /// OSCURA de Grafito (gbResumen toma Surface) deben seguir legibles: texto
    /// grande en negrita, mínimo 3:1. Sobre los claros son el look histórico.
    /// </summary>
    [TestMethod]
    public void Tema_PorcentajesResumenLegiblesSobreLaSuperficieOscura()
    {
        var p = SoproTheme.GetPalette(SoproTemaPreset.Grafito);

        AssertContraste(Color.FromArgb(33, 150, 243), p.Surface, 3.0, "Grafito: % Oficina Central");
        AssertContraste(Color.FromArgb(255, 152, 0), p.Surface, 3.0, "Grafito: % Campo");
        AssertContraste(Color.FromArgb(76, 175, 80), p.Surface, 3.0, "Grafito: % Total");
    }

    /// <summary>
    /// El borde de celda activa es no textual (≥3:1) contra la superficie del
    /// grid y contra el encabezado de columna (donde también se dibuja).
    /// </summary>
    [TestMethod]
    public void Tema_BordeCeldaActivaVisibleSobreSuperficieYEncabezado()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            AssertContraste(p.BrandMd, p.Surface, 3.0, $"{preset}: borde de celda sobre superficie");
            AssertContraste(p.BrandMd, p.HeaderBg, 3.0, $"{preset}: borde de celda sobre encabezado");
        }
    }

    /// <summary>
    /// El header oscuro y la toolbar no pueden resolverse al mismo color: el
    /// formulario distingue banda de título de banda de comandos.
    /// </summary>
    [TestMethod]
    public void Tema_HeaderYToolbarSonSuperficiesDistintas()
    {
        foreach (var preset in Presets)
        {
            var p = SoproTheme.GetPalette(preset);

            Assert.AreNotEqual(p.HeaderDark, p.SecondaryBg, preset.ToString());
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static void AssertContraste(Color frente, Color fondo, double minimo, string contexto)
    {
        double contraste = Contraste(frente, fondo);
        Assert.IsTrue(contraste >= minimo, $"{contexto}: {contraste:F2}:1 (mínimo {minimo:F1}:1)");
    }

    private static double Contraste(Color a, Color b)
    {
        double la = Luminancia(a);
        double lb = Luminancia(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminancia(Color color)
    {
        static double Canal(int valor)
        {
            double s = valor / 255d;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Canal(color.R) + 0.7152 * Canal(color.G) + 0.0722 * Canal(color.B);
    }
}
