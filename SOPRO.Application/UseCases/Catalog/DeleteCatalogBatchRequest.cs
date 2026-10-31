namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Identifica el lote homogéneo de insumos a eliminar. Los Ids se validan
/// contra el alcance de la sesión: o todos pertenecen al proyecto de la sesión
/// (o al catálogo maestro cuando la sesión es el maestro) o el lote completo se
/// rechaza. Los Ids repetidos se deduplican; un Id no positivo invalida el lote.
/// </summary>
/// <param name="Kind">Catálogo del que provienen todos los Ids.</param>
/// <param name="Ids">Identificadores seleccionados en la UI.</param>
/// <param name="ExpectedFingerprint">
/// Huella devuelta por <see cref="PreviewCatalogBatchDeletion"/> y mostrada al
/// usuario en la confirmación. Si se envía, el comando recalcula el plan dentro
/// de su transacción y rechaza el lote con <c>Conflict</c> cuando la selección o
/// sus dependencias cambiaron. Opcional: sin huella se conserva el
/// comportamiento previo.
/// </param>
public sealed record DeleteCatalogBatchRequest(
    CatalogItemKind Kind,
    IReadOnlyList<int> Ids,
    string? ExpectedFingerprint = null);
