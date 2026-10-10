using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Financiamiento que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de columnas
/// y de los tests de paridad semántica PDF/Excel del reporte.
///
/// Reutiliza la proyección serializable <see cref="PilotoSnapshotDto"/> (mismo
/// ensamblado de pruebas) con un esquema propio para que el golden
/// <c>financiamiento-columnas.json</c> sea independiente de los demás.
/// </summary>
internal static class FinanciamientoReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.financiamiento-columnas.1";

    /// <summary>Proyecto sintético del reporte.</summary>
    public const int ProyectoId = 7;

    /// <summary>
    /// Snapshot del catálogo de financiamiento: <c>ColumnaFinanciamiento</c> como
    /// fuente, con las quince columnas del grid (Período, Inicio, Fin, Días, costos
    /// e intereses) y estilo neutral <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot Financiamiento()
    {
        var columnas = new[]
        {
            ColumnaF("colPeriodo",   "Período",        orden: 1,  ancho: 90),
            ColumnaF("colInicio",    "Inicio",         orden: 2,  ancho: 75),
            ColumnaF("colFin",       "Fin",            orden: 3,  ancho: 75),
            ColumnaF("colDias",      "Días",           orden: 4,  ancho: 45),
            ColumnaF("colCD",        "CD $",           orden: 5,  ancho: 95,  alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colCI",        "CI $",           orden: 6,  ancho: 95,  alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colEgresos",   "Egreso $",       orden: 7,  ancho: 105, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colAnticipo",  "Anticipo $",     orden: 8,  ancho: 95,  alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colEstim",     "Estimación $",   orden: 9,  ancho: 105, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colAmort",     "Amortización $", orden: 10, ancho: 110, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colCobro",     "Cobro neto $",   orden: 11, ancho: 105, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colFlujoNeto", "Flujo $",        orden: 12, ancho: 95,  alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colSaldo",     "Saldo acum. $",  orden: 13, ancho: 110, alineacion: AlineacionColumna.Derecha, formato: "C2"),
            ColumnaF("colTasa",      "Tasa período",   orden: 14, ancho: 85),
            ColumnaF("colInteres",   "Interés $",      orden: 15, ancho: 95,  alineacion: AlineacionColumna.Derecha, formato: "C2"),
        };

        return new FinanciamientoReportSnapshotBuilder().Build(
            proyectoId: ProyectoId,
            titulo: "Análisis de Financiamiento Sintético",
            columnas: columnas,
            decimalesCantidad: 5,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);
    }

    /// <summary>
    /// Snapshot de los defaults neutrales (sin configuración persistida). Sirve
    /// para verificar que el reporte arranca con las columnas del catálogo.
    /// </summary>
    public static ReportColumnSnapshot Defaults()
        => new FinanciamientoReportSnapshotBuilder().Build(ProyectoId, "Análisis de Financiamiento", columnas: null);

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden. Usa
    /// los mismos DTO aplanados del piloto con un esquema propio.
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

    private static ColumnaFinanciamiento ColumnaF(
        string nombreInterno,
        string nombre,
        int orden,
        int ancho = 100,
        AlineacionColumna alineacion = AlineacionColumna.Centro,
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
