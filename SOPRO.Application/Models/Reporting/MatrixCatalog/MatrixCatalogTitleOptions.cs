namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Opciones neutrales para el título del catálogo (contraparte legible de
/// <c>ConfiguracionTituloReporte</c>). Los valores <c>null</c> usan los defaults
/// legacy del generador (fuente "Segoe UI", tamaño 14, negrita, color blanco
/// sobre "#33334C", texto "CATÁLOGO DE MATRICES").
/// </summary>
/// <param name="Text">Texto del título (null → "CATÁLOGO DE MATRICES").</param>
/// <param name="FontName">Tipografía.</param>
/// <param name="Size">Tamaño en puntos.</param>
/// <param name="Bold">Negrita.</param>
/// <param name="Italic">Cursiva.</param>
/// <param name="TextColorHex">Color de texto (#RRGGBB).</param>
/// <param name="BackgroundHex">Color de fondo (#RRGGBB).</param>
public sealed record MatrixCatalogTitleOptions(
    string? Text,
    string? FontName,
    double? Size,
    bool? Bold,
    bool? Italic,
    string? TextColorHex,
    string? BackgroundHex)
{
    public static readonly MatrixCatalogTitleOptions Default = new(
        Text: null, FontName: null, Size: null, Bold: null, Italic: null,
        TextColorHex: null, BackgroundHex: null);
}