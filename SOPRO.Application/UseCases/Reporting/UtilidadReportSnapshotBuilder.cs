using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Utilidad.
///
/// El reporte de Utilidad es un resumen (no un grid editable): históricamente
/// dependía de un <c>DataGridView</c> "dummy" del formulario para resolver sus
/// columnas y formatos. Esta migración reemplaza esa dependencia semántica por un
/// <see cref="ReportColumnSnapshot"/> neutral construido EN MEMORIA, de modo que
/// PDF y Excel consuman exactamente la misma lista, orden, anchos, roles y
/// formatos.
///
/// La fuente de configuración es <see cref="ColumnaFinanciamiento"/> —extensible en
/// el futuro— y se proyecta con el mapper neutral ya existente
/// (<see cref="ReportColumnDefinitionMapper.MapearFinanciamiento"/>, señalado por el
/// plan como reutilizable para Utilidad). Cuando no hay configuración persistida se
/// usan los defaults neutrales del reporte construidos aquí, NUNCA leyendo ni
/// escribiendo controles de UI.
///
/// Las columnas describen los tres roles numéricos del resumen:
/// <list type="bullet">
///   <item><c>Base</c>: importes que integran la base (CD, CI, financiamiento y
///   subtotal), con los decimales de importe del proyecto y el símbolo <c>$</c>.</item>
///   <item><c>Porcentaje</c>: porcentajes (Up, ISR, PTU y utilidad neta), con los
///   decimales de porcentaje del proyecto.</item>
///   <item><c>ImporteFinal</c>: importes resultantes (utilidad, ISR, PTU y utilidad
///   neta estimada), con la misma semántica monetaria que <c>Base</c>.</item>
/// </list>
/// Más una columna de texto <c>Concepto</c> para las etiquetas de cada línea.
/// </summary>
public sealed class UtilidadReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Utilidad";

    /// <summary>
    /// Columnas por defecto cuando el proyecto no tiene configuración persistida. Se
    /// expresan como <see cref="ColumnaFinanciamiento"/> para que viajen por el mismo
    /// mapper neutral que las columnas reales.
    /// </summary>
    private static readonly IReadOnlyList<ColumnaFinanciamiento> ColumnasPredeterminadas = new List<ColumnaFinanciamiento>
    {
        new() { Nombre = "Concepto",      NombreInterno = UtilidadReportColumns.Concepto,     Visible = true, Orden = 0, AnchoColumna = 340, Alineacion = AlineacionColumna.Izquierda },
        new() { Nombre = "Base",          NombreInterno = UtilidadReportColumns.Base,         Visible = true, Orden = 1, AnchoColumna = 130, Alineacion = AlineacionColumna.Derecha,   FormatoNumerico = "C2" },
        new() { Nombre = "Porcentaje",    NombreInterno = UtilidadReportColumns.Porcentaje,   Visible = true, Orden = 2, AnchoColumna =  90, Alineacion = AlineacionColumna.Derecha,   FormatoNumerico = "P2" },
        new() { Nombre = "Importe final", NombreInterno = UtilidadReportColumns.ImporteFinal, Visible = true, Orden = 3, AnchoColumna = 130, Alineacion = AlineacionColumna.Derecha,   FormatoNumerico = "C2" },
    };

    /// <summary>
    /// Construye el snapshot desde las columnas de Utilidad del proyecto. Si
    /// <paramref name="columnas"/> es nulo o vacío se usan los defaults neutrales
    /// del reporte construidos EN MEMORIA.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas persistidas del reporte de Utilidad.</param>
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
            DecimalesPorcentaje = decimalesPorcentaje,
        };
    }

    /// <summary>
    /// Columnas neutrales por defecto del reporte de Utilidad (sin proyecto ni
    /// configuración), ya filtradas de columnas internas y ordenadas por
    /// <c>Orden</c>. Es la línea base que reutilizan los renderizadores cuando el
    /// snapshot no aporta una columna concreta.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearFinanciamiento)
            .ToList();
}

/// <summary>
/// Resolver EN MEMORIA de las columnas del reporte de Utilidad. Sustituye la
/// dependencia semántica del <c>DataGridView</c> "dummy" del formulario: los
/// generadores de PDF y Excel piden aquí las definiciones neutrales (por
/// identificador estable) y la regla única <c>ReportColumnGridFormat</c> decide el
/// formato. No toca ningún control de UI.
/// </summary>
public static class UtilidadReportColumns
{
    /// <summary>Columna de etiquetas del resumen.</summary>
    public const string Concepto = "Concepto";

    /// <summary>Columna monetaria de la base (CD, CI, financiamiento y subtotal).</summary>
    public const string Base = "Base";

    /// <summary>Columna de porcentaje (Up, ISR, PTU y utilidad neta).</summary>
    public const string Porcentaje = "Porcentaje";

    /// <summary>Columna monetaria de los importes resultantes.</summary>
    public const string ImporteFinal = "ImporteFinal";

    /// <summary>
    /// Devuelve la columna neutral con el identificador indicado, o <c>null</c> si
    /// el snapshot no la expone.
    /// </summary>
    public static ReportColumnDefinition? Buscar(ReportColumnSnapshot? snapshot, string identificador)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(identificador))
            return null;

        return snapshot.Columnas.FirstOrDefault(c =>
            string.Equals(c.Identificador, identificador, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Devuelve las columnas del snapshot ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> Obtener(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Columnas.OrderBy(c => c.Orden).ToList();
    }
}
