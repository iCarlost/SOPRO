using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): busca un material por clave dentro del alcance de la
/// sesión (mismo proyecto). Devuelve <c>null</c> si no existe; nunca entidades
/// rastreadas. Es la versión headless del autocompletado por clave de la UI
/// legacy (FormEditarMaterial).
///
/// N4: el contexto es POR OPERACIÓN (IProjectDbContextFactory transient);
/// cada paso de I/O comprueba la cancelación.
/// </summary>
public sealed class FindMaterialByKey
{
    private readonly IProjectDbContextFactory _factory;

    public FindMaterialByKey(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<MaterialListItem?>> Execute(
        ProjectSessionInfo session,
        FindMaterialByKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!session.Project.ProjectId.HasValue)
            return Result<MaterialListItem?>.Ok(null);

        var clave = request.Clave?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clave))
            return Result<MaterialListItem?>.Ok(null);

        try
        {
            await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

            var material = await context.Materiales
                .AsNoTracking()
                .Where(m => m.ProyectoId == session.Project.ProjectId.Value)
                .Where(m => m.Clave.ToUpper() == clave.ToUpper())
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (material == null)
                return Result<MaterialListItem?>.Ok(null);

            return Result<MaterialListItem?>.Ok(new MaterialListItem(
                material.Id,
                material.Clave,
                material.Descripcion,
                material.Unidad,
                material.PrecioUnitario,
                material.Notas ?? string.Empty,
                material.Origen));
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
            return Result<MaterialListItem?>.Fail(
                AppErrorCode.Database,
                "No se pudo buscar el material por clave.",
                ex.Message);
        }
    }
}