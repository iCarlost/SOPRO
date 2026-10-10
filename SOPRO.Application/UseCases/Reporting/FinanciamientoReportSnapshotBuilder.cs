using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Financiamiento.
///
/// Toma la configuración persistida (<c>ColumnaFinanciamiento</c>) —o, si no hay,
/// los defaults neutrales del catálogo— y produce un <see cref="ReportColumnSnapshot"/>
/// inmutable con <c>MapearFinanciamiento</c>. NO toca grids, NO lee de controles de
/// UI y NO escribe configuración.
///
/// El reporte conserva sus secciones internas (encabezado de plantilla, título,
/// bloque de datos y matriz de períodos); el snapshot sólo aporta las COLUMNAS de
/// la tabla (visibilidad, orden, encabezado, ancho, alineación, wrap, formato y rol
/// numérico) que PDF y Excel comparten. Las precisiones se resuelven con los
/// decimales globales del proyecto (<see cref="ReportColumnSnapshot.DecimalesCantidad"/>,
/// <see cref="ReportColumnSnapshot.DecimalesImporte"/> y
/// <see cref="ReportColumnSnapshot.DecimalesPorcentaje"/>).
/// </summary>
public sealed class FinanciamientoReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Financiamiento";

    /// <summary>
    /// Columnas por defecto cuando el proyecto aún no tiene configuración
    /// persistida. Se expresan como <see cref="ColumnaFinanciamiento"/> para que
    /// viajen por el mismo mapper neutral que las columnas reales; reproducen los
    /// defaults del grid del catálogo (<c>ColumnasFinanciamientoHelper</c>).
    /// </summary>
    private static readonly IReadOnlyList<ColumnaFinanciamiento> ColumnasPredeterminadas = new List<ColumnaFinanciamiento>
    {
        new() { Nombre = "Período",        NombreInterno = "colPeriodo",   AnchoColumna = 90,  Orden = 1,  Alineacion = AlineacionColumna.Centro,  Visible = true },
        new() { Nombre = "Inicio",         NombreInterno = "colInicio",    AnchoColumna = 75,  Orden = 2,  Alineacion = AlineacionColumna.Centro,  Visible = true },
        new() { Nombre = "Fin",            NombreInterno = "colFin",       AnchoColumna = 75,  Orden = 3,  Alineacion = AlineacionColumna.Centro,  Visible = true },
        new() { Nombre = "Días",           NombreInterno = "colDias",      AnchoColumna = 45,  Orden = 4,  Alineacion = AlineacionColumna.Centro,  Visible = true },
        new() { Nombre = "CD $",           NombreInterno = "colCD",        AnchoColumna = 95,  Orden = 5,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "CI $",           NombreInterno = "colCI",        AnchoColumna = 95,  Orden = 6,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Egreso $",       NombreInterno = "colEgresos",   AnchoColumna = 105, Orden = 7,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Anticipo $",     NombreInterno = "colAnticipo",  AnchoColumna = 95,  Orden = 8,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Estimación $",   NombreInterno = "colEstim",     AnchoColumna = 105, Orden = 9,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Amortización $", NombreInterno = "colAmort",     AnchoColumna = 110, Orden = 10, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Cobro neto $",   NombreInterno = "colCobro",     AnchoColumna = 105, Orden = 11, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Flujo $",        NombreInterno = "colFlujoNeto", AnchoColumna = 95,  Orden = 12, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Saldo acum. $",  NombreInterno = "colSaldo",     AnchoColumna = 110, Orden = 13, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
        new() { Nombre = "Tasa período",   NombreInterno = "colTasa",      AnchoColumna = 85,  Orden = 14, Alineacion = AlineacionColumna.Centro,  Visible = true },
        new() { Nombre = "Interés $",      NombreInterno = "colInteres",   AnchoColumna = 95,  Orden = 15, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
    };

    /// <summary>
    /// Construye el snapshot desde las columnas de financiamiento del proyecto.
    /// Si <paramref name="columnas"/> es nulo o vacío se usan los defaults
    /// neutrales del catálogo.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de financiamiento persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaFinanciamiento>? columnas,
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
            .Select(ReportColumnDefinitionMapper.MapearFinanciamiento)
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
    /// Columnas neutrales por defecto del catálogo de financiamiento (sin proyecto
    /// ni configuración), ya filtradas de columnas internas y ordenadas por
    /// <c>Orden</c>. Es la línea base que reutilizan los renderizadores cuando no
    /// hay configuración persistida.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearFinanciamiento)
            .ToList();
}
