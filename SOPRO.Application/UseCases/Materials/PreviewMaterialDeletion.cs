using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): previsualiza el impacto de eliminar un material:
/// cuántos componentes de matriz lo usan y en qué matrices. No modifica nada.
/// Solo opera sobre materiales del alcance de la sesión (aislamiento entre
/// proyectos y catálogo maestro).
/// </summary>
public sealed class PreviewMaterialDeletion
{
    public async Task<Result<MaterialDeletionPreview>> Execute(
        ProjectSessionInfo session,
        PreviewMaterialDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = session.Context;

        try
        {
            var material = await context.Materiales
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);

            if (material == null || !MaterialScope.IsInSessionScope(session, material))
            {
                return Result<MaterialDeletionPreview>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId}.");
            }

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<MaterialDeletionPreview>.Fail(
                AppErrorCode.Database,
                "No se pudo previsualizar la eliminación del material.",
                ex.Message);
        }
    }
}