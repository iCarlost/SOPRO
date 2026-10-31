namespace SOPRO.Application.UseCases.Matrices;

/// <summary>
/// Identifica el lote de matrices a eliminar. Los Ids se validan contra el
/// proyecto de la sesión: o todos pertenecen a él o el lote completo se rechaza.
/// Los Ids repetidos se deduplican; un Id no positivo invalida el lote.
/// </summary>
/// <param name="Ids">Identificadores de las matrices seleccionadas en la UI.</param>
public sealed record DeleteMatricesBatchRequest(IReadOnlyList<int> Ids);
