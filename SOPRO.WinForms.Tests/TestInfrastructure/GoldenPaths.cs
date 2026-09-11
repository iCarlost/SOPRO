namespace SOPRO.WinForms.Tests.TestInfrastructure;

/// <summary>
/// Rutas de los goldens de reportes legacy.
/// En modo lectura se usa la copia en el directorio de salida (CopyToOutputDirectory).
/// En modo regeneración (variable de entorno SOPRO_REGENERATE_GOLDENS=1) se escribe
/// la fuente en el directorio del proyecto para dejarla versionada.
/// </summary>
internal static class GoldenPaths
{
    public const string RegenerateEnvVar = "SOPRO_REGENERATE_GOLDENS";

    public static string OutputDir => Path.Combine(
        AppContext.BaseDirectory, "TestData", "Goldens");

    public static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SOPRO.WinForms.Tests.csproj")))
                return Path.Combine(dir.FullName, "TestData", "Goldens");
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "No se localizó la raíz de SOPRO.WinForms.Tests para escribir goldens.");
    }

    public static bool Regenerating =>
        string.Equals(Environment.GetEnvironmentVariable(RegenerateEnvVar) ?? "", "1",
            StringComparison.Ordinal);

    public static string Resolve(string fileName)
    {
        var output = Path.Combine(OutputDir, fileName);
        if (Regenerating)
            return Path.Combine(SourceRoot(), fileName);
        return output;
    }
}