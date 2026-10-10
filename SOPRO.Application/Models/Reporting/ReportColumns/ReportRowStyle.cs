namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Estilo de las filas alternas (bandeado) de una tabla de reporte. Un
/// <see cref="ColorFondoAlterno"/> nulo indica que la tabla NO usa bandeado y
/// todas las filas usan el fondo por defecto del estilo de contenido.
/// </summary>
/// <param name="ColorFondoAlterno">Fondo hexadecimal de la fila alterna, o <c>null</c>.</param>
/// <param name="ColorFuente">Color de texto hexadecimal de la fila alterna.</param>
public sealed record ReportRowStyle(
    string? ColorFondoAlterno,
    string ColorFuente);
