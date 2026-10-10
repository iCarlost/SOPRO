using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Reporting.Programa;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas y de las filas neutrales del reporte
/// de Programa de Obra.
///
/// <para>
/// El snapshot se construye desde la configuración persistida
/// (<c>ColumnaProgramaObra</c>) mediante <see cref="ReportColumnDefinitionMapper.MapearProgramaObra"/>;
/// no toca grids, no lee valores de la UI y NO escribe configuración. Las filas se
/// materializan desde los datos neutrales (<see cref="ActivityGridRowDto"/>) más el
/// <see cref="GanttRenderModel"/>, de forma que las rutas PDF y Excel consuman
/// exactamente el mismo orden, encabezados, formatos y datos del Gantt sin leer el
/// <c>DataGridView</c>. El estilo de tabla usa el default neutral compartido
/// <see cref="ReportTableStyle.LegacyCatalogo"/> (encabezado #4A4A6A, bandeado
/// #F5F5F5 y grilla #DDDDDD), la misma línea base de los catálogos y programas.
/// </para>
/// </summary>
public sealed class ProgramaObraReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "ProgramaObra";

    // Identificadores internos canónicos de las columnas del Programa de Obra.
    public const string ColOrden = "colOrden";
    public const string ColClave = "colClave";
    public const string ColDescripcion = "colDescripcion";
    public const string ColUnidad = "colUnidad";
    public const string ColPredecesora = "colPredecesora";
    public const string ColCantidad = "colCantidad";
    public const string ColFechaInicio = "colFechaInicio";
    public const string ColFechaFin = "colFechaFin";
    public const string ColDuracionDias = "colDuracionDias";
    public const string ColRendimientoDiario = "colRendimientoDiario";
    public const string ColFrentes = "colFrentes";
    public const string ColPrecioUnitario = "colPrecioUnitario";
    public const string ColImporte = "colImporte";
    public const string ColRutaCritica = "colRutaCritica";

    private static readonly ReportTableStyle Estilo = ReportTableStyle.LegacyCatalogo();

    /// <summary>
    /// Columnas que el grid legacy mostraba en blanco para las filas de agrupador
    /// (resumen): cantidad, rendimiento, precio unitario, importe y frentes.
    /// </summary>
    private static readonly HashSet<string> ColumnasVaciasEnResumen = new(StringComparer.OrdinalIgnoreCase)
    {
        ColCantidad, ColRendimientoDiario, ColPrecioUnitario, ColImporte, ColFrentes
    };

    /// <summary>
    /// Construye el snapshot desde las columnas persistidas del proyecto y los
    /// decimales configurados en <paramref name="proyecto"/>.
    /// </summary>
    public ReportColumnSnapshot Build(Proyecto proyecto, string? titulo, IEnumerable<ColumnaProgramaObra>? columnas)
    {
        ArgumentNullException.ThrowIfNull(proyecto);
        return Build(
            proyecto.Id, titulo, columnas,
            proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);
    }

    /// <summary>
    /// Construye el snapshot desde las columnas persistidas. Si no hay columnas se
    /// usan las predeterminadas en memoria (sin persistir ni llamar a SaveChanges).
    /// </summary>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaProgramaObra>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var definiciones = ConstruirDefiniciones(columnas);

        return new ReportColumnSnapshot(
            TipoReporte: TipoReporte,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: definiciones,
            EstiloTabla: Estilo)
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };
    }

    /// <summary>Columnas neutrales predeterminadas del Programa de Obra, en memoria.</summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas()
            .Select(ReportColumnDefinitionMapper.MapearProgramaObra)
            .ToList();

    /// <summary>
    /// Materializa las filas neutrales de actividad desde los datos neutrales del
    /// programa, correlacionando cada una con su fila del
    /// <see cref="GanttRenderModel"/> (inicio/fin/crítica/segmentos). Reproduce el
    /// vaciado de celdas que el grid aplicaba a los agrupadores.
    /// </summary>
    public IReadOnlyList<ProgramaRow> ConstruirFilas(
        IEnumerable<ActivityGridRowDto>? actividades,
        GanttRenderModel? gantt)
    {
        var resultado = new List<ProgramaRow>();
        if (actividades == null)
            return resultado;

        var filasGantt = (gantt?.Filas ?? new List<GanttRowDto>())
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var actividad in actividades)
        {
            if (actividad == null)
                continue;

            var fila = new ProgramaRow
            {
                Kind = ProgramaRowKind.Actividad,
                ItemId = actividad.Id,
                Nivel = actividad.Nivel,
                Orden = actividad.Orden,
                EsResumen = actividad.EsResumen
            };

            if (filasGantt.TryGetValue(actividad.Id, out var ganttFila))
            {
                fila.Inicio = ganttFila.Inicio;
                fila.Fin = ganttFila.Fin;
                fila.EsCritica = ganttFila.EsCritica;
                fila.SegmentosFinancieros = ganttFila.SegmentosFinancieros ?? new List<GanttPeriodSegmentDto>();
            }

            fila.Textos[ColOrden] = actividad.Orden.ToString(CultureInfo.CurrentCulture);
            fila.Textos[ColClave] = actividad.Clave ?? string.Empty;
            fila.Textos[ColDescripcion] = actividad.Descripcion ?? string.Empty;
            fila.Textos[ColUnidad] = actividad.Unidad ?? string.Empty;
            fila.Textos[ColPredecesora] = actividad.PredecesoraResumen ?? string.Empty;
            fila.Numeros[ColCantidad] = actividad.CantidadTotal;
            fila.Textos[ColFechaInicio] = FormatearFecha(actividad.FechaInicioProgramada);
            fila.Textos[ColFechaFin] = FormatearFecha(actividad.FechaFinProgramada);
            fila.Numeros[ColDuracionDias] = actividad.DuracionDiasHabiles;
            fila.Numeros[ColRendimientoDiario] = actividad.RendimientoDiario;
            fila.Numeros[ColFrentes] = actividad.FrentesTrabajo;
            fila.Numeros[ColPrecioUnitario] = actividad.PrecioUnitario;
            fila.Numeros[ColImporte] = actividad.ImporteTotal;
            fila.Textos[ColRutaCritica] = actividad.RutaCritica ? "Sí" : "No";

            if (actividad.EsResumen)
                VaciarColumnasDeResumen(fila);

            resultado.Add(fila);
        }

        return resultado;
    }

    /// <summary>Columnas de periodo neutrales derivadas de la escala del Gantt.</summary>
    public static IReadOnlyList<ProgramaPeriodColumn> ConstruirPeriodos(GanttRenderModel? gantt)
        => ProgramaPeriodColumn.FromEscala(gantt?.Escala);

    /// <summary>
    /// Construye la carga neutral completa (filas + columnas de periodo) que
    /// consumen las rutas PDF y Excel.
    /// </summary>
    public ProgramaReportData ConstruirDatos(
        IEnumerable<ActivityGridRowDto>? actividades,
        GanttRenderModel? gantt)
        => new()
        {
            Filas = ConstruirFilas(actividades, gantt),
            Periodos = ConstruirPeriodos(gantt)
        };

    private static void VaciarColumnasDeResumen(ProgramaRow fila)
    {
        foreach (var identificador in ColumnasVaciasEnResumen)
            fila.Numeros.Remove(identificador);
    }

    private static string FormatearFecha(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;

    private static List<ReportColumnDefinition> ConstruirDefiniciones(IEnumerable<ColumnaProgramaObra>? columnas)
    {
        var lista = columnas?.Where(c => c != null).ToList();
        if (lista == null || lista.Count == 0)
            return DefaultColumns().OrderBy(c => c.Orden).ToList();

        return lista
            .OrderBy(c => c.Orden)
            .Select(ReportColumnDefinitionMapper.MapearProgramaObra)
            .ToList();
    }

    /// <summary>
    /// Definiciones predeterminadas en memoria del Programa de Obra (mismas que
    /// persiste <c>ColumnasProgramaObraHelper</c> la primera vez). No escriben en la
    /// base de datos.
    /// </summary>
    public static IReadOnlyList<ColumnaProgramaObra> ColumnasPredeterminadas() => new List<ColumnaProgramaObra>
    {
        Col(ColOrden, "Orden", 60, 1, AlineacionColumna.Centro),
        Col(ColClave, "Clave", 110, 2, AlineacionColumna.Centro),
        Col(ColDescripcion, "Descripción", 360, 3, AlineacionColumna.Izquierda),
        Col(ColUnidad, "Unidad", 70, 4, AlineacionColumna.Centro),
        Col(ColPredecesora, "Predecesora", 140, 5, AlineacionColumna.Izquierda),
        Col(ColCantidad, "Cantidad", 90, 6, AlineacionColumna.Derecha),
        Col(ColFechaInicio, "Inicio", 95, 7, AlineacionColumna.Centro),
        Col(ColFechaFin, "Fin", 95, 8, AlineacionColumna.Centro),
        Col(ColDuracionDias, "Días hábiles", 90, 9, AlineacionColumna.Derecha),
        Col(ColRendimientoDiario, "Rend. diario", 95, 10, AlineacionColumna.Derecha),
        Col(ColFrentes, "Frentes", 70, 11, AlineacionColumna.Derecha),
        Col(ColPrecioUnitario, "P.U.", 90, 12, AlineacionColumna.Derecha),
        Col(ColImporte, "Importe", 105, 13, AlineacionColumna.Derecha),
        Col(ColRutaCritica, "Crítica", 70, 14, AlineacionColumna.Centro),
    };

    private static ColumnaProgramaObra Col(
        string nombreInterno,
        string nombre,
        int ancho,
        int orden,
        AlineacionColumna alineacion)
        => new()
        {
            NombreInterno = nombreInterno,
            Nombre = nombre,
            AnchoColumna = ancho,
            Orden = orden,
            Alineacion = alineacion,
            Visible = true,
            FormatoNumerico = string.Empty
        };
}
