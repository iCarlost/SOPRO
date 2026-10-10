using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de APU (Análisis de Precios Unitarios) que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de columnas
/// del APU y de los tests de paridad semántica PDF/Excel.
///
/// Usa un esquema propio (<see cref="ApuSnapshotDto"/>) con el rol monetario
/// explícito para que el golden <c>apu-columnas.json</c> sea independiente de los
/// del piloto y de la Fase 1.
/// </summary>
internal static class ApuReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.apu-columnas.1";

    /// <summary>Proyecto sintético del reporte de APU.</summary>
    public const int ProyectoId = 13;

    /// <summary>
    /// Snapshot del APU: siete columnas canónicas (Tipo, Clave, Descripción,
    /// Unidad, Cantidad, Costo Unit. e Importe) más el relleno de grid
    /// <c>colRelleno</c> que debe excluirse. La Cantidad es rol de cantidad
    /// (N2) y el Costo Unit./Importe son monetarios. Usa el estilo neutral
    /// <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot Apu()
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: 0, ancho: 60, alineacion: AlineacionColumna.Centro),
            Columna("Clave", "Clave", orden: 1, ancho: 80, alineacion: AlineacionColumna.Centro),
            Columna("Descripcion", "Descripción", orden: 2, ancho: 250, wrap: true),
            Columna("Unidad", "Unidad", orden: 3, ancho: 55, alineacion: AlineacionColumna.Centro),
            Columna("Cantidad", "Cantidad", orden: 4, ancho: 80, tipo: TipoDatoColumna.Numerico,
                alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("PrecioUnitario", "Costo Unit.", orden: 5, ancho: 90, tipo: TipoDatoColumna.Moneda,
                alineacion: AlineacionColumna.Derecha),
            Columna("ImporteTotal", "Importe", orden: 6, ancho: 100, tipo: TipoDatoColumna.Moneda,
                alineacion: AlineacionColumna.Derecha),
            Columna("colRelleno", "", orden: 99),
        };

        return new ApuReportSnapshotBuilder().Build(
            proyectoId: ProyectoId,
            titulo: "Análisis de Precios Unitarios Sintético",
            columnas: columnas,
            decimalesCantidad: 5,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);
    }

    /// <summary>
    /// Snapshot de los defaults en memoria (sin configuración persistida). Sirve
    /// para verificar que el reporte arranca con las siete columnas del APU.
    /// </summary>
    public static ReportColumnSnapshot Defaults()
        => new ApuReportSnapshotBuilder().Build(ProyectoId, "Análisis de Precios Unitarios", columnas: null);

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden:
    /// identificador, encabezado, visible, orden, ancho, formato, rol numérico
    /// (incluido el rol monetario), alineación, wrap y estilo resumido de la tabla.
    /// </summary>
    public static ApuSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ApuSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new ApuEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new ApuFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new ApuBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static ApuColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static ApuEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

    private static ColumnaPersonalizada Columna(
        string nombreInterno,
        string nombre,
        int orden,
        int ancho = 100,
        TipoDatoColumna tipo = TipoDatoColumna.Texto,
        AlineacionColumna alineacion = AlineacionColumna.Izquierda,
        bool wrap = false,
        string? formato = null)
        => new()
        {
            ProyectoId = ProyectoId,
            NombreInterno = nombreInterno,
            Nombre = nombre,
            Orden = orden,
            AnchoColumna = ancho,
            TipoDato = tipo,
            Alineacion = alineacion,
            WrapTexto = wrap,
            FormatoNumerico = formato ?? string.Empty,
        };
}

/// <summary>Proyección serializable y estable de un <see cref="ReportColumnSnapshot"/> de APU.</summary>
internal sealed class ApuSnapshotDto
{
    public string Schema { get; set; } = ApuReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public List<ApuColumnaDto> Columnas { get; set; } = new();
    public ApuEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada del APU.</summary>
internal sealed class ApuColumnaDto
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
    public ApuEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public ApuEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado del APU.</summary>
internal sealed class ApuEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado del APU.</summary>
internal sealed class ApuEstiloTablaDto
{
    public ApuEstiloTextoDto Encabezado { get; set; } = new();
    public ApuEstiloTextoDto Contenido { get; set; } = new();
    public ApuFilaAlternaDto FilaAlterna { get; set; } = new();
    public ApuBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado del APU.</summary>
internal sealed class ApuFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado del APU.</summary>
internal sealed class ApuBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
