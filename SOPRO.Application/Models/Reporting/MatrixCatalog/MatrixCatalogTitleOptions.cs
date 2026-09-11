namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Opciones neutrales para el título del catálogo (contraparte legible de
/// <c>ConfiguracionTituloReporte</c>). Los valores <c>null</c> usan los defaults
/// legacy del generador (fuente "Segoe UI", tamaño 14, negrita, color blanco,
/// texto "CATÁLOGO DE MATRICES").
///
/// El fondo del título NO está aquí: es política de cada renderer (ver
/// <see cref="MatrixCatalogTitleStyle"/>).
/// </summary>
/// <param name="Text">Texto del título (null → "CATÁLOGO DE MATRICES").</param>
/// <param name="FontName">Tipografía.</param>
/// <param name="Size">Tamaño en puntos.</param>
/// <param name="Bold">Negrita.</param>
/// <param name="Italic">Cursiva.</param>
/// <param name="TextColorHex">Color de texto (#RRGGBB).</param>
public sealed record MatrixCatalogTitleOptions(
    string? Text,
    string? FontName,
    double? Size,
    bool? Bold,
    bool? Italic,
    string? TextColorHex)
{
    public static readonly MatrixCatalogTitleOptions Default = new(
        Text: null, FontName: null, Size: null, Bold: null, Italic: null,
        TextColorHex: null);
}