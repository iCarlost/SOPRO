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
    /// Migración manual para agregar la base del módulo de Programación de Obra.
    /// </summary>
    [Obsolete("LEGACY: consolidado en SchemaManager. No invocar.", error: false)]
    public static class MigracionProgramacionObra
    {
        public static void EjecutarMigracion(SOPROContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS CalendariosLaborales (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProyectoId INTEGER NOT NULL,
                        Nombre TEXT NOT NULL,
                        Lunes INTEGER NOT NULL DEFAULT 1,
                        Martes INTEGER NOT NULL DEFAULT 1,
                        Miercoles INTEGER NOT NULL DEFAULT 1,
                        Jueves INTEGER NOT NULL DEFAULT 1,
                        Viernes INTEGER NOT NULL DEFAULT 1,
                        Sabado INTEGER NOT NULL DEFAULT 0,
                        Domingo INTEGER NOT NULL DEFAULT 0,
                        HoraInicio TEXT NOT NULL DEFAULT '08:00:00',
                        HoraFin TEXT NOT NULL DEFAULT '18:00:00',
                        Activo INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE
                    );
                ");

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ProgramasObra (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProyectoId INTEGER NOT NULL,
                        Nombre TEXT NOT NULL,
                        Descripcion TEXT NULL,
                        FechaInicioPrograma TEXT NOT NULL,
                        FechaFinPrograma TEXT NULL,
                        TipoPeriodo INTEGER NOT NULL DEFAULT 2,
                        DuracionPeriodoDias INTEGER NOT NULL DEFAULT 7,
                        CalendarioLaboralId INTEGER NULL,
                        Activo INTEGER NOT NULL DEFAULT 1,
                        GeneradoDesdePresupuesto INTEGER NOT NULL DEFAULT 1,
                        FechaCreacion TEXT NOT NULL,
                        FechaModificacion TEXT NOT NULL,
                        FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE,
                        FOREIGN KEY (CalendarioLaboralId) REFERENCES CalendariosLaborales(Id) ON DELETE RESTRICT
                    );
                ");

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ActividadesProgramadas (
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
                        CantidadTotal REAL NOT NULL DEFAULT 0,
                        CantidadProgramada REAL NOT NULL DEFAULT 0,
                        AvanceProgramadoPorcentaje REAL NOT NULL DEFAULT 0,
                        PrecioUnitario REAL NOT NULL DEFAULT 0,
                        ImporteTotal REAL NOT NULL DEFAULT 0,
                        ImporteProgramado REAL NOT NULL DEFAULT 0,
                        FechaInicioTemprana TEXT NULL,
                        FechaFinTemprana TEXT NULL,
                        FechaInicioTardia TEXT NULL,
                        FechaFinTardia TEXT NULL,
                        FechaInicioProgramada TEXT NULL,
                        FechaFinProgramada TEXT NULL,
                        DuracionDiasNaturales INTEGER NOT NULL DEFAULT 0,
                        DuracionDiasHabiles INTEGER NOT NULL DEFAULT 0,
                        RendimientoDiario REAL NOT NULL DEFAULT 0,
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
                        FOREIGN KEY (ActividadPadreId) REFERENCES ActividadesProgramadas(Id) ON DELETE RESTRICT
                    );
                ");

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS PeriodosPrograma (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProgramaObraId INTEGER NOT NULL,
                        NumeroPeriodo INTEGER NOT NULL,
                        Etiqueta TEXT NOT NULL,
                        FechaInicio TEXT NOT NULL,
                        FechaFin TEXT NOT NULL,
                        EsCerrado INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY (ProgramaObraId) REFERENCES ProgramasObra(Id) ON DELETE CASCADE
                    );
                ");

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS DistribucionesPeriodo (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ActividadProgramadaId INTEGER NOT NULL,
                        PeriodoProgramaId INTEGER NOT NULL,
                        CantidadProgramada REAL NOT NULL DEFAULT 0,
                        PorcentajeProgramado REAL NOT NULL DEFAULT 0,
                        PrecioUnitario REAL NOT NULL DEFAULT 0,
                        ImporteProgramado REAL NOT NULL DEFAULT 0,
                        FechaCreacion TEXT NOT NULL,
                        FechaModificacion TEXT NOT NULL,
                        FOREIGN KEY (ActividadProgramadaId) REFERENCES ActividadesProgramadas(Id) ON DELETE CASCADE,
                        FOREIGN KEY (PeriodoProgramaId) REFERENCES PeriodosPrograma(Id) ON DELETE CASCADE
                    );
                ");

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS DependenciasActividad (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ActividadOrigenId INTEGER NOT NULL,
                        ActividadDestinoId INTEGER NOT NULL,
                        TipoDependencia INTEGER NOT NULL DEFAULT 1,
                        DesfaseDias INTEGER NOT NULL DEFAULT 0,
                        FechaCreacion TEXT NOT NULL,
                        FOREIGN KEY (ActividadOrigenId) REFERENCES ActividadesProgramadas(Id) ON DELETE CASCADE,
                        FOREIGN KEY (ActividadDestinoId) REFERENCES ActividadesProgramadas(Id) ON DELETE CASCADE
                    );
                ");

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ExcepcionesCalendario (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CalendarioLaboralId INTEGER NOT NULL,
                        Fecha TEXT NOT NULL,
                        Descripcion TEXT NULL,
                        Tipo INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (CalendarioLaboralId) REFERENCES CalendariosLaborales(Id) ON DELETE CASCADE
                    );
                ");

                context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_CalendariosLaborales_ProyectoId_Nombre ON CalendariosLaborales(ProyectoId, Nombre);");
                context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_ProgramasObra_ProyectoId_Nombre ON ProgramasObra(ProyectoId, Nombre);");
                context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_ActividadesProgramadas_ProgramaObraId_Orden ON ActividadesProgramadas(ProgramaObraId, Orden);");
                context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_ActividadesProgramadas_ProgramaObraId_ConceptoPresupuestoId ON ActividadesProgramadas(ProgramaObraId, ConceptoPresupuestoId);");
                context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_PeriodosPrograma_ProgramaObraId_NumeroPeriodo ON PeriodosPrograma(ProgramaObraId, NumeroPeriodo);");
                context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_DistribucionesPeriodo_ActividadProgramadaId_PeriodoProgramaId ON DistribucionesPeriodo(ActividadProgramadaId, PeriodoProgramaId);");
                context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_DependenciasActividad_ActividadOrigenId_ActividadDestinoId ON DependenciasActividad(ActividadOrigenId, ActividadDestinoId);");
                context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_ExcepcionesCalendario_CalendarioLaboralId_Fecha ON ExcepcionesCalendario(CalendarioLaboralId, Fecha);");

                Console.WriteLine("✓ Migración de Programación de Obra completada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en migración de programación: {ex.Message}");
            }
        }
    }
}
