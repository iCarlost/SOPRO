using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Data.Factories;

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
///
/// N4: el contexto es POR OPERACIÓN (IProjectDbContextFactory transient);
/// cada paso de I/O comprueba la cancelación (ThrowIfCancellationRequested).
/// </summary>
public sealed class DeleteMaterial
{
    private readonly IProjectDbContextFactory _factory;

    public DeleteMaterial(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<DeleteMaterialResult>> Execute(
        ProjectSessionInfo session,
        DeleteMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var material = await context.Materiales
                .FindAsync(new object?[] { request.MaterialId }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (material == null || !MaterialScope.IsInSessionScope(session, material))
            {
                return Result<DeleteMaterialResult>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId}.");
            }

            // Solo se eliminan componentes de matrices del proyecto de la sesión:
            // una colisión numérica de Ids no debe dejar eliminar un componente
            // que pertenece a una matriz de otro proyecto.
            var componentesEnUso = await context.ComponentesMatriz
                .Where(c => c.MaterialId == request.MaterialId
                    && c.Matriz.ProyectoId == session.Project.ProjectId)
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // El FK MaterialId es Restrict: si el material está referenciado por
            // matrices de OTRO proyecto, eliminarlo es imposible sin tocar datos
            // ajenos. La operación se rechaza con un error tipado en lugar de
            // fallar por restricción o de alcanzar el componente ajeno.
            bool referenciasExternas = await context.ComponentesMatriz
                .AnyAsync(c => c.MaterialId == request.MaterialId
                    && c.Matriz.ProyectoId != session.Project.ProjectId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (referenciasExternas)
            {
                return Result<DeleteMaterialResult>.Fail(
                    AppErrorCode.Conflict,
                    "No se puede eliminar el material: está referenciado en matrices de otro proyecto.");
            }

            if (componentesEnUso.Count > 0)
                context.ComponentesMatriz.RemoveRange(componentesEnUso);

            context.Materiales.Remove(material);
            await context.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            List<int>? matrizIds = null;
            if (componentesEnUso.Count > 0)
            {
                matrizIds = componentesEnUso.Select(c => c.MatrizId).Distinct().ToList();
                RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(
                    context, matrizIds, session.Project.ProjectId);
            }

            await tx.CommitAsync(cancellationToken);

            return Result<DeleteMaterialResult>.Ok(new DeleteMaterialResult(
                componentesEnUso.Count,
                matrizIds?.Count ?? 0,
                matrizIds is { Count: > 0 }));
        }
        catch (OperationCanceledException)
        {
            // El contexto es por operación (await using): no hay estado
            // compartido que limpiar; la transacción se revierte al disponerse.
            throw;
        }
        catch (Exception ex)
        {
            return Result<DeleteMaterialResult>.Fail(
                AppErrorCode.Database,
                "No se pudo eliminar el material.",
                ex.Message);
        }
    }
}