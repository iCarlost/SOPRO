namespace SOPRO.Application.UseCases.Materials;

/// <summary>Referencia neutral a una matriz que usa un material.</summary>
public sealed record MaterialUsageInMatrix(int MatrizId, string Clave, string Descripcion);