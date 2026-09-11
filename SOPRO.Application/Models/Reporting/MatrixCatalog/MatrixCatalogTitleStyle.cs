namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Intención visual neutral del título del documento (semántica del reporte,
/// no política de renderer).
/// </summary>
/// <remarks>
/// El fondo del título es política de cada renderer (Excel #33334C, PDF #1F4E79)
/// y NO forma parte del modelo neutral: un solo valor no puede reproducir ambos
/// goldens de N7-18a. El color de texto sí es semántico porque viene de la
/// configuración del usuario (<c>ConfiguracionTituloReporte.ColorTexto</c>).
/// </remarks>
/// <param name="FontName">Nombre de la tipografía.</param>
/// <param name="Size">Tamaño de fuente en puntos.</param>
/// <param name="Bold">Negrita.</param>
/// <param name="Italic">Cursiva.</param>
/// <param name="TextColorHex">Color de texto en hexadecimal (#RRGGBB).</param>
public sealed record MatrixCatalogTitleStyle(
    string FontName,
    double Size,
    bool Bold,
    bool Italic,
    string TextColorHex);