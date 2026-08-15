namespace SOPRO.Application.Contracts;

/// <summary>
/// Información de sesión neutral para los casos de uso de la frontera de
/// Application: identidad del proyecto y rutas de la base de datos.
///
/// REGLA N4: la sesión NO posee ni expone <c>SOPROContext</c> (ni es
/// desechable): los casos de uso construyen y liberan su contexto dentro del
/// método a través de <c>IProjectDbContextFactory</c> (una operación, un
/// contexto). La identidad del proyecto se resuelve al crear la sesión
/// (puente legacy) y viaja como datos puros.
/// </summary>
public sealed record ProjectSessionInfo
{
    /// <summary>Proyecto activo (o catálogo maestro).</summary>
    public ProjectRef Project { get; }

    /// <summary>Ruta de la base de datos de la sesión.</summary>
    public string DatabasePath { get; }

    /// <summary>Ruta de la base del catálogo maestro (CatalogoMaestro.db).</summary>
    public string MasterDatabasePath { get; }

    /// <summary>Decimales de importe del proyecto (null en catálogo maestro).</summary>
    public int? DecimalesImporte { get; }

    private ProjectSessionInfo(
        ProjectRef project,
        string databasePath,
        string masterDatabasePath,
        int? decimalesImporte)
    {
        Project = project;
        DatabasePath = databasePath;
        MasterDatabasePath = masterDatabasePath;
        DecimalesImporte = decimalesImporte;
    }

    /// <summary>
    /// Crea la información de sesión a partir de la identidad del proyecto y la
    /// ruta de su base de datos. <see cref="ProjectRef.Master"/> para la sesión
    /// del catálogo maestro.
    /// </summary>
    public static ProjectSessionInfo Create(
        ProjectRef project,
        string databasePath,
        string? masterDatabasePath = null,
        int? decimalesImporte = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(databasePath);

        return new ProjectSessionInfo(
            project,
            databasePath,
            masterDatabasePath ?? WorkspacePaths.MasterDatabasePath,
            decimalesImporte);
    }
}