namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Impacto de eliminar un material: cuántos componentes lo usan y en qué
/// matrices. Con <see cref="ComponentCount"/> = 0 la eliminación es limpia.
/// </summary>
public sealed record MaterialDeletionPreview(int ComponentCount, IReadOnlyList<MaterialUsageInMatrix> Matrices)
{
    public int AffectedMatrixCount => Matrices.Count;
}