using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Caso de uso (comando): elimina un lote homogéneo de insumos del catálogo de la
/// sesión (materiales, mano de obra, herramientas o maquinaria) y propaga el
/// recálculo del cierre afectado como parte de la MISMA operación lógica.
///
/// Frontera transaccional: contexto nuevo por operación
/// (<see cref="IProjectDbContextFactory"/>), una sola transacción desde la
/// validación hasta el recálculo y un único commit. Cualquier rechazo o fallo
/// revierte el lote completo: nunca queda una eliminación parcial.
///
/// Revalidación de la confirmación: si la UI envía la huella de
/// <see cref="PreviewCatalogBatchDeletion"/>, el comando recalcula el plan dentro
/// de su transacción y rechaza el lote con <see cref="AppErrorCode.Conflict"/> si
/// la selección o cualquiera de sus dependencias cambió entre la confirmación y
/// la escritura. Sin huella se conserva el comportamiento previo.
///
/// Reglas de alcance:
/// <list type="bullet">
/// <item>Un Id inexistente, de otro proyecto o ya eliminado (obsoleto) rechaza el
/// lote completo con <see cref="AppErrorCode.NotFound"/>: no se elimina nada.</item>
/// <item>Un lote que mezcla filas del proyecto con filas del catálogo maestro (o
/// viceversa) se rechaza con <see cref="AppErrorCode.Conflict"/>.</item>
/// <item>Si algún insumo está referenciado por una matriz de otro proyecto (o del
/// catálogo maestro) se rechaza el lote completo con
/// <see cref="AppErrorCode.Conflict"/>: no se tocan datos ajenos.</item>
/// <item>Las herramientas solo existen dentro de un proyecto: pedirlas desde la
/// sesión del catálogo maestro se rechaza con <see cref="AppErrorCode.NotSupported"/>.</item>
/// <item>Desde el catálogo maestro no se elimina un insumo en uso: la propagación
/// canónica no recalcula matrices sin proyecto y el costo quedaría obsoleto, así
/// que el lote se rechaza con <see cref="AppErrorCode.Conflict"/>.</item>
/// </list>
///
/// El alcance lo define <c>ProyectoId</c> (no <c>Origen</c>), igual que la regla
/// de <c>MaterialScope</c>: las copias importadas del maestro viven en el
/// proyecto y son eliminables desde él sin tocar <c>CatalogoMaestro.db</c>; las
/// filas maestras puras (<c>ProyectoId</c> null) solo son alcanzables desde la
/// sesión del catálogo maestro.
///
/// Solo se eliminan los componentes de matrices del proyecto de la sesión; el
/// recálculo usa <see cref="PricePropagationService.RecalcularCierreAuxiliarSinGuardar"/>
/// (matrices afectadas + ascendientes auxiliares + hojas y agrupadores del
/// presupuesto) y se confirma en el mismo commit.
/// </summary>
public sealed class DeleteCatalogBatch
{
    private readonly IProjectDbContextFactory _factory;

    public DeleteCatalogBatch(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Ejecuta la eliminación del lote. Devuelve el impacto confirmado solo si la
    /// transacción se confirmó; en cualquier otro caso devuelve un error tipado y
    /// la base queda sin cambios.
    /// </summary>
    public async Task<Result<DeleteCatalogBatchResult>> Execute(
        ProjectSessionInfo session,
        DeleteCatalogBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // El plan se recalcula DENTRO de la transacción con la misma lógica de la
            // previsualización: si el estado cambió desde que el usuario confirmó, la
            // huella no coincide y no se escribe nada.
            var (error, plan) = await CatalogBatchDeletionPlanner.PlanificarAsync(
                context, session, request.Kind, request.Ids, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (error is not null)
                return Fail(error.Code, error.Message, error.Detail);

            var planificado = plan!;

            if (!string.IsNullOrEmpty(request.ExpectedFingerprint)
                && !string.Equals(request.ExpectedFingerprint, planificado.Fingerprint, StringComparison.Ordinal))
            {
                return Fail(
                    AppErrorCode.Conflict,
                    "La selección o sus dependencias cambiaron desde la confirmación. " +
                    "Vuelva a previsualizar antes de eliminar.");
            }

            var eliminarFilas = await CargarEliminacionAsync(
                context, request.Kind, planificado.Ids, cancellationToken);

            var componentes = await context.ComponentesMatriz
                .Where(c => planificado.ComponentIds.Contains(c.Id))
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (componentes.Count > 0)
                context.ComponentesMatriz.RemoveRange(componentes);

            eliminarFilas();
            await context.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var matrizIds = componentes
                .Select(c => c.MatrizId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            int recalculadas = 0;

            if (matrizIds.Count > 0)
            {
                var cierre = PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                    context, matrizIds, session.Project.ProjectId);
                recalculadas = cierre.Count;

                // El recálculo no guarda por sí mismo: se confirma con el mismo commit.
                await context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return Result<DeleteCatalogBatchResult>.Ok(new DeleteCatalogBatchResult(
                request.Kind,
                planificado.Ids,
                componentes.Count,
                matrizIds.Count,
                recalculadas,
                planificado.AffectedConceptIds.Count,
                planificado.AffectedAggregatorCount));
        }
        catch (OperationCanceledException)
        {
            // La cancelación es un abandono, no un fallo recuperable: la transacción
            // se revierte al cerrarse y el llamador recibe la excepción (N4).
            throw;
        }
        catch (Exception ex)
        {
            bool revertido = await RevertirAsync(transaction);

            return revertido
                ? Fail(
                    AppErrorCode.Database,
                    "No se pudieron eliminar los insumos seleccionados; no se confirmó ningún cambio.",
                    ex.Message)
                : Fail(
                    AppErrorCode.Unknown,
                    "No se pudo confirmar si la eliminación quedó aplicada. Reconcilie con el catálogo antes de reintentar.",
                    ex.Message);
        }
        finally
        {
            if (transaction is not null)
            {
                try
                {
                    await transaction.DisposeAsync();
                }
                catch
                {
                    // El resultado ya clasificó el cierre; un fallo al liberar el
                    // recurso no cambia lo que quedó (o no) en la base.
                }
            }
        }
    }

    /// <summary>
    /// Carga con seguimiento las filas del catálogo que el plan ya validó y devuelve
    /// la remoción tipada. La lectura del plan es sin seguimiento; la escritura
    /// necesita las entidades rastreadas para eliminar exactamente ese conjunto.
    /// </summary>
    private static async Task<Action> CargarEliminacionAsync(
        SOPROContext context,
        CatalogItemKind kind,
        List<int> ids,
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case CatalogItemKind.Material:
            {
                var filas = await context.Materiales
                    .Where(m => ids.Contains(m.Id))
                    .ToListAsync(cancellationToken);
                return () => context.Materiales.RemoveRange(filas);
            }

            case CatalogItemKind.ManoDeObra:
            {
                var filas = await context.ManoDeObra
                    .Where(m => ids.Contains(m.Id))
                    .ToListAsync(cancellationToken);
                return () => context.ManoDeObra.RemoveRange(filas);
            }

            case CatalogItemKind.Herramienta:
            {
                var filas = await context.Herramientas
                    .Where(h => ids.Contains(h.Id))
                    .ToListAsync(cancellationToken);
                return () => context.Herramientas.RemoveRange(filas);
            }

            default:
            {
                var filas = await context.Maquinaria
                    .Where(m => ids.Contains(m.Id))
                    .ToListAsync(cancellationToken);
                return () => context.Maquinaria.RemoveRange(filas);
            }
        }
    }

    private static async Task<bool> RevertirAsync(IDbContextTransaction? transaction)
    {
        if (transaction is null) return true;

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Result<DeleteCatalogBatchResult> Fail(
        AppErrorCode code,
        string message,
        string? detail = null)
        => Result<DeleteCatalogBatchResult>.Fail(code, message, detail);
}
