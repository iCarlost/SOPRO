using SOPRO.Application.Models.Reporting.MatrixCatalog;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Solicitud del catálogo de matrices. <c>FiltroTitulo</c> determina el sufijo
/// del título ("" → sin sufijo, "APU" → " (APU)", etc.).
/// <c>MatrixIds</c> define las matrices a incluir (IDs duplicados o inexistentes
/// se ignoran, comportamiento legacy: WHERE IN deduplica).
/// </summary>
public sealed record BuildMatrixCatalogReportRequest(
    IReadOnlyList<int> MatrixIds,
    string? FiltroTitulo = null,
    MatrixCatalogTitleOptions? TitleOptions = null);