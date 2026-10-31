namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Impacto confirmado de una eliminación en lote: solo existe cuando la
/// operación se confirmó en la base. Es la instantánea que la UI usa para
/// refrescar el catálogo y el presupuesto sin volver a consultar todo.
/// </summary>
/// <param name="Kind">Catálogo del lote eliminado.</param>
/// <param name="DeletedIds">Ids efectivamente eliminados (ordenados, sin repetir).</param>
/// <param name="DeletedComponents">Componentes de matriz eliminados por referenciar el lote.</param>
/// <param name="AffectedMatrixCount">Matrices que contenían directamente algún componente eliminado.</param>
/// <param name="RecalculatedMatrixCount">
/// Matrices recalculadas por el cierre: las afectadas más sus ascendientes
/// auxiliares (sin repetir). Cero si el lote no estaba en uso.
/// </param>
/// <param name="AffectedConceptCount">
/// Hojas del presupuesto del proyecto que referencian alguna matriz recalculada.
/// </param>
/// <param name="AffectedAggregatorCount">
/// Agrupadores del proyecto recalculados (la política de agrupadores recalcula
/// todos los del proyecto afectado).
/// </param>
public sealed record DeleteCatalogBatchResult(
    CatalogItemKind Kind,
    IReadOnlyList<int> DeletedIds,
    int DeletedComponents,
    int AffectedMatrixCount,
    int RecalculatedMatrixCount,
    int AffectedConceptCount,
    int AffectedAggregatorCount)
{
    /// <summary>True si se propagó el recálculo a matrices y presupuesto.</summary>
    public bool TriggeredRecalculation => RecalculatedMatrixCount > 0;
}
