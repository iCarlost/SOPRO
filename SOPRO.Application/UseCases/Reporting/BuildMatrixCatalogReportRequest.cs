using SOPRO.Application.Models.Reporting.MatrixCatalog;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Solicitud del catálogo de matrices. <c>FiltroTitulo</c> añade el sufijo
/// ("APU" → " (APU)", etc.): el título visible conserva el sufijo SOLO para el
/// título por defecto (si <see cref="MatrixCatalogTitleOptions.Text"/> está
/// configurado, el legacy reaplica el texto propio y el sufijo NO aparece en el
/// título visible), pero el título documental (Info.Title del PDF) conserva el
/// sufijo SIEMPRE (paridad estricta, dictamen Oracle N7-18c).
/// <c>MatrixIds</c> define las matrices a incluir (IDs duplicados o inexistentes
/// se ignoran, comportamiento legacy: WHERE IN deduplica).
///
/// Los IDs se materializan en una copia defensiva envuelta en una colección de
/// solo lectura: ni la lista que recibe el constructor ni un cast posterior
/// pueden mutar la solicitud (gate de inputs inmutables, PLAN-01:703).
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
            ArgumentNullException.ThrowIfNull(matrixIds);
            MatrixIds = Array.AsReadOnly(matrixIds.ToArray());
            FiltroTitulo = filtroTitulo;
            TitleOptions = titleOptions;
        }
    }