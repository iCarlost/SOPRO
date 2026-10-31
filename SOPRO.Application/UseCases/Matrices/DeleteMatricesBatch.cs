using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Matrices;

/// <summary>
/// Caso de uso (comando): elimina un lote de matrices del proyecto de la sesión
/// como UNA sola operación lógica. Contexto nuevo por operación
/// (<see cref="IProjectDbContextFactory"/>), una transacción desde la validación
/// hasta el borrado y el recálculo, y un único commit: cualquier rechazo o fallo
/// revierte el lote completo, nunca queda una eliminación parcial.
///
/// Validación previa a cualquier escritura:
/// <list type="bullet">
/// <item>Un Id inexistente, de otro proyecto o ya eliminado rechaza el lote con
/// <see cref="AppErrorCode.NotFound"/>.</item>
/// <item>Las matrices fuera de la selección que usan una matriz seleccionada como
/// <c>Auxiliar</c> no bloquean: sus componentes auxiliares se eliminan con el lote
/// y sus matrices padre se recalculan con el cierre canónico.</item>
/// <item>Un concepto de presupuesto con <c>MatrizId</c> en la selección rechaza el
/// lote con <see cref="AppErrorCode.Conflict"/>: la fila del presupuesto quedaría
/// huérfana.</item>
/// </list>
///
/// Las matrices que SÍ forman parte de la selección pueden referenciarse entre
/// ellas: ambas desaparecen y sus componentes se eliminan con su matriz padre.
/// No se habilita la aplicación de claves foráneas de SQLite: el borrado de
/// aristas auxiliares externas es explícito, no depende del motor.
/// </summary>
public sealed class DeleteMatricesBatch
{
    private readonly IProjectDbContextFactory _factory;

    public DeleteMatricesBatch(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Ejecuta la eliminación del lote. Devuelve el impacto confirmado solo si la
    /// transacción se confirmó; en cualquier otro caso devuelve un error tipado y
    /// la base queda sin cambios.
    /// </summary>
    public async Task<Result<DeleteMatricesBatchResult>> Execute(
        ProjectSessionInfo session,
        DeleteMatricesBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Ids is null || request.Ids.Count == 0)
        {
            return Fail(AppErrorCode.Validation, "Seleccione al menos una matriz para eliminar.");
        }

        if (request.Ids.Any(id => id <= 0))
        {
            return Fail(AppErrorCode.Validation, "La selección contiene identificadores inválidos.");
        }

        int? proyectoId = session.Project.ProjectId;
        if (!proyectoId.HasValue)
        {
            return Fail(
                AppErrorCode.NotSupported,
                "Las matrices pertenecen a un proyecto; no se pueden eliminar desde el catálogo maestro.");
        }

        // Un Id repetido no debe eliminar dos veces ni contar dos veces el impacto;
        // el plan normaliza la selección dentro de la transacción.
        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var (error, plan) = await MatricesBatchDeletionPlanner.PlanificarAsync(
                context, session, request.Ids, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (error is not null)
                return Fail(error.Code, error.Message, error.Detail);

            var planificado = plan!;

            if (planificado.BlockingConceptIds.Count > 0)
            {
                return Fail(
                    AppErrorCode.Conflict,
                    $"No se pueden eliminar las matrices seleccionadas: " +
                    $"{DescribirConceptosReferenciados(planificado.BlockingConceptIds.Count)} " +
                    $"({DescribirClaves(planificado.BlockingConceptIds
                        .Zip(planificado.BlockingConceptClaves, (id, clave) => (id, clave)))}).");
            }

            // Los componentes de las matrices seleccionadas se eliminan de forma
            // explícita: la cascada del esquema no se aplica (SQLite abre sin
            // PRAGMA foreign_keys) y el borrado debe ser completo sin depender del
            // motor. Las aristas auxiliares externas se eliminan también para no
            // dejar un AuxiliarId apuntando a una matriz inexistente.
            var componentes = await context.ComponentesMatriz
                .Where(c => planificado.Ids.Contains(c.MatrizId))
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (planificado.ExternalAuxiliarComponents.Count > 0)
                context.ComponentesMatriz.RemoveRange(planificado.ExternalAuxiliarComponents);

            if (componentes.Count > 0)
                context.ComponentesMatriz.RemoveRange(componentes);

            var matrices = await context.Matrices
                .Where(m => planificado.Ids.Contains(m.Id))
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            context.Matrices.RemoveRange(matrices);

            // Primera escritura dentro de la transacción: las filas borradas dejan
            // de existir para el cierre canónico que se ejecuta a continuación.
            await context.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (planificado.ExternalParentMatrixIds.Count > 0)
            {
                // Recalcula padres afectados, sus ascendientes auxiliares, hojas y
                // agrupadores con la misma ruta canónica que el resto del sistema.
                PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                    context, planificado.ExternalParentMatrixIds, proyectoId.Value);

                await context.SaveChangesAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            await transaction.CommitAsync(cancellationToken);

            return Result<DeleteMatricesBatchResult>.Ok(new DeleteMatricesBatchResult(
                planificado.Ids,
                componentes.Count,
                planificado.ExternalAuxiliarComponents.Count,
                planificado.RecalculatedMatrixIds.Count,
                planificado.AffectedConceptCount,
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
                    "No se pudieron eliminar las matrices seleccionadas; no se confirmó ningún cambio.",
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

    private static string DescribirConceptosReferenciados(int cantidad)
        => cantidad == 1
            ? "1 concepto del presupuesto la referencia"
            : $"{cantidad} conceptos del presupuesto las referencian";

    /// <summary>
    /// Nombra como máximo tres filas que bloquean el lote (por Id, sin repetir)
    /// para que el usuario identifique el origen sin recibir una lista enorme.
    /// </summary>
    private static string DescribirClaves(IEnumerable<(int Id, string? Clave)> referencias)
    {
        var claves = referencias
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .OrderBy(r => r.Id)
            .Take(3)
            .Select(r => string.IsNullOrWhiteSpace(r.Clave) ? $"Id {r.Id}" : r.Clave.Trim())
            .ToList();

        return string.Join(", ", claves);
    }

    private static AppError NotFound() => new(
        AppErrorCode.NotFound,
        "No se encontraron todas las matrices seleccionadas en el proyecto de la sesión.");

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

    private static Result<DeleteMatricesBatchResult> Fail(
        AppErrorCode code,
        string message,
        string? detail = null)
        => Result<DeleteMatricesBatchResult>.Fail(code, message, detail);
}
