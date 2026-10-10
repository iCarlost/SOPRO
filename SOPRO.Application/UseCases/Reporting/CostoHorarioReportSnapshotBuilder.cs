using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de
/// Costo Horario / Maquinaria.
///
/// Toma la configuración persistida (<c>ColumnaMaquinaria</c>) —o, si no hay,
/// los defaults neutrales del catálogo— y produce un <see cref="ReportColumnSnapshot"/>
/// inmutable. NO toca grids, NO lee de controles de UI y NO escribe configuración.
///
/// El reporte conserva sus secciones internas (encabezado de plantilla, título y
/// tabla de datos); el snapshot sólo aporta las COLUMNAS de la tabla (visibilidad,
/// orden, encabezado, ancho, alineación, wrap, formato y rol numérico). Las
/// precisiones históricas de las columnas de cantidad (hasta cinco decimales)
/// se resuelven con los decimales globales del proyecto
/// (<see cref="ReportColumnSnapshot.DecimalesCantidad"/>), no con el layout fijo
/// de cada renderizador.
/// </summary>
public sealed class CostoHorarioReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Maquinaria";

    /// <summary>
    /// Columnas por defecto cuando el proyecto aún no tiene configuración
    /// persistida. Se expresan como <see cref="ColumnaMaquinaria"/> para que
    /// viajen por el mismo mapper neutral que las columnas reales; reproducen los
    /// defaults del grid del catálogo (<c>ColumnasMaquinariaHelper</c>).
    /// </summary>
    private static readonly IReadOnlyList<ColumnaMaquinaria> ColumnasPredeterminadas = new List<ColumnaMaquinaria>
    {
        new() { Nombre = "Clave",           NombreInterno = "Clave",           Visible = true, Orden = 1, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Descripción",     NombreInterno = "Descripcion",     Visible = true, Orden = 2, AnchoColumna = 320, Alineacion = AlineacionColumna.Izquierda },
        new() { Nombre = "Potencia (HP)",   NombreInterno = "PotenciaNominal", Visible = true, Orden = 3, AnchoColumna = 100, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" },
        new() { Nombre = "Combustible",     NombreInterno = "Combustible",     Visible = true, Orden = 4, AnchoColumna = 100, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Costo Horario",   NombreInterno = "CostoHorario",    Visible = true, Orden = 5, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "#,##0.00" },
        new() { Nombre = "Tipo",            NombreInterno = "TipoCosto",       Visible = true, Orden = 6, AnchoColumna =  80, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Origen",          NombreInterno = "Origen",          Visible = true, Orden = 7, AnchoColumna =  80, Alineacion = AlineacionColumna.Centro },
    };

    /// <summary>
    /// Construye el snapshot desde las columnas del catálogo de maquinaria del
    /// proyecto. Si <paramref name="columnas"/> es nulo o vacío se usan los
    /// defaults neutrales del catálogo.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de maquinaria persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaMaquinaria>? columnas,
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
            .Select(ReportColumnDefinitionMapper.MapearMaquinaria)
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
    /// Columnas neutrales por defecto del catálogo de maquinaria (sin proyecto ni
    /// configuración), ya filtradas de columnas internas y ordenadas por
    /// <c>Orden</c>. Es la línea base que reutilizan los renderizadores cuando no
    /// hay configuración persistida.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearMaquinaria)
            .ToList();
}
