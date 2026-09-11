using System.Collections.ObjectModel;

namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Documento de "Catálogo de Matrices" neutral y ya resuelto. Representa la
/// semántica del reporte (no celdas ni direcciones A1):
///   - Título con sufijo según filtro (APU/BÁSICOS/CUADRILLAS).
///   - Nombre del proyecto (ambos goldens de N7-18a lo muestran: el PDF con un
///     párrafo explícito y el Excel en el encabezado de plantilla).
///   - Encabezado y pie con textos de la plantilla y campos dinámicos resueltos
///     ({pagina}/{total_paginas} quedan para que el medio los resuelva).
///   - Matrices con la aritmética cruda legacy (sin redondeo, sin cálculo canónico).
/// Es inmutable y no expone entidades de EF, objetos de UI ni rutas de salida.
/// </summary>
public sealed class MatrixCatalogReportDocument
{
    private readonly ReadOnlyCollection<MatrixCatalogMatrix> _matrices;
    private readonly ReadOnlyCollection<MatrixCatalogPageElement> _headerElements;
    private readonly ReadOnlyCollection<MatrixCatalogPageElement> _footerElements;

    /// <summary>Título del documento (con sufijo de filtro aplicado).</summary>
    public string Title { get; }

    /// <summary>Intención visual neutral del título.</summary>
    public MatrixCatalogTitleStyle TitleStyle { get; }

    /// <summary>Nombre del proyecto del reporte.</summary>
    public string ProjectName { get; }

    /// <summary>Encabezado del documento (zonas izquierda/centro/derecha).</summary>
    public MatrixCatalogZoneSet Header { get; }

    /// <summary>Pie del documento (zonas izquierda/centro/derecha).</summary>
    public MatrixCatalogZoneSet Footer { get; }

    /// <summary>
    /// Elementos libres del encabezado PDF, ya resueltos y ordenados de forma
    /// definitiva (el renderer no vuelve a ordenar).
    /// </summary>
    public IReadOnlyList<MatrixCatalogPageElement> HeaderElements => _headerElements;

    /// <summary>Elementos libres del pie PDF, ordenados igual que el encabezado.</summary>
    public IReadOnlyList<MatrixCatalogPageElement> FooterElements => _footerElements;

    /// <summary>Alturas de las franjas de encabezado y pie de la plantilla.</summary>
    public MatrixCatalogPageHeights PageHeights { get; }

    /// <summary>Matrices del catálogo ordenadas por clave.</summary>
    public IReadOnlyList<MatrixCatalogMatrix> Matrices => _matrices;

    public MatrixCatalogReportDocument(
        string title,
        MatrixCatalogTitleStyle titleStyle,
        MatrixCatalogZoneSet header,
        MatrixCatalogZoneSet footer,
        IEnumerable<MatrixCatalogMatrix> matrices,
        string projectName = "",
        IEnumerable<MatrixCatalogPageElement>? headerElements = null,
        IEnumerable<MatrixCatalogPageElement>? footerElements = null,
        MatrixCatalogPageHeights? pageHeights = null)
    {
        Title = title ?? string.Empty;
        TitleStyle = titleStyle ?? throw new ArgumentNullException(nameof(titleStyle));
        ProjectName = projectName ?? string.Empty;
        Header = header;
        Footer = footer;
        _matrices = new ReadOnlyCollection<MatrixCatalogMatrix>(
            (matrices ?? throw new ArgumentNullException(nameof(matrices))).ToList());
        _headerElements = new ReadOnlyCollection<MatrixCatalogPageElement>(
            (headerElements ?? Array.Empty<MatrixCatalogPageElement>()).ToList());
        _footerElements = new ReadOnlyCollection<MatrixCatalogPageElement>(
            (footerElements ?? Array.Empty<MatrixCatalogPageElement>()).ToList());
        PageHeights = pageHeights ?? MatrixCatalogPageHeights.Default;
    }
}