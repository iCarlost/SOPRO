using System.Transactions;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Caso de uso (comando): guarda un material (creación o actualización) y, en
/// una sola operación lógica, propaga el recálculo de precios a matrices,
/// auxiliares y conceptos (Gate N3: "Guardado y propagación son una operación
/// lógica única").
///
/// Frontera transaccional: una transacción única envuelve la persistencia y la
/// propagación; ante cualquier fallo de la propagación o de la base de datos se
/// revierte TODO (rollback completo) y se devuelve un error tipado.
///
/// Guardar "en maestro" (SaveToMaster) escribe en la base del catálogo maestro
/// (CatalogoMaestro.db), no en la base del proyecto: la fila maestra es una
/// entidad compartida entre proyectos.
/// </summary>
public sealed class SaveMaterial
{
    public async Task<Result<SaveMaterialResult>> Execute(
        ProjectSessionInfo session,
        SaveMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string clave = request.Clave?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(clave)
            || string.IsNullOrWhiteSpace(request.Descripcion)
            || string.IsNullOrWhiteSpace(request.Unidad))
        {
            return Result<SaveMaterialResult>.Fail(
                AppErrorCode.Validation,
                "La clave, la descripción y la unidad del material son requeridas.");
        }

        if (request.ProjectId != session.Project.ProjectId)
        {
            return Result<SaveMaterialResult>.Fail(
                AppErrorCode.Validation,
                "La operación no corresponde al proyecto de la sesión.");
        }

        try
        {
            if (request.SaveToMaster)
                return await GuardarEnMaestroAsync(session, request, clave, cancellationToken);

            return await GuardarEnProyectoAsync(session, request, clave, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<SaveMaterialResult>.Fail(
                AppErrorCode.Database,
                "No se pudo guardar el material.",
                ex.Message);
        }
    }

    private async Task<Result<SaveMaterialResult>> GuardarEnProyectoAsync(
        ProjectSessionInfo session,
        SaveMaterialRequest request,
        string clave,
        CancellationToken cancellationToken)
    {
        var context = session.Context;

        if (request.ProjectId.HasValue)
        {
            var errorClave = KeyValidationService.ValidateUniqueKey(
                context,
                clave,
                request.ProjectId.Value,
                request.MaterialId,
                CatalogItemType.Material);

            if (errorClave != null)
                return Result<SaveMaterialResult>.Fail(AppErrorCode.Conflict, errorClave);
        }

        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        Material? material = null;
        if (request.MaterialId.HasValue)
        {
            material = await context.Materiales
                .FindAsync(new object?[] { request.MaterialId.Value }, cancellationToken);

            if (material == null || !MaterialScope.IsInSessionScope(session, material))
            {
                return Result<SaveMaterialResult>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material con Id {request.MaterialId.Value}.");
            }
        }

        var resultado = await CatalogItemService.SaveMaterialAsync(
            context,
            new MaterialEditDto
            {
                Clave = clave,
                Descripcion = request.Descripcion,
                Unidad = request.Unidad,
                PrecioUnitario = request.PrecioUnitario,
                Notas = request.Notas ?? string.Empty,
                GuardarEnMaestro = false,
                ProyectoId = request.ProjectId,
            },
            material);

        await tx.CommitAsync(cancellationToken);

        return Result<SaveMaterialResult>.Ok(new SaveMaterialResult(
            resultado.EntityId,
            resultado.IsNew,
            resultado.TriggeredRecalculation));
    }

    private async Task<Result<SaveMaterialResult>> GuardarEnMaestroAsync(
        ProjectSessionInfo session,
        SaveMaterialRequest request,
        string clave,
        CancellationToken cancellationToken)
    {
        using var masterCtx = new SOPROContext(session.MasterDatabasePath);
        await masterCtx.Database.EnsureCreatedAsync(cancellationToken);

        bool esNuevo = !request.MaterialId.HasValue;

        Material? materialMaestro = null;
        if (esNuevo)
        {
            materialMaestro = new Material();
            AplicarEnMaestro(clave, request, materialMaestro);
            masterCtx.Materiales.Add(materialMaestro);
        }
        else
        {
            int materialId = request.MaterialId!.Value;
            var materialMaestroTemp = await masterCtx.Materiales
                .FindAsync(new object?[] { materialId }, cancellationToken);

            if (materialMaestroTemp == null)
            {
                return Result<SaveMaterialResult>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material del catálogo maestro con Id {request.MaterialId.Value}.");
            }

            materialMaestro = materialMaestroTemp;
            AplicarEnMaestro(clave, request, materialMaestro);
            materialMaestro.FechaModificacion = DateTime.Now;
        }

        await masterCtx.SaveChangesAsync(cancellationToken);

        return Result<SaveMaterialResult>.Ok(new SaveMaterialResult(
            materialMaestro.Id,
            esNuevo,
            TriggeredRecalculation: false));
    }

    /// <summary>
    /// Normalización de la fila maestra, con la misma semántica de
    /// <see cref="CatalogItemService"/> (paridad legacy). La fila maestra no
    /// participa en matrices: no hay recálculo asociado.
    /// </summary>
    private static void AplicarEnMaestro(string clave, SaveMaterialRequest request, Material material)
    {
        material.Clave = clave.Trim().ToUpperInvariant();
        material.Descripcion = request.Descripcion.Trim();
        material.Unidad = request.Unidad.Trim().ToLowerInvariant();
        material.PrecioUnitario = request.PrecioUnitario;
        material.Notas = request.Notas?.Trim() ?? string.Empty;
        material.Origen = OrigenInsumo.Maestro;
        material.ProyectoId = null;
    }
}