using System.Drawing;

namespace SOPRO.Reporting.Excel;

/// <summary>
/// Helper para crear XLColor de forma segura (misma política que
/// ExcelColorHelper de SOPRO.WinForms.Services). XLColor.FromHtml solo acepta
/// hex (#RRGGBB); SafeFromHtml normaliza cualquier formato de System.Drawing.
/// </summary>
internal static class ExcelColorHelper
{
    public static ClosedXML.Excel.XLColor SafeFromHtml(string? valor, string fallbackHex = "#FFFFFF")
    {
        if (string.IsNullOrWhiteSpace(valor))
            return FromHex(fallbackHex);

        try
        {
            var c = ColorTranslator.FromHtml(valor.Trim());
            return ClosedXML.Excel.XLColor.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch
        {
            return FromHex(fallbackHex);
        }
    }

    private static ClosedXML.Excel.XLColor FromHex(string hex)
    {
        try
        {
            var c = ColorTranslator.FromHtml(hex);
            return ClosedXML.Excel.XLColor.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch
        {
            return ClosedXML.Excel.XLColor.FromArgb(255, 255, 255, 255);
        }
    }
}