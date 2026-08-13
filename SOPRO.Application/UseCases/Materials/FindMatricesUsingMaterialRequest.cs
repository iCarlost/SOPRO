namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Consulta de matrices donde se usa un material ("Dónde se usa").
/// </summary>
/// <param name="MaterialId">Id del material dentro del alcance de la sesión.</param>
public sealed record FindMatricesUsingMaterialRequest(int MaterialId);
