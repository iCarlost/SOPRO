using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
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
    /// Vista por defecto del catálogo. Se usa <c>""</c> (comodín) para que el
    /// override guardado desde el grid resuelva en cualquier vista del reporte.
    /// </summary>
    public const string ClaveVista = "";

    /// <summary>
    /// Construye el snapshot desde las columnas del catálogo de materiales del proyecto.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de material persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaMaterial> columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
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
            EstiloTabla: ReportTableStyle.LegacyMateriales())
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };
    }

    /// <summary>
    /// Devuelve una copia del snapshot con <c>EstilosCelda</c>/<c>EstilosFila</c>
    /// poblados desde la configuración persistida del (proyecto, tipo). Si no hay
    /// overrides, ambas colecciones quedan <c>null</c> (retrocompatibilidad total).
    /// </summary>
    public static ReportColumnSnapshot ConEstilos(ReportColumnSnapshot snapshot, FormatoReporteService formatos)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(formatos);
        return new ReportStyleSnapshotBuilder(formatos).Build(snapshot);
    }
}
