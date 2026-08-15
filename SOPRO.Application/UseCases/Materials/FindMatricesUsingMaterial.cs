using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): matrices donde se usa un material ("Dónde se usa").
/// Devuelve DTOs de presentación (nunca entidades rastreadas) y solo opera
/// sobre materiales del alcance de la sesión.
///
/// N4: el contexto es POR OPERACIÓN (IProjectDbContextFactory transient);
/// cada paso de I/O comprueba la cancelación.
/// </summary>
public sealed class FindMatricesUsingMaterial
{
    private readonly IProjectDbContextFactory _factory;

    public FindMatricesUsingMaterial(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<IReadOnlyList<MaterialUsageInMatrix>>> Execute(
        ProjectSessionInfo session,
        FindMatricesUsingMaterialRequest request,
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
                return Result<IReadOnlyList<MaterialUsageInMatrix>>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId}.");
            }

            var matrizIds = await context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.MaterialId == request.MaterialId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (matrizIds.Count == 0)
                return Result<IReadOnlyList<MaterialUsageInMatrix>>.Ok(Array.Empty<MaterialUsageInMatrix>());

            // Las matrices se restringen al proyecto de la sesión: el esquema no
            // garantiza que un Id de matriz referencie el mismo proyecto que el
            // material, y una colisión numérica no debe cruzar proyectos.
            var matrices = await context.Matrices
                .AsNoTracking()
                .Where(m => matrizIds.Contains(m.Id)
                    && m.ProyectoId == session.Project.ProjectId)
                .OrderBy(m => m.Clave)
                .Select(m => new MaterialUsageInMatrix(m.Id, m.Clave, m.Descripcion ?? string.Empty))
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return Result<IReadOnlyList<MaterialUsageInMatrix>>.Ok(matrices);
        }
        catch (OperationCanceledException)
        {
            // N4-2: sin filtro when (cancellationToken.IsCancellationRequested): cualquier
            // OperationCanceledException interrumpe por diseño; el contexto es por operación
            // (await using), no hay estado compartido que limpiar.
            throw;
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<MaterialUsageInMatrix>>.Fail(
                AppErrorCode.Database,
                "No se pudo consultar dónde se usa el material.",
                ex.Message);
        }
    }
}