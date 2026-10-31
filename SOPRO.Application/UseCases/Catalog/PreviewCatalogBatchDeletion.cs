using SOPRO.Application.Contracts;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Caso de uso (consulta): previsualiza la eliminación de un lote homogéneo del
/// catálogo. Abre su propio contexto por operación y solo lee: no abre
/// transacción, no muta entidades ni guarda. Devuelve el número de recursos
/// seleccionados y el impacto en componentes, matrices y presupuesto para la
/// confirmación única de la UI, más la huella que el comando revalida.
///
/// Comparte el descubrimiento del comando (<see cref="CatalogBatchDeletionPlanner"/>):
/// lo previsualizado y lo ejecutado describen siempre el mismo conjunto.
/// </summary>
public sealed class PreviewCatalogBatchDeletion
{
    private readonly IProjectDbContextFactory _factory;

    public PreviewCatalogBatchDeletion(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<CatalogBatchDeletionPreview>> Execute(
        ProjectSessionInfo session,
        PreviewCatalogBatchDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        var (error, plan) = await CatalogBatchDeletionPlanner.PlanificarAsync(
            context, session, request.Kind, request.Ids, cancellationToken);

        if (error is not null)
            return Result<CatalogBatchDeletionPreview>.Fail(error);

        var planificado = plan!;

        return Result<CatalogBatchDeletionPreview>.Ok(new CatalogBatchDeletionPreview(
            planificado.Kind,
            planificado.Ids,
            planificado.Ids.Count,
            planificado.ComponentIds.Count,
            planificado.DirectMatrixIds.Count,
            planificado.ClosureMatrixIds.Count,
            planificado.AffectedConceptIds.Count,
            planificado.AffectedAggregatorCount,
            planificado.Fingerprint));
    }
}
