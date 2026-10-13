using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Cambio de formato de UNA celda dentro de un lote transaccional. Si
/// <see cref="Estilo"/> es <c>null</c> el cambio BORRA el override existente; en
/// otro caso aplica un upsert parcial.
/// </summary>
/// <param name="ClaveVista">Vista lógica; "" = vista por defecto.</param>
/// <param name="ClaveFila">Clave estable de la fila.</param>
/// <param name="NombreColumna">Identificador estable de la columna.</param>
/// <param name="Estilo">Superposición parcial a aplicar, o <c>null</c> para borrar.</param>
public sealed record CambioFormatoCelda(
    string ClaveVista,
    string ClaveFila,
    string NombreColumna,
    ReportStyleOverlay? Estilo);

/// <summary>
/// Cambio de estilo de UNA fila por rol dentro de un lote transaccional. Si
/// <see cref="Estilo"/> es <c>null</c> el cambio BORRA el estilo existente; en
/// otro caso aplica un upsert parcial.
/// </summary>
/// <param name="RolFila">Rol funcional de la fila.</param>
/// <param name="Estilo">Superposición parcial a aplicar, o <c>null</c> para borrar.</param>
public sealed record CambioEstiloFila(
    string RolFila,
    ReportStyleOverlay? Estilo);

/// <summary>
/// Servicio de almacenamiento de la configuración de formato de reportes:
/// overrides por celda (<see cref="FormatoCeldaReporte"/>) y estilos semánticos
/// por rol de fila (<see cref="ConfiguracionEstiloFilaReporte"/>).
///
/// Semántica de override PARCIAL: en cada upsert sólo se escriben las propiedades
/// NO nulas del <see cref="ReportStyleOverlay"/>; una propiedad <c>null</c> deja
/// intacto el valor previo (no lo pisa con null). Si tras aplicar el cambio la
/// fila queda SIN ninguna propiedad de estilo, se elimina (no se guardan filas
/// vacías). La carga masiva hace UNA sola consulta por (ProyectoId, TipoReporte)
/// y devuelve un diccionario en memoria (sin N+1). Los lotes son atómicos:
/// cualquier fallo revierte TODO el lote.
/// </summary>
public sealed class FormatoReporteService
{
    private readonly SOPROContext _context;

    public FormatoReporteService(SOPROContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // ═══════════════════════════ Overrides de celda ═══════════════════════════

    /// <summary>
    /// Upsert parcial del override de estilo de una celda. Devuelve la entidad
    /// persistida o <c>null</c> cuando el resultado queda vacío (y por tanto no se
    /// persiste / se elimina la fila existente).
    /// </summary>
    public FormatoCeldaReporte? UpsertCelda(
        int proyectoId,
        string tipoReporte,
        string claveVista,
        string claveFila,
        string nombreColumna,
        ReportStyleOverlay cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);
        return AplicarCambioCelda(proyectoId, tipoReporte, claveVista, claveFila, nombreColumna, cambios);
    }

    /// <summary>
    /// Elimina el override de una celda. Devuelve <c>true</c> si existía.
    /// </summary>
    public bool EliminarCelda(
        int proyectoId,
        string tipoReporte,
        string claveVista,
        string claveFila,
        string nombreColumna)
    {
        ValidarCabecera(proyectoId, tipoReporte);
        var entidad = BuscarCelda(
            proyectoId,
            tipoReporte,
            claveVista ?? string.Empty,
            NormalizarClaveFila(claveFila),
            NormalizarColumna(nombreColumna));

        if (entidad is null) return false;

        _context.FormatosCeldaReporte.Remove(entidad);
        _context.SaveChanges();
        return true;
    }

    /// <summary>
    /// Carga masiva de overrides de celda del (ProyectoId, TipoReporte) pedido.
    /// Una sola consulta; el diccionario se indexa por la referencia estable
    /// <see cref="ReportCellAddress"/> (incluye la vista, de modo que "" conserva
    /// el rol de comodín que resuelve <c>ReportCellStyleResolver</c>).
    /// </summary>
    public IReadOnlyDictionary<ReportCellAddress, ReportStyleOverlay> CargarCeldas(int proyectoId, string tipoReporte)
    {
        ValidarCabecera(proyectoId, tipoReporte);

        var filas = _context.FormatosCeldaReporte
            .AsNoTracking()
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        var resultado = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
        foreach (var fila in filas)
        {
            var direccion = new ReportCellAddress(
                fila.ClaveVista ?? string.Empty,
                fila.ClaveFila,
                fila.NombreColumna);
            resultado[direccion] = AOverlay(fila);
        }

        return resultado;
    }

    // ═══════════════════ Estilos semánticos por rol de fila ═══════════════════

    /// <summary>
    /// Upsert parcial del estilo semántico de un rol de fila. Devuelve la entidad
    /// persistida o <c>null</c> cuando el resultado queda vacío.
    /// </summary>
    public ConfiguracionEstiloFilaReporte? UpsertEstiloFila(
        int proyectoId,
        string tipoReporte,
        string rolFila,
        ReportStyleOverlay cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);
        return AplicarCambioEstiloFila(proyectoId, tipoReporte, rolFila, cambios);
    }

    /// <summary>
    /// Elimina el estilo semántico de un rol de fila. Devuelve <c>true</c> si existía.
    /// </summary>
    public bool EliminarEstiloFila(int proyectoId, string tipoReporte, string rolFila)
    {
        ValidarCabecera(proyectoId, tipoReporte);
        var entidad = BuscarEstiloFila(proyectoId, tipoReporte, NormalizarRolFila(rolFila));
        if (entidad is null) return false;

        _context.ConfiguracionesEstiloFilaReporte.Remove(entidad);
        _context.SaveChanges();
        return true;
    }

    /// <summary>
    /// Carga masiva de estilos de fila del (ProyectoId, TipoReporte) pedido, en
    /// una sola consulta, indexados por rol.
    /// </summary>
    public IReadOnlyDictionary<string, ReportStyleOverlay> CargarEstilosFila(int proyectoId, string tipoReporte)
    {
        ValidarCabecera(proyectoId, tipoReporte);

        var filas = _context.ConfiguracionesEstiloFilaReporte
            .AsNoTracking()
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        var resultado = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in filas)
            resultado[fila.RolFila] = AOverlay(fila);

        return resultado;
    }

    // ═══════════════════════════ Lotes transaccionales ═══════════════════════════

    /// <summary>
    /// Aplica un lote de cambios de celdas de forma ATÓMICA: si cualquier cambio
    /// falla, se revierte TODO el lote (nada queda persistido).
    /// </summary>
    public void AplicarCeldas(int proyectoId, string tipoReporte, IEnumerable<CambioFormatoCelda> cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);
        ValidarCabecera(proyectoId, tipoReporte);

        using var transaccion = _context.Database.BeginTransaction();
        try
        {
            // UNA consulta: se cargan las filas existentes del (proyecto, tipo)
            // indexadas por dirección estable. El upsert del lote resuelve cada celda
            // en memoria (sin SELECT por celda) y se persiste con UN solo SaveChanges
            // (sin uno por celda): así el costo es O(n), no O(n²).
            var existentes = _context.FormatosCeldaReporte
                .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
                .ToDictionary(f => new ReportCellAddress(
                    f.ClaveVista ?? string.Empty, f.ClaveFila, f.NombreColumna));

            foreach (var cambio in cambios)
            {
                ArgumentNullException.ThrowIfNull(cambio);
                AplicarCambioCelda(
                    proyectoId,
                    tipoReporte,
                    cambio.ClaveVista,
                    cambio.ClaveFila,
                    cambio.NombreColumna,
                    cambio.Estilo,
                    guardar: false,
                    indice: existentes);
            }

            _context.SaveChanges();
            transaccion.Commit();
        }
        catch
        {
            transaccion.Rollback();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Aplica un lote de cambios de estilos de fila de forma ATÓMICA.
    /// </summary>
    public void AplicarEstilosFila(int proyectoId, string tipoReporte, IEnumerable<CambioEstiloFila> cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);
        ValidarCabecera(proyectoId, tipoReporte);

        using var transaccion = _context.Database.BeginTransaction();
        try
        {
            // Una consulta + un SaveChanges por lote (ver AplicarCeldas).
            var existentes = _context.ConfiguracionesEstiloFilaReporte
                .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
                .ToDictionary(f => f.RolFila, StringComparer.Ordinal);

            foreach (var cambio in cambios)
            {
                ArgumentNullException.ThrowIfNull(cambio);
                AplicarCambioEstiloFila(
                    proyectoId, tipoReporte, cambio.RolFila, cambio.Estilo,
                    guardar: false, indice: existentes);
            }

            _context.SaveChanges();
            transaccion.Commit();
        }
        catch
        {
            transaccion.Rollback();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    // ═══════════════════════════ Núcleo de upsert ═══════════════════════════

    private FormatoCeldaReporte? AplicarCambioCelda(
        int proyectoId,
        string tipoReporte,
        string? claveVista,
        string? claveFila,
        string? nombreColumna,
        ReportStyleOverlay? cambios,
        bool guardar = true,
        Dictionary<ReportCellAddress, FormatoCeldaReporte>? indice = null)
    {
        ValidarCabecera(proyectoId, tipoReporte);

        var vista = claveVista ?? string.Empty;
        var fila = NormalizarClaveFila(claveFila);
        var columna = NormalizarColumna(nombreColumna);
        var direccion = new ReportCellAddress(vista, fila, columna);

        // Con índice (lote) la resolución es en memoria; sin él (upsert único) se
        // consulta la base.
        FormatoCeldaReporte? entidad;
        if (indice is not null)
            indice.TryGetValue(direccion, out entidad);
        else
            entidad = BuscarCelda(proyectoId, tipoReporte, vista, fila, columna);

        // Overlay ausente o totalmente vacío = borrar el override (no hay nada que aplicar).
        if (cambios is null || OverlayVacio(cambios))
        {
            if (entidad is null) return null;
            _context.FormatosCeldaReporte.Remove(entidad);
            indice?.Remove(direccion);
            if (guardar) _context.SaveChanges();
            return null;
        }

        if (entidad is null)
        {
            entidad = new FormatoCeldaReporte
            {
                ProyectoId = proyectoId,
                TipoReporte = tipoReporte,
                ClaveVista = vista,
                ClaveFila = fila,
                NombreColumna = columna,
            };
            AplicarOverlay(entidad, cambios);
            _context.FormatosCeldaReporte.Add(entidad);
            if (indice is not null) indice[direccion] = entidad;
        }
        else
        {
            AplicarOverlay(entidad, cambios);
        }

        if (guardar) _context.SaveChanges();
        return entidad;
    }

    private ConfiguracionEstiloFilaReporte? AplicarCambioEstiloFila(
        int proyectoId,
        string tipoReporte,
        string? rolFila,
        ReportStyleOverlay? cambios,
        bool guardar = true,
        Dictionary<string, ConfiguracionEstiloFilaReporte>? indice = null)
    {
        ValidarCabecera(proyectoId, tipoReporte);
        var rol = NormalizarRolFila(rolFila);

        ConfiguracionEstiloFilaReporte? entidad;
        if (indice is not null)
            indice.TryGetValue(rol, out entidad);
        else
            entidad = BuscarEstiloFila(proyectoId, tipoReporte, rol);

        // Overlay ausente o totalmente vacío = borrar el estilo (no hay nada que aplicar).
        if (cambios is null || OverlayVacio(cambios))
        {
            if (entidad is null) return null;
            _context.ConfiguracionesEstiloFilaReporte.Remove(entidad);
            indice?.Remove(rol);
            if (guardar) _context.SaveChanges();
            return null;
        }

        if (entidad is null)
        {
            entidad = new ConfiguracionEstiloFilaReporte
            {
                ProyectoId = proyectoId,
                TipoReporte = tipoReporte,
                RolFila = rol,
            };
            AplicarOverlay(entidad, cambios);
            _context.ConfiguracionesEstiloFilaReporte.Add(entidad);
            if (indice is not null) indice[rol] = entidad;
        }
        else
        {
            AplicarOverlay(entidad, cambios);
        }

        if (guardar) _context.SaveChanges();
        return entidad;
    }

    private FormatoCeldaReporte? BuscarCelda(int proyectoId, string tipoReporte, string claveVista, string claveFila, string nombreColumna)
        => _context.FormatosCeldaReporte.FirstOrDefault(f =>
            f.ProyectoId == proyectoId &&
            f.TipoReporte == tipoReporte &&
            f.ClaveVista == claveVista &&
            f.ClaveFila == claveFila &&
            f.NombreColumna == nombreColumna);

    private ConfiguracionEstiloFilaReporte? BuscarEstiloFila(int proyectoId, string tipoReporte, string rolFila)
        => _context.ConfiguracionesEstiloFilaReporte.FirstOrDefault(f =>
            f.ProyectoId == proyectoId &&
            f.TipoReporte == tipoReporte &&
            f.RolFila == rolFila);

    // ═══════════════════════════ Overlay ↔ entidad ═══════════════════════════

    private static void AplicarOverlay(FormatoCeldaReporte entidad, ReportStyleOverlay overlay)
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

    private static void AplicarOverlay(ConfiguracionEstiloFilaReporte entidad, ReportStyleOverlay overlay)
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

    private static ReportStyleOverlay AOverlay(FormatoCeldaReporte entidad) => new(
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

    private static ReportStyleOverlay AOverlay(ConfiguracionEstiloFilaReporte entidad) => new(
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

    /// <summary>
    /// Indica si un overlay no aporta NINGUNA propiedad. En los flujos de upsert un
    /// overlay vacío se interpreta como borrado del override.
    /// </summary>
    private static bool OverlayVacio(ReportStyleOverlay overlay)
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

    // ═══════════════════════════ Alineaciones ═══════════════════════════

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

    // ═══════════════════════════ Validaciones ═══════════════════════════

    private static void ValidarCabecera(int proyectoId, string tipoReporte)
    {
        if (proyectoId <= 0) throw new ArgumentOutOfRangeException(nameof(proyectoId));
        if (string.IsNullOrWhiteSpace(tipoReporte))
            throw new ArgumentException("El tipo de reporte es requerido.", nameof(tipoReporte));
    }

    private static string NormalizarClaveFila(string? claveFila)
    {
        if (string.IsNullOrWhiteSpace(claveFila))
            throw new ArgumentException("La clave de fila es requerida.", nameof(claveFila));
        return claveFila.Trim();
    }

    private static string NormalizarColumna(string? nombreColumna)
    {
        if (string.IsNullOrWhiteSpace(nombreColumna))
            throw new ArgumentException("El nombre de columna es requerido.", nameof(nombreColumna));
        return nombreColumna.Trim();
    }

    private static string NormalizarRolFila(string? rolFila)
    {
        if (string.IsNullOrWhiteSpace(rolFila))
            throw new ArgumentException("El rol de fila es requerido.", nameof(rolFila));
        return rolFila.Trim();
    }
}
