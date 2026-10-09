using SOPRO.Application.Services;

namespace SOPRO.Reporting.Formatting;

/// <summary>
/// Traduce el formato numérico del contrato compartido
/// (<c>ReportColumnDefinition.FormatoNumerico</c>: "N2", "C2", "C4", "P2", ...)
/// al formato aplicable por los renderizadores.
///
/// La tabla canónica vive en <see cref="ReportNumberFormatMap"/> (SOPRO.Application),
/// que es la única capa alcanzable tanto por la ruta PDF (resolver de materiales)
/// como por la ruta Excel, sin crear una referencia circular Application→Reporting
/// ni duplicar el mapper. Este tipo se conserva como facade estable de la capa
/// Reporting y delega en dicha tabla única.
///
/// La traducción es POR RENDERIZADOR (fidelidad legacy de la versión publicada):
/// <see cref="ToPdfFormat"/> reproduce la tabla del PDF legacy (N0/N2/N3/N4/N5/C2 +
/// passthrough literal del resto, por lo que "C4" se aplica con la cultura
/// invariante y produce ¤, nunca '$') y <see cref="ToExcelFormat"/> la tabla del
/// Excel legacy (N0/N2/N3/N4/N5/C2/P2 + passthrough de formatos .NET explícitos +
/// default <see cref="DefaultFormat"/> sin '$' para el resto de tokens letra).
/// <see cref="Map"/> se conserva por compatibilidad y equivale a
/// <see cref="ToPdfFormat"/>.
/// Un valor no reconocido se interpreta como formato .NET explícito; un valor
/// vacío o nulo cae al formato por defecto <see cref="DefaultFormat"/>
/// (equivalente a "N2").
/// </summary>
public static class ReportNumberFormatMapper
{
    /// <summary>Formato por defecto para columnas numéricas sin formato ("N2").</summary>
    public const string DefaultFormat = ReportNumberFormatMap.DefaultFormat;

    /// <summary>
    /// Método de conveniencia histórico: equivale a <see cref="ToPdfFormat"/>.
    /// Se prefiere <see cref="ToPdfFormat"/>/<see cref="ToExcelFormat"/> explícitos
    /// porque la tabla ya diverge por renderizador.
    /// </summary>
    /// <param name="formato">Token del contrato ("N0".."N5", "C2", "C4", "P2") o formato .NET explícito.</param>
    public static string Map(string? formato) => ReportNumberFormatMap.Map(formato);

    /// <summary>
    /// Formato para el renderizador PDF/MigraDoc (tabla legacy PDF + passthrough).
    /// </summary>
    public static string ToPdfFormat(string? formato) => ReportNumberFormatMap.ToPdfFormat(formato);

    /// <summary>
    /// Formato para el renderizador Excel/ClosedXML (tabla legacy Excel + default
    /// seguro sin '$' para tokens no soportados).
    /// </summary>
    public static string ToExcelFormat(string? formato) => ReportNumberFormatMap.ToExcelFormat(formato);
}
