using System;

namespace SOPRO.Reporting.Formatting;

/// <summary>
/// Traduce el formato numérico del contrato compartido
/// (<c>ReportColumnDefinition.FormatoNumerico</c>: "N2", "C2", "C4", "P2", ...)
/// al formato aplicable por los renderizadores.
///
/// Reproduce la tabla de los generadores legacy
/// (<c>GeneradorExcelPresupuesto.ConvertirFormato</c>,
/// <c>GeneradorPdfPresupuesto.FormatearDecimal</c> y
/// <c>FormCatalogoMateriales.ConvertirFormatoExcel</c>): cubre
/// <c>N0</c>..<c>N5</c>, <c>C0</c>..<c>C4</c> y <c>P0</c>..<c>P3</c>. Para
/// PDF/MigraDoc y Excel/ClosedXML el formato
/// resultante coincide (ambos consumen la misma cadena .NET), por lo que se
/// exponen <see cref="ToPdfFormat"/> y <see cref="ToExcelFormat"/> por claridad
/// y para permitir divergencias futuras.
/// Un valor no reconocido se interpreta como formato .NET explícito y se
/// devuelve tal cual; un valor vacío o nulo cae al formato por defecto
/// <see cref="DefaultFormat"/> (equivalente a "N2").
/// </summary>
public static class ReportNumberFormatMapper
{
    /// <summary>Formato por defecto para columnas numéricas sin formato ("N2").</summary>
    public const string DefaultFormat = "#,##0.00";

    /// <summary>
    /// Devuelve el formato .NET/Excel aplicable a un token del contrato.
    /// </summary>
    /// <param name="formato">Token del contrato ("N0".."N5", "C2", "C4", "P2") o formato .NET explícito.</param>
    /// <returns>Cadena de formato lista para <c>decimal.ToString</c> o <c>NumberFormat.Format</c>.</returns>
    public static string Map(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato))
            return DefaultFormat;

        string token = formato.Trim();
        return token.ToUpperInvariant() switch
        {
            "N0" => "#,##0",
            "N1" => "#,##0.0",
            "N2" => "#,##0.00",
            "N3" => "#,##0.000",
            "N4" => "#,##0.0000",
            "N5" => "#,##0.00000",
            "C0" => "$#,##0",
            "C1" => "$#,##0.0",
            "C2" => "$#,##0.00",
            "C3" => "$#,##0.000",
            "C4" => "$#,##0.0000",
            "P0" => "0%",
            "P1" => "0.0%",
            "P2" => "0.00%",
            "P3" => "0.000%",
            // Formato .NET explícito: se respeta literalmente.
            _ => token,
        };
    }

    /// <summary>
    /// Formato para el renderizador PDF/MigraDoc (misma cadena .NET que
    /// <see cref="Map"/>).
    /// </summary>
    public static string ToPdfFormat(string? formato) => Map(formato);

    /// <summary>
    /// Formato para el renderizador Excel/ClosedXML (misma cadena que
    /// <see cref="Map"/>).
    /// </summary>
    public static string ToExcelFormat(string? formato) => Map(formato);
}
