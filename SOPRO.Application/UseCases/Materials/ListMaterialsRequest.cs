namespace SOPRO.Application.UseCases.Materials;

/// <summary>Filtros de listado del catálogo de materiales.</summary>
public sealed record ListMaterialsRequest(
    string? SearchText = null,
    bool OnlyProjectItems = false,
    bool OnlyMasterItems = false);