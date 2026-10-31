namespace SOPRO.Application.UseCases.Matrices;

/// <summary>
/// Impacto confirmado de una eliminación en lote de matrices. Solo existe cuando
/// la transacción se confirmó en la base.
/// </summary>
/// <param name="DeletedIds">Ids efectivamente eliminados (ordenados, sin repetir).</param>
/// <param name="DeletedComponents">
/// Componentes de las matrices eliminadas (se borran con su matriz padre).
/// </param>
/// <param name="RemovedExternalAuxiliarComponents">
/// Aristas auxiliares de matrices fuera de la selección que apuntaban a una matriz
/// eliminada y se quitaron para no dejar referencias inválidas.
/// </param>
/// <param name="RecalculatedMatrixCount">
/// Matrices recalculadas por el cierre: padres afectados más sus ascendientes.
/// </param>
/// <param name="AffectedConceptCount">
/// Hojas del presupuesto recalculadas por el cierre.
/// </param>
/// <param name="AffectedAggregatorCount">
/// Agrupadores del proyecto recalculados por el cierre.
/// </param>
public sealed record DeleteMatricesBatchResult(
    IReadOnlyList<int> DeletedIds,
    int DeletedComponents,
    int RemovedExternalAuxiliarComponents = 0,
    int RecalculatedMatrixCount = 0,
    int AffectedConceptCount = 0,
    int AffectedAggregatorCount = 0);
