using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Cálculo de Indirectos que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de
/// columnas de indirectos y de los tests de paridad semántica PDF/Excel del
/// reporte, siguiendo el mismo patrón que <see cref="HerramientasReportFixtures"/>.
///
/// Punto diferido: en <c>SOPRO.Reporting</c> no existe un renderizador neutral de
/// Indirectos; el de WinForms no es invocable desde este proyecto, así que la
/// paridad byte-level de los renderers queda para verificación posterior.
/// </summary>
internal static class IndirectosReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.indirectos-columnas.1";

    /// <summary>Proyecto sintético del reporte.</summary>
    public const int ProyectoId = 7;

    /// <summary>
    /// Snapshot del reporte de Indirectos: columnas canónicas (Grupo/Concepto,
    /// Importe Mensual, Duración e Importe Total) más dos columnas internas que
    /// deben excluirse. Usa el estilo neutral
    /// <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot Indirectos(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: -1),
            Columna("Grupo", "Grupo / Concepto", orden: 0, ancho: 400, alineacion: AlineacionColumna.Izquierda),
            Columna("ImporteMensual", "Importe Mensual $", orden: 1, ancho: 180, alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("Duracion", "Duración (Meses)", orden: 2, ancho: 150, alineacion: AlineacionColumna.Centro),
            Columna("ImporteTotal", "Importe Total $", orden: 3, ancho: 180, alineacion: AlineacionColumna.Derecha, formato: "N2"),
            Columna("colRelleno", "", orden: 99),
        };

        return IndirectosExportResolver.BuildSnapshot(
            ProyectoId,
            "Análisis de Costos Indirectos Sintético",
            columnas,
            decimalesCantidad,
            decimalesImporte,
            decimalesPorcentaje);
    }

    /// <summary>
    /// Snapshot de los defaults neutrales (sin configuración persistida). Sirve
    /// para verificar que el reporte arranca con las columnas del catálogo.
    /// </summary>
    public static ReportColumnSnapshot Defaults()
        => IndirectosExportResolver.BuildSnapshot(ProyectoId, "Análisis de Costos Indirectos", columnas: null);

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden:
    /// identificador, encabezado, visible, orden, ancho, formato, rol numérico,
    /// alineación, wrap y estilo resumido de la tabla.
    /// </summary>
    public static IndirectosSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new IndirectosSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new IndirectosEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new IndirectosFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new IndirectosBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static IndirectosColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static IndirectosEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

    private static ColumnaIndirectos Columna(
        string nombreInterno,
        string nombre,
        int orden,
        int ancho = 100,
        bool visible = true,
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
            Visible = visible,
            Alineacion = alineacion,
            WrapTexto = wrap,
            FormatoNumerico = formato ?? string.Empty,
        };
}

/// <summary>Proyección serializable y estable de un <see cref="ReportColumnSnapshot"/> de indirectos.</summary>
internal sealed class IndirectosSnapshotDto
{
    public string Schema { get; set; } = IndirectosReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public List<IndirectosColumnaDto> Columnas { get; set; } = new();
    public IndirectosEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada de indirectos.</summary>
internal sealed class IndirectosColumnaDto
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
    public IndirectosEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public IndirectosEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado de indirectos.</summary>
internal sealed class IndirectosEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado de indirectos.</summary>
internal sealed class IndirectosEstiloTablaDto
{
    public IndirectosEstiloTextoDto Encabezado { get; set; } = new();
    public IndirectosEstiloTextoDto Contenido { get; set; } = new();
    public IndirectosFilaAlternaDto FilaAlterna { get; set; } = new();
    public IndirectosBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado de indirectos.</summary>
internal sealed class IndirectosFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado de indirectos.</summary>
internal sealed class IndirectosBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
