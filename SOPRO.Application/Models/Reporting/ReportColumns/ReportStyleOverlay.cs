namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Superposición NEUTRAL de estilo por celda o por fila. Cada propiedad es
/// anulable y <c>null</c> significa "heredar" del nivel de menor precedencia; un
/// valor no nulo GANA según la cascada del resolver (ver
/// <c>ReportCellStyleResolver</c>). No depende de System.Drawing, WinForms,
/// MigraDoc ni ClosedXML: los colores viajan como cadenas "#RRGGBB"/"#AARRGGBB"
/// y la alineación como enums neutrales.
/// </summary>
/// <param name="Fuente">Nombre de la tipografía, o <c>null</c> para heredar.</param>
/// <param name="TamanoFuente">Tamaño de fuente en puntos, o <c>null</c> para heredar.</param>
/// <param name="Negrita">Negrita, o <c>null</c> para heredar.</param>
/// <param name="Cursiva">Cursiva, o <c>null</c> para heredar.</param>
/// <param name="ColorFuente">Color de texto hexadecimal, o <c>null</c> para heredar.</param>
/// <param name="ColorFondo">Color de fondo hexadecimal, o <c>null</c> para heredar.</param>
/// <param name="AlineacionHorizontal">Alineación horizontal, o <c>null</c> para heredar.</param>
/// <param name="AlineacionVertical">Alineación vertical, o <c>null</c> para heredar.</param>
/// <param name="Wrap">Ajuste de texto en varias líneas, o <c>null</c> para heredar.</param>
/// <param name="FormatoNumerico">Formato numérico ("N2", "C2", ...), o <c>null</c> para heredar.</param>
public sealed record ReportStyleOverlay(
    string? Fuente = null,
    float? TamanoFuente = null,
    bool? Negrita = null,
    bool? Cursiva = null,
    string? ColorFuente = null,
    string? ColorFondo = null,
    ReportTextAlignment? AlineacionHorizontal = null,
    ReportVerticalAlignment? AlineacionVertical = null,
    bool? Wrap = null,
    string? FormatoNumerico = null);
