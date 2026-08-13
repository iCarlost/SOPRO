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
            // El contexto compartido de la sesión no debe conservar mutaciones
            // de una operación que no se confirmó.
            session.Context.ChangeTracker.Clear();
            throw;
        }
        catch (Exception ex)
        {
            session.Context.ChangeTracker.Clear();
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

        // Identidad de la fila maestra (los Ids NO se comparten entre bases):
        //  - Sesión de proyecto: la fila maestra asociada del material de origen
        //    (MaterialMaestroId, lo que escribe FormImportarMaestro). Sin fila
        //    asociada (material de proyecto puro) → se crea una fila maestra nueva.
        //  - Sesión del catálogo maestro: el MaterialId ES el Id de la base maestra.
        int? idFilaMaestra = null;
        if (session.Project.ProjectId.HasValue)
        {
            if (request.MaterialId.HasValue)
            {
                var origen = await session.Context.Materiales
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == request.MaterialId.Value, cancellationToken);

                if (origen == null || !MaterialScope.IsInSessionScope(session, origen))
                {
                    return Result<SaveMaterialResult>.Fail(
                        AppErrorCode.NotFound,
                        $"No se encontró el material con Id {request.MaterialId.Value}.");
                }

                idFilaMaestra = origen.MaterialMaestroId;
            }
        }
        else
        {
            idFilaMaestra = request.MaterialId;
        }

        bool esNuevo = !idFilaMaestra.HasValue;

        string claveNormalizada = clave.Trim().ToUpperInvariant();

        // Unicidad de clave en el catálogo maestro sin importar la capitalización
        // de los datos almacenados (SQLite compara en binario por defecto).
        int? idExcluir = esNuevo ? null : idFilaMaestra;
        bool claveDuplicada = await masterCtx.Materiales
            .AnyAsync(m => m.ProyectoId == null
                && m.Clave.ToUpper() == claveNormalizada
                && m.Id != idExcluir, cancellationToken);

        if (claveDuplicada)
        {
            return Result<SaveMaterialResult>.Fail(
                AppErrorCode.Conflict,
                $"Ya existe un material del catálogo maestro con la clave '{claveNormalizada}'.");
        }

        Material? materialMaestro = null;
        if (esNuevo)
        {
            materialMaestro = new Material();
            AplicarEnMaestro(clave, request, materialMaestro);
            masterCtx.Materiales.Add(materialMaestro);
        }
        else
        {
            int materialId = idFilaMaestra!.Value;
            var materialMaestroTemp = await masterCtx.Materiales
                .FindAsync(new object?[] { materialId }, cancellationToken);

            if (materialMaestroTemp == null)
            {
                return Result<SaveMaterialResult>.Fail(
                    AppErrorCode.NotFound,
                    $"No se encontró el material del catálogo maestro con Id {materialId}.");
            }

            materialMaestro = materialMaestroTemp;
            AplicarEnMaestro(clave, request, materialMaestro);
            materialMaestro.FechaModificacion = DateTime.Now;
        }

        await masterCtx.SaveChangesAsync(cancellationToken);

        // Establecer la identidad maestra del material local: al crear una fila
        // maestra nueva se persiste la asociación (MaterialMaestroId) para que
        // los guardados posteriores editen la MISMA fila maestra en lugar de
        // crear una copia independiente por cada guardado.
        //
        // Dos bases (proyecto y maestro) → dos commits: SQLite no admite
        // transacciones distribuidas. Si la segunda escritura falla se revierte
        // la fila maestra recién creada (compensación), de modo que la operación
        // queda sin efectos visibles y es reintentable: no queda una fila
        // huérfana que provoque Conflict en el reintento.
        if (esNuevo && session.Project.ProjectId.HasValue && request.MaterialId.HasValue)
        {
            try
            {
                var local = await session.Context.Materiales
                    .FindAsync(new object?[] { request.MaterialId.Value }, cancellationToken);

                if (local != null && local.MaterialMaestroId == null)
                {
                    local.MaterialMaestroId = materialMaestro.Id;
                    await session.Context.SaveChangesAsync(cancellationToken);
                }
            }
            catch
            {
                masterCtx.Materiales.Remove(materialMaestro);
                await masterCtx.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

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