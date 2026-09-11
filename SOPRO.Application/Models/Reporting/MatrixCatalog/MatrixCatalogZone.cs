namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Zona de encabezado o pie de página.
/// </summary>
/// <param name="Kind">Tipo de contenido (texto o imagen).</param>
/// <param name="Content">Texto con campos dinámicos resueltos, o ruta de la imagen.</param>
/// <param name="Style">Intención visual neutral.</param>
public sealed record MatrixCatalogZone(
    MatrixCatalogZoneKind Kind,
    string Content,
    MatrixCatalogZoneStyle Style);