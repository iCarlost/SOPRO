using MigraDoc.DocumentObjectModel;
using MigraFont = MigraDoc.DocumentObjectModel.Font;

namespace SOPRO.Reporting.Pdf;

/// <summary>
/// Normaliza nombres de tipografía y aplica fuentes en MigraDoc con la misma
/// política que <c>PdfFontHelper</c> de SOPRO.WinForms.Services.
/// </summary>
internal static class PdfFontHelper
{
    public static string NormalizeFontName(string? fontName)
    {
        var source = (fontName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(source))
            return "Segoe UI";

        return source.ToLowerInvariant() switch
        {
            "cambria" or "cambria math" or "calibri" or "calibri light" or "aptos" or "aptos display" or "aptos narrow"
                => "Segoe UI",
            _ => source
        };
    }

    public static void ApplyFont(MigraFont font, string? name, double size, bool bold = false, bool italic = false)
    {
        font.Name = NormalizeFontName(name);
        font.Size = size > 0 ? size : 9;
        font.Bold = bold;
        font.Italic = italic;
    }
}