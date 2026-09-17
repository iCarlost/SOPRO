using SOPRO.Data.Context;

namespace SOPRO.Tests.TestInfrastructure;

/// <summary>
/// Fixture del proyecto sintético de regresión (100% sintético, sin datos reales).
///
/// La BD canónica vive en <c>TestData/proyecto-sintetico-vial-demo.db</c> y se
/// abre sobre una copia temporal de solo lectura: el archivo fuente es inmutable
/// y cualquier escritura accidental falla en lugar de mutar la copia.
/// </summary>
internal static class ProyectoSinteticoFixture
{
    private const string DbFileName = "proyecto-sintetico-vial-demo.db";

    public static SOPROContext OpenReadOnlyCopy()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData", DbFileName);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"No se encontró el proyecto sintético de prueba. Ruta esperada: {sourcePath}",
                sourcePath);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"sopro_sintetico_regression_{Guid.NewGuid():N}.db");
        File.Copy(sourcePath, tempPath, overwrite: true);

        // Apertura REALMENTE de solo lectura: el manifiesto N0 promete copia inmutable,
        // así que se fija el atributo de archivo y se abre con Mode=ReadOnly para que
        // cualquier SaveChanges accidental falle en lugar de mutar silenciosamente la copia.
        // SOPROContext(string) antepone "Data Source=" a la ruta, por eso se pasa el
        // sufijo de conexión junto a la ruta.
        File.SetAttributes(tempPath, FileAttributes.ReadOnly);
        return new SOPROContext($"{tempPath};Mode=ReadOnly;Cache=Shared");
    }
}
