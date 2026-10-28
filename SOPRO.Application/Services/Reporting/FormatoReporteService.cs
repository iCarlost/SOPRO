using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services.Reporting;

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
/// Servicio de LECTURA y de siembra de la configuración de formato de reportes:
/// overrides por celda (<see cref="FormatoCeldaReporte"/>) y estilos semánticos
/// por rol de fila (<see cref="ConfiguracionEstiloFilaReporte"/>).
///
/// Semántica de override PARCIAL: en cada upsert sólo se escriben las propiedades
/// NO nulas del <see cref="ReportStyleOverlay"/>; una propiedad <c>null</c> deja
/// intacto el valor previo (no lo pisa con null). Si tras aplicar el cambio la
/// fila queda SIN ninguna propiedad de estilo, se elimina (no se guardan filas
/// vacías). La carga masiva hace UNA sola consulta por (ProyectoId, TipoReporte)
/// y devuelve un diccionario en memoria (sin N+1).
///
/// IMPORTANTE: la ruta VIVA de escritura por lote de celdas es
/// <see cref="FormatoCeldasCommandService"/>, que abre un contexto nuevo,
/// valida la base esperada de CADA dirección y confirma con una sola
/// transacción. Los métodos de escritura de esta clase se conservan para
/// siembra/pruebas y para estilos por rol; ningún formulario productivo los usa
/// para persistir formato de celdas.
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
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);
        var entidad = BuscarCelda(
            proyectoId,
            tipoReporte,
            FormatoCeldaCore.NormalizarVista(claveVista),
            FormatoCeldaCore.NormalizarClaveFila(claveFila),
            FormatoCeldaCore.NormalizarColumna(nombreColumna));

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
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        var filas = _context.FormatosCeldaReporte
            .AsNoTracking()
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        var resultado = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
        foreach (var fila in filas)
            resultado[FormatoCeldaCore.Direccion(fila)] = FormatoCeldaCore.AOverlay(fila);

        return resultado;
    }

    /// <summary>
    /// Borrado MASIVO de los overrides de celda del (ProyectoId, TipoReporte)
    /// pedido: se cargan las filas del par con UNA sola consulta, se eliminan en
    /// bloque y se persisten con UN único <c>SaveChanges</c>. Devuelve el número de
    /// overrides eliminados. NO toca otros proyectos ni otros tipos de reporte del
    /// mismo proyecto. No es una ruta productiva de formato por celda: el reset de
    /// cada módulo lo invoca como operación de configuración independiente.
    /// </summary>
    public int EliminarCeldas(int proyectoId, string tipoReporte)
    {
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        var filas = _context.FormatosCeldaReporte
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        if (filas.Count == 0) return 0;

        _context.FormatosCeldaReporte.RemoveRange(filas);
        _context.SaveChanges();
        return filas.Count;
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
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);
        var entidad = BuscarEstiloFila(proyectoId, tipoReporte, FormatoCeldaCore.NormalizarRolFila(rolFila));
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
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        var filas = _context.ConfiguracionesEstiloFilaReporte
            .AsNoTracking()
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        var resultado = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in filas)
            resultado[fila.RolFila] = FormatoCeldaCore.AOverlay(fila);

        return resultado;
    }

    /// <summary>
    /// Borrado MASIVO de los estilos semánticos de fila del (ProyectoId,
    /// TipoReporte) pedido, con el mismo patrón que <see cref="EliminarCeldas"/>
    /// (una consulta + un <c>SaveChanges</c>). Devuelve el número de estilos
    /// eliminados. NO toca otros proyectos ni otros tipos de reporte.
    /// </summary>
    public int EliminarEstilosFila(int proyectoId, string tipoReporte)
    {
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        var filas = _context.ConfiguracionesEstiloFilaReporte
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        if (filas.Count == 0) return 0;

        _context.ConfiguracionesEstiloFilaReporte.RemoveRange(filas);
        _context.SaveChanges();
        return filas.Count;
    }

    // ═══════════════════════════ Lotes transaccionales ═══════════════════════════

    /// <summary>
    /// Aplica un lote de cambios de estilos de fila de forma ATÓMICA.
    /// </summary>
    public void AplicarEstilosFila(int proyectoId, string tipoReporte, IEnumerable<CambioEstiloFila> cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        using var transaccion = _context.Database.BeginTransaction();
        try
        {
            // Una consulta + un SaveChanges por lote.
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
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        var vista = FormatoCeldaCore.NormalizarVista(claveVista);
        var fila = FormatoCeldaCore.NormalizarClaveFila(claveFila);
        var columna = FormatoCeldaCore.NormalizarColumna(nombreColumna);
        var direccion = new ReportCellAddress(vista, fila, columna);

        // Con índice (lote) la resolución es en memoria; sin él (upsert único) se
        // consulta la base.
        FormatoCeldaReporte? entidad;
        if (indice is not null)
            indice.TryGetValue(direccion, out entidad);
        else
            entidad = BuscarCelda(proyectoId, tipoReporte, vista, fila, columna);

        // Overlay ausente o totalmente vacío = borrar el override (no hay nada que aplicar).
        if (cambios is null || FormatoCeldaCore.OverlayVacio(cambios))
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
            FormatoCeldaCore.AplicarOverlay(entidad, cambios);
            _context.FormatosCeldaReporte.Add(entidad);
            if (indice is not null) indice[direccion] = entidad;
        }
        else
        {
            FormatoCeldaCore.AplicarOverlay(entidad, cambios);
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
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);
        var rol = FormatoCeldaCore.NormalizarRolFila(rolFila);

        ConfiguracionEstiloFilaReporte? entidad;
        if (indice is not null)
            indice.TryGetValue(rol, out entidad);
        else
            entidad = BuscarEstiloFila(proyectoId, tipoReporte, rol);

        // Overlay ausente o totalmente vacío = borrar el estilo (no hay nada que aplicar).
        if (cambios is null || FormatoCeldaCore.OverlayVacio(cambios))
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
            FormatoCeldaCore.AplicarOverlay(entidad, cambios);
            _context.ConfiguracionesEstiloFilaReporte.Add(entidad);
            if (indice is not null) indice[rol] = entidad;
        }
        else
        {
            FormatoCeldaCore.AplicarOverlay(entidad, cambios);
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
}
