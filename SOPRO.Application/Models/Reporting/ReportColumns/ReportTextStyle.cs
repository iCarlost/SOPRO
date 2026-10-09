namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Intención visual neutral de un texto de reporte (semántica, no política de
/// renderer). No depende de ninguna tecnología de UI o renderizado: los colores
/// viajan como cadenas hexadecimales "#RRGGBB".
/// </summary>
/// <param name="Fuente">Nombre de la tipografía (ej. "Segoe UI").</param>
/// <param name="Tamano">Tamaño de fuente en puntos.</param>
/// <param name="Negrita">Negrita.</param>
/// <param name="Cursiva">Cursiva.</param>
/// <param name="ColorFuente">Color de texto hexadecimal ("#RRGGBB"/"#AARRGGBB").</param>
/// <param name="ColorFondo">Color de fondo hexadecimal, o <c>null</c> para sin relleno.</param>
/// <param name="ColorFuenteEncabezado">Color de texto del encabezado asociado, o <c>null</c>.</param>
/// <param name="ColorFondoEncabezado">Color de fondo del encabezado asociado, o <c>null</c>.</param>
public sealed record ReportTextStyle(
    string Fuente,
    float Tamano,
    bool Negrita,
    bool Cursiva,
    string ColorFuente,
    string? ColorFondo,
    string? ColorFuenteEncabezado = null,
    string? ColorFondoEncabezado = null);
