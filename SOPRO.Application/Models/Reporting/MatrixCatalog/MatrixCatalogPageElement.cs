using System.Collections.ObjectModel;

namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Elemento libre de la plantilla de PDF (contraparte neutral e inmutable de
/// <c>PlantillaReporteElemento</c>).
///
/// El orden de la colección en el documento ES definitivo: la proyección los
/// materializa ya ordenados por (ZOrder, Id legacy) y el renderer NO vuelve a
/// ordenarlos ni a ver los IDs técnicos. Para evitar exponer el ID técnico,
/// este desempate no forma parte del modelo.
///
/// <c>Content</c> ya tiene los tokens resueltos por Application con el reloj
/// explícito; únicamente {pagina} y {total_paginas} quedan sin resolver para el
/// medio de salida. Los bytes de imagen se copian defensivamente y se exponen
/// como colección de solo lectura (gate de inmutabilidad, PLAN-01:703).
/// </summary>
public sealed class MatrixCatalogPageElement
{
    private readonly ReadOnlyCollection<byte> _imageBytes;

    public MatrixCatalogPageZone Zone { get; }
    public MatrixCatalogPageElementKind Kind { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public string Content { get; }
    public MatrixCatalogPageElementStyle Style { get; }
    public string Alignment { get; }
    public IReadOnlyList<byte> ImageBytes => _imageBytes;
    public string ImageFileName { get; }
    public string ImageMimeType { get; }

    public MatrixCatalogPageElement(
        MatrixCatalogPageZone zone,
        MatrixCatalogPageElementKind kind,
        int x,
        int y,
        int width,
        int height,
        string content,
        MatrixCatalogPageElementStyle style,
        string alignment,
        IEnumerable<byte>? imageBytes,
        string imageFileName = "",
        string imageMimeType = "")
    {
        Zone = zone;
        Kind = kind;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Content = content ?? string.Empty;
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Alignment = alignment ?? string.Empty;
        _imageBytes = Array.AsReadOnly((imageBytes ?? Array.Empty<byte>()).ToArray());
        ImageFileName = imageFileName ?? string.Empty;
        ImageMimeType = imageMimeType ?? string.Empty;
    }
}