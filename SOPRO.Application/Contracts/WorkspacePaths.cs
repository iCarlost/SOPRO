namespace SOPRO.Application.Contracts;

/// <summary>
/// Rutas del workspace de la aplicación (convención legacy de la UI).
/// </summary>
public static class WorkspacePaths
{
    public static string MasterDatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SOPRO",
            "CatalogoMaestro.db");
}