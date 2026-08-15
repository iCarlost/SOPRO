using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): lista el catálogo de materiales con los filtros de
/// pantalla legacy. Devuelve DTOs sin rastreo; nunca entidades de EF.
///
/// Paridad (comportamiento legacy de CatalogLoadService):
///   - Sin proyecto: lista todos los materiales (catálogo maestro).
///   - Búsqueda por Clave o Descripción (casing-insensitive).
///   - SoleProjectItems / SoleMasterItems se resuelven por la marca de origen
///     en Notas (ImportOriginStampService.ExtractProjectName), en memoria.
///
/// N4: el contexto es POR OPERACIÓN (IProjectDbContextFactory transient);
/// cada paso de I/O comprueba la cancelación.
/// </summary>
public sealed class ListMaterials
{
    private readonly IProjectDbContextFactory _factory;

    public ListMaterials(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    private static bool EsImportado(string? notas)
        => !string.IsNullOrWhiteSpace(ImportOriginStampService.ExtractProjectName(notas));

    public async Task<Result<List<MaterialListItem>>> Execute(
        ProjectSessionInfo session,
        ListMaterialsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        try
        {
            var query = context.Materiales.AsNoTracking();

            if (session.Project.ProjectId.HasValue)
                query = query.Where(m => m.ProyectoId == session.Project.ProjectId.Value);

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                var term = request.SearchText.Trim().ToLower();
                query = query.Where(m => m.Clave.ToLower().Contains(term) || m.Descripcion.ToLower().Contains(term));
            }

            var list = await query
                .OrderBy(m => m.Clave)
                .Select(m => new MaterialListItem(
                    m.Id,
                    m.Clave,
                    m.Descripcion,
                    m.Unidad,
                    m.PrecioUnitario,
                    m.Notas ?? string.Empty,
                    m.Origen))
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (request.OnlyProjectItems)
                list = list.Where(m => !EsImportado(m.Notas)).ToList();
            else if (request.OnlyMasterItems)
                list = list.Where(m => EsImportado(m.Notas)).ToList();

            return Result<List<MaterialListItem>>.Ok(list);
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
            return Result<List<MaterialListItem>>.Fail(
                AppErrorCode.Database,
                "No se pudo cargar el catálogo de materiales.",
                ex.Message);
        }
    }
}