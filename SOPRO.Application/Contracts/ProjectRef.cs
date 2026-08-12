namespace SOPRO.Application.Contracts;

/// <summary>
/// Identidad neutral de un proyecto (o del catálogo maestro) para la frontera
/// de Application. No expone entidades de dominio ni EF.
/// </summary>
/// <param name="ProjectId">Id del proyecto; <c>null</c> = catálogo maestro.</param>
/// <param name="Name">Nombre para presentación.</param>
public sealed record ProjectRef(int? ProjectId, string Name)
{
    public static ProjectRef Master { get; } = new(null, "Catálogo Maestro");

    public bool IsMasterCatalog => ProjectId == null;
}