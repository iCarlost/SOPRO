using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Reglas ÚNICAS de mapeo y normalización de los overrides de formato por celda.
/// Las comparten el servicio histórico (<see cref="FormatoReporteService"/>, usado
/// para lecturas y siembra) y el comando transaccional
/// (<see cref="FormatoCeldasCommandService"/>), de modo que claves, recortes y
/// conversiones overlay ↔ entidad no diverjan entre rutas.
/// </summary>
internal static class FormatoCeldaCore
{
    /// <summary>"" es la vista canónica por defecto (nunca NULL: SQLite admite duplicados con NULL).</summary>
    internal static string NormalizarVista(string? claveVista) => claveVista ?? string.Empty;

    internal static string NormalizarClaveFila(string? claveFila)
    {
        if (string.IsNullOrWhiteSpace(claveFila))
            throw new ArgumentException("La clave de fila es requerida.", nameof(claveFila));
        return claveFila.Trim();
    }

    internal static string NormalizarColumna(string? nombreColumna)
    {
        if (string.IsNullOrWhiteSpace(nombreColumna))
            throw new ArgumentException("El nombre de columna es requerido.", nameof(nombreColumna));
        return nombreColumna.Trim();
    }

    internal static string NormalizarRolFila(string? rolFila)
    {
        if (string.IsNullOrWhiteSpace(rolFila))
            throw new ArgumentException("El rol de fila es requerido.", nameof(rolFila));
        return rolFila.Trim();
    }

    internal static void ValidarCabecera(int proyectoId, string tipoReporte)
    {
        if (proyectoId <= 0) throw new ArgumentOutOfRangeException(nameof(proyectoId));
        if (string.IsNullOrWhiteSpace(tipoReporte))
            throw new ArgumentException("El tipo de reporte es requerido.", nameof(tipoReporte));
    }

    internal static ReportCellAddress Direccion(FormatoCeldaReporte entidad)
        => new(
            NormalizarVista(entidad.ClaveVista),
            entidad.ClaveFila,
            entidad.NombreColumna);

    /// <summary>
    /// Indica si un overlay no aporta NINGUNA propiedad. En las rutas de escritura
    /// un overlay vacío se interpreta igual que <c>null</c>: borra el override.
    /// </summary>
    internal static bool OverlayVacio(ReportStyleOverlay overlay)
        => overlay.Fuente is null
           && overlay.TamanoFuente is null
           && overlay.Negrita is null
           && overlay.Cursiva is null
           && overlay.ColorFuente is null
           && overlay.ColorFondo is null
           && overlay.AlineacionHorizontal is null
           && overlay.AlineacionVertical is null
           && overlay.Wrap is null
           && overlay.FormatoNumerico is null;

    /// <summary>
    /// Estado canónico de una celda: <c>null</c> cuando no hay override (ausente o
    /// vacío). Las filas persistidas nunca son vacías, así que tratar ambos casos
    /// igual no pierde información y hace comparables la base y la vista.
    /// </summary>
    internal static ReportStyleOverlay? NormalizarEstado(ReportStyleOverlay? overlay)
        => overlay is null || OverlayVacio(overlay) ? null : overlay;

    /// <summary>
    /// Fusión parcial idéntica a la que aplica <see cref="AplicarOverlay(FormatoCeldaReporte, ReportStyleOverlay)"/>:
    /// una propiedad no nula de <paramref name="propuesta"/> gana; una nula conserva
    /// la del estado previo. Se usa para simular el resultado de operaciones
    /// encadenadas sobre la MISMA dirección sin tocar la entidad.
    /// </summary>
    internal static ReportStyleOverlay Combinar(ReportStyleOverlay? previo, ReportStyleOverlay propuesta)
        => new(
            Fuente: propuesta.Fuente ?? previo?.Fuente,
            TamanoFuente: propuesta.TamanoFuente ?? previo?.TamanoFuente,
            Negrita: propuesta.Negrita ?? previo?.Negrita,
            Cursiva: propuesta.Cursiva ?? previo?.Cursiva,
            ColorFuente: propuesta.ColorFuente ?? previo?.ColorFuente,
            ColorFondo: propuesta.ColorFondo ?? previo?.ColorFondo,
            AlineacionHorizontal: propuesta.AlineacionHorizontal ?? previo?.AlineacionHorizontal,
            AlineacionVertical: propuesta.AlineacionVertical ?? previo?.AlineacionVertical,
            Wrap: propuesta.Wrap ?? previo?.Wrap,
            FormatoNumerico: propuesta.FormatoNumerico ?? previo?.FormatoNumerico);

    /// <summary>
    /// Fija el estado COMPLETO de la entidad (todas las propiedades, incluidas las
    /// nulas). Se usa al materializar un lote ya simulado, donde el overlay final
    /// representa el estado exacto: tras un borrar+recrear no deben sobrevivir
    /// propiedades de la fila anterior.
    /// </summary>
    internal static void EstablecerOverlay(FormatoCeldaReporte entidad, ReportStyleOverlay overlay)
    {
        entidad.Fuente = overlay.Fuente;
        entidad.TamanoFuente = overlay.TamanoFuente;
        entidad.Negrita = overlay.Negrita;
        entidad.Cursiva = overlay.Cursiva;
        entidad.ColorFuente = overlay.ColorFuente;
        entidad.ColorFondo = overlay.ColorFondo;
        entidad.AlineacionHorizontal = overlay.AlineacionHorizontal is null
            ? null
            : ATexto(overlay.AlineacionHorizontal.Value);
        entidad.AlineacionVertical = overlay.AlineacionVertical is null
            ? null
            : ATexto(overlay.AlineacionVertical.Value);
        entidad.Wrap = overlay.Wrap;
        entidad.FormatoNumerico = overlay.FormatoNumerico;
        entidad.FechaModificacion = DateTime.Now;
    }

    internal static void AplicarOverlay(FormatoCeldaReporte entidad, ReportStyleOverlay overlay)
    {
        if (overlay.Fuente is not null) entidad.Fuente = overlay.Fuente;
        if (overlay.TamanoFuente is not null) entidad.TamanoFuente = overlay.TamanoFuente;
        if (overlay.Negrita is not null) entidad.Negrita = overlay.Negrita;
        if (overlay.Cursiva is not null) entidad.Cursiva = overlay.Cursiva;
        if (overlay.ColorFuente is not null) entidad.ColorFuente = overlay.ColorFuente;
        if (overlay.ColorFondo is not null) entidad.ColorFondo = overlay.ColorFondo;
        if (overlay.AlineacionHorizontal is not null) entidad.AlineacionHorizontal = ATexto(overlay.AlineacionHorizontal.Value);
        if (overlay.AlineacionVertical is not null) entidad.AlineacionVertical = ATexto(overlay.AlineacionVertical.Value);
        if (overlay.Wrap is not null) entidad.Wrap = overlay.Wrap;
        if (overlay.FormatoNumerico is not null) entidad.FormatoNumerico = overlay.FormatoNumerico;
        entidad.FechaModificacion = DateTime.Now;
    }

    internal static void AplicarOverlay(ConfiguracionEstiloFilaReporte entidad, ReportStyleOverlay overlay)
    {
        if (overlay.Fuente is not null) entidad.Fuente = overlay.Fuente;
        if (overlay.TamanoFuente is not null) entidad.TamanoFuente = overlay.TamanoFuente;
        if (overlay.Negrita is not null) entidad.Negrita = overlay.Negrita;
        if (overlay.Cursiva is not null) entidad.Cursiva = overlay.Cursiva;
        if (overlay.ColorFuente is not null) entidad.ColorFuente = overlay.ColorFuente;
        if (overlay.ColorFondo is not null) entidad.ColorFondo = overlay.ColorFondo;
        if (overlay.AlineacionHorizontal is not null) entidad.AlineacionHorizontal = ATexto(overlay.AlineacionHorizontal.Value);
        if (overlay.AlineacionVertical is not null) entidad.AlineacionVertical = ATexto(overlay.AlineacionVertical.Value);
        if (overlay.Wrap is not null) entidad.Wrap = overlay.Wrap;
        if (overlay.FormatoNumerico is not null) entidad.FormatoNumerico = overlay.FormatoNumerico;
        entidad.FechaModificacion = DateTime.Now;
    }

    internal static ReportStyleOverlay AOverlay(FormatoCeldaReporte entidad) => new(
        Fuente: entidad.Fuente,
        TamanoFuente: entidad.TamanoFuente,
        Negrita: entidad.Negrita,
        Cursiva: entidad.Cursiva,
        ColorFuente: entidad.ColorFuente,
        ColorFondo: entidad.ColorFondo,
        AlineacionHorizontal: ParsearAlineacionHorizontal(entidad.AlineacionHorizontal),
        AlineacionVertical: ParsearAlineacionVertical(entidad.AlineacionVertical),
        Wrap: entidad.Wrap,
        FormatoNumerico: entidad.FormatoNumerico);

    internal static ReportStyleOverlay AOverlay(ConfiguracionEstiloFilaReporte entidad) => new(
        Fuente: entidad.Fuente,
        TamanoFuente: entidad.TamanoFuente,
        Negrita: entidad.Negrita,
        Cursiva: entidad.Cursiva,
        ColorFuente: entidad.ColorFuente,
        ColorFondo: entidad.ColorFondo,
        AlineacionHorizontal: ParsearAlineacionHorizontal(entidad.AlineacionHorizontal),
        AlineacionVertical: ParsearAlineacionVertical(entidad.AlineacionVertical),
        Wrap: entidad.Wrap,
        FormatoNumerico: entidad.FormatoNumerico);

    private static string ATexto(ReportTextAlignment valor) => valor.ToString();

    private static string ATexto(ReportVerticalAlignment valor) => valor.ToString();

    private static ReportTextAlignment? ParsearAlineacionHorizontal(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor.Trim().ToLowerInvariant() switch
        {
            "izquierda" or "left" or "0" => ReportTextAlignment.Izquierda,
            "centro" or "center" or "centre" or "1" => ReportTextAlignment.Centro,
            "derecha" or "right" or "2" => ReportTextAlignment.Derecha,
            "justificado" or "justify" or "justified" or "3" => ReportTextAlignment.Justificado,
            _ => null,
        };
    }

    private static ReportVerticalAlignment? ParsearAlineacionVertical(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor.Trim().ToLowerInvariant() switch
        {
            "superior" or "top" or "0" => ReportVerticalAlignment.Superior,
            "medio" or "middle" or "center" or "centre" or "1" => ReportVerticalAlignment.Medio,
            "inferior" or "bottom" or "2" => ReportVerticalAlignment.Inferior,
            _ => null,
        };
    }
}
