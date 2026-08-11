using Microsoft.EntityFrameworkCore;
using SOPRO.Data.Context;

namespace SOPRO.Tests.TestInfrastructure;

internal static class TestDbFactory
{
    public static SOPROContext CreateContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_tests_{Guid.NewGuid():N}.db");
        var context = new SOPROContext(dbPath);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Crea una base de datos nueva en la ruta indicada. Útil para pruebas de
    /// persistencia: permite cerrar el contexto, reabrir la misma ruta y
    /// verificar que los datos realmente quedaron guardados en disco.
    /// </summary>
    public static SOPROContext CreateContextAt(string dbPath)
    {
        var context = new SOPROContext(dbPath);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    public static string CreateTempDbPath()
        => Path.Combine(Path.GetTempPath(), $"sopro_persist_{Guid.NewGuid():N}.db");
}
