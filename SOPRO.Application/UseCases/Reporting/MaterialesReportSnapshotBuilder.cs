using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Catálogo de Materiales.
///
/// Toma la configuración persistida (<c>ColumnaMaterial</c>) y produce un
/// <see cref="ReportColumnSnapshot"/> inmutable. NO toca grids y NO escribe
/// configuración. <c>ColumnaMaterial</c> es la única fuente; el encabezado usa el
/// default neutral <see cref="ReportTableStyle.LegacyMateriales"/>.
/// </summary>
public sealed class MaterialesReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Materiales";

    /// <summary>
    /// Construye el snapshot desde las columnas del catálogo de materiales del proyecto.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de material persistidas.</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaMaterial> columnas)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var definiciones = columnas
            .Where(c => c != null && !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(ReportColumnDefinitionMapper.MapearMaterial)
            .ToList();

        return new ReportColumnSnapshot(
            TipoReporte: TipoReporte,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: definiciones,
            EstiloTabla: ReportTableStyle.LegacyMateriales());
    }
}
