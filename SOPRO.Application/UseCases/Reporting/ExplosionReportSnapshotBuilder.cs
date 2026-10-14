using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Explosión de Insumos.
///
/// Toma la configuración persistida (<c>ColumnaExplosion</c>) y produce un
/// <see cref="ReportColumnSnapshot"/> inmutable. NO toca grids, NO lee valores de
/// la UI y NO escribe configuración. <c>ColumnaExplosion</c> es la única fuente; el
/// estilo de tabla usa el default neutral
/// <see cref="ReportTableStyle.LegacyCatalogo"/> (misma línea base que el resto de
/// catálogos de la Fase 1: encabezado #4A4A6A, bandeado #F5F5F5 y grilla #DDDDDD).
///
/// El reporte conserva sus secciones internas (cuatro familias de insumo y sus
/// totales); el snapshot sólo aporta las COLUMNAS de la tabla (visibilidad, orden,
/// encabezado, ancho, alineación, wrap, formato y rol numérico) para que las rutas
/// PDF y Excel compartan UNA sola definición.
/// </summary>
public sealed class ExplosionReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Explosion";

    /// <summary>
    /// Construye el snapshot desde las columnas del módulo Explosión de Insumos del
    /// proyecto.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de explosión persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaExplosion> columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var definiciones = columnas
            .Where(c => c != null && !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(ReportColumnDefinitionMapper.MapearExplosion)
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

    /// <summary>
    /// Clave de vista canónica del reporte de Explosión. Es la vista por defecto
    /// (<c>""</c>), la MISMA que persiste el grid en <c>FormatoCeldaReporte.ClaveVista</c>
    /// (ver <c>FormExplosionInsumos.FormatoVistaExplosion</c>), de modo que el
    /// resolver de estilos coincide por igualdad exacta (o por comodín) en PDF/Excel.
    /// </summary>
    public const string ClaveVista = "";

    /// <summary>Clave sintética estable de la fila de TOTAL GENERAL del reporte.</summary>
    public const string ClaveFilaTotalGeneral = "TOTALGENERAL";

    /// <summary>
    /// Rol funcional de fila (<see cref="RolFilaReporte"/>) correspondiente al tipo
    /// de renglón del grid (<see cref="ExplosionRowKind"/>). Es el punto único de
    /// paridad grid↔PDF↔Excel para los estilos por rol.
    /// </summary>
    public static string? RolFila(ExplosionRowKind kind) => kind switch
    {
        ExplosionRowKind.Encabezado => RolFilaReporte.Encabezado,
        ExplosionRowKind.Total => RolFilaReporte.Total,
        ExplosionRowKind.TotalGeneral => RolFilaReporte.TotalGeneral,
        ExplosionRowKind.Referencia => RolFilaReporte.Referencia,
        ExplosionRowKind.Detalle => RolFilaReporte.Concepto,
        _ => null,
    };

    /// <summary>
    /// Clave de fila ESTABLE de la BANDA de sección (p. ej. "MATERIALES"):
    /// <c>SEC:{seccion}</c>. Deriva de la sección (no del índice visual), de modo
    /// que sobrevive a filtros y reordenamientos. Es EXACTAMENTE la que persiste el
    /// grid en <c>FormatoCeldaReporte.ClaveFila</c>.
    /// </summary>
    public static string ClaveFilaSeccion(string? seccion) => "SEC:" + NormalizarClave(seccion);

    /// <summary>
    /// Clave de fila ESTABLE de un INSUMO (detalle): <c>D:{seccion}:{clave}</c>. La
    /// sección evita colisiones entre insumos homónimos de familias distintas
    /// (material vs. herramienta). Coincide con la que persiste el grid.
    /// </summary>
    public static string ClaveFilaDetalle(string? seccion, string? clave)
        => "D:" + NormalizarClave(seccion) + ":" + (clave ?? string.Empty);

    /// <summary>
    /// Clave de fila ESTABLE del TOTAL de una sección: <c>T:{seccion}</c>.
    /// </summary>
    public static string ClaveFilaTotalSeccion(string? seccion) => "T:" + NormalizarClave(seccion);

    /// <summary>
    /// Clave de fila ESTABLE de una fila de REFERENCIA del grid, indexada por su
    /// orden de aparición (0 = Costo Directo del Presupuesto, 1 = Diferencia por
    /// redondeo).
    /// </summary>
    public static string ClaveFilaReferencia(int indice) => "REF:" + indice;

    /// <summary>
    /// Devuelve una copia del snapshot con <see cref="ReportColumnSnapshot.EstilosCelda"/>
    /// y <see cref="ReportColumnSnapshot.EstilosFila"/> poblados desde la
    /// configuración persistida del (proyecto, tipo) —overrides de celda y estilos
    /// de rol del usuario—. Si no hay overrides, ambas colecciones quedan
    /// <c>null</c> (retrocompatibilidad total). Mismo mecanismo que Presupuesto
    /// (4.2) e Indirectos (5.1), aplicado a Explosión.
    /// </summary>
    public static ReportColumnSnapshot ConEstilos(ReportColumnSnapshot snapshot, FormatoReporteService formatos)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(formatos);
        return new ReportStyleSnapshotBuilder(formatos).Build(snapshot);
    }

    private static string NormalizarClave(string? seccion)
        => (seccion ?? string.Empty).Trim().ToUpperInvariant();
}
