using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Semántica con la que <see cref="CambioFormatoCeldaCommand.ProposedOverlay"/>
/// se interpreta contra el estado persistido.
/// </summary>
public enum OverlayApplyMode
{
    /// <summary>
    /// DELTA parcial (por defecto): las propiedades no nulas del propuesto ganan
    /// y las nulas conservan lo persistido. Es la edición normal.
    /// </summary>
    Merge = 0,

    /// <summary>
    /// Estado COMPLETO del destino: las propiedades nulas del propuesto BORRAN lo
    /// persistido (<c>null</c> borra el override entero). Es el modo de Undo/Redo,
    /// donde "antes" y "después" son snapshots completos y no deltas: revertir un
    /// cambio debe quitar también las propiedades que la edición añadió.
    /// </summary>
    Replace = 1,
}

/// <summary>
/// Operación de escritura de formato para UNA celda dentro de un lote atómico
/// con base esperada (optimistic base por address).
///
/// <see cref="ExpectedOverlay"/> es el estado EXACTO que la vista cree que hay
/// persistido para la dirección: <c>null</c> (o un overlay vacío) significa que
/// la celda NO tiene override. Si la base no coincide, el lote completo se
/// aborta con conflicto y no se escribe nada.
///
/// <see cref="ProposedOverlay"/> es el estado deseado según
/// <see cref="ApplyMode"/>: <c>null</c> (o un overlay vacío) BORRA el override;
/// en <see cref="OverlayApplyMode.Merge"/> un overlay parcial aplica SOLO sus
/// propiedades no nulas sobre la fila existente; en
/// <see cref="OverlayApplyMode.Replace"/> el overlay es el estado exacto del
/// destino (las propiedades nulas quedan nulas).
/// </summary>
/// <param name="ClaveVista">Vista lógica; "" = vista por defecto.</param>
/// <param name="ClaveFila">Clave estable de la fila.</param>
/// <param name="NombreColumna">Identificador estable de la columna.</param>
/// <param name="ExpectedOverlay">Base esperada exacta, o <c>null</c> si no existe.</param>
/// <param name="ProposedOverlay">Estado a persistir, o <c>null</c> para borrar.</param>
/// <param name="ApplyMode">
/// Fusión parcial (<see cref="OverlayApplyMode.Merge"/>, por defecto) o
/// reemplazo exacto del overlay completo (<see cref="OverlayApplyMode.Replace"/>).
/// </param>
public sealed record CambioFormatoCeldaCommand(
    string ClaveVista,
    string ClaveFila,
    string NombreColumna,
    ReportStyleOverlay? ExpectedOverlay,
    ReportStyleOverlay? ProposedOverlay,
    OverlayApplyMode ApplyMode = OverlayApplyMode.Merge);

/// <summary>
/// Estado confirmado de las direcciones TOCADAS por un lote de formato. Las
/// direcciones ausentes del diccionario quedaron BORRADAS; las presentes
/// contienen el overlay completo persistido.
/// </summary>
public sealed class FormatoCeldasEditBatchSnapshot
{
    internal FormatoCeldasEditBatchSnapshot(
        int proyectoId,
        string tipoReporte,
        IReadOnlyDictionary<ReportCellAddress, ReportStyleOverlay> overlays)
    {
        ProyectoId = proyectoId;
        TipoReporte = tipoReporte ?? throw new ArgumentNullException(nameof(tipoReporte));
        Overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
    }

    public int ProyectoId { get; }

    public string TipoReporte { get; }

    /// <summary>Overlay confirmado por dirección tocada; ausente = sin override.</summary>
    public IReadOnlyDictionary<ReportCellAddress, ReportStyleOverlay> Overlays { get; }
}

/// <summary>
/// Resultado del comando de formato por celda: clasificación de la operación y,
/// SOLO si se confirmó, el estado persistido de las direcciones tocadas.
/// </summary>
public sealed class FormatoCeldasEditBatchResult
{
    internal FormatoCeldasEditBatchResult(
        BudgetOperationResult operation,
        FormatoCeldasEditBatchSnapshot? confirmed,
        string? diagnostic)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        Confirmed = confirmed;
        Diagnostic = diagnostic;
    }

    public BudgetOperationResult Operation { get; }

    public BudgetOperationStatus Status => Operation.Status;

    public bool IsCommitted => Operation.IsCommitted;

    /// <summary>Mensaje seguro para el usuario.</summary>
    public string? Message => Operation.Message;

    /// <summary>Detalle técnico para registro y pruebas; nunca se muestra en la UI.</summary>
    public string? Diagnostic { get; }

    /// <summary>Estado confirmado; null si la operación no se confirmó.</summary>
    public FormatoCeldasEditBatchSnapshot? Confirmed { get; }
}
