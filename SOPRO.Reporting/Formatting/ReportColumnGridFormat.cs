using System;
using System.Globalization;
using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.Reporting.Formatting;

/// <summary>
/// Resuelve el formato numérico de una columna del reporte de Presupuesto con
/// paridad EXACTA con el grid, que es la fuente de verdad visual:
/// <list type="bullet">
///   <item>Columna monetaria (<see cref="ReportColumnDefinition.EsMoneda"/>):
///   decimales de importe del proyecto y símbolo de moneda de la cultura actual
///   (misma semántica que <c>MotorCalculoSopro.FormatImporte</c>).</item>
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
            || (columna.EsNumerica && !columna.EsMoneda && EsTokenN(columna.FormatoNumerico));
    }

    /// <summary>
    /// Formatea un valor decimal para el PDF con la misma semántica que el grid.
    /// Las rutas monetaria y de cantidad usan <see cref="CultureInfo.CurrentCulture"/>
    /// (igual que el grid); el resto conserva la traducción legacy con
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public static string FormatearPdf(decimal valor, ReportColumnDefinition columna, ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(columna);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (EsMonedaGrid(columna))
            return valor.ToString($"C{snapshot.DecimalesImporte}", CultureInfo.CurrentCulture);

        if (EsCantidadGrid(columna))
            return valor.ToString($"N{snapshot.DecimalesCantidad}", CultureInfo.CurrentCulture);

        return valor.ToString(
            ReportNumberFormatMapper.ToPdfFormat(columna.FormatoNumerico),
            CultureInfo.InvariantCulture);
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
    /// Formato Excel de moneda: símbolo de moneda de la cultura + separador de
    /// miles + los decimales indicados. Ej.: (2, es-US) → "$#,##0.00".
    /// </summary>
    public static string FormatoMonedaExcel(int decimales, CultureInfo cultura)
    {
        ArgumentNullException.ThrowIfNull(cultura);
        string simbolo = cultura.NumberFormat.CurrencySymbol;
        return $"{simbolo}#,##0{BloqueDecimales(decimales)}";
    }

    /// <summary>
    /// Formato Excel numérico (sin símbolo de moneda): separador de miles + los
    /// decimales indicados. Ej.: (4) → "#,##0.0000".
    /// </summary>
    public static string FormatoNumeroExcel(int decimales)
        => $"#,##0{BloqueDecimales(decimales)}";

    private static string BloqueDecimales(int decimales)
        => decimales > 0 ? "." + new string('0', decimales) : string.Empty;

    private static bool EsTokenN(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato)) return false;
        var token = formato!.Trim();
        return token[0] == 'N' || token[0] == 'n';
    }
}
