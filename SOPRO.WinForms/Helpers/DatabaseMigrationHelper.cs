using SOPRO.Data.Context;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Wrapper de compatibilidad — delega a SchemaManager.
    /// Toda la lógica de migración está centralizada en SOPRO.Application.Services.SchemaManager.
    /// </summary>
    public static class DatabaseMigrationHelper
    {
        public static void EnsureTablesExist(SOPROContext context)
        {
            SOPRO.Application.Services.SchemaManager.EnsureCurrentSchema(context);
        }
    }
}
