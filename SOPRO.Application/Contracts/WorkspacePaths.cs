namespace SOPRO.Application.Contracts;

/// <summary>
/// Rutas del workspace de la aplicación (convención legacy de la UI).
/// Fuente única de las carpetas por defecto para que los servicios puedan
/// recibir un <em>seam</em> inyectable sin duplicar la convención.
/// </summary>
public static class WorkspacePaths
{
    /// <summary>Carpeta legacy de documentos: <c>%USERPROFILE%\Documents\SOPRO</c>.</summary>
    public static string SoproFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SOPRO");

    /// <summary>Carpeta de datos locales: <c>%LOCALAPPDATA%\SOPRO</c>.</summary>
    public static string LocalDataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SOPRO");

    public static string MasterDatabasePath =>
        Path.Combine(SoproFolder, "CatalogoMaestro.db");
}
