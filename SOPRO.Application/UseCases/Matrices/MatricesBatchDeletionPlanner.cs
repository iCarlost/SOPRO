using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.UseCases.Matrices;

/// <summary>
/// Plan de una eliminación en lote de matrices: selección normalizada, componentes
/// auxiliares externos que quedarían colgando (y se eliminarán), matrices padre a
/// recalcular con su cierre ascendente y conceptos de presupuesto que bloquean el
/// lote. Es una lectura; no muta ni guarda.
/// </summary>
internal sealed class MatricesBatchDeletionPlan
{
    public MatricesBatchDeletionPlan(
        List<int> ids,
        List<ComponenteMatriz> externalAuxiliarComponents,
        List<int> externalParentMatrixIds,
        List<int> blockingConceptIds,
        List<string?> blockingConceptClaves,
        List<int> recalculatedMatrixIds,
        int affectedConceptCount,
        int affectedAggregatorCount)
    {
        Ids = ids;
        ExternalAuxiliarComponents = externalAuxiliarComponents;
        ExternalParentMatrixIds = externalParentMatrixIds;
        BlockingConceptIds = blockingConceptIds;
        BlockingConceptClaves = blockingConceptClaves;
        RecalculatedMatrixIds = recalculatedMatrixIds;
        AffectedConceptCount = affectedConceptCount;
        AffectedAggregatorCount = affectedAggregatorCount;
    }

    public List<int> Ids { get; }

    /// <summary>Componentes fuera de la selección que apuntan a una matriz seleccionada.</summary>
    public List<ComponenteMatriz> ExternalAuxiliarComponents { get; }

    /// <summary>Matrices padre de esos componentes (origen del recálculo).</summary>
    public List<int> ExternalParentMatrixIds { get; }

    public List<int> BlockingConceptIds { get; }

    public List<string?> BlockingConceptClaves { get; }

    /// <summary>Cierre ascendente de los padres externos, sin repetir.</summary>
    public List<int> RecalculatedMatrixIds { get; }

    public int AffectedConceptCount { get; }

    public int AffectedAggregatorCount { get; }
}

/// <summary>
/// Descubrimiento ÚNICO compartido por la previsualización (solo lectura) y por el
/// comando (dentro de su transacción), para que lo confirmado y lo ejecutado
/// describan el mismo conjunto de dependencias.
/// </summary>
internal static class MatricesBatchDeletionPlanner
{
    public static async Task<(AppError? Error, MatricesBatchDeletionPlan? Plan)> PlanificarAsync(
        SOPROContext context,
        ProjectSessionInfo session,
        IReadOnlyList<int>? ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);

        if (ids is null || ids.Count == 0)
            return (new AppError(AppErrorCode.Validation, "Seleccione al menos una matriz para eliminar."), null);

        if (ids.Any(id => id <= 0))
            return (new AppError(AppErrorCode.Validation, "La selección contiene identificadores inválidos."), null);

        int? proyectoId = session.Project.ProjectId;
        if (!proyectoId.HasValue)
        {
            return (new AppError(
                AppErrorCode.NotSupported,
                "Las matrices pertenecen a un proyecto; no se pueden eliminar desde el catálogo maestro."), null);
        }

        var idsNormalizados = ids.Distinct().OrderBy(id => id).ToList();

        var matrices = await context.Matrices
            .AsNoTracking()
            .Where(m => idsNormalizados.Contains(m.Id))
            .Select(m => new { m.Id, m.ProyectoId })
            .ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (matrices.Count != idsNormalizados.Count
            || matrices.Any(m => m.ProyectoId != proyectoId.Value))
        {
            return (new AppError(
                AppErrorCode.NotFound,
                "No se encontraron todas las matrices seleccionadas en el proyecto de la sesión."), null);
        }

        // Componentes auxiliares externos: se eliminarán con el lote para no dejar
        // un AuxiliarId apuntando a una matriz inexistente.
        var externalAuxiliarComponents = await context.ComponentesMatriz
            .Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar
                && c.AuxiliarId.HasValue
                && idsNormalizados.Contains(c.AuxiliarId.Value)
                && !idsNormalizados.Contains(c.MatrizId))
            .ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var externalParentMatrixIds = externalAuxiliarComponents
            .Select(c => c.MatrizId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        // Una fila del presupuesto que referencia directamente una matriz
        // seleccionada quedaría huérfana: el lote se bloquea.
        var blockingConcepts = await context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.MatrizId.HasValue && idsNormalizados.Contains(c.MatrizId.Value))
            .Select(c => new { c.Id, Clave = (string?)c.Clave })
            .ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var recalculatedMatrixIds = externalParentMatrixIds.Count == 0
            ? new List<int>()
            : PricePropagationService.CalcularCierreAscendente(
                    context, externalParentMatrixIds, proyectoId.Value)
                .OrderBy(id => id)
                .ToList();

        int affectedConceptCount = 0;
        int affectedAggregatorCount = 0;

        if (recalculatedMatrixIds.Count > 0)
        {
            affectedConceptCount = await context.ConceptosPresupuesto
                .AsNoTracking()
                .CountAsync(c => c.ProyectoId == proyectoId.Value
                    && !c.EsAgrupador
                    && c.MatrizId.HasValue
                    && recalculatedMatrixIds.Contains(c.MatrizId.Value),
                    cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            affectedAggregatorCount = await context.ConceptosPresupuesto
                .AsNoTracking()
                .CountAsync(c => c.ProyectoId == proyectoId.Value && c.EsAgrupador,
                    cancellationToken);
        }

        return (null, new MatricesBatchDeletionPlan(
            idsNormalizados,
            externalAuxiliarComponents,
            externalParentMatrixIds,
            blockingConcepts.Select(c => c.Id).ToList(),
            blockingConcepts.Select(c => c.Clave).ToList(),
            recalculatedMatrixIds,
            affectedConceptCount,
            affectedAggregatorCount));
    }
}
