namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Impacto de la eliminación en lote ANTES de escribir: lo que la UI necesita
/// para la confirmación única (número de recursos seleccionados + componentes y
/// matrices afectadas). Se calcula con lecturas sin seguimiento y no modifica la
/// base.
///
/// La <see cref="Fingerprint"/> resume la selección y todas sus dependencias; la
/// UI la envía en <see cref="DeleteCatalogBatchRequest.ExpectedFingerprint"/> para
/// que el comando rechace el lote si algo cambió entre confirmar y eliminar.
/// </summary>
/// <param name="Kind">Catálogo del lote.</param>
/// <param name="Ids">Ids seleccionados, sin repetir y ordenados.</param>
/// <param name="SelectedCount">Número de recursos seleccionados (ya deduplicado).</param>
/// <param name="AffectedComponentCount">Componentes de matriz que se eliminarían.</param>
/// <param name="AffectedMatrixCount">Matrices que contienen directamente esos componentes.</param>
/// <param name="RecalculatedMatrixCount">
/// Matrices que se recalcularían: las afectadas más sus ascendientes auxiliares
/// (sin repetir). Cero si el lote no está en uso.
/// </param>
/// <param name="AffectedConceptCount">Hojas del presupuesto que referencian alguna matriz recalculada.</param>
/// <param name="AffectedAggregatorCount">Agrupadores del proyecto que se recalcularían.</param>
/// <param name="Fingerprint">Huella estable de la selección y sus dependencias.</param>
public sealed record CatalogBatchDeletionPreview(
    CatalogItemKind Kind,
    IReadOnlyList<int> Ids,
    int SelectedCount,
    int AffectedComponentCount,
    int AffectedMatrixCount,
    int RecalculatedMatrixCount,
    int AffectedConceptCount,
    int AffectedAggregatorCount,
    string Fingerprint)
{
    /// <summary>True si el lote está en uso y su eliminación exige recálculo.</summary>
    public bool IsInUse => AffectedComponentCount > 0;

    /// <summary>True si se propagaría el recálculo a matrices y presupuesto.</summary>
    public bool TriggeredRecalculation => RecalculatedMatrixCount > 0;
}
