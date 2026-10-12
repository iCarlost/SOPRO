using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Catálogo de Herramientas.
///
/// Toma la configuración persistida (<c>ColumnaHerramienta</c>) y produce un
/// <see cref="ReportColumnSnapshot"/> inmutable. NO toca grids y NO escribe
/// configuración. <c>ColumnaHerramienta</c> es la única fuente; el encabezado usa
/// el default neutral <see cref="ReportTableStyle.LegacyCatalogo"/>.
/// </summary>
public sealed class HerramientasReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Herramientas";

    /// <summary>
    /// Construye el snapshot desde las columnas del catálogo de herramientas del proyecto.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de herramienta persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaHerramienta> columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var definiciones = columnas
            .Where(c => c != null && !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(ReportColumnDefinitionMapper.MapearHerramienta)
            .ToList();

        return new ReportColumnSnapshot(
            TipoReporte: TipoReporte,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: definiciones,
            EstiloTabla: ReportTableStyle.LegacyCatalogo())
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };
    }
}
