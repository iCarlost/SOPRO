using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Cálculo de Indirectos.
///
/// Toma la configuración persistida (<c>ColumnaIndirectos</c>) —o, si no hay, los
/// defaults neutrales del catálogo— y produce un <see cref="ReportColumnSnapshot"/>
/// inmutable. NO toca grids, NO lee de controles de UI y NO escribe configuración.
///
/// El reporte conserva sus secciones internas (encabezado de plantilla, título,
/// filas jerárquicas de grupo/concepto y subtotales); el snapshot sólo aporta las
/// COLUMNAS de la tabla (visibilidad, orden, encabezado, ancho, alineación, wrap,
/// formato y rol numérico). La precisión de importes y porcentajes se resuelve con
/// los decimales globales del proyecto
/// (<see cref="ReportColumnSnapshot.DecimalesImporte"/> y
/// <see cref="ReportColumnSnapshot.DecimalesPorcentaje"/>), no con literales
/// "N2"/"N4" embebidos en cada renderizador.
/// </summary>
public sealed class IndirectosReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Indirectos";

    /// <summary>
    /// Clave de vista canónica del reporte de Indirectos. Es la vista por defecto
    /// (<c>""</c>), la MISMA que persiste el grid en
    /// <c>FormatoCeldaReporte.ClaveVista</c> (ver <c>FormIndirectos.FormatoVistaIndirectos</c>),
    /// de modo que el resolver de estilos coincide por igualdad exacta en PDF/Excel.
    /// </summary>
    public const string ClaveVista = "";

    /// <summary>
    /// Columnas por defecto cuando el proyecto aún no tiene configuración
    /// persistida. Se expresan como <see cref="ColumnaIndirectos"/> para que viajen
    /// por el mismo mapper neutral que las columnas reales; reproducen los defaults
    /// del grid del catálogo (<c>ColumnasIndirectosHelper</c>).
    /// </summary>
    private static readonly IReadOnlyList<ColumnaIndirectos> ColumnasPredeterminadas = new List<ColumnaIndirectos>
    {
        new() { Nombre = "Grupo / Concepto",  NombreInterno = "Grupo",          Visible = true, Orden = 1, AnchoColumna = 400, Alineacion = AlineacionColumna.Izquierda },
        new() { Nombre = "Importe Mensual $", NombreInterno = "ImporteMensual", Visible = true, Orden = 2, AnchoColumna = 180, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" },
        new() { Nombre = "Duración (Meses)",  NombreInterno = "Duracion",       Visible = true, Orden = 3, AnchoColumna = 150, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Importe Total $",   NombreInterno = "ImporteTotal",   Visible = true, Orden = 4, AnchoColumna = 180, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" },
    };

    /// <summary>
    /// Construye el snapshot desde las columnas del catálogo de indirectos del
    /// proyecto. Si <paramref name="columnas"/> es nulo o vacío se usan los
    /// defaults neutrales del catálogo.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de indirectos persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaIndirectos>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        var definiciones = fuente
            .Where(c => !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(ReportColumnDefinitionMapper.MapearIndirectos)
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
    /// Clave de fila ESTABLE de un grupo de indirectos, derivada del dominio (nunca
    /// del índice visual del grid): <c>G:{GrupoIndirecto.Id}</c>. Es EXACTAMENTE la
    /// clave que persiste el grid en <c>FormatoCeldaReporte.ClaveFila</c> (ver
    /// <c>FormIndirectos.ObtenerClaveFilaFormato</c>), de modo que los overrides de
    /// celda sobreviven a reordenamientos y filtros.
    /// </summary>
    public static string ClaveFila(GrupoIndirecto grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        return ClaveFilaReporte.GrupoIndirecto(grupo.Id);
    }

    /// <summary>
    /// Clave de fila ESTABLE de un concepto de indirectos: <c>IC:{ConceptoIndirecto.Id}</c>.
    /// Coincide con la que persiste el grid.
    /// </summary>
    public static string ClaveFila(ConceptoIndirecto concepto)
    {
        ArgumentNullException.ThrowIfNull(concepto);
        return ClaveFilaReporte.ConceptoIndirecto(concepto.Id);
    }

    /// <summary>
    /// Rol funcional de fila (<see cref="RolFilaReporte"/>) correspondiente al tipo
    /// de renglón del grid (<c>GRUPO</c>/<c>CONCEPTO</c>). Es el punto único de
    /// paridad grid↔PDF↔Excel para las filas editables.
    /// </summary>
    public static string? RolFila(string? tipoFila) => tipoFila?.Trim().ToUpperInvariant() switch
    {
        "GRUPO" => RolFilaReporte.Grupo,
        "CONCEPTO" => RolFilaReporte.Concepto,
        _ => null,
    };

    /// <summary>
    /// Devuelve una copia del snapshot con <see cref="ReportColumnSnapshot.EstilosCelda"/>
    /// y <see cref="ReportColumnSnapshot.EstilosFila"/> poblados desde la
    /// configuración persistida del (proyecto, tipo) —overrides de celda y estilos
    /// de rol del usuario—. Si no hay overrides, ambas colecciones quedan
    /// <c>null</c> (retrocompatibilidad total). Es el mecanismo de 4.2 aplicado a
    /// Indirectos.
    /// </summary>
    public static ReportColumnSnapshot ConEstilos(ReportColumnSnapshot snapshot, FormatoReporteService formatos)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(formatos);
        return new ReportStyleSnapshotBuilder(formatos).Build(snapshot);
    }

    /// <summary>
    /// Columnas neutrales por defecto del catálogo de indirectos (sin proyecto ni
    /// configuración), ya filtradas de columnas internas y ordenadas por
    /// <c>Orden</c>. Es la línea base que reutilizan los renderizadores cuando no
    /// hay configuración persistida.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearIndirectos)
            .ToList();
}
