using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (comando): elimina un material. Si está en uso, elimina primero
/// los componentes de matriz que lo referencian (misma semántica del flujo
/// legacy de FormCatalogoMateriales) y propaga el recálculo de las matrices
/// afectadas como parte de la misma operación lógica.
///
/// Frontera transaccional: un único SaveChanges con la propagación inmediata
/// después (patrón legacy conservado; la propagación es idempotente por matriz).
/// </summary>
public sealed class DeleteMaterial
{
    public async Task<Result<DeleteMaterialResult>> Execute(
        ProjectSessionInfo session,
        DeleteMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = session.Context;

        var componentesEnUso = await context.ComponentesMatriz
            .Where(c => c.MaterialId == request.MaterialId)
            .ToListAsync(cancellationToken);

        var material = await context.Materiales
            .FindAsync(new object?[] { request.MaterialId }, cancellationToken);

        if (material == null)
        {
            return Result<DeleteMaterialResult>.Fail(
                AppErrorCode.NotFound,
                $"No se encontró el material con Id {request.MaterialId}.");
        }

        if (componentesEnUso.Count > 0)
            context.ComponentesMatriz.RemoveRange(componentesEnUso);

        context.Materiales.Remove(material);
        await context.SaveChangesAsync(cancellationToken);

        List<int>? matrizIds = null;
        if (componentesEnUso.Count > 0)
        {
            matrizIds = componentesEnUso.Select(c => c.MatrizId).Distinct().ToList();
            RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(context, matrizIds);
        }

        return Result<DeleteMaterialResult>.Ok(new DeleteMaterialResult(
            componentesEnUso.Count,
            matrizIds?.Count ?? 0,
            matrizIds is { Count: > 0 }));
    }
}