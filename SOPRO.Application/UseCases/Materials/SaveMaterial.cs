using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (comando): guarda un material (creación o actualización) y, en
/// una sola operación lógica, propaga el recálculo de precios a matrices,
/// auxiliares y conceptos (Gate N3: "Guardado y propagación son una operación
/// lógica única").
///
/// Validaciones (probables sin formularios): clave/descripción/unidad requeridas
/// y clave única dentro del alcance del proyecto (KeyValidationService, con la
/// misma semántica del flujo legacy FormEditarMaterial).
/// </summary>
public sealed class SaveMaterial
{
    public async Task<Result<SaveMaterialResult>> Execute(
        ProjectSessionInfo session,
        SaveMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        string clave = request.Clave?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(clave)
            || string.IsNullOrWhiteSpace(request.Descripcion)
            || string.IsNullOrWhiteSpace(request.Unidad))
        {
            return Result<SaveMaterialResult>.Fail(
                AppErrorCode.Validation,
                "La clave, la descripción y la unidad del material son requeridas.");
        }

        if (request.ProjectId.HasValue)
        {
            var errorClave = KeyValidationService.ValidateUniqueKey(
                session.Context,
                clave,
                request.ProjectId.Value,
                request.MaterialId,
                CatalogItemType.Material);

            if (errorClave != null)
                return Result<SaveMaterialResult>.Fail(AppErrorCode.Conflict, errorClave);
        }

        Material? material = null;
        if (request.MaterialId.HasValue)
        {
            material = await session.Context.Materiales
                .FindAsync(new object?[] { request.MaterialId.Value }, cancellationToken);

            if (material == null)
            {
                return Result<SaveMaterialResult>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId.Value}.");
            }
        }

        var resultado = await CatalogItemService.SaveMaterialAsync(
            session.Context,
            new MaterialEditDto
            {
                Clave = clave,
                Descripcion = request.Descripcion,
                Unidad = request.Unidad,
                PrecioUnitario = request.PrecioUnitario,
                Notas = request.Notas,
                GuardarEnMaestro = request.SaveToMaster,
                ProyectoId = request.ProjectId,
            },
            material);

        cancellationToken.ThrowIfCancellationRequested();

        return Result<SaveMaterialResult>.Ok(new SaveMaterialResult(
            resultado.EntityId,
            resultado.IsNew,
            resultado.TriggeredRecalculation));
    }
}