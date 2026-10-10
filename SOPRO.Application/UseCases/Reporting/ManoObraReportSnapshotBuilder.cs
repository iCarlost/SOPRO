using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Catálogo de Mano de Obra.
///
/// Toma la configuración persistida (<c>ColumnaManoObra</c>) y produce un
/// <see cref="ReportColumnSnapshot"/> inmutable. NO toca grids, NO lee valores de
/// la UI y NO escribe configuración. <c>ColumnaManoObra</c> es la única fuente; el
/// estilo de tabla usa el default neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/> (misma línea base que el resto de
/// catálogos de la Fase 1: encabezado #4A4A6A, bandeado #F5F5F5 y grilla #DDDDDD).
/// </summary>
public sealed class ManoObraReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "ManoDeObra";

    /// <summary>
    /// Construye el snapshot desde las columnas del catálogo de mano de obra del proyecto.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de mano de obra persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaManoObra> columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var definiciones = columnas
            .Where(c => c != null && !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(ReportColumnDefinitionMapper.MapearManoObra)
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
