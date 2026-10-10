using System.Collections.Generic;
using System.Linq;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas de los reportes del Factor de Salario
/// Real (FSR). Produce snapshots deterministas SIN tocar grids ni escribir
/// configuración, para que las rutas PDF y Excel del mismo reporte consuman
/// exactamente la misma lista, orden, anchos, alineaciones y formatos.
///
/// A diferencia de los catálogos (Fase 1), el FSR no tiene columnas persistidas:
/// su forma es fija y depende de la configuración global de decimales del proyecto.
/// Se exponen dos formas:
/// <list type="bullet">
///   <item><see cref="BuildAE2A"/> — AE-2(A) "Tabla de cálculo del FSR": 4 columnas
///   (Descripción, Operación, Unidad, Valor). La columna <c>Valor</c> es de cantidad,
///   por lo que se formatea con los decimales de cantidad del proyecto.</item>
///   <item><see cref="BuildAE2C"/> — AE-2(C) "Tabulador FSR": 21 columnas; las
///   cantidades usan los decimales de cantidad y las monetarias (Sal. Base M.N. y
///   Salario Real) el símbolo único '$' con los decimales de importe.</item>
/// </list>
///
/// El estilo de tabla es el neutral compartido por los catálogos
/// (<see cref="ReportTableStyle.LegacyCatalogo"/>), que reproduce el encabezado
/// #4A4A6A, el bandeado #F5F5F5 y la grilla #DDDDDD de las tablas del FSR.
/// </summary>
public sealed class FsrReportSnapshotBuilder
{
    /// <summary>Tipo de reporte del AE-2(A) (tabla de cálculo del FSR).</summary>
    public const string TipoReporteAE2A = "FactorSalarioReal";

    /// <summary>Tipo de reporte del AE-2(C) (tabulador desglosado por insumo).</summary>
    public const string TipoReporteAE2C = "TabuladorFsr";

    /// <summary>
    /// Precisión canónica de las cantidades del FSR cuando no se aporta la
    /// configuración del proyecto (legacy N5). Los proyectos reales heredan su
    /// <c>DecimalesCantidad</c> configurado.
    /// </summary>
    public const int DecimalesCantidadFsr = 5;

    /// <summary>Identificador neutral de la columna de valor del AE-2(A).</summary>
    public const string ColValor = "Valor";

    /// <summary>Identificador neutral de la columna Salario Base del AE-2(C).</summary>
    public const string ColSalarioBase = "SalarioBase";

    /// <summary>Identificador neutral de la columna Salario Real del AE-2(C).</summary>
    public const string ColSalarioReal = "SalarioReal";

    private static readonly ReportTableStyle Estilo = ReportTableStyle.LegacyCatalogo();

    /// <summary>Columnas canónicas del AE-2(A), en orden.</summary>
    private static readonly ReportColumnDefinition[] ColumnasAE2A =
    {
        Col("Descripcion", "Descripción", 0, 340, ReportTextAlignment.Izquierda, wrap: true),
        Col("Operacion", "Operación", 1, 320, ReportTextAlignment.Izquierda, wrap: true),
        Col("Unidad", "Unidad", 2, 70, ReportTextAlignment.Centro),
        Col(ColValor, "Valor", 3, 150, ReportTextAlignment.Derecha,
            numerica: true, formato: "N5"),
    };

    /// <summary>Columnas canónicas del AE-2(C), en orden.</summary>
    private static readonly ReportColumnDefinition[] ColumnasAE2C =
    {
        Col("Clave", "Clave", 0, 70, ReportTextAlignment.Centro),
        Col("Descripcion", "Descripción", 1, 220, ReportTextAlignment.Izquierda, wrap: true),
        Col(ColSalarioBase, "Sal. Base M.N.", 2, 90, ReportTextAlignment.Derecha, numerica: true, moneda: true, formato: "C2"),
        Col("SalarioNominal", "Salario Nominal", 3, 80, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("FactorSbc", "Factor SBC", 4, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("SalarioBaseCotizacion", "Salario Base Cotización", 5, 85, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("CuotaFija", "Cuota fija", 6, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("Excedente", "Excedente a 3 SMGDF", 7, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("PrestacionesEspecie", "Prest. especie", 8, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("PrestacionesDinero", "Prest. dinero", 9, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("InvalidezVida", "Inv. y vida", 10, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("Guarderias", "Guarderías", 11, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("CesantiaVejez", "Cesantía y vejez", 12, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("Retiro", "Retiro", 13, 60, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("SumaCuotasImss", "Suma cuotas IMSS", 14, 80, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("Infonavit", "INFONAVIT", 15, 75, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("SumaPrestacionesPatronales", "Suma prest. patronales", 16, 95, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("ObligacionesPs", "Obligac. PS", 17, 80, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("FactorTpTl", "Factor TP/TL", 18, 70, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col("Fsr", "FSR", 19, 60, ReportTextAlignment.Derecha, numerica: true, formato: "N5"),
        Col(ColSalarioReal, "Salario Real", 20, 85, ReportTextAlignment.Derecha, numerica: true, moneda: true, formato: "C2"),
    };

    /// <summary>
    /// Construye el snapshot del AE-2(A) usando la configuración de decimales del
    /// proyecto (cantidad/importe/porcentaje). Núcleo compartido PDF↔Excel.
    /// </summary>
    public ReportColumnSnapshot BuildAE2A(Proyecto proyecto, string? titulo)
    {
        ArgumentNullException.ThrowIfNull(proyecto);
        return BuildAE2A(
            proyecto.Id, titulo,
            proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);
    }

    /// <summary>Construye el snapshot del AE-2(A) con decimales explícitos.</summary>
    public ReportColumnSnapshot BuildAE2A(
        int proyectoId,
        string? titulo,
        int decimalesCantidad = DecimalesCantidadFsr,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new(
            TipoReporte: TipoReporteAE2A,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: ColumnasAE2A.ToList(),
            EstiloTabla: Estilo)
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };

    /// <summary>
    /// Construye el snapshot del AE-2(C) usando la configuración de decimales del
    /// proyecto. Núcleo compartido PDF↔Excel del Tabulador FSR.
    /// </summary>
    public ReportColumnSnapshot BuildAE2C(Proyecto proyecto, string? titulo)
    {
        ArgumentNullException.ThrowIfNull(proyecto);
        return BuildAE2C(
            proyecto.Id, titulo,
            proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);
    }

    /// <summary>Construye el snapshot del AE-2(C) con decimales explícitos.</summary>
    public ReportColumnSnapshot BuildAE2C(
        int proyectoId,
        string? titulo,
        int decimalesCantidad = DecimalesCantidadFsr,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new(
            TipoReporte: TipoReporteAE2C,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: ColumnasAE2C.ToList(),
            EstiloTabla: Estilo)
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };

    /// <summary>Columnas neutrales del AE-2(A), ordenadas por <c>Orden</c>.</summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumnsAE2A()
        => ColumnasAE2A.ToList();

    /// <summary>Columnas neutrales del AE-2(C), ordenadas por <c>Orden</c>.</summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumnsAE2C()
        => ColumnasAE2C.ToList();

    private static ReportColumnDefinition Col(
        string identificador,
        string encabezado,
        int orden,
        int ancho,
        ReportTextAlignment alineacion,
        bool numerica = false,
        bool moneda = false,
        string formato = "",
        bool wrap = false)
        => new(
            Identificador: identificador,
            Encabezado: encabezado,
            Visible: true,
            Orden: orden,
            Ancho: ancho,
            EstiloEncabezado: Estilo.EstiloEncabezado,
            EstiloContenido: Estilo.EstiloContenido,
            Alineacion: alineacion,
            AlineacionVertical: ReportVerticalAlignment.Medio,
            Wrap: wrap,
            FormatoNumerico: formato,
            EsNumerica: numerica)
        {
            EsMoneda = moneda
        };
}
