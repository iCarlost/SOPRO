using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Programa de Obra. Construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde las columnas
/// predeterminadas en memoria del builder neutral (sin EF, sin UI, sin WinForms) y
/// son la única fuente del golden del contrato neutral de columnas
/// (<c>programa-obra-columnas.json</c>).
///
/// Punto diferido: en <c>SOPRO.Reporting</c> no existe un renderizador neutral de
/// Programa de Obra; los de WinForms no son invocables desde este proyecto, así que
/// la paridad byte-level queda para verificación posterior (igual que el piloto y F1).
/// </summary>
internal static class ProgramaObraReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.programa-obra-columnas.1";

    /// <summary>Proyecto sintético del reporte de Programa de Obra.</summary>
    public const int ProyectoId = 31;

    /// <summary>
    /// Snapshot del Programa de Obra con las catorce columnas canónicas
    /// (descripción y claves de texto, cantidad, fechas, días/rendimiento/frentes,
    /// precio unitario e importe monetarios y la marca de ruta crítica). Usa el
    /// estilo neutral <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot ProgramaObra(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var columnas = ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas();
        return new ProgramaObraReportSnapshotBuilder().Build(
            ProyectoId, "Programa de Obra Sintético", columnas,
            decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden.
    /// </summary>
    public static ProgramaObraSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProgramaObraSnapshotDto
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
            EstiloTabla = new ProgramaObraEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new ProgramaObraFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new ProgramaObraBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static ProgramaObraColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static ProgramaObraEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

/// <summary>Proyección serializable y estable del snapshot del Programa de Obra.</summary>
internal sealed class ProgramaObraSnapshotDto
{
    public string Schema { get; set; } = ProgramaObraReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public int DecimalesCantidad { get; set; }
    public int DecimalesImporte { get; set; }
    public int DecimalesPorcentaje { get; set; }
    public List<ProgramaObraColumnaDto> Columnas { get; set; } = new();
    public ProgramaObraEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada del Programa de Obra.</summary>
internal sealed class ProgramaObraColumnaDto
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
    public ProgramaObraEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public ProgramaObraEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado del Programa de Obra.</summary>
internal sealed class ProgramaObraEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado del Programa de Obra.</summary>
internal sealed class ProgramaObraEstiloTablaDto
{
    public ProgramaObraEstiloTextoDto Encabezado { get; set; } = new();
    public ProgramaObraEstiloTextoDto Contenido { get; set; } = new();
    public ProgramaObraFilaAlternaDto FilaAlterna { get; set; } = new();
    public ProgramaObraBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado del Programa de Obra.</summary>
internal sealed class ProgramaObraFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado del Programa de Obra.</summary>
internal sealed class ProgramaObraBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
