using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Models.Reporting.Programa;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Vista lógica del reporte del Programa de Insumos. Determina el conjunto de
/// columnas que el grid muestra para la vista activa: sólo cantidades, sólo
/// importes o ambas (mixto). Es neutral (no depende de la UI): los formularios
/// mapean su combo de vistas a este contrato.
/// </summary>
public enum ProgramaInsumosVista
{
    /// <summary>Sólo cantidades y acumulados físicos.</summary>
    Cantidades,

    /// <summary>Sólo importes y acumulados financieros.</summary>
    Importes,

    /// <summary>Cantidades e importes a la vez.</summary>
    Mixto
}

/// <summary>
/// Constructor puro del snapshot de columnas y de las filas neutrales del reporte
/// de Programa de Insumos.
///
/// <para>
/// Reutiliza el contrato neutral de los Programas (paso 4.1): arma un
/// <see cref="ReportColumnSnapshot"/> y materializa <see cref="ProgramaRow"/> más
/// el encabezado temporal (<see cref="ProgramaPeriodColumn"/>). No recibe ni lee
/// la cuadrícula viva, no toca <c>DataGridView</c> y no escribe configuración: los
/// defaults de columna se construyen en memoria.
/// </para>
///
/// <para>
/// A diferencia del Programa de Obra, el Programa de Insumos expone las series
/// por período como columnas propias de la tabla izquierda (cantidad, acumulado,
/// importe e importe acumulado), además del encabezado temporal del Gantt. Las
/// columnas dinámicas se derivan de los períodos del programa y se identifican por
/// el <c>PeriodoId</c> (<c>per_</c>, <c>acu_</c>, <c>imp_</c>, <c>iacu_</c>). Los
/// roles numéricos (cantidad vs. importe con el símbolo único <c>$</c>) los resuelve
/// <see cref="ReportColumnGridFormat"/> a partir del snapshot.
/// </para>
/// </summary>
public sealed class ProgramaInsumosReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "ProgramaInsumos";

    // Identificadores canónicos de las columnas estáticas del Programa de Insumos.
    public const string ColClave = "colClave";
    public const string ColDescripcion = "colDescripcion";
    public const string ColUnidad = "colUnidad";
    public const string ColRendimiento = "colRendimiento";
    public const string ColInicio = "colInicio";
    public const string ColTermino = "colTermino";
    public const string ColDondeSeUsa = "colDondeSeUsa";
    public const string ColPrecioUnitario = "colPU";
    public const string ColTotal = "colTotal";
    public const string ColImporteTotal = "colImporteTotal";

    // Prefijos de las columnas dinámicas de período.
    public const string PrefijoPeriodo = "per_";
    public const string PrefijoAcumulado = "acu_";
    public const string PrefijoImporte = "imp_";
    public const string PrefijoImporteAcumulado = "iacu_";

    private static readonly ReportTableStyle Estilo = ReportTableStyle.LegacyCatalogo();

    /// <summary>Identificador de la columna dinámica de cantidad del período.</summary>
    public static string IdentificadorPeriodo(int periodoId) => PrefijoPeriodo + periodoId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Identificador de la columna dinámica de cantidad acumulada del período.</summary>
    public static string IdentificadorAcumulado(int periodoId) => PrefijoAcumulado + periodoId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Identificador de la columna dinámica de importe del período.</summary>
    public static string IdentificadorImporte(int periodoId) => PrefijoImporte + periodoId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Identificador de la columna dinámica de importe acumulado del período.</summary>
    public static string IdentificadorImporteAcumulado(int periodoId) => PrefijoImporteAcumulado + periodoId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Construye el snapshot con los decimales de cantidad/importe/porcentaje
    /// configurados en el proyecto.
    /// </summary>
    public ReportColumnSnapshot Build(
        Proyecto proyecto,
        string? titulo,
        ProgramaInsumosResultDto? programa,
        ProgramaInsumosVista vista)
    {
        ArgumentNullException.ThrowIfNull(proyecto);
        return Build(
            proyecto.Id, titulo, programa, vista,
            proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);
    }

    /// <summary>
    /// Construye el snapshot desde los períodos del programa y la vista activa. Los
    /// defaults se arman en memoria (sin persistir ni llamar a SaveChanges).
    /// </summary>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        ProgramaInsumosResultDto? programa,
        ProgramaInsumosVista vista = ProgramaInsumosVista.Mixto,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new(
            TipoReporte: TipoReporte,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: DefaultColumns(programa, vista),
            EstiloTabla: Estilo)
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };

    /// <summary>
    /// Columnas neutrales del Programa de Insumos para la vista dada, en memoria y
    /// en orden canónico. Incluye las columnas estáticas y las columnas dinámicas
    /// de período derivadas de <see cref="ProgramaInsumosResultDto.Periodos"/>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns(
        ProgramaInsumosResultDto? programa,
        ProgramaInsumosVista vista)
        => ColumnasPredeterminadas(programa, vista)
            .Select(Mapear)
            .OrderBy(c => c.Orden)
            .ToList();

    /// <summary>
    /// Definiciones predeterminadas en memoria del Programa de Insumos (mismas que
    /// el grid construye para el tipo y la vista activos). No escriben en la base
    /// de datos.
    /// </summary>
    public static IReadOnlyList<ColumnaProgramaInsumos> ColumnasPredeterminadas(
        ProgramaInsumosResultDto? programa,
        ProgramaInsumosVista vista)
    {
        var tipo = programa?.Tipo ?? ProgramaInsumoTipo.Materiales;
        var resultado = new List<ColumnaProgramaInsumos>();
        var orden = 0;

        void Agregar(string interno, string nombre, int ancho, AlineacionColumna alineacion, string formato, bool negrita)
            => resultado.Add(Col(interno, nombre, ancho, ++orden, alineacion, formato, negrita));

        Agregar(ColClave, "Clave", 110, AlineacionColumna.Izquierda, string.Empty, false);
        Agregar(ColDescripcion, "Descripción", 420, AlineacionColumna.Izquierda, string.Empty, false);
        Agregar(ColUnidad, "Unidad", 80, AlineacionColumna.Centro, string.Empty, false);
        if (tipo == ProgramaInsumoTipo.Maquinaria)
            Agregar(ColRendimiento, "Rendimiento", 110, AlineacionColumna.Derecha, "N2", false);
        Agregar(ColInicio, "Inicio", 95, AlineacionColumna.Centro, string.Empty, false);
        Agregar(ColTermino, "Término", 95, AlineacionColumna.Centro, string.Empty, false);
        Agregar(ColDondeSeUsa, "Dónde se usa", 180, AlineacionColumna.Izquierda, string.Empty, false);
        if (vista != ProgramaInsumosVista.Cantidades)
            Agregar(ColPrecioUnitario, "P.U.", 90, AlineacionColumna.Derecha, "C2", false);
        Agregar(ColTotal, "Total", 90, AlineacionColumna.Derecha, "N2", false);
        if (vista != ProgramaInsumosVista.Cantidades)
            Agregar(ColImporteTotal, "Importe total", 110, AlineacionColumna.Derecha, "C2", true);

        if (programa?.Periodos != null)
        {
            foreach (var periodo in programa.Periodos.OrderBy(p => p.Orden))
            {
                if (vista != ProgramaInsumosVista.Importes)
                {
                    Agregar(IdentificadorPeriodo(periodo.PeriodoId), periodo.Etiqueta, 105, AlineacionColumna.Derecha, "N2", false);
                    Agregar(IdentificadorAcumulado(periodo.PeriodoId), $"Acum {periodo.Orden:00}", 110, AlineacionColumna.Derecha, "N2", true);
                }
                if (vista != ProgramaInsumosVista.Cantidades)
                {
                    Agregar(IdentificadorImporte(periodo.PeriodoId), $"Imp. {periodo.Orden:00}", 105, AlineacionColumna.Derecha, "C2", false);
                    Agregar(IdentificadorImporteAcumulado(periodo.PeriodoId), $"Imp. acum {periodo.Orden:00}", 115, AlineacionColumna.Derecha, "C2", true);
                }
            }
        }

        return resultado;
    }

    /// <summary>
    /// Materializa las filas neutrales del Programa de Insumos desde el resultado
    /// neutral (<see cref="ProgramaInsumosResultDto"/>) más el
    /// <see cref="GanttRenderModel"/> (inicio/fin/segmentos para las barras). No lee
    /// la cuadrícula viva. Los ceros de las series por período se omiten para
    /// reproducir la celda vacía del grid.
    /// </summary>
    public IReadOnlyList<ProgramaRow> ConstruirFilas(
        ProgramaInsumosResultDto? programa,
        GanttRenderModel? gantt)
    {
        var resultado = new List<ProgramaRow>();
        if (programa?.Rows == null)
            return resultado;

        var filasGantt = (gantt?.Filas ?? new List<GanttRowDto>())
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var insumo in programa.Rows)
        {
            if (insumo == null)
                continue;

            var fila = new ProgramaRow
            {
                Kind = ProgramaRowKind.Actividad,
                ItemId = insumo.InsumoId
            };

            fila.Textos[ColClave] = insumo.Clave ?? string.Empty;
            fila.Textos[ColDescripcion] = insumo.Descripcion ?? string.Empty;
            fila.Textos[ColUnidad] = insumo.Unidad ?? string.Empty;
            fila.Textos[ColInicio] = FormatearFecha(insumo.FechaInicio);
            fila.Textos[ColTermino] = FormatearFecha(insumo.FechaFin);
            fila.Textos[ColDondeSeUsa] = insumo.DondeSeUsa ?? string.Empty;

            if (insumo.Rendimiento > 0m)
                fila.Numeros[ColRendimiento] = insumo.Rendimiento;

            fila.Numeros[ColPrecioUnitario] = insumo.PrecioUnitario;
            fila.Numeros[ColTotal] = insumo.Total;
            fila.Numeros[ColImporteTotal] = insumo.ImporteTotal;

            AgregarSeries(fila.Numeros, PrefijoPeriodo, insumo.CantidadesPorPeriodo);
            AgregarSeries(fila.Numeros, PrefijoAcumulado, insumo.AcumuladosPorPeriodo);
            AgregarSeries(fila.Numeros, PrefijoImporte, insumo.ImportesPorPeriodo);
            AgregarSeries(fila.Numeros, PrefijoImporteAcumulado, insumo.ImportesAcumuladosPorPeriodo);

            if (filasGantt.TryGetValue(insumo.InsumoId, out var filaGantt))
            {
                fila.Inicio = filaGantt.Inicio;
                fila.Fin = filaGantt.Fin;
                fila.EsCritica = filaGantt.EsCritica;
                fila.EsResumen = filaGantt.EsResumen;
                fila.SegmentosFinancieros = filaGantt.SegmentosFinancieros ?? new List<GanttPeriodSegmentDto>();
            }

            resultado.Add(fila);
        }

        return resultado;
    }

    /// <summary>Columnas de período neutrales derivadas de la escala del Gantt.</summary>
    public static IReadOnlyList<ProgramaPeriodColumn> ConstruirPeriodos(GanttRenderModel? gantt)
        => ProgramaPeriodColumn.FromEscala(gantt?.Escala);

    /// <summary>
    /// Construye la carga neutral completa (filas + columnas de período) que
    /// consumen las rutas PDF y Excel.
    /// </summary>
    public ProgramaReportData ConstruirDatos(
        ProgramaInsumosResultDto? programa,
        GanttRenderModel? gantt)
        => new()
        {
            Filas = ConstruirFilas(programa, gantt),
            Periodos = ConstruirPeriodos(gantt)
        };

    private static void AgregarSeries(
        Dictionary<string, decimal> destino,
        string prefijo,
        Dictionary<int, decimal>? serie)
    {
        if (serie == null)
            return;

        foreach (var (periodoId, valor) in serie)
        {
            if (valor == 0m)
                continue;
            destino[prefijo + periodoId.ToString(CultureInfo.InvariantCulture)] = valor;
        }
    }

    private static string FormatearFecha(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) : string.Empty;

    private static ReportColumnDefinition Mapear(ColumnaProgramaInsumos columna)
    {
        var definicion = ReportColumnDefinitionMapper.MapearProgramaInsumos(columna);

        // La columna "Total" del Programa de Insumos es la cantidad total del insumo
        // (suma de cantidades), no un importe: el grid la pinta con decimales de
        // cantidad y sin símbolo de moneda. El reconocimiento por nombre del mapper
        // la marcaría monetaria (p. ej. Financiamiento); aquí se corrige el rol.
        if (string.Equals(definicion.Identificador, ColTotal, StringComparison.OrdinalIgnoreCase))
            definicion = definicion with { EsMoneda = false };

        return definicion;
    }

    private static ColumnaProgramaInsumos Col(
        string nombreInterno,
        string nombre,
        int ancho,
        int orden,
        AlineacionColumna alineacion,
        string formato,
        bool negrita)
        => new()
        {
            NombreInterno = nombreInterno,
            Nombre = nombre,
            AnchoColumna = ancho,
            Orden = orden,
            Alineacion = alineacion,
            FormatoNumerico = formato,
            Negrita = negrita,
            Visible = true
        };
}
