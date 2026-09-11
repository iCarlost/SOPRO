using SOPRO.Application.Models.Reporting.MatrixCatalog;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Solicitud del catálogo de matrices. <c>FiltroTitulo</c> determina el sufijo
/// del título ("" → sin sufijo, "APU" → " (APU)", etc.).
/// <c>MatrixIds</c> define las matrices a incluir (IDs duplicados o inexistentes
/// se ignoran, comportamiento legacy: WHERE IN deduplica).
///
/// Los IDs se materializan en una copia defensiva: la lista que recibe el
/// constructor puede seguir mutándose sin alterar la solicitud (gate de inputs
/// inmutables, PLAN-01:703).
/// </summary>
public sealed record BuildMatrixCatalogReportRequest
{
    /// <summary>IDs de matriz a incluir (copia inmodificable).</summary>
    public IReadOnlyList<int> MatrixIds { get; }

    /// <summary>Filtro de título ("" → sin sufijo, "APU", "Básicos", "Cuadrillas").</summary>
    public string? FiltroTitulo { get; init; }

    /// <summary>Opciones de título del documento.</summary>
    public MatrixCatalogTitleOptions? TitleOptions { get; init; }

    public BuildMatrixCatalogReportRequest(
        IReadOnlyList<int> matrixIds,
        string? filtroTitulo = null,
        MatrixCatalogTitleOptions? titleOptions = null)
    {
        MatrixIds = (matrixIds ?? throw new ArgumentNullException(nameof(matrixIds))).ToArray();
        FiltroTitulo = filtroTitulo;
        TitleOptions = titleOptions;
    }
}