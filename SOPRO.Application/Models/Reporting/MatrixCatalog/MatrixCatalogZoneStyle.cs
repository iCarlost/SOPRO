namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Intención visual neutral de una zona de encabezado/pie (sin objetos UI ni de
/// ClosedXML/PDFsharp): tipografía, peso y color expresados como escalares.
/// </summary>
/// <param name="FontName">Nombre de la tipografía (p.ej. "Segoe UI").</param>
/// <param name="Size">Tamaño de fuente en puntos.</param>
/// <param name="Bold">Negrita.</param>
/// <param name="Italic">Cursiva.</param>
/// <param name="ColorHex">Color de texto en hexadecimal (#RRGGBB).</param>
/// <param name="Alignment">Alineación horizontal.</param>
public sealed record MatrixCatalogZoneStyle(
    string FontName,
    double Size,
    bool Bold,
    bool Italic,
    string ColorHex,
    MatrixCatalogTextAlignment Alignment);