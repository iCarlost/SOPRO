using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Adapta la configuración de título legacy al contrato neutral del reporte.
/// </summary>
public static class MatrixCatalogTitleOptionsMapper
{
    public static MatrixCatalogTitleOptions? FromLegacy(ConfiguracionTituloReporte? configuration)
    {
        if (configuration == null) return null;

        return new MatrixCatalogTitleOptions(
            Text: configuration.TextoTitulo,
            FontName: configuration.NombreFuente,
            Size: configuration.TamanoFuente,
            Bold: configuration.Negrita,
            Italic: configuration.Cursiva,
            TextColorHex: configuration.ColorTexto);
    }
}
