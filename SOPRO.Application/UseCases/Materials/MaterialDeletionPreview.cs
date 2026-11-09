namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Impacto de eliminar un material: cuántos componentes lo usan y en qué
/// matrices (solo el alcance real de la eliminación: el proyecto de la sesión).
/// Con <see cref="ComponentCount"/> = 0 la eliminación es limpia.
/// <see cref="CrossProjectReferenceCount"/> indica referencias fuera del
/// alcance: no se eliminan, pero impiden eliminar el material (DeleteMaterial
/// devuelve Conflict).
/// </summary>
public sealed record MaterialDeletionPreview(
    int ComponentCount,
    IReadOnlyList<MaterialUsageInMatrix> Matrices,
    int CrossProjectReferenceCount = 0,
    IReadOnlyList<MaterialMachineryUsage>? MachineryUsages = null)
{
    public int AffectedMatrixCount => Matrices.Count;
    public IReadOnlyList<MaterialMachineryUsage> LinkedMachinery => MachineryUsages ?? Array.Empty<MaterialMachineryUsage>();
}

public sealed record MaterialMachineryUsage(int MaquinariaId, string Clave, string Descripcion, string Rol);
