using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Catálogo de Mano de Obra que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de mano de
/// obra y de los tests de paridad semántica PDF/Excel, siguiendo el mismo patrón
/// que <see cref="PilotoReportesFixtures"/>.
///
/// Punto diferido: en <c>SOPRO.Reporting</c> no existe un renderizador neutral de
/// Mano de Obra; el de WinForms no es invocable desde este proyecto, así que la
/// paridad byte-level queda para verificación posterior (igual que el piloto).
/// </summary>
internal static class ManoObraReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.mano-obra-columnas.1";

    /// <summary>Proyecto sintético del reporte de mano de obra.</summary>
    public const int ProyectoId = 11;

    /// <summary>
    /// Snapshot del catálogo de mano de obra: siete columnas canónicas (monetarias
    /// C2, FSR N4 y textos), una columna oculta y dos columnas internas que deben
    /// excluirse. Usa el estilo neutral <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot ManoObra(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: -1),
            Columna("Clave", "Clave", orden: 0, ancho: 90, alineacion: AlineacionColumna.Centro),
            Columna("Descripcion", "Descripción", orden: 1, ancho: 260, wrap: true),
            Columna("Unidad", "Unidad", orden: 2, ancho: 60, alineacion: AlineacionColumna.Centro),
            Columna("SalarioBase", "Salario Base", orden: 3, ancho: 120, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("FactorSalarioReal", "FSR", orden: 4, ancho: 80, alineacion: AlineacionColumna.Derecha, formato: "N4"),
            Columna("SalarioReal", "Salario Real", orden: 5, ancho: 120, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("Origen", "Origen", orden: 6, ancho: 80, alineacion: AlineacionColumna.Centro),
            Columna("Notas", "Notas", orden: 7, ancho: 140, visible: false),
            Columna("colRelleno", "", orden: 99),
        };

        return new ManoObraReportSnapshotBuilder().Build(
            ProyectoId, "Catálogo de Mano de Obra Sintético", columnas,
            decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden:
    /// identificador, encabezado, visible, orden, ancho, formato, rol numérico,
    /// alineación, wrap y estilo resumido de la tabla.
    /// </summary>
    public static ManoObraSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ManoObraSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new ManoObraEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new ManoObraFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new ManoObraBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static ManoObraColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static ManoObraEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

    private static ColumnaManoObra Columna(
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

/// <summary>Proyección serializable y estable de un <see cref="ReportColumnSnapshot"/> de mano de obra.</summary>
internal sealed class ManoObraSnapshotDto
{
    public string Schema { get; set; } = ManoObraReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public List<ManoObraColumnaDto> Columnas { get; set; } = new();
    public ManoObraEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada de mano de obra.</summary>
internal sealed class ManoObraColumnaDto
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
    public ManoObraEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public ManoObraEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado de mano de obra.</summary>
internal sealed class ManoObraEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado de mano de obra.</summary>
internal sealed class ManoObraEstiloTablaDto
{
    public ManoObraEstiloTextoDto Encabezado { get; set; } = new();
    public ManoObraEstiloTextoDto Contenido { get; set; } = new();
    public ManoObraFilaAlternaDto FilaAlterna { get; set; } = new();
    public ManoObraBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado de mano de obra.</summary>
internal sealed class ManoObraFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado de mano de obra.</summary>
internal sealed class ManoObraBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
