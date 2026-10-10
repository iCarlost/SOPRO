using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Programa de Insumos. Construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde los períodos sintéticos
/// del programa (sin EF, sin UI, sin WinForms) y son la única fuente del golden del
/// contrato neutral de columnas (<c>programa-insumos-columnas.json</c>).
///
/// A diferencia del Programa de Obra, el Programa de Insumos expone las series por
/// período como columnas de la tabla izquierda (cantidad, acumulado, importe e
/// importe acumulado), además del encabezado temporal del Gantt.
/// </summary>
internal static class ProgramaInsumosReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.programa-insumos-columnas.1";

    /// <summary>Proyecto sintético del reporte de Programa de Insumos.</summary>
    public const int ProyectoId = 41;

    /// <summary>Períodos sintéticos del programa (dos períodos mensuales).</summary>
    public static ProgramaInsumosResultDto Programa() => new()
    {
        NombrePrograma = "Programa de Insumos Sintético",
        Tipo = ProgramaInsumoTipo.Materiales,
        Periodos =
        {
            new ProgramaInsumoPeriodoDto { PeriodoId = 501, Orden = 1, Etiqueta = "Ene" },
            new ProgramaInsumoPeriodoDto { PeriodoId = 502, Orden = 2, Etiqueta = "Feb" },
        }
    };

    /// <summary>
    /// Snapshot del Programa de Insumos para la vista mixta: columnas estáticas
    /// (clave, descripción, unidad, fechas, uso, precio unitario, total e importe
    /// total) más las series dinámicas por período. Usa el estilo neutral
    /// <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot ProgramaInsumos(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new ProgramaInsumosReportSnapshotBuilder().Build(
            ProyectoId,
            "Programa de Insumos Sintético",
            Programa(),
            ProgramaInsumosVista.Mixto,
            decimalesCantidad,
            decimalesImporte,
            decimalesPorcentaje);

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden.
    /// </summary>
    public static ProgramaInsumosSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProgramaInsumosSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            DecimalesCantidad = snapshot.DecimalesCantidad,
            DecimalesImporte = snapshot.DecimalesImporte,
            DecimalesPorcentaje = snapshot.DecimalesPorcentaje,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new ProgramaInsumosEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new ProgramaInsumosFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new ProgramaInsumosBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static ProgramaInsumosColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
    {
        Identificador = columna.Identificador,
        Encabezado = columna.Encabezado,
        Visible = columna.Visible,
        Orden = columna.Orden,
        Ancho = columna.Ancho,
        Formato = columna.FormatoNumerico,
        EsNumerica = columna.EsNumerica,
        EsMoneda = columna.EsMoneda,
        Alineacion = columna.Alineacion.ToString(),
        AlineacionVertical = columna.AlineacionVertical.ToString(),
        Wrap = columna.Wrap,
        EstiloEncabezado = ProyectarEstilo(columna.EstiloEncabezado),
        EstiloContenido = ProyectarEstilo(columna.EstiloContenido),
    };

    private static ProgramaInsumosEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
    {
        Fuente = estilo.Fuente,
        Tamano = estilo.Tamano,
        Negrita = estilo.Negrita,
        Cursiva = estilo.Cursiva,
        ColorFuente = estilo.ColorFuente,
        ColorFondo = estilo.ColorFondo,
        ColorFuenteEncabezado = estilo.ColorFuenteEncabezado,
        ColorFondoEncabezado = estilo.ColorFondoEncabezado,
    };
}

/// <summary>Proyección serializable y estable del snapshot del Programa de Insumos.</summary>
internal sealed class ProgramaInsumosSnapshotDto
{
    public string Schema { get; set; } = ProgramaInsumosReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public int DecimalesCantidad { get; set; }
    public int DecimalesImporte { get; set; }
    public int DecimalesPorcentaje { get; set; }
    public List<ProgramaInsumosColumnaDto> Columnas { get; set; } = new();
    public ProgramaInsumosEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada del Programa de Insumos.</summary>
internal sealed class ProgramaInsumosColumnaDto
{
    public string Identificador { get; set; } = "";
    public string Encabezado { get; set; } = "";
    public bool Visible { get; set; }
    public int Orden { get; set; }
    public int Ancho { get; set; }
    public string Formato { get; set; } = "";
    public bool EsNumerica { get; set; }
    public bool EsMoneda { get; set; }
    public string Alineacion { get; set; } = "";
    public string AlineacionVertical { get; set; } = "";
    public bool Wrap { get; set; }
    public ProgramaInsumosEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public ProgramaInsumosEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado del Programa de Insumos.</summary>
internal sealed class ProgramaInsumosEstiloTextoDto
{
    public string Fuente { get; set; } = "";
    public float Tamano { get; set; }
    public bool Negrita { get; set; }
    public bool Cursiva { get; set; }
    public string ColorFuente { get; set; } = "";
    public string? ColorFondo { get; set; }
    public string? ColorFuenteEncabezado { get; set; }
    public string? ColorFondoEncabezado { get; set; }
}

/// <summary>Estilo de tabla aplanado del Programa de Insumos.</summary>
internal sealed class ProgramaInsumosEstiloTablaDto
{
    public ProgramaInsumosEstiloTextoDto Encabezado { get; set; } = new();
    public ProgramaInsumosEstiloTextoDto Contenido { get; set; } = new();
    public ProgramaInsumosFilaAlternaDto FilaAlterna { get; set; } = new();
    public ProgramaInsumosBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado del Programa de Insumos.</summary>
internal sealed class ProgramaInsumosFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado del Programa de Insumos.</summary>
internal sealed class ProgramaInsumosBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
