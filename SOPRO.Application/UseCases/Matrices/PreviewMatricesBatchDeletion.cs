using SOPRO.Application.Contracts;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Matrices;

/// <summary>
/// Caso de uso (consulta): previsualiza la eliminación de un lote de matrices.
/// Abre su propio contexto por operación y solo lee. Comparte el descubrimiento
/// con <see cref="DeleteMatricesBatch"/> para que la confirmación y la ejecución
/// describan el mismo impacto.
/// </summary>
public sealed class PreviewMatricesBatchDeletion
{
    private readonly IProjectDbContextFactory _factory;

    public PreviewMatricesBatchDeletion(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<MatricesBatchDeletionPreview>> Execute(
        ProjectSessionInfo session,
        PreviewMatricesBatchDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        var (error, plan) = await MatricesBatchDeletionPlanner.PlanificarAsync(
            context, session, request.Ids, cancellationToken);

        if (error is not null)
            return Result<MatricesBatchDeletionPreview>.Fail(error);

        var planificado = plan!;

        return Result<MatricesBatchDeletionPreview>.Ok(new MatricesBatchDeletionPreview(
            planificado.Ids,
            planificado.ExternalAuxiliarComponents.Count,
            planificado.ExternalParentMatrixIds.Count,
            planificado.RecalculatedMatrixIds,
            planificado.AffectedConceptCount,
            planificado.AffectedAggregatorCount,
            planificado.BlockingConceptIds,
            planificado.BlockingConceptClaves.Select(clave => clave ?? string.Empty).ToList()));
    }
}
