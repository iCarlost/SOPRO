// =============================================================================
// LEGACY — Consolidado en SOPRO.Application.Services.SchemaManager
// Este archivo se conserva solo como referencia histórica.
// No invocar directamente. No agregar nueva lógica aquí.
// =============================================================================
using Microsoft.EntityFrameworkCore;
using SOPRO.Data.Context;
using System;

namespace SOPRO.Data.Migrations
{
    [Obsolete("LEGACY: consolidado en SchemaManager. No invocar.", error: false)]
    public static class MigracionParametrosFSR
    {
        public static void EjecutarMigracion(SOPROContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    ALTER TABLE Proyectos
                    ADD COLUMN ParametrosFSR TEXT NULL;
                ");
                Console.WriteLine("✓ Columna ParametrosFSR agregada");
            }
            catch
            {
                // La columna ya existe
                Console.WriteLine("✓ Columna ParametrosFSR ya existe");
            }
        }
    }
}
