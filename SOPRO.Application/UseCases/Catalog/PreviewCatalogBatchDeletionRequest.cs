namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Identifica el lote homogéneo cuya eliminación se quiere previsualizar. La
/// previsualización es de solo lectura y devuelve los mismos errores tipados que
/// el comando (<c>NotFound</c>, <c>Conflict</c>, <c>NotSupported</c>,
/// <c>Validation</c>).
/// </summary>
/// <param name="Kind">Catálogo del que provienen todos los Ids.</param>
/// <param name="Ids">Identificadores seleccionados en la UI.</param>
public sealed record PreviewCatalogBatchDeletionRequest(CatalogItemKind Kind, IReadOnlyList<int> Ids);
