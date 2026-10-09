using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Presupuesto.
///
/// Toma configuración persistida (columnas personalizadas + overlays de reporte)
/// y produce un <see cref="ReportColumnSnapshot"/> inmutable. NO toca grids, NO
/// lee de controles de UI y NO escribe configuración.
///
/// Precedencia: <c>ColumnaPersonalizada</c> es primaria; <c>ConfigColumnaReporte</c>
/// sólo aporta el estilo del encabezado cuando su nombre interno coincide.
/// </summary>
public sealed class PresupuestoReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Presupuesto";

    /// <summary>
    /// Construye el snapshot desde las columnas personalizadas del proyecto y su
    /// configuración de reporte (overlay de encabezados).
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas personalizadas persistidas.</param>
    /// <param name="configuracionReporte">Overlays de encabezado persistidos.</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaPersonalizada> columnas,
        IEnumerable<ConfigColumnaReporte>? configuracionReporte = null)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var overlays = (configuracionReporte ?? Enumerable.Empty<ConfigColumnaReporte>())
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.NombreInterno))
            .GroupBy(c => ReportColumnDefinitionMapper.NormalizarIdentificador(c.NombreInterno), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var definiciones = columnas
            .Where(c => c != null && !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(c =>
            {
                var identificador = ReportColumnDefinitionMapper.NormalizarIdentificador(c.NombreInterno);
                overlays.TryGetValue(identificador, out var overlay);
                return ReportColumnDefinitionMapper.MapearPresupuesto(c, overlay);
            })
            .ToList();

        return new ReportColumnSnapshot(
            TipoReporte: TipoReporte,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: definiciones,
            EstiloTabla: ReportTableStyle.LegacyPresupuesto());
    }
}
