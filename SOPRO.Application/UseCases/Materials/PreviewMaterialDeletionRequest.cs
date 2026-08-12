namespace SOPRO.Application.UseCases.Materials;

/// <summary>Identifica el material cuyo impacto de eliminación se quiere previsualizar.</summary>
public sealed record PreviewMaterialDeletionRequest(int MaterialId);