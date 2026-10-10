using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Catálogo de Herramientas que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de
/// herramientas y de los tests de paridad semántica PDF/Excel, siguiendo el mismo
/// patrón que <see cref="PilotoReportesFixtures"/>.
///
/// Punto diferido: en <c>SOPRO.Reporting</c> no existe un renderizador neutral de
/// Herramientas; el de WinForms no es invocable desde este proyecto, así que la
/// paridad byte-level queda para verificación posterior (igual que el piloto).
/// </summary>
internal static class HerramientasReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.herramientas-columnas.1";

    /// <summary>Proyecto sintético del reporte de herramientas.</summary>
    public const int ProyectoId = 7;

    /// <summary>
    /// Snapshot del catálogo de herramientas: columnas canónicas (el Precio/
    /// Porcentaje con rol monetario, textos y Origen) y dos columnas internas que
    /// deben excluirse. Usa el estilo neutral
    /// <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot Herramientas(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: -1),
            Columna("Clave", "Clave", orden: 0, ancho: 110, alineacion: AlineacionColumna.Centro),
            Columna("Descripcion", "Descripción", orden: 1, ancho: 300, wrap: true),
            Columna("Unidad", "Unidad", orden: 2, ancho: 80, alineacion: AlineacionColumna.Centro),
            Columna("PrecioUnitario", "Precio/Porcentaje", orden: 3, ancho: 150, alineacion: AlineacionColumna.Derecha),
            Columna("OrigenDetalle", "Origen", orden: 4, ancho: 180),
            Columna("colRelleno", "", orden: 99),
        };

        return new HerramientasReportSnapshotBuilder().Build(
            ProyectoId, "Catálogo de Herramientas Sintético", columnas,
            decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden:
    /// identificador, encabezado, visible, orden, ancho, formato, rol numérico,
    /// alineación, wrap y estilo resumido de la tabla.
    /// </summary>
    public static HerramientasSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new HerramientasSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new HerramientasEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new HerramientasFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new HerramientasBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static HerramientasColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static HerramientasEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

    private static ColumnaHerramienta Columna(
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

/// <summary>Proyección serializable y estable de un <see cref="ReportColumnSnapshot"/> de herramientas.</summary>
internal sealed class HerramientasSnapshotDto
{
    public string Schema { get; set; } = HerramientasReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public List<HerramientasColumnaDto> Columnas { get; set; } = new();
    public HerramientasEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada de herramientas.</summary>
internal sealed class HerramientasColumnaDto
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
    public HerramientasEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public HerramientasEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado de herramientas.</summary>
internal sealed class HerramientasEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado de herramientas.</summary>
internal sealed class HerramientasEstiloTablaDto
{
    public HerramientasEstiloTextoDto Encabezado { get; set; } = new();
    public HerramientasEstiloTextoDto Contenido { get; set; } = new();
    public HerramientasFilaAlternaDto FilaAlterna { get; set; } = new();
    public HerramientasBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado de herramientas.</summary>
internal sealed class HerramientasFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado de herramientas.</summary>
internal sealed class HerramientasBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
