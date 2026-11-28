using System;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Inicio;

/// <summary>
/// Contrato del núcleo de temas (issue #203): datos de las tres paletas
/// (regresión exacta del azul histórico, IDs estables y contraste WCAG AA de
/// los roles semánticos) y persistencia local tolerante del preset.
///
/// Estos tests NO tocan la paleta activa del proceso: resuelven paletas por
/// preset y usan carpetas temporales para el store, de modo que no dependen de
/// la preferencia real del desarrollador ni del orden de ejecución.
/// </summary>
[TestClass]
public class SoproTemaPaletaStoreTests
{
    // =====================================================================
    // Paletas: datos
    // =====================================================================

    [TestMethod]
    public void ClaroAzul_ReplicaExactamenteLosTokensVigentes()
    {
        var paleta = SoproTheme.GetPalette(SoproTemaPreset.ClaroAzul);

        Assert.AreEqual(SoproTemaPreset.ClaroAzul, paleta.Preset);
        Assert.AreEqual("claro-azul", paleta.Id);
        Assert.AreEqual("Azul claro", paleta.Nombre);

        // Marca
        AssertColor(0x1E, 0x40, 0xAF, paleta.Brand, nameof(paleta.Brand));
        AssertColor(0x1D, 0x4E, 0xD8, paleta.BrandMd, nameof(paleta.BrandMd));
        AssertColor(0x3B, 0x82, 0xF6, paleta.BrandLt, nameof(paleta.BrandLt));

        // Texto
        AssertColor(0x0A, 0x0F, 0x1E, paleta.Ink, nameof(paleta.Ink));
        AssertColor(0x1E, 0x29, 0x3B, paleta.Ink2, nameof(paleta.Ink2));
        AssertColor(0x4B, 0x5A, 0x72, paleta.Muted, nameof(paleta.Muted));

        // Fondos
        AssertColor(0xDB, 0xEA, 0xFE, paleta.BgBase, nameof(paleta.BgBase));
        AssertColor(0xE0, 0xE7, 0xFF, paleta.BgTint2, nameof(paleta.BgTint2));

        // Superficies
        AssertColor(0xFF, 0xFF, 0xFF, paleta.Surface, nameof(paleta.Surface));
        AssertColor(0xC7, 0xD7, 0xF0, paleta.SurfaceBorder, nameof(paleta.SurfaceBorder));

        // Rejilla
        AssertColor(0xF8, 0xFA, 0xFC, paleta.RowAlt, nameof(paleta.RowAlt));
        AssertColor(0xE2, 0xE8, 0xF0, paleta.GridLine, nameof(paleta.GridLine));
        AssertColor(0xDB, 0xEA, 0xFE, paleta.SelBg, nameof(paleta.SelBg));

        // Chrome
        AssertColor(0xFA, 0xFA, 0xFA, paleta.SidebarBg, nameof(paleta.SidebarBg));
        AssertColor(0xF0, 0xF0, 0xF3, paleta.SidebarHeaderBg, nameof(paleta.SidebarHeaderBg));
        AssertColor(0xC8, 0xC8, 0xC8, paleta.RibbonSeparator, nameof(paleta.RibbonSeparator));
        AssertColor(0x78, 0x78, 0x78, paleta.Disabled, nameof(paleta.Disabled));

        AssertColor(0x1E, 0x29, 0x3B, paleta.HeaderDark, nameof(paleta.HeaderDark));
        AssertColor(0x0F, 0x17, 0x2A, paleta.HeaderBottom, nameof(paleta.HeaderBottom));
        AssertColor(0xE2, 0xE8, 0xF0, paleta.RibbonText, nameof(paleta.RibbonText));
        AssertColor(0x33, 0x41, 0x55, paleta.RibbonHover, nameof(paleta.RibbonHover));
        AssertColor(0x1D, 0x4E, 0xD8, paleta.RibbonActive, nameof(paleta.RibbonActive));
        AssertColor(0x1E, 0x40, 0xAF, paleta.RibbonActiveHover, nameof(paleta.RibbonActiveHover));
        AssertColor(0x0F, 0x17, 0x2A, paleta.RibbonDown, nameof(paleta.RibbonDown));
        AssertColor(0x1E, 0x3A, 0x8A, paleta.RibbonActivePressed, nameof(paleta.RibbonActivePressed));
        AssertColor(0x94, 0xA3, 0xB8, paleta.RibbonDisabledIcon, nameof(paleta.RibbonDisabledIcon));
        AssertColor(0x47, 0x55, 0x69, paleta.RibbonSeparatorDark, nameof(paleta.RibbonSeparatorDark));
        AssertColor(0xF1, 0xF5, 0xF9, paleta.SidebarBg2, nameof(paleta.SidebarBg2));
        AssertColor(0xE2, 0xE8, 0xF0, paleta.SidebarHeaderBg2, nameof(paleta.SidebarHeaderBg2));
        AssertColor(0xCB, 0xD5, 0xE1, paleta.SidebarHairline, nameof(paleta.SidebarHairline));
        AssertColor(0x33, 0x41, 0x55, paleta.SidebarText, nameof(paleta.SidebarText));
        AssertColor(0x0F, 0x17, 0x2A, paleta.SidebarTextGroup, nameof(paleta.SidebarTextGroup));
        AssertColor(0xDB, 0xEA, 0xFE, paleta.SidebarActive, nameof(paleta.SidebarActive));

        AssertColor(0xCB, 0xD5, 0xE1, paleta.SubtitleText, nameof(paleta.SubtitleText));
        AssertColor(0xF1, 0xF5, 0xF9, paleta.HeaderBg, nameof(paleta.HeaderBg));
        AssertColor(0xF8, 0xFA, 0xFC, paleta.CenterBg, nameof(paleta.CenterBg));
        AssertColor(0xF1, 0xF5, 0xF9, paleta.SecondaryBg, nameof(paleta.SecondaryBg));
        AssertColor(0xE2, 0xE8, 0xF0, paleta.SecondaryHoverBg, nameof(paleta.SecondaryHoverBg));
        AssertColor(0x8A, 0x94, 0xA6, paleta.GlyphIdle, nameof(paleta.GlyphIdle));
        AssertColor(0xEA, 0xF2, 0xFF, paleta.HoverBg, nameof(paleta.HoverBg));

        // Glow conserva el alfa historico (sombra neutra sutil).
        Assert.AreEqual(Color.FromArgb(0x18, 0x0F, 0x17, 0x2A), paleta.Glow, nameof(paleta.Glow));

        // Roles semanticos nuevos: el azul historico usa blanco sobre marca y
        // tinta sobre la seleccion (mismo resultado visible que antes).
        AssertColor(0xFF, 0xFF, 0xFF, paleta.OnBrand, nameof(paleta.OnBrand));
        AssertColor(0xFF, 0xFF, 0xFF, paleta.OnHeader, nameof(paleta.OnHeader));
        AssertColor(0xB4, 0x23, 0x18, paleta.Error, nameof(paleta.Error));
        AssertColor(0x0A, 0x0F, 0x1E, paleta.OnSelection, nameof(paleta.OnSelection));
        AssertColor(0xDB, 0xEA, 0xFE, paleta.RibbonPressed, nameof(paleta.RibbonPressed));
    }

    [TestMethod]
    public void Paletas_TresPresetsConIdEstableYNombreVisible()
    {
        Assert.AreEqual(3, SoproTheme.Paletas.Count);

        var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in new[]
                 {
                     SoproTemaPreset.ClaroAzul,
                     SoproTemaPreset.Grafito,
                     SoproTemaPreset.ClaroVerde,
                 })
        {
            var paleta = SoproTheme.GetPalette(preset);

            Assert.AreEqual(preset, paleta.Preset);
            Assert.IsFalse(string.IsNullOrWhiteSpace(paleta.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(paleta.Nombre));
            Assert.IsTrue(ids.Add(paleta.Id), $"ID duplicado: {paleta.Id}");

            Assert.AreEqual(paleta.Id, SoproTheme.GetPresetId(preset));
            Assert.IsTrue(SoproTheme.TryParsePreset(paleta.Id, out var resuelto));
            Assert.AreEqual(preset, resuelto);
            Assert.AreEqual(paleta, SoproTheme.GetPalette(preset));
        }

        Assert.IsTrue(ids.Contains("claro-azul"));
        Assert.IsTrue(ids.Contains("grafito"));
        Assert.IsTrue(ids.Contains("claro-verde"));

        // IDs desconocidos o vacios no se interpretan.
        Assert.IsFalse(SoproTheme.TryParsePreset(null, out _));
        Assert.IsFalse(SoproTheme.TryParsePreset(string.Empty, out _));
        Assert.IsFalse(SoproTheme.TryParsePreset("GRAFITO", out _));
        Assert.IsFalse(SoproTheme.TryParsePreset("no-existe", out _));
    }

    [TestMethod]
    public void Paletas_ContrasteSuficienteEnRolesDeTexto()
    {
        foreach (var preset in new[]
                 {
                     SoproTemaPreset.ClaroAzul,
                     SoproTemaPreset.Grafito,
                     SoproTemaPreset.ClaroVerde,
                 })
        {
            var p = SoproTheme.GetPalette(preset);

            // Texto normal (>= 4.5:1, WCAG AA).
            AssertContraste(p.Ink, p.Surface, 4.5, $"{preset}: Ink sobre Surface");
            AssertContraste(p.Ink2, p.Surface, 4.5, $"{preset}: Ink2 sobre Surface");
            AssertContraste(p.Muted, p.Surface, 4.5, $"{preset}: Muted sobre Surface");
            AssertContraste(p.Muted, p.HeaderBg, 4.5, $"{preset}: Muted sobre HeaderBg");
            AssertContraste(p.Brand, p.Surface, 4.5, $"{preset}: Brand sobre Surface");
            AssertContraste(p.BrandMd, p.Surface, 4.5, $"{preset}: BrandMd sobre Surface");

            // Primeros planos de marca y seleccion.
            AssertContraste(p.OnBrand, p.RibbonActive, 4.5, $"{preset}: OnBrand sobre RibbonActive");
            AssertContraste(p.OnBrand, p.RibbonActiveHover, 4.5, $"{preset}: OnBrand sobre RibbonActiveHover");
            AssertContraste(p.OnBrand, p.BrandHover, 4.5, $"{preset}: OnBrand sobre BrandHover");
            AssertContraste(p.OnBrand, p.RibbonDown, 4.5, $"{preset}: OnBrand sobre RibbonDown");
            AssertContraste(p.OnBrand, p.RibbonActivePressed, 4.5, $"{preset}: OnBrand sobre RibbonActivePressed");
            AssertContraste(p.OnSelection, p.SelBg, 4.5, $"{preset}: OnSelection sobre SelBg");

            // Encabezado de dialogo y estados del ribbon.
            AssertContraste(p.OnHeader, p.HeaderDark, 4.5, $"{preset}: OnHeader sobre HeaderDark");
            AssertContraste(p.SubtitleText, p.HeaderDark, 4.5, $"{preset}: SubtitleText sobre HeaderDark");
            AssertContraste(p.Ink2, p.RibbonPressed, 4.5, $"{preset}: Ink2 sobre RibbonPressed");
            AssertContraste(p.Ink2, p.HoverBg, 4.5, $"{preset}: Ink2 sobre HoverBg");

            // Error semantico: visible sobre la tarjeta (Surface) y el cuerpo
            // (CenterBg) del dialogo, y sobre el encabezado.
            AssertContraste(p.Error, p.Surface, 4.5, $"{preset}: Error sobre Surface");
            AssertContraste(p.Error, p.CenterBg, 4.5, $"{preset}: Error sobre CenterBg");
            AssertContraste(p.Error, p.HeaderBg, 4.5, $"{preset}: Error sobre HeaderBg");

            // Elementos no textuales: minimo 3:1.
            AssertContraste(p.RibbonDisabledIcon, p.HeaderDark, 3.0, $"{preset}: RibbonDisabledIcon sobre HeaderDark");
            AssertContraste(p.Muted, p.Surface, 3.0, $"{preset}: deshabilitado sobre Surface");
        }
    }

    [TestMethod]
    public void Paletas_ResuelvenVistasPreviasDelPreset()
    {
        foreach (var preset in new[]
                 {
                     SoproTemaPreset.ClaroAzul,
                     SoproTemaPreset.Grafito,
                     SoproTemaPreset.ClaroVerde,
                 })
        {
            var p = SoproTheme.GetPalette(preset);
            Assert.AreEqual(p.Surface, p.PreviewFondo, preset.ToString());
            Assert.AreEqual(p.RibbonActive, p.PreviewAcento, preset.ToString());
            Assert.AreEqual(p.Ink, p.PreviewTinta, preset.ToString());
            Assert.AreEqual(p.SurfaceBorder, p.PreviewBorde, preset.ToString());
        }
    }

    // =====================================================================
    // Store: roundtrip y tolerancia
    // =====================================================================

    [TestMethod]
    public void Store_GuardaYRecuperaCadaPresetConIdEstable()
    {
        using var carpeta = new CarpetaTemporal();
        var store = new SoproTemaStore(carpeta.Ruta);

        foreach (var preset in new[]
                 {
                     SoproTemaPreset.ClaroAzul,
                     SoproTemaPreset.Grafito,
                     SoproTemaPreset.ClaroVerde,
                 })
        {
            Assert.IsTrue(store.TrySave(preset), $"No se pudo guardar {preset}");
            Assert.AreEqual(preset, store.Load(), $"Roundtrip de {preset}");

            string json = File.ReadAllText(store.ArchivoPreferencia);
            StringAssert.Contains(json, SoproTheme.GetPresetId(preset),
                $"El JSON debe contener el ID estable de {preset}");
        }

        // Escritura atomica: no quedan temporales de la operacion.
        var archivos = Directory.GetFiles(carpeta.Ruta);
        Assert.AreEqual(1, archivos.Length, "Solo debe quedar el archivo de preferencia.");
        Assert.AreEqual(SoproTemaStore.NombreArchivo, Path.GetFileName(archivos[0]));
    }

    [TestMethod]
    public void Store_SinArchivo_CaeAlAzul()
    {
        using var carpeta = new CarpetaTemporal();
        var store = new SoproTemaStore(carpeta.Ruta);

        Assert.IsFalse(File.Exists(store.ArchivoPreferencia));
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());
        Assert.IsFalse(File.Exists(store.ArchivoPreferencia),
            "La lectura no debe crear el archivo de preferencia.");
    }

    [TestMethod]
    public void Store_ArchivoCorrupto_CaeAlAzulSinReescribirlo()
    {
        using var carpeta = new CarpetaTemporal();
        var store = new SoproTemaStore(carpeta.Ruta);
        const string corrupto = "{ esto no es json ";
        File.WriteAllText(store.ArchivoPreferencia, corrupto);

        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());
        Assert.AreEqual(corrupto, File.ReadAllText(store.ArchivoPreferencia),
            "Un archivo corrupto no se reescribe durante la lectura.");
    }

    [TestMethod]
    public void Store_IdDesconocidoOCasingDistinto_CaeAlAzul()
    {
        using var carpeta = new CarpetaTemporal();
        var store = new SoproTemaStore(carpeta.Ruta);

        File.WriteAllText(store.ArchivoPreferencia, "{ \"tema\": \"no-existe\" }");
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());

        File.WriteAllText(store.ArchivoPreferencia, "{ \"tema\": \"GRAFITO\" }");
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());

        File.WriteAllText(store.ArchivoPreferencia, "{}");
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());

        File.WriteAllText(store.ArchivoPreferencia, "null");
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());
    }

    [TestMethod]
    public void Store_GuardadoFallido_DevuelveFalseSinLanzar()
    {
        using var carpeta = new CarpetaTemporal();

        // La "carpeta" de datos es en realidad un archivo: CreateDirectory no
        // puede usarla y el guardado debe informar el fallo, sin excepcion.
        string rutaBloqueada = Path.Combine(carpeta.Ruta, "no-es-carpeta");
        File.WriteAllText(rutaBloqueada, "bloqueo");

        var store = new SoproTemaStore(rutaBloqueada);

        Assert.IsFalse(store.TrySave(SoproTemaPreset.Grafito));
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load(),
            "Un store inutilizable cae al preset por omision.");
    }

    [TestMethod]
    public void Store_NoAceptaPresetInvalidoEnDiscoYConservaElAnterior()
    {
        using var carpeta = new CarpetaTemporal();
        var store = new SoproTemaStore(carpeta.Ruta);

        Assert.IsTrue(store.TrySave(SoproTemaPreset.ClaroVerde));
        File.WriteAllText(store.ArchivoPreferencia, "{ \"tema\": 42 }");

        // Tipo incorrecto en el JSON: lectura tolerante, sin tocar el archivo.
        Assert.AreEqual(SoproTemaPreset.ClaroAzul, store.Load());
        StringAssert.Contains(File.ReadAllText(store.ArchivoPreferencia), "42");
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static void AssertColor(byte r, byte g, byte b, Color color, string nombre)
        => Assert.AreEqual(Color.FromArgb(r, g, b), color, $"{nombre} debe conservar el token histórico");

    private static void AssertContraste(Color frente, Color fondo, double minimo, string contexto)
    {
        double contraste = Contraste(frente, fondo);
        Assert.IsTrue(
            contraste >= minimo,
            $"{contexto}: {contraste:F2}:1 (mínimo {minimo:F1}:1)");
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

    private sealed class CarpetaTemporal : IDisposable
    {
        public CarpetaTemporal()
        {
            Ruta = Path.Combine(Path.GetTempPath(), "sopro-tema-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Ruta);
        }

        public string Ruta { get; }

        public void Dispose()
        {
            try { Directory.Delete(Ruta, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
