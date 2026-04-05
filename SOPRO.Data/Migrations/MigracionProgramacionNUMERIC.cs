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
    /// Migración de reparación: cambia ActividadPadreId de ON DELETE RESTRICT
    /// a ON DELETE SET NULL en ActividadesProgramadas.
    /// Con EF Core 8 + Microsoft.Data.Sqlite 8, las FK están activas por defecto,
    /// por lo que RESTRICT bloqueaba el borrado de actividades padre antes de que
    /// EF Core pudiera poner las hijas en NULL.
    /// Solo actúa si la tabla tiene RESTRICT (idempotente).
    /// </summary>
    [Obsolete("LEGACY: consolidado en SchemaManager. No invocar.", error: false)]
    public static class MigracionProgramacionNUMERIC
    {
        public static void Aplicar(SOPROContext context)
        {
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                // Verificar si ActividadesProgramadas tiene ON DELETE RESTRICT en ActividadPadreId
                string sqlActual = "";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='ActividadesProgramadas'";
                    sqlActual = cmd.ExecuteScalar() as string ?? "";
                }

                bool tieneRestrict = sqlActual.Contains("ActividadPadreId", StringComparison.OrdinalIgnoreCase)
                                  && sqlActual.Contains("ON DELETE RESTRICT", StringComparison.OrdinalIgnoreCase);

                // También migrar si las columnas numéricas aún son REAL (no NUMERIC)
                bool tieneReal = sqlActual.Contains("CantidadTotal REAL", StringComparison.OrdinalIgnoreCase)
                              || sqlActual.Contains("ImporteTotal REAL", StringComparison.OrdinalIgnoreCase);

                bool necesitaMigracion = tieneRestrict || tieneReal;

                if (!necesitaMigracion)
                {
                    Console.WriteLine("✓ MigracionProgramacionNUMERIC: tabla ya tiene FK SET NULL y columnas NUMERIC, sin acción.");
                    return;
                }

                Console.WriteLine("⚠ MigracionProgramacionNUMERIC: migrando (RESTRICT→SET NULL y/o REAL→NUMERIC)...");

                using var transaction = conn.BeginTransaction();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = transaction;

                    // SQLite no tiene ALTER COLUMN — necesitamos recrear la tabla
                    cmd.CommandText = "PRAGMA foreign_keys = OFF;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE IF EXISTS ActividadesProgramadas_FIX;";
                    cmd.ExecuteNonQuery();

                    // Crear tabla con SET NULL en lugar de RESTRICT
                    cmd.CommandText = @"
                        CREATE TABLE ActividadesProgramadas_FIX (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ProgramaObraId INTEGER NOT NULL,
                            ConceptoPresupuestoId INTEGER NULL,
                            ActividadPadreId INTEGER NULL,
                            Clave TEXT NULL,
                            Descripcion TEXT NOT NULL,
                            Unidad TEXT NULL,
                            EsResumen INTEGER NOT NULL DEFAULT 0,
                            EsHito INTEGER NOT NULL DEFAULT 0,
                            EsManual INTEGER NOT NULL DEFAULT 0,
                            Nivel INTEGER NOT NULL DEFAULT 1,
                            Orden INTEGER NOT NULL DEFAULT 0,
                            CantidadTotal NUMERIC NOT NULL DEFAULT 0,
                            CantidadProgramada NUMERIC NOT NULL DEFAULT 0,
                            AvanceProgramadoPorcentaje NUMERIC NOT NULL DEFAULT 0,
                            PrecioUnitario NUMERIC NOT NULL DEFAULT 0,
                            ImporteTotal NUMERIC NOT NULL DEFAULT 0,
                            ImporteProgramado NUMERIC NOT NULL DEFAULT 0,
                            FechaInicioTemprana TEXT NULL,
                            FechaFinTemprana TEXT NULL,
                            FechaInicioTardia TEXT NULL,
                            FechaFinTardia TEXT NULL,
                            FechaInicioProgramada TEXT NULL,
                            FechaFinProgramada TEXT NULL,
                            DuracionDiasNaturales INTEGER NOT NULL DEFAULT 0,
                            DuracionDiasHabiles INTEGER NOT NULL DEFAULT 0,
                            RendimientoDiario NUMERIC NOT NULL DEFAULT 0,
                            FrentesTrabajo INTEGER NOT NULL DEFAULT 1,
                            TipoRestriccion INTEGER NOT NULL DEFAULT 1,
                            FechaRestriccion TEXT NULL,
                            MetodoDistribucion INTEGER NOT NULL DEFAULT 1,
                            RutaCritica INTEGER NOT NULL DEFAULT 0,
                            HolguraDias INTEGER NOT NULL DEFAULT 0,
                            Notas TEXT NULL,
                            FechaCreacion TEXT NOT NULL,
                            FechaModificacion TEXT NOT NULL,
                            FOREIGN KEY (ProgramaObraId) REFERENCES ProgramasObra(Id) ON DELETE CASCADE,
                            FOREIGN KEY (ConceptoPresupuestoId) REFERENCES ConceptosPresupuesto(Id) ON DELETE SET NULL,
                            FOREIGN KEY (ActividadPadreId) REFERENCES ActividadesProgramadas_FIX(Id) ON DELETE SET NULL
                        );";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "INSERT INTO ActividadesProgramadas_FIX SELECT * FROM ActividadesProgramadas;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE ActividadesProgramadas;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "ALTER TABLE ActividadesProgramadas_FIX RENAME TO ActividadesProgramadas;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "PRAGMA foreign_keys = ON;";
                    cmd.ExecuteNonQuery();

                    transaction.Commit();
                    Console.WriteLine("✓ MigracionProgramacionNUMERIC: ActividadPadreId corregido a SET NULL.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    using var fix = conn.CreateCommand();
                    fix.CommandText = "PRAGMA foreign_keys = ON;";
                    fix.ExecuteNonQuery();
                    Console.WriteLine($"✗ MigracionProgramacionNUMERIC falló: {ex.Message}");
                    // No relanzar — no bloquear apertura del proyecto
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MigracionProgramacionNUMERIC: {ex.Message}");
            }
        }
    }
}
