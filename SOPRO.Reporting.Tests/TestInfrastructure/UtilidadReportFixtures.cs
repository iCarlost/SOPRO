using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Utilidad que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde el builder neutral (sin
/// EF, sin UI). Es la única fuente del golden del contrato neutral de columnas y de
/// los tests de paridad semántica PDF/Excel del reporte.
///
/// Reutiliza la proyección serializable <see cref="PilotoSnapshotDto"/> del piloto
/// (mismo ensamblado de pruebas) con un esquema propio para que el golden
/// <c>utilidad-columnas.json</c> sea independiente de los del piloto y de la Fase 1.
/// </summary>
internal static class UtilidadReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.utilidad-columnas.1";

    /// <summary>Proyecto sintético del reporte.</summary>
    public const int ProyectoId = 5;

    /// <summary>
    /// Snapshot del reporte de Utilidad con los cuatro roles neutrales (Concepto,
    /// Base, Porcentaje e ImporteFinal), decimales globales del proyecto y estilo
    /// neutral <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot Utilidad()
        => new UtilidadReportSnapshotBuilder().Build(
            proyectoId: ProyectoId,
            titulo: "Determinación de la Utilidad Sintética",
            columnas: null,
            decimalesCantidad: 2,
            decimalesImporte: 2,
            decimalesPorcentaje: 4);

    /// <summary>
    /// Snapshot de los defaults neutrales del reporte (sin configuración
    /// persistida). Sirve para verificar que el reporte arranca con sus roles base.
    /// </summary>
    public static ReportColumnSnapshot Defaults()
        => new UtilidadReportSnapshotBuilder().Build(ProyectoId, "Determinación de la Utilidad", columnas: null);

    /// <summary>
    /// Proyección semántica estable del snapshot para versionar como golden. Usa los
    /// mismos DTO aplanados del piloto con un esquema propio.
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
}
