using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (comando): elimina un material del alcance de la sesión. Si está
/// en uso, elimina primero los componentes de matriz que lo referencian (misma
/// semántica del flujo legacy) y propaga el recálculo de las matrices afectadas
/// como parte de la misma operación lógica.
///
/// Frontera transaccional: transacción única desde la validación de alcance
/// hasta la propagación; ante cualquier fallo se revierte todo (rollback
/// completo) y se devuelve un error tipado.
/// </summary>
public sealed class DeleteMaterial
{
    public async Task<Result<DeleteMaterialResult>> Execute(
        ProjectSessionInfo session,
        DeleteMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = session.Context;

        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var material = await context.Materiales
                .FindAsync(new object?[] { request.MaterialId }, cancellationToken);

            if (material == null || !MaterialScope.IsInSessionScope(session, material))
            {
                return Result<DeleteMaterialResult>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId}.");
            }

            var componentesEnUso = await context.ComponentesMatriz
                .Where(c => c.MaterialId == request.MaterialId)
                .ToListAsync(cancellationToken);

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

            await tx.CommitAsync(cancellationToken);

            return Result<DeleteMaterialResult>.Ok(new DeleteMaterialResult(
                componentesEnUso.Count,
                matrizIds?.Count ?? 0,
                matrizIds is { Count: > 0 }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            context.ChangeTracker.Clear();
            throw;
        }
        catch (Exception ex)
        {
            context.ChangeTracker.Clear();
            return Result<DeleteMaterialResult>.Fail(
                AppErrorCode.Database,
                "No se pudo eliminar el material.",
                ex.Message);
        }
    }
}