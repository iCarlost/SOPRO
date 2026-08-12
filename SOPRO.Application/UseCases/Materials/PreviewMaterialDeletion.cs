using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): previsualiza el impacto de eliminar un material:
/// cuántos componentes de matriz lo usan y en qué matrices. No modifica nada.
/// El cliente decide la confirmación al usuario a partir de la respuesta.
/// </summary>
public sealed class PreviewMaterialDeletion
{
    public async Task<Result<MaterialDeletionPreview>> Execute(
        ProjectSessionInfo session,
        PreviewMaterialDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = session.Context;

        var componentes = await context.ComponentesMatriz
            .AsNoTracking()
            .Where(c => c.MaterialId == request.MaterialId)
            .ToListAsync(cancellationToken);

        if (componentes.Count == 0)
            return Result<MaterialDeletionPreview>.Ok(new MaterialDeletionPreview(0, Array.Empty<MaterialUsageInMatrix>()));

        var matrizIds = componentes.Select(c => c.MatrizId).Distinct().ToList();

        var matrices = await context.Matrices
            .AsNoTracking()
            .Where(m => matrizIds.Contains(m.Id))
            .OrderBy(m => m.Clave)
            .Select(m => new MaterialUsageInMatrix(m.Id, m.Clave, m.Descripcion ?? string.Empty))
            .ToListAsync(cancellationToken);

        return Result<MaterialDeletionPreview>.Ok(
            new MaterialDeletionPreview(componentes.Count, matrices));
    }
}