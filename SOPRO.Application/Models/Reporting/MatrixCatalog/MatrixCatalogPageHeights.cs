namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Alturas de las franjas de encabezado y pie de la plantilla, tal y como las
/// persiste la entidad legacy <c>PlantillaReporte</c>:
///   - <c>HeaderHeight</c>/<c>FooterHeight</c>: px en pantalla / pts en Excel
///     (EncabezadoAltura/PiePaginaAltura).
///   - <c>HeaderHeightDmm</c>/<c>FooterHeightDmm</c>: altura de franja en décimas
///     de milímetro para el diseñador PDF (AlturaEncabezadoDmm/AlturaPieDmm).
/// Son datos de la plantilla; la interpretación exacta (división por 28, por
/// 100, factor 0.75 en Excel) es política de cada renderer.
/// </summary>
public sealed record MatrixCatalogPageHeights(
    int HeaderHeight,
    int HeaderHeightDmm,
    int FooterHeight,
    int FooterHeightDmm)
{
    /// <summary>Valores por defecto de la entidad legacy (sin plantilla).</summary>
    public static MatrixCatalogPageHeights Default { get; } = new(HeaderHeight: 60, HeaderHeightDmm: 400, FooterHeight: 40, FooterHeightDmm: 200);
}