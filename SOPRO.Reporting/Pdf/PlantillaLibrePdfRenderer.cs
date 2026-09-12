using System.Security.Cryptography;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;
using SysColor = System.Drawing.Color;

namespace SOPRO.Reporting.Pdf;

/// <summary>
/// Render de los elementos libres del diseñador PDF sobre un documento neutral
/// (contraparte de PlantillaLibrePdfRenderer de SOPRO.WinForms.Services).
///
/// Los elementos ya llegan filtrados por zona y en su orden definitivo
/// (ZOrder, Id) desde el modelo; aquí NO se reordenan. El contenido ya tiene los
/// tokens resueltos por Application; únicamente {pagina}/{total_paginas} se
/// convierten en campos MigraDoc reales.
/// </summary>
internal static class PlantillaLibrePdfRenderer
{
    private const int AnchoBaseDmm = 1959; // Unidades.ANCHO_BASE_DMM (195.9 mm)

    public static double ObtenerAlturaEncabezadoCm(MatrixCatalogPageHeights heights, bool tieneLibre)
        => tieneLibre
            ? Math.Max(0.20, heights.HeaderHeightDmm / 100.0)
            : Math.Max(1.8, heights.HeaderHeight / 28.0);

    public static double ObtenerAlturaPieCm(MatrixCatalogPageHeights heights, bool tieneLibre)
        => tieneLibre
            ? Math.Max(0.15, heights.FooterHeightDmm / 100.0)
            : Math.Max(1.2, heights.FooterHeight / 28.0);

    public static double ObtenerSeparacionContenidoSuperiorCm(bool tieneLibre, double fallbackExtraCm)
        => tieneLibre ? 0.05 : fallbackExtraCm;

    public static double ObtenerSeparacionContenidoInferiorCm(bool tieneLibre, double fallbackExtraCm)
        => tieneLibre ? 0.05 : fallbackExtraCm;

    public static bool TryRenderHeader(HeaderFooter container, Section section,
        IReadOnlyList<MatrixCatalogPageElement> elementos, int alturaZonaDmm)
        => TryRenderMigraDoc(container, section,
            ReportPageLayoutHelper.GetContentWidthCm(section) * 100.0,
            elementos, alturaZonaDmm, esPie: false);

    public static bool TryRenderFooter(HeaderFooter container, Section section,
        IReadOnlyList<MatrixCatalogPageElement> elementos, int alturaZonaDmm)
        => TryRenderMigraDoc(container, section,
            ReportPageLayoutHelper.GetContentWidthCm(section) * 100.0,
            elementos, alturaZonaDmm, esPie: true);

    private static bool TryRenderMigraDoc(HeaderFooter container, Section section,
        double anchoDisponibleDmm, IReadOnlyList<MatrixCatalogPageElement> elementos,
        int alturaZonaDmm, bool esPie)
    {
        if (elementos == null || elementos.Count == 0)
            return false;

        foreach (var el in elementos)
        {
            if (el.Y >= alturaZonaDmm)
                continue;

            double xDmm = DistribuirX(el.X, el.Width, anchoDisponibleDmm);
            double yDmm = el.Y;
            double wDmm = el.Width;
            double hDmm = Math.Min(el.Height, Math.Max(0, alturaZonaDmm - el.Y));
            if (hDmm <= 0)
                continue;

            if (el.Kind == MatrixCatalogPageElementKind.Imagen)
            {
                string? path = MaterializarImagenTemporal(el);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                var frameImg = container.AddTextFrame();
                frameImg.WrapFormat.Style = WrapStyle.Through;
                if (!esPie)
                {
                    frameImg.RelativeHorizontal = RelativeHorizontal.Margin;
                    frameImg.RelativeVertical = RelativeVertical.Page;
                }
                frameImg.Left = Unit.FromMillimeter(xDmm / 10.0);
                frameImg.Top = Unit.FromMillimeter(yDmm / 10.0);
                frameImg.Width = Unit.FromMillimeter(wDmm / 10.0);
                frameImg.Height = Unit.FromMillimeter(hDmm / 10.0);
                frameImg.MarginLeft = 0;
                frameImg.MarginRight = 0;
                frameImg.MarginTop = 0;
                frameImg.MarginBottom = 0;
                var img = frameImg.AddImage(path);
                img.LockAspectRatio = false;
                img.Width = frameImg.Width;
                img.Height = frameImg.Height;
                continue;
            }

            var frame = container.AddTextFrame();
            frame.WrapFormat.Style = WrapStyle.Through;
            if (!esPie)
            {
                frame.RelativeHorizontal = RelativeHorizontal.Margin;
                frame.RelativeVertical = RelativeVertical.Page;
            }
            frame.Left = Unit.FromMillimeter(xDmm / 10.0);
            frame.Top = Unit.FromMillimeter(yDmm / 10.0);
            frame.Width = Unit.FromMillimeter(wDmm / 10.0);
            frame.Height = Unit.FromMillimeter(hDmm / 10.0);
            frame.MarginLeft = 0;
            frame.MarginRight = 0;
            frame.MarginTop = 0;
            frame.MarginBottom = 0;

            var p = frame.AddParagraph();
            p.Format.Alignment = ConvertirAlineacion(el.Alignment);
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            p.Format.Font.Name = PdfFontHelper.NormalizeFontName(
                string.IsNullOrWhiteSpace(el.Style.FontName) ? "Segoe UI" : el.Style.FontName);
            p.Format.Font.Size = Math.Max(6, el.Style.Size);
            p.Format.Font.Bold = el.Style.Bold;
            p.Format.Font.Italic = el.Style.Italic;
            if (!string.IsNullOrWhiteSpace(el.Style.ColorHex))
            {
                var c = SysColorTranslator(el.Style.ColorHex);
                p.Format.Font.Color = new MColor(c.R, c.G, c.B);
            }

            // El contenido ya viene resuelto por Application; {pagina} y
            // {total_paginas} se convierten en campos MigraDoc reales.
            AgregarTextoConCamposPagina(p, el.Content ?? string.Empty);
        }
        return true;
    }

    private static double DistribuirX(double xDmm, double anchoElementoDmm, double anchoDisponibleDmm)
    {
        // AnchoBaseDmm es constante (1959 dmm) y siempre > 0.
        double centroBase = xDmm + (anchoElementoDmm / 2.0);
        double ratioCentro = centroBase / AnchoBaseDmm;
        double nuevoCentro = ratioCentro * anchoDisponibleDmm;
        double nuevoX = nuevoCentro - (anchoElementoDmm / 2.0);

        double maxX = Math.Max(0, anchoDisponibleDmm - anchoElementoDmm);
        if (nuevoX < 0) return 0;
        if (nuevoX > maxX) return maxX;
        return nuevoX;
    }

    private static string? MaterializarImagenTemporal(MatrixCatalogPageElement el)
    {
        if (el.ImageBytes.Count == 0)
            return null;

        var bytes = el.ImageBytes.ToArray();
        var ext = ObtenerExtension(el);
        var hash = Convert.ToHexString(MD5.HashData(bytes));
        var dir = Path.Combine(Path.GetTempPath(), "SOPRO", "PlantillaPdfImg");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, hash + ext);
        if (!File.Exists(path))
            File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string ObtenerExtension(MatrixCatalogPageElement el)
    {
        var ext = Path.GetExtension(el.ImageFileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(ext)) return ext;
        return (el.ImageMimeType ?? string.Empty).ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/bmp" => ".bmp",
            _ => ".img"
        };
    }

    internal static void AgregarTextoConCamposPagina(Paragraph p, string texto)
    {
        texto ??= string.Empty;
        int index = 0;
        while (index < texto.Length)
        {
            int posPagina = texto.IndexOf("{pagina}", index, StringComparison.OrdinalIgnoreCase);
            int posTotal = texto.IndexOf("{total_paginas}", index, StringComparison.OrdinalIgnoreCase);
            int next = MinPos(posPagina, posTotal);
            if (next < 0)
            {
                p.AddText(texto.Substring(index));
                break;
            }
            if (next > index)
                p.AddText(texto.Substring(index, next - index));
            if (next == posPagina)
            {
                p.AddPageField();
                index = next + "{pagina}".Length;
            }
            else
            {
                p.AddNumPagesField();
                index = next + "{total_paginas}".Length;
            }
        }
    }

    private static int MinPos(int a, int b)
    {
        if (a < 0) return b;
        if (b < 0) return a;
        return Math.Min(a, b);
    }

    private static MParagraphAlignment ConvertirAlineacion(string alineacion)
    {
        var s = (alineacion ?? string.Empty).ToLowerInvariant();
        if (s.Contains("right") || s.Contains("der")) return MParagraphAlignment.Right;
        if (s.Contains("center") || s.Contains("cen")) return MParagraphAlignment.Center;
        return MParagraphAlignment.Left;
    }

    private static SysColor SysColorTranslator(string? hex)
    {
        try { return System.Drawing.ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(hex) ? "#000000" : hex); }
        catch { return SysColor.Black; }
    }
}