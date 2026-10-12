using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Explosión de Insumos que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de la
/// explosión y de los tests de paridad semántica PDF/Excel, siguiendo el mismo
/// patrón que <see cref="ManoObraReportFixtures"/>.
///
/// El reporte cubre las cuatro familias de insumo (Materiales, Mano de Obra,
/// Herramientas y Maquinaria) y sus totales; aquí sólo se congela el CONTRATO DE
/// COLUMNAS, que es común a todas las familias.
///
/// Punto diferido: en <c>SOPRO.Reporting</c> no existe un renderizador neutral de
/// Explosión; el de WinForms no es invocable desde este proyecto, así que la
/// paridad byte-level del PDF queda para verificación posterior (igual que el
/// piloto y la Fase 1).
/// </summary>
internal static class ExplosionReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.explosion-columnas.1";

    /// <summary>Proyecto sintético del reporte de explosión.</summary>
    public const int ProyectoId = 22;

    /// <summary>
    /// Snapshot del reporte de Explosión de Insumos: siete columnas canónicas
    /// (cantidad N4, precio unitario C4, importe C2, porcentaje P2 y textos), una
    /// columna oculta y dos columnas internas que deben excluirse. Usa el estilo
    /// neutral <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot Explosion(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var columnas = new[]
        {
            Columna("Tipo", "Tipo", orden: -1),
            Columna("Clave", "Clave", orden: 1, ancho: 80, alineacion: AlineacionColumna.Izquierda),
            Columna("Descripcion", "Descripción", orden: 2, ancho: 350, alineacion: AlineacionColumna.Izquierda, wrap: true),
            Columna("Unidad", "Unidad", orden: 3, ancho: 70, alineacion: AlineacionColumna.Centro),
            Columna("Cantidad", "Cantidad", orden: 4, ancho: 100, alineacion: AlineacionColumna.Derecha, formato: "N4"),
            Columna("PrecioUnitario", "P.U.", orden: 5, ancho: 120, alineacion: AlineacionColumna.Derecha, formato: "C4"),
            Columna("Importe", "Importe", orden: 6, ancho: 130, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            Columna("Porcentaje", "%", orden: 7, ancho: 80, alineacion: AlineacionColumna.Derecha, formato: "P2"),
            Columna("Observaciones", "Observaciones", orden: 8, ancho: 140, visible: false),
            Columna("colRelleno", "", orden: 99),
        };

        return new ExplosionReportSnapshotBuilder().Build(
            ProyectoId, "Explosión de Insumos Sintética", columnas,
            decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden:
    /// identificador, encabezado, visible, orden, ancho, formato, rol numérico,
    /// alineación, wrap y estilo resumido de la tabla.
    /// </summary>
    public static ExplosionSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ExplosionSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new ExplosionEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new ExplosionFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new ExplosionBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static ExplosionColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
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

    private static ExplosionEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

    private static ColumnaExplosion Columna(
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

/// <summary>Proyección serializable y estable de un <see cref="ReportColumnSnapshot"/> de explosión.</summary>
internal sealed class ExplosionSnapshotDto
{
    public string Schema { get; set; } = ExplosionReportFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public List<ExplosionColumnaDto> Columnas { get; set; } = new();
    public ExplosionEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada de explosión.</summary>
internal sealed class ExplosionColumnaDto
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
    public ExplosionEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public ExplosionEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado de explosión.</summary>
internal sealed class ExplosionEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado de explosión.</summary>
internal sealed class ExplosionEstiloTablaDto
{
    public ExplosionEstiloTextoDto Encabezado { get; set; } = new();
    public ExplosionEstiloTextoDto Contenido { get; set; } = new();
    public ExplosionFilaAlternaDto FilaAlterna { get; set; } = new();
    public ExplosionBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado de explosión.</summary>
internal sealed class ExplosionFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado de explosión.</summary>
internal sealed class ExplosionBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
