using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures de los reportes del Factor de Salario Real (FSR): AE-2(A) tabla de
/// cálculo y AE-2(C) tabulador por insumo. Construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde el builder neutral
/// (sin EF, sin UI, sin WinForms) y son la única fuente del golden del contrato
/// neutral de columnas (<c>fsr-columnas.json</c>).
///
/// Punto diferido: en <c>SOPRO.Reporting</c> no existe un renderizador neutral de
/// FSR; los de WinForms no son invocables desde este proyecto, así que la paridad
/// byte-level queda para verificación posterior (igual que el piloto y F1).
/// </summary>
internal static class FsrReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.fsr-columnas.1";

    /// <summary>Proyecto sintético del reporte FSR.</summary>
    public const int ProyectoId = 21;

    /// <summary>Snapshot del AE-2(A) (tabla de cálculo del FSR).</summary>
    public static ReportColumnSnapshot AE2A(
        int decimalesCantidad = FsrReportSnapshotBuilder.DecimalesCantidadFsr,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new FsrReportSnapshotBuilder().BuildAE2A(
            ProyectoId, "FSR Sintético", decimalesCantidad, decimalesImporte, decimalesPorcentaje);

    /// <summary>Snapshot del AE-2(C) (tabulador desglosado por insumo).</summary>
    public static ReportColumnSnapshot AE2C(
        int decimalesCantidad = FsrReportSnapshotBuilder.DecimalesCantidadFsr,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new FsrReportSnapshotBuilder().BuildAE2C(
            ProyectoId, "Tabulador FSR Sintético", decimalesCantidad, decimalesImporte, decimalesPorcentaje);

    /// <summary>
    /// Proyección semántica estable de ambos snapshots para versionar como golden.
    /// </summary>
    public static FsrSnapshotDto Proyectar(ReportColumnSnapshot ae2a, ReportColumnSnapshot ae2c)
    {
        ArgumentNullException.ThrowIfNull(ae2a);
        ArgumentNullException.ThrowIfNull(ae2c);

        return new FsrSnapshotDto
        {
            Schema = Schema,
            AE2A = ProyectarTabla(ae2a),
            AE2C = ProyectarTabla(ae2c),
        };
    }

    private static FsrTablaDto ProyectarTabla(ReportColumnSnapshot snapshot) => new()
    {
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
        EstiloTabla = new FsrEstiloTablaDto
        {
            Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
            Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
            FilaAlterna = new FsrFilaAlternaDto
            {
                ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
            },
            Bordes = new FsrBordeDto
            {
                Visible = snapshot.EstiloTabla.Bordes.Visible,
                ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
            },
        },
    };

    private static FsrColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static FsrEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

/// <summary>Proyección serializable y estable de los snapshots del FSR.</summary>
internal sealed class FsrSnapshotDto
{
    public string Schema { get; set; } = FsrReportFixtures.Schema;
    public FsrTablaDto AE2A { get; set; } = new();
    public FsrTablaDto AE2C { get; set; } = new();
}

/// <summary>Tabla aplanada del FSR (AE-2A o AE-2C).</summary>
internal sealed class FsrTablaDto
{
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public int DecimalesCantidad { get; set; }
    public int DecimalesImporte { get; set; }
    public int DecimalesPorcentaje { get; set; }
    public List<FsrColumnaDto> Columnas { get; set; } = new();
    public FsrEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada del FSR.</summary>
internal sealed class FsrColumnaDto
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
    public FsrEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public FsrEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado del FSR.</summary>
internal sealed class FsrEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado del FSR.</summary>
internal sealed class FsrEstiloTablaDto
{
    public FsrEstiloTextoDto Encabezado { get; set; } = new();
    public FsrEstiloTextoDto Contenido { get; set; } = new();
    public FsrFilaAlternaDto FilaAlterna { get; set; } = new();
    public FsrBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado del FSR.</summary>
internal sealed class FsrFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado del FSR.</summary>
internal sealed class FsrBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
