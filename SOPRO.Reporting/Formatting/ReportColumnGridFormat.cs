using System;
using System.Globalization;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.Reporting.Formatting;

/// <summary>
/// Resuelve el formato numérico de una columna del reporte con paridad EXACTA con
/// el grid, que es la fuente de verdad visual:
/// <list type="bullet">
///   <item>Columna monetaria (<see cref="ReportColumnDefinition.EsMoneda"/>):
///   decimales de importe del proyecto y el símbolo de moneda ÚNICO
///   <see cref="SimboloMoneda"/> ('$'), centralizado para PDF y Excel.</item>
///   <item>Columna Cantidad (o numérica con token "N*" y no monetaria): decimales
///   de cantidad del proyecto (misma semántica que
///   <c>MotorCalculoSopro.FormatCantidad</c>).</item>
///   <item>Resto de columnas: traducción token→formato legacy
///   (<see cref="ReportNumberFormatMapper"/>), sin cambios.</item>
/// </list>
///
/// Es neutral (no depende de MigraDoc/ClosedXML ni de UI) para que las rutas PDF y
/// Excel compartan UNA sola regla y pueda probarse sin formularios.
/// </summary>
public static class ReportColumnGridFormat
{
    /// <summary>Identificador canónico de la columna de cantidad del Presupuesto.</summary>
    public const string IdentificadorCantidad = "Cantidad";

    /// <summary>
    /// Símbolo de moneda ÚNICO del sistema de reportes (SOPRO cotiza en pesos
    /// mexicanos). Es el ÚNICO punto de decisión de la moneda: las rutas PDF y
    /// Excel lo comparten, de modo que ningún generador introduce literales '$'
    /// dispersos ni depende de la <see cref="CultureInfo.CurrentCulture"/> (que
    /// podía producir '€' o el glifo '¤' de la cultura invariante). Para cambiar la
    /// moneda del producto se modifica SOLO este valor.
    /// </summary>
    public const string SimboloMoneda = "$";

    /// <summary>
    /// Indica si la columna debe formatearse con la semántica monetaria del grid
    /// (símbolo de moneda + decimales de importe).
    /// </summary>
    public static bool EsMonedaGrid(ReportColumnDefinition columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return columna.EsMoneda;
    }

    /// <summary>
    /// Indica si la columna debe formatearse con la semántica de cantidad del grid
    /// (decimales de cantidad): la columna <c>Cantidad</c> o cualquier columna
    /// numérica no monetaria con token de formato "N*".
    /// </summary>
    public static bool EsCantidadGrid(ReportColumnDefinition columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return string.Equals(columna.Identificador, IdentificadorCantidad, StringComparison.OrdinalIgnoreCase)
            || (columna.EsNumerica && !columna.EsMoneda
                && ReportColumnDefinitionMapper.EsTokenCantidad(columna.FormatoNumerico));
    }

    /// <summary>
    /// Indica si la columna es de porcentaje: token de formato "P*" o nombre
    /// interno canónico del rol (p. ej. <c>PorcentajeIndirectos</c>). Es
    /// reconocimiento común de rol (Fase 0); NO altera la traducción legacy por
    /// token de las columnas de porcentaje ya existentes.
    /// </summary>
    public static bool EsPorcentajeGrid(ReportColumnDefinition columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return ReportColumnDefinitionMapper.EsRolPorcentaje(columna.Identificador, columna.FormatoNumerico);
    }

    /// <summary>
    /// Formatea un valor decimal para el PDF con la misma semántica que el grid.
    /// La ruta monetaria usa el símbolo centralizado <see cref="SimboloMoneda"/> y
    /// los decimales de importe del proyecto (con la cultura actual para los
    /// separadores); la de cantidad usa los decimales de cantidad con
    /// <see cref="CultureInfo.CurrentCulture"/>; el resto conserva la traducción
    /// legacy con <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public static string FormatearPdf(decimal valor, ReportColumnDefinition columna, ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(columna);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (EsMonedaGrid(columna))
            return valor.ToString(FormatoMonedaPdf(snapshot.DecimalesImporte), CultureInfo.CurrentCulture);

        if (EsCantidadGrid(columna))
            return valor.ToString($"N{snapshot.DecimalesCantidad}", CultureInfo.CurrentCulture);

        return valor.ToString(
            ReportNumberFormatMapper.ToPdfFormat(columna.FormatoNumerico),
            CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formatea un valor de porcentaje para el PDF con los decimales de porcentaje
    /// del proyecto (misma semántica que <c>MotorCalculoSopro.FormatPorcentaje</c>,
    /// sin signo '%'). Helper explícito para las migraciones de Fase 1; no altera
    /// la traducción legacy por token.
    /// </summary>
    public static string FormatearPorcentajePdf(decimal valor, ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return valor.ToString($"N{snapshot.DecimalesPorcentaje}", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Devuelve el formato de celda Excel con la misma semántica que el grid. Las
    /// columnas monetaria y de cantidad usan el número de decimales del proyecto;
    /// el resto conserva la traducción token→formato legacy.
    /// </summary>
    public static string ResolveExcelFormat(ReportColumnDefinition columna, ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(columna);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (EsMonedaGrid(columna))
            return FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);

        if (EsCantidadGrid(columna))
            return FormatoNumeroExcel(snapshot.DecimalesCantidad);

        return ReportNumberFormatMapper.ToExcelFormat(columna.FormatoNumerico);
    }

    /// <summary>
    /// Formato Excel de moneda: símbolo ÚNICO <see cref="SimboloMoneda"/> +
    /// separador de miles + los decimales indicados. Ej.: (2) → "$#,##0.00".
    /// El parámetro <paramref name="cultura"/> se conserva por compatibilidad de
    /// firma; el símbolo ya NO depende de la cultura (centralización de la moneda).
    /// </summary>
    public static string FormatoMonedaExcel(int decimales, CultureInfo cultura)
    {
        ArgumentNullException.ThrowIfNull(cultura);
        return FormatoMonedaPdf(decimales);
    }

    /// <summary>
    /// Formato .NET de moneda (símbolo ÚNICO + miles + decimales) para la ruta
    /// PDF. Ej.: (2) → "$#,##0.00"; (0) → "$#,##0".
    /// </summary>
    public static string FormatoMonedaPdf(int decimales)
        => $"{SimboloMoneda}#,##0{BloqueDecimales(decimales)}";

    /// <summary>
    /// Formato Excel numérico (sin símbolo de moneda): separador de miles + los
    /// decimales indicados. Ej.: (4) → "#,##0.0000".
    /// </summary>
    public static string FormatoNumeroExcel(int decimales)
        => $"#,##0{BloqueDecimales(decimales)}";

    /// <summary>
    /// Formato Excel de porcentaje con los decimales indicados. Ej.: (2) →
    /// "0.00%"; (0) → "0%". Helper explícito para las migraciones de Fase 1.
    /// </summary>
    public static string FormatoPorcentajeExcel(int decimales)
        => $"0{BloqueDecimales(decimales)}%";

    private static string BloqueDecimales(int decimales)
        => decimales > 0 ? "." + new string('0', decimales) : string.Empty;
}
