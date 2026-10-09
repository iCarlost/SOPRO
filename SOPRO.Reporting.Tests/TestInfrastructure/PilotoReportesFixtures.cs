using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del piloto (Presupuesto + Catálogo de Materiales) que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente de los goldens del contrato neutral del
/// piloto y de los tests de paridad semántica PDF/Excel.
///
/// Punto C diferido: en <c>SOPRO.Reporting</c> sólo existen renderizadores
/// neutrales del catálogo de matrices; no hay uno neutral de Presupuesto ni de
/// Materiales, y los de WinForms no son invocables desde este proyecto. La
/// paridad byte-level de esos renderers se cubrirá en verificación posterior; no
/// se referencian WinForms ni se crean proyectos nuevos.
/// </summary>
internal static class PilotoReportesFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.piloto-columnas.1";

    /// <summary>Proyecto sintético compartido por ambos reportes del piloto.</summary>
    public const int ProyectoId = 7;

    /// <summary>
    /// Snapshot del Presupuesto: <c>ColumnaPersonalizada</c> primaria con overlay
    /// de encabezado <c>ConfigColumnaReporte</c> sobre <c>PrecioUnitario</c>, más
    /// dos columnas internas (<c>Tipo</c>, <c>colRelleno</c>) que deben excluirse.
    /// </summary>
    public static ReportColumnSnapshot Presupuesto()
    {
        var columnas = new[]
        {
            ColumnaP("Tipo", "Tipo", orden: -1),
            ColumnaP("Clave", "Clave", orden: 0, ancho: 80),
            ColumnaP("Descripcion", "Descripción", orden: 1, ancho: 250, wrap: true),
            ColumnaP("Unidad", "Unidad", orden: 2, ancho: 60),
            ColumnaP("Cantidad", "Cantidad", orden: 3, ancho: 70, tipo: TipoDatoColumna.Numerico),
            ColumnaP("PrecioUnitario", "Precio Unitario", orden: 4, ancho: 90,
                tipo: TipoDatoColumna.Moneda, formato: "C2"),
            ColumnaP("Importe", "Importe", orden: 5, ancho: 100, tipo: TipoDatoColumna.Moneda),
            ColumnaP("colRelleno", "", orden: 99),
        };

        // El overlay sólo aporta el estilo del encabezado; el encabezado visible y
        // el contenido siguen viniendo de la entidad primaria.
        var overlays = new[]
        {
            new ConfigColumnaReporte
            {
                NombreInterno = "preciounitario",
                Encabezado = "ENCABEZADO IGNORADO",
                EncFuente = "Arial",
                EncTamaño = 10f,
                EncNegrita = true,
                EncCursiva = true,
                EncColorFondo = "#0D47A1",
                EncColorTexto = "#FFEB3B",
            },
        };

        return new PresupuestoReportSnapshotBuilder().Build(
            proyectoId: ProyectoId,
            titulo: "Presupuesto Sintético Piloto",
            columnas: columnas,
            configuracionReporte: overlays);
    }

    /// <summary>
    /// Snapshot del Catálogo de Materiales: <c>ColumnaMaterial</c> como única
    /// fuente, con formato monetario de 4 decimales (<c>C4</c>) y bandeado neutral
    /// <see cref="ReportTableStyle.LegacyMateriales"/>.
    /// </summary>
    public static ReportColumnSnapshot Materiales()
    {
        var columnas = new[]
        {
            ColumnaM("Clave", "Clave", orden: 0, ancho: 90),
            ColumnaM("Descripcion", "Descripción", orden: 1, ancho: 260, wrap: true),
            ColumnaM("Unidad", "Unidad", orden: 2, ancho: 60),
            ColumnaM("Cantidad", "Cantidad", orden: 3, ancho: 70,
                alineacion: AlineacionColumna.Derecha, formato: "N2"),
            ColumnaM("PrecioUnitario", "Precio Unitario", orden: 4, ancho: 100,
                alineacion: AlineacionColumna.Derecha, formato: "C4"),
            ColumnaM("Total", "Total", orden: 5, ancho: 110,
                alineacion: AlineacionColumna.Derecha, formato: "C4"),
        };

        return new MaterialesReportSnapshotBuilder().Build(
            proyectoId: ProyectoId,
            titulo: "Catálogo de Materiales Sintético Piloto",
            columnas: columnas);
    }

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden:
    /// identificador, encabezado, visible, orden, ancho, formato, alineación,
    /// wrap y estilo resumido de la tabla. Enumeraciones como cadena y estilos
    /// aplanados para que el JSON sea determinista y legible.
    /// </summary>
    public static PilotoSnapshotDto Proyectar(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new PilotoSnapshotDto
        {
            Schema = Schema,
            TipoReporte = snapshot.TipoReporte,
            ProyectoId = snapshot.ProyectoId,
            Titulo = snapshot.Titulo,
            Columnas = snapshot.Columnas
                .OrderBy(c => c.Orden)
                .Select(ProyectarColumna)
                .ToList(),
            EstiloTabla = new PilotoEstiloTablaDto
            {
                Encabezado = ProyectarEstilo(snapshot.EstiloTabla.EstiloEncabezado),
                Contenido = ProyectarEstilo(snapshot.EstiloTabla.EstiloContenido),
                FilaAlterna = new PilotoFilaAlternaDto
                {
                    ColorFondoAlterno = snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                    ColorFuente = snapshot.EstiloTabla.FilaAlterna.ColorFuente,
                },
                Bordes = new PilotoBordeDto
                {
                    Visible = snapshot.EstiloTabla.Bordes.Visible,
                    ColorHex = snapshot.EstiloTabla.Bordes.ColorHex,
                    GrosorPuntos = snapshot.EstiloTabla.Bordes.GrosorPuntos,
                },
            },
        };
    }

    private static PilotoColumnaDto ProyectarColumna(ReportColumnDefinition columna) => new()
    {
        Identificador = columna.Identificador,
        Encabezado = columna.Encabezado,
        Visible = columna.Visible,
        Orden = columna.Orden,
        Ancho = columna.Ancho,
        Formato = columna.FormatoNumerico,
        EsNumerica = columna.EsNumerica,
        Alineacion = columna.Alineacion.ToString(),
        AlineacionVertical = columna.AlineacionVertical.ToString(),
        Wrap = columna.Wrap,
        EstiloEncabezado = ProyectarEstilo(columna.EstiloEncabezado),
        EstiloContenido = ProyectarEstilo(columna.EstiloContenido),
    };

    private static PilotoEstiloTextoDto ProyectarEstilo(ReportTextStyle estilo) => new()
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

    private static ColumnaPersonalizada ColumnaP(
        string nombreInterno,
        string nombre,
        int orden,
        int ancho = 100,
        TipoDatoColumna tipo = TipoDatoColumna.Texto,
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
            WrapTexto = wrap,
            FormatoNumerico = formato ?? string.Empty,
        };

    private static ColumnaMaterial ColumnaM(
        string nombreInterno,
        string nombre,
        int orden,
        int ancho = 100,
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
            Alineacion = alineacion,
            WrapTexto = wrap,
            FormatoNumerico = formato ?? string.Empty,
        };
}

/// <summary>Proyección serializable y estable de un <see cref="ReportColumnSnapshot"/> del piloto.</summary>
internal sealed class PilotoSnapshotDto
{
    public string Schema { get; set; } = PilotoReportesFixtures.Schema;
    public string TipoReporte { get; set; } = "";
    public int ProyectoId { get; set; }
    public string Titulo { get; set; } = "";
    public List<PilotoColumnaDto> Columnas { get; set; } = new();
    public PilotoEstiloTablaDto EstiloTabla { get; set; } = new();
}

/// <summary>Columna aplanada del piloto.</summary>
internal sealed class PilotoColumnaDto
{
    public string Identificador { get; set; } = "";
    public string Encabezado { get; set; } = "";
    public bool Visible { get; set; }
    public int Orden { get; set; }
    public int Ancho { get; set; }
    public string Formato { get; set; } = "";
    public bool EsNumerica { get; set; }
    public string Alineacion { get; set; } = "";
    public string AlineacionVertical { get; set; } = "";
    public bool Wrap { get; set; }
    public PilotoEstiloTextoDto EstiloEncabezado { get; set; } = new();
    public PilotoEstiloTextoDto EstiloContenido { get; set; } = new();
}

/// <summary>Estilo de texto aplanado del piloto.</summary>
internal sealed class PilotoEstiloTextoDto
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

/// <summary>Estilo de tabla aplanado del piloto.</summary>
internal sealed class PilotoEstiloTablaDto
{
    public PilotoEstiloTextoDto Encabezado { get; set; } = new();
    public PilotoEstiloTextoDto Contenido { get; set; } = new();
    public PilotoFilaAlternaDto FilaAlterna { get; set; } = new();
    public PilotoBordeDto Bordes { get; set; } = new();
}

/// <summary>Bandeado aplanado del piloto.</summary>
internal sealed class PilotoFilaAlternaDto
{
    public string? ColorFondoAlterno { get; set; }
    public string ColorFuente { get; set; } = "";
}

/// <summary>Borde aplanado del piloto.</summary>
internal sealed class PilotoBordeDto
{
    public bool Visible { get; set; }
    public string ColorHex { get; set; } = "";
    public double GrosorPuntos { get; set; }
}
