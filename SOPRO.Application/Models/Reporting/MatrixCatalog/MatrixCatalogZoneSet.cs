namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Tres zonas de una franja (encabezado o pie): izquierda, centro y derecha.
/// </summary>
/// <param name="Left">Zona izquierda.</param>
/// <param name="Center">Zona central.</param>
/// <param name="Right">Zona derecha.</param>
public sealed record MatrixCatalogZoneSet(
    MatrixCatalogZone Left,
    MatrixCatalogZone Center,
    MatrixCatalogZone Right);