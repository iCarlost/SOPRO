namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Parámetros de construcción del documento: snapshots neutrales de proyecto y
/// plantilla más las opciones de título del request. Datos puros, sin entidades.
/// </summary>
internal sealed record MatrixCatalogReportSettings(
    MatrixCatalogProject Project,
    MatrixCatalogTemplate? Template,
    MatrixCatalogTitleOptions? TitleOptions,
    string? FiltroTitulo);