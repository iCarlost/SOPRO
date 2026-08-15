using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): previsualiza el impacto de eliminar un material:
/// cuántos componentes de matriz lo usan y en qué matrices. No modifica nada.
/// Solo opera sobre materiales del alcance de la sesión (aislamiento entre
/// proyectos y catálogo maestro).
///
/// N4: el contexto es POR OPERACIÓN (IProjectDbContextFactory transient);
/// cada paso de I/O comprueba la cancelación.
/// </summary>
public sealed class PreviewMaterialDeletion
{
    private readonly IProjectDbContextFactory _factory;

    public PreviewMaterialDeletion(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<MaterialDeletionPreview>> Execute(
        ProjectSessionInfo session,
        PreviewMaterialDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        try
        {
            var material = await context.Materiales
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (material == null || !MaterialScope.IsInSessionScope(session, material))
            {
                return Result<MaterialDeletionPreview>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId}.");
            }

            // El alcance real de la eliminación son los componentes de matrices
            // del proyecto de la sesión (lo que DeleteMaterial elimina). Un
            // componente de una matriz de otro proyecto no debe contarse ni
            // mostrarse: el preview debe reflejar exactamente lo que se borrará.
            var componentes = await context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.MaterialId == request.MaterialId
                    && c.Matriz.ProyectoId == session.Project.ProjectId)
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Referencias fuera del alcance: no se eliminan, pero bloquean la
            // eliminación del material (FK Restrict + aislamiento de proyecto).
            int referenciasExternas = await context.ComponentesMatriz
                .AsNoTracking()
                .CountAsync(c => c.MaterialId == request.MaterialId
                    && c.Matriz.ProyectoId != session.Project.ProjectId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (componentes.Count == 0)
                return Result<MaterialDeletionPreview>.Ok(
                    new MaterialDeletionPreview(0, Array.Empty<MaterialUsageInMatrix>(), referenciasExternas));

            var matrizIds = componentes.Select(c => c.MatrizId).Distinct().ToList();

            var matrices = await context.Matrices
                .AsNoTracking()
                .Where(m => matrizIds.Contains(m.Id)
                    && m.ProyectoId == session.Project.ProjectId)
                .OrderBy(m => m.Clave)
                .Select(m => new MaterialUsageInMatrix(m.Id, m.Clave, m.Descripcion ?? string.Empty))
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return Result<MaterialDeletionPreview>.Ok(
                new MaterialDeletionPreview(componentes.Count, matrices, referenciasExternas));
        }
        catch (OperationCanceledException)
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