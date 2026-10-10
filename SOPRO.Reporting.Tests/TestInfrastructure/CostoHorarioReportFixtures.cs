using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixtures del reporte de Costo Horario / Maquinaria que construyen
/// <see cref="ReportColumnSnapshot"/> deterministas desde entidades en memoria
/// (sin EF, sin UI). Es la única fuente del golden del contrato neutral de
/// columnas y de los tests de paridad semántica PDF/Excel del reporte.
///
/// Reutiliza la proyección serializable <see cref="PilotoSnapshotDto"/> del piloto
/// (mismo ensamblado de pruebas) con un esquema propio para que el golden
/// <c>costo-horario-columnas.json</c> sea independiente de los del piloto.
/// </summary>
internal static class CostoHorarioReportFixtures
{
    /// <summary>Identificador de esquema de la proyección semántica versionada.</summary>
    public const string Schema = "sopro.costo-horario-columnas.1";

    /// <summary>Proyecto sintético del reporte.</summary>
    public const int ProyectoId = 7;

    /// <summary>
    /// Snapshot del catálogo de Maquinaria: <c>ColumnaMaquinaria</c> como fuente,
    /// con las siete columnas del grid (Clave, Descripción, Potencia, Combustible,
    /// Costo Horario, Tipo y Origen) y estilo neutral
    /// <see cref="ReportTableStyle.LegacyCatalogo"/>.
    /// </summary>
    public static ReportColumnSnapshot CostoHorario()
    {
        var columnas = new[]
        {
            ColumnaM("Clave", "Clave", orden: 0, ancho: 110, alineacion: AlineacionColumna.Centro),
            ColumnaM("Descripcion", "Descripción", orden: 1, ancho: 320),
            ColumnaM("PotenciaNominal", "Potencia (HP)", orden: 2, ancho: 100,
                alineacion: AlineacionColumna.Derecha, formato: "N2"),
            ColumnaM("Combustible", "Combustible", orden: 3, ancho: 100, alineacion: AlineacionColumna.Centro),
            ColumnaM("CostoHorario", "Costo Horario", orden: 4, ancho: 120,
                alineacion: AlineacionColumna.Derecha, formato: "#,##0.00"),
            ColumnaM("TipoCosto", "Tipo", orden: 5, ancho: 80, alineacion: AlineacionColumna.Centro),
            ColumnaM("Origen", "Origen", orden: 6, ancho: 80, alineacion: AlineacionColumna.Centro),
        };

        return new CostoHorarioReportSnapshotBuilder().Build(
            proyectoId: ProyectoId,
            titulo: "Catálogo de Maquinaria Sintético",
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
        => new CostoHorarioReportSnapshotBuilder().Build(ProyectoId, "Catálogo de Maquinaria", columnas: null);

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

    private static ColumnaMaquinaria ColumnaM(
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
