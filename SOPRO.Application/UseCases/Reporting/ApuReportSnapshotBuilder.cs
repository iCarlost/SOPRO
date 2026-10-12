using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de APU (Análisis de
/// Precios Unitarios).
///
/// El APU es un reporte por concepto: conserva sus secciones internas (bloques
/// por tipo de insumo, subtotales e integración del precio unitario); el snapshot
/// sólo aporta las COLUMNAS de la tabla de componentes (visibilidad, orden,
/// encabezado, ancho, alineación, wrap, rol numérico y formato). Se expresa con
/// <see cref="ColumnaPersonalizada"/> para reutilizar el núcleo neutral de
/// <see cref="ReportColumnDefinitionMapper.MapearPresupuesto"/> (mismo contrato que
/// Presupuesto, con overlay opcional de encabezado). NO toca grids, NO lee de la
/// UI y NO escribe configuración.
///
/// La columna <c>Tipo</c> es una columna VISIBLE del APU (abreviatura del tipo de
/// insumo), por lo que NO se descarta como interna; sólo se excluye el relleno de
/// grid <c>colRelleno</c>.
///
/// El estilo de tabla usa el default neutral <see cref="ReportTableStyle.LegacyCatalogo"/>,
/// y el encabezado de cada columna queda alineado con él mediante un overlay
/// neutral, de modo que PDF y Excel compartan exactamente la misma presentación.
/// </summary>
public sealed class ApuReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "APU";

    /// <summary>
    /// Overlay neutral de encabezado: fija el estilo de encabezado de todas las
    /// columnas APU al de <see cref="ReportTableStyle.LegacyCatalogo"/>
    /// (<c>#4A4A6A</c> con texto blanco), independientemente del estilo por
    /// columna de la entidad de origen.
    /// </summary>
    private static readonly ConfigColumnaReporte OverlayEncabezadoNeutral = new()
    {
        EncFuente = "Segoe UI",
        EncTamaño = 9f,
        EncNegrita = true,
        EncCursiva = false,
        EncColorTexto = "#FFFFFF",
        EncColorFondo = "#4A4A6A",
    };

    /// <summary>
    /// Columnas por defecto cuando el proyecto aún no tiene configuración. Se
    /// expresan como <see cref="ColumnaPersonalizada"/> para que viajen por el
    /// mismo mapper neutral que las columnas reales. Reproducen los defaults del
    /// reporte APU del catálogo (Tipo, Clave, Descripción, Unidad, Cantidad,
    /// Costo Unit. e Importe) con el rol numérico correcto (Cantidad = cantidad;
    /// Costo Unit. e Importe = monetarios).
    /// </summary>
    private static readonly IReadOnlyList<ColumnaPersonalizada> ColumnasPredeterminadas = new List<ColumnaPersonalizada>
    {
        new() { Nombre = "Tipo",          NombreInterno = "Tipo",           Visible = true, Orden = 0, AnchoColumna = 60,  TipoDato = TipoDatoColumna.Texto,    Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Clave",         NombreInterno = "Clave",          Visible = true, Orden = 1, AnchoColumna = 80,  TipoDato = TipoDatoColumna.Texto,    Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Descripción",   NombreInterno = "Descripcion",    Visible = true, Orden = 2, AnchoColumna = 250, TipoDato = TipoDatoColumna.Texto,    Alineacion = AlineacionColumna.Izquierda, WrapTexto = true },
        new() { Nombre = "Unidad",        NombreInterno = "Unidad",         Visible = true, Orden = 3, AnchoColumna = 55,  TipoDato = TipoDatoColumna.Texto,    Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Cantidad",      NombreInterno = "Cantidad",       Visible = true, Orden = 4, AnchoColumna = 80,  TipoDato = TipoDatoColumna.Numerico, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" },
        new() { Nombre = "Costo Unit.",   NombreInterno = "PrecioUnitario", Visible = true, Orden = 5, AnchoColumna = 90,  TipoDato = TipoDatoColumna.Moneda,   Alineacion = AlineacionColumna.Derecha },
        new() { Nombre = "Importe",       NombreInterno = "ImporteTotal",   Visible = true, Orden = 6, AnchoColumna = 100, TipoDato = TipoDatoColumna.Moneda,   Alineacion = AlineacionColumna.Derecha },
    };

    /// <summary>
    /// Construye el snapshot desde las columnas neutrales del reporte APU. Si
    /// <paramref name="columnas"/> es nulo o vacío se usan los defaults en memoria
    /// (sin inicializar ni persistir configuración).
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas neutrales del APU.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaPersonalizada>? columnas = null,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        var definiciones = fuente
            .Where(c => !EsColumnaRelleno(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(c => ReportColumnDefinitionMapper.MapearPresupuesto(c, OverlayEncabezadoNeutral))
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
    /// Columnas neutrales por defecto del reporte APU (sin proyecto ni
    /// configuración), ya ordenadas por <c>Orden</c>. Es la línea base que
    /// reutilizan los renderizadores cuando no hay configuración persistida.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(c => ReportColumnDefinitionMapper.MapearPresupuesto(c, OverlayEncabezadoNeutral))
            .ToList();

    /// <summary>
    /// Sólo el relleno de grid es interno en el APU; <c>Tipo</c> es una columna
    /// visible legítima (abreviatura del tipo de insumo).
    /// </summary>
    private static bool EsColumnaRelleno(string? nombreInterno)
        => string.Equals(nombreInterno, ReportColumnDefinitionMapper.ColRellenoInterno, StringComparison.OrdinalIgnoreCase);
}
