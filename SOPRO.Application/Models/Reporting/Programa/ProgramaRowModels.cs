using System;
using System.Collections.Generic;
using SOPRO.Application.DTOs.Programacion;

namespace SOPRO.Application.Models.Reporting.Programa;

/// <summary>
/// Tipo de fila de un reporte de Programa (Obra/Insumos). Permite que el mismo
/// modelo neutral describa bandas de periodo, filas de actividad y filas de total.
/// </summary>
public enum ProgramaRowKind
{
    /// <summary>Banda de periodo.</summary>
    Periodo,

    /// <summary>Fila de actividad (puede ser agrupador/resumen).</summary>
    Actividad,

    /// <summary>Fila de total.</summary>
    Total
}

/// <summary>
/// Fila neutral de un reporte de Programa. Los valores viajan crudos: los textos
/// (texto, fecha y booleano ya materializados) en <see cref="Textos"/> y los
/// numéricos en <see cref="Numeros"/>, de modo que cada renderizador aplique el
/// formato del contrato neutral (<c>ReportColumnGridFormat</c>) sin depender de la
/// cuadrícula viva. El Gantt viaja ya correlacionado por fila
/// (<see cref="Inicio"/>/<see cref="Fin"/>/<see cref="EsCritica"/> y
/// <see cref="SegmentosFinancieros"/>), eliminando la correlación grid↔Gantt por
/// <c>ItemId</c> de los generadores legacy. No depende de UI ni de renderizado.
/// </summary>
public sealed class ProgramaRow
{
    /// <summary>Tipo de fila.</summary>
    public ProgramaRowKind Kind { get; set; } = ProgramaRowKind.Actividad;

    /// <summary>Identificador de la actividad (nulo para bandas/totales).</summary>
    public int? ItemId { get; set; }

    /// <summary>Nivel de jerarquía de la actividad (0 = raíz).</summary>
    public int Nivel { get; set; }

    /// <summary>Orden de la actividad en el programa.</summary>
    public int Orden { get; set; }

    /// <summary>Indica que la actividad es un agrupador calculado.</summary>
    public bool EsResumen { get; set; }

    /// <summary>Indica que la actividad pertenece a la ruta crítica.</summary>
    public bool EsCritica { get; set; }

    /// <summary>Inicio programado (del Gantt).</summary>
    public DateTime? Inicio { get; set; }

    /// <summary>Fin programado (del Gantt).</summary>
    public DateTime? Fin { get; set; }

    /// <summary>Valores textuales ya materializados por identificador de columna.</summary>
    public Dictionary<string, string> Textos { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Valores numéricos crudos por identificador de columna (los formatea el renderizador).</summary>
    public Dictionary<string, decimal> Numeros { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Segmentos financieros del Gantt de esta fila (si aplica).</summary>
    public List<GanttPeriodSegmentDto> SegmentosFinancieros { get; set; } = new();

    /// <summary>Devuelve el texto de la columna, o <c>null</c> si no existe.</summary>
    public string? ObtenerTexto(string? identificador)
        => !string.IsNullOrEmpty(identificador) && Textos.TryGetValue(identificador, out var valor)
            ? valor
            : null;

    /// <summary>Intenta obtener el valor numérico crudo de la columna.</summary>
    public bool TryObtenerNumero(string? identificador, out decimal valor)
    {
        valor = 0m;
        return !string.IsNullOrEmpty(identificador) && Numeros.TryGetValue(identificador!, out valor);
    }
}

/// <summary>
/// Columna neutral de periodo: una celda de la escala del Gantt (etiqueta, grupo
/// y rango de fechas). Reutilizable por los reportes de Programa para componer su
/// encabezado temporal sin leer el control visual.
/// </summary>
public sealed class ProgramaPeriodColumn
{
    /// <summary>Etiqueta corta del periodo.</summary>
    public string Etiqueta { get; set; } = string.Empty;

    /// <summary>Etiqueta de la agrupación superior (ej. año o trimestre).</summary>
    public string Grupo { get; set; } = string.Empty;

    /// <summary>Fecha inicial del periodo.</summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>Fecha final del periodo.</summary>
    public DateTime FechaFin { get; set; }

    /// <summary>Proyecta una celda de escala del Gantt al modelo neutral.</summary>
    public static ProgramaPeriodColumn From(GanttScaleCellDto celda)
    {
        ArgumentNullException.ThrowIfNull(celda);
        return new ProgramaPeriodColumn
        {
            Etiqueta = celda.Etiqueta ?? string.Empty,
            Grupo = celda.GrupoEtiqueta ?? string.Empty,
            FechaInicio = celda.FechaInicio,
            FechaFin = celda.FechaFin
        };
    }

    /// <summary>Proyecta la escala completa del Gantt a columnas de periodo neutrales.</summary>
    public static IReadOnlyList<ProgramaPeriodColumn> FromEscala(IEnumerable<GanttScaleCellDto>? escala)
    {
        var resultado = new List<ProgramaPeriodColumn>();
        if (escala == null)
            return resultado;

        foreach (var celda in escala)
            resultado.Add(From(celda));

        return resultado;
    }
}

/// <summary>
/// Carga neutral de un reporte de Programa: las filas materializadas y las
/// columnas de periodo. Es el contrato compartido por las rutas PDF y Excel.
/// </summary>
public sealed class ProgramaReportData
{
    /// <summary>Filas del reporte.</summary>
    public IReadOnlyList<ProgramaRow> Filas { get; set; } = Array.Empty<ProgramaRow>();

    /// <summary>Columnas de periodo (encabezado temporal del Gantt).</summary>
    public IReadOnlyList<ProgramaPeriodColumn> Periodos { get; set; } = Array.Empty<ProgramaPeriodColumn>();
}
