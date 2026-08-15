using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (consulta): busca un material por clave dentro del alcance de la
/// sesión (mismo proyecto). Devuelve <c>null</c> si no existe; nunca entidades
/// rastreadas. Es la versión headless del autocompletado por clave de la UI
/// legacy (FormEditarMaterial).
/// </summary>
public sealed class FindMaterialByKey
{
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
            var material = await session.Context.Materiales
                .AsNoTracking()
                .Where(m => m.ProyectoId == session.Project.ProjectId.Value)
                .Where(m => m.Clave.ToUpper() == clave.ToUpper())
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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