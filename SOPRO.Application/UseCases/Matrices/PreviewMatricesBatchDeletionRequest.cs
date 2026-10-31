namespace SOPRO.Application.UseCases.Matrices;

/// <summary>Identifica el lote de matrices a previsualizar.</summary>
/// <param name="Ids">Identificadores seleccionados en la UI.</param>
public sealed record PreviewMatricesBatchDeletionRequest(IReadOnlyList<int> Ids);
