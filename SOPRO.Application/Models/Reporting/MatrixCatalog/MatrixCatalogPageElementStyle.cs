namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Intención visual de un elemento libre (misma política que
/// <c>MatrixCatalogZoneStyle</c> pero SIN alineación: la alineación del
/// elemento libre se conserva como texto legacy, p.ej. "MiddleLeft", porque el
/// renderer la interpreta como pares vertical/horizontal).
/// </summary>
/// <param name="FontName">Nombre de la tipografía.</param>
/// <param name="Size">Tamaño de fuente en puntos (valor crudo de la plantilla).</param>
/// <param name="Bold">Negrita.</param>
/// <param name="Italic">Cursiva.</param>
/// <param name="ColorHex">Color de texto en hexadecimal (#RRGGBB, "" = default).</param>
public sealed record MatrixCatalogPageElementStyle(
    string FontName,
    double Size,
    bool Bold,
    bool Italic,
    string ColorHex);