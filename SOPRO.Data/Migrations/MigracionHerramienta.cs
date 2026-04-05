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
    /// <summary>
    /// Migración manual para agregar soporte de Herramientas
    /// </summary>
    [Obsolete("LEGACY: consolidado en SchemaManager. No invocar.", error: false)]
    public static class MigracionHerramienta
    {
        public static void EjecutarMigracion(SOPROContext context)
        {
            try
            {
                // Usar ExecuteSqlRaw para ejecutar SQL directamente
                
                // 1. Crear tabla Herramientas si no existe
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS Herramientas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProyectoId INTEGER NOT NULL,
                        Clave TEXT NOT NULL,
                        Descripcion TEXT NOT NULL,
                        Unidad TEXT NOT NULL,
                        PrecioUnitario REAL NOT NULL DEFAULT 0,
                        FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE
                    );
                ");
                
                // 2. Crear índice en ProyectoId
                context.Database.ExecuteSqlRaw(@"
                    CREATE INDEX IF NOT EXISTS IX_Herramientas_ProyectoId 
                    ON Herramientas(ProyectoId);
                ");
                
                // 3. Verificar si existe columna HerramientaId
                // Si no existe, agregarla
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        ALTER TABLE ComponentesMatriz 
                        ADD COLUMN HerramientaId INTEGER NULL;
                    ");
                    Console.WriteLine("✓ Columna HerramientaId agregada");
                }
                catch
                {
                    // La columna ya existe, no hacer nada
                    Console.WriteLine("✓ Columna HerramientaId ya existe");
                }
                
                // 4. Crear índice en HerramientaId
                context.Database.ExecuteSqlRaw(@"
                    CREATE INDEX IF NOT EXISTS IX_ComponentesMatriz_HerramientaId 
                    ON ComponentesMatriz(HerramientaId);
                ");

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        ALTER TABLE Herramientas
                        ADD COLUMN Origen INTEGER NOT NULL DEFAULT 1;
                    ");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        ALTER TABLE Herramientas
                        ADD COLUMN Notas TEXT NULL;
                    ");
                }
                catch { }
                
                Console.WriteLine("✓ Migración de Herramientas completada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en migración: {ex.Message}");
                // No lanzar excepción para no bloquear la app
            }
        }
    }
}
