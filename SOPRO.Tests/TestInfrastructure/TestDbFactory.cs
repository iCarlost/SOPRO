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
}
