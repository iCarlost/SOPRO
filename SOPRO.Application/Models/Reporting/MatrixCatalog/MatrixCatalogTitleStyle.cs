namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Intención visual neutral del título del documento.
/// </summary>
/// <param name="FontName">Nombre de la tipografía.</param>
/// <param name="Size">Tamaño de fuente en puntos.</param>
/// <param name="Bold">Negrita.</param>
/// <param name="Italic">Cursiva.</param>
/// <param name="TextColorHex">Color de texto en hexadecimal (#RRGGBB).</param>
/// <param name="BackgroundHex">Color de fondo en hexadecimal (#RRGGBB).</param>
public sealed record MatrixCatalogTitleStyle(
    string FontName,
    double Size,
    bool Bold,
    bool Italic,
    string TextColorHex,
    string BackgroundHex);