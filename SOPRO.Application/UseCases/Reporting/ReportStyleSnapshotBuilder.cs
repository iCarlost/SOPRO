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
                : NormalizarCeldas(celdas),
            EstilosFila = filas.Count == 0
                ? null
                : filas.Select(kv => new ReportRowRoleStyle(kv.Key, kv.Value)).ToList(),
        };
    }

    /// <summary>
    /// Proyecta los overrides de celda persistidos al contrato del snapshot,
    /// normalizando la clave de columna a su IDENTIFICADOR CANÓNICO (el MISMO que
    /// expone <see cref="ReportColumnDefinition.Identificador"/>). Sin esta
    /// normalización, la clave que el grid persiste (p. ej. el alias legacy
    /// <c>Importe</c>) NO coincidiría con la que los generadores PDF/Excel pasan al
    /// resolver (<c>ImporteTotal</c>) y el override se perdería en los reportes.
    ///
    /// Si un alias normalizado choca con la clave canónica ya persistida, gana la
    /// canónica (la que ya es idéntica a su identificador normalizado).
    /// </summary>
    private static List<ReportCellStyleOverride> NormalizarCeldas(
        IReadOnlyDictionary<ReportCellAddress, ReportStyleOverlay> celdas)
    {
        var porDireccion = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
        foreach (var kv in celdas)
        {
            var claveCanonica = ReportColumnDefinitionMapper.NormalizarIdentificador(kv.Key.NombreColumna);
            var direccion = kv.Key with { NombreColumna = claveCanonica };

            if (!porDireccion.ContainsKey(direccion)
                || string.Equals(kv.Key.NombreColumna, claveCanonica, StringComparison.OrdinalIgnoreCase))
            {
                porDireccion[direccion] = kv.Value;
            }
        }

        return porDireccion.Select(kv => new ReportCellStyleOverride(kv.Key, kv.Value)).ToList();
    }
}
