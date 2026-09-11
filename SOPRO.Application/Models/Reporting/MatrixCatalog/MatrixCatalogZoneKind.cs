namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Contenido de una zona de encabezado/pie: texto (con campos dinámicos ya
/// resueltos) o imagen (ruta de la marca/imagen de la plantilla legacy).
/// </summary>
public enum MatrixCatalogZoneKind
{
    Texto = 0,
    Imagen = 1
}