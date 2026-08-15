namespace SOPRO.Application.UseCases.Materials;

/// <summary>Resultado del guardado de un material.</summary>
/// <param name="MaterialId">Id persistido.</param>
/// <param name="IsNew">True si fue una creación.</param>
/// <param name="TriggeredRecalculation">True si el guardado disparó la propagación de precios.</param>
public sealed record SaveMaterialResult(int MaterialId, bool IsNew, bool TriggeredRecalculation);