using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Integra la configuración persistida de formato (overrides de celda y estilos
/// semánticos de fila) dentro de un <see cref="ReportColumnSnapshot"/> ya
/// construido por el builder del reporte. El proyecto y el tipo de reporte se
/// toman del propio snapshot (<see cref="ReportColumnSnapshot.ProyectoId"/> y
/// <see cref="ReportColumnSnapshot.TipoReporte"/>), de modo que la consulta se
/// hace por esa pareja.
///
/// Es un paso puro de composición: NO toca grids, NO renderiza y NO escribe
/// configuración. Cuando no hay overrides persistidos deja
/// <c>EstilosCelda</c>/<c>EstilosFila</c> en <c>null</c>, preservando el
/// comportamiento de los snapshots sin estilos.
/// </summary>
public sealed class ReportStyleSnapshotBuilder
{
    private readonly FormatoReporteService _formatos;

    public ReportStyleSnapshotBuilder(FormatoReporteService formatos)
    {
        _formatos = formatos ?? throw new ArgumentNullException(nameof(formatos));
    }

    /// <summary>
    /// Devuelve una copia del snapshot con <see cref="ReportColumnSnapshot.EstilosCelda"/>
    /// y <see cref="ReportColumnSnapshot.EstilosFila"/> poblados desde la
    /// configuración persistida del (proyecto, tipo) del snapshot. Si no hay
    /// overrides, ambas colecciones quedan <c>null</c>.
    /// </summary>
    public ReportColumnSnapshot Build(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var celdas = _formatos.CargarCeldas(snapshot.ProyectoId, snapshot.TipoReporte);
        var filas = _formatos.CargarEstilosFila(snapshot.ProyectoId, snapshot.TipoReporte);

        return snapshot with
        {
            EstilosCelda = celdas.Count == 0
                ? null
                : celdas.Select(kv => new ReportCellStyleOverride(kv.Key, kv.Value)).ToList(),
            EstilosFila = filas.Count == 0
                ? null
                : filas.Select(kv => new ReportRowRoleStyle(kv.Key, kv.Value)).ToList(),
        };
    }
}
