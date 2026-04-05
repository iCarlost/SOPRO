using ClosedXML.Excel;
using PdfSharp.Drawing;
using MigraDoc.DocumentObjectModel;
using SOPRO.Core.Entities;
using DrawingColor = System.Drawing.Color;
using DrawingFontStyle = System.Drawing.FontStyle;
using Color = MigraDoc.DocumentObjectModel.Color;

namespace SOPRO.WinForms.Services
{
    internal static class ReportTitleStyleHelper
    {
        public const string StandardBackgroundHex = "#1F4E79";
        public const string StandardTextHex = "#FFFFFF";

        public static string ObtenerTexto(ConfiguracionTituloReporte? cfg, string fallback)
            => string.IsNullOrWhiteSpace(cfg?.TextoTitulo) ? fallback : cfg!.TextoTitulo;

        public static void ApplyToParagraph(Paragraph paragraph, ConfiguracionTituloReporte? cfg, string fallbackText)
        {
            if (paragraph == null) return;

            paragraph.Elements.Clear();
            paragraph.AddText(ObtenerTexto(cfg, fallbackText));

            paragraph.Format.Alignment = ParagraphAlignment.Center;
            paragraph.Format.Shading.Color = ParseMigraColor(StandardBackgroundHex, Colors.DarkBlue);
            paragraph.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(cfg?.NombreFuente) ? "Segoe UI" : cfg!.NombreFuente);
            paragraph.Format.Font.Size = cfg?.TamanoFuente > 0 ? cfg.TamanoFuente : 14f;
            paragraph.Format.Font.Bold = cfg?.Negrita ?? true;
            paragraph.Format.Font.Italic = cfg?.Cursiva ?? false;
            paragraph.Format.Font.Color = ParseMigraColor(cfg?.ColorTexto, Colors.White);
            paragraph.Format.SpaceBefore = 0;
            paragraph.Format.SpaceAfter = 0;
            paragraph.Format.LeftIndent = 0;
            paragraph.Format.RightIndent = 0;
        }

        public static void ApplyToClosedXmlTitle(IXLRange range, ConfiguracionTituloReporte? cfg, string fallbackText, string backgroundHex = StandardBackgroundHex)
        {
            if (range == null) return;

            range.Merge();
            var cell = range.FirstCell();
            cell.Value = ObtenerTexto(cfg, fallbackText);
            cell.Style.Font.Bold = cfg?.Negrita ?? true;
            cell.Style.Font.Italic = cfg?.Cursiva ?? false;
            cell.Style.Font.FontName = string.IsNullOrWhiteSpace(cfg?.NombreFuente) ? "Segoe UI" : cfg!.NombreFuente;
            cell.Style.Font.FontSize = cfg?.TamanoFuente > 0 ? cfg.TamanoFuente : 14;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(backgroundHex, StandardBackgroundHex);
            // SafeFromHtml normaliza "White", "white", "#FFFFFF", etc. a XLColor seguro
            cell.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(cfg?.ColorTexto, "#FFFFFF");
        }

        public static XFont CreatePdfSharpFont(ConfiguracionTituloReporte? cfg, double fallbackSize = 14d)
        {
            var style = XFontStyleEx.Regular;
            if (cfg?.Negrita == true && cfg.Cursiva)
                style = XFontStyleEx.BoldItalic;
            else if (cfg?.Negrita == true)
                style = XFontStyleEx.Bold;
            else if (cfg?.Cursiva == true)
                style = XFontStyleEx.Italic;

            var fontName = string.IsNullOrWhiteSpace(cfg?.NombreFuente) ? "Segoe UI" : cfg!.NombreFuente;
            var size = cfg?.TamanoFuente > 0 ? cfg.TamanoFuente : (float)fallbackSize;
            return new XFont(fontName, size, style);
        }

        public static XBrush CreatePdfSharpBrush(ConfiguracionTituloReporte? cfg, string fallbackHex = StandardTextHex)
            => new XSolidBrush(ParseDrawingColor(cfg?.ColorTexto, fallbackHex));

        public static DrawingColor ParseDrawingColor(string? value, string fallbackHex = "#FFFFFF")
        {
            try { return System.Drawing.ColorTranslator.FromHtml(NormalizeHex(value, fallbackHex)); }
            catch { return System.Drawing.ColorTranslator.FromHtml(fallbackHex); }
        }

        private static Color ParseMigraColor(string? value, Color fallback)
        {
            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(NormalizeHex(value, "#FFFFFF"));
                return new Color(color.R, color.G, color.B);
            }
            catch
            {
                return fallback;
            }
        }

        private static string NormalizeHex(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value!;
    }
}
