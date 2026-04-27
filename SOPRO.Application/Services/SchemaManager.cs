using Microsoft.EntityFrameworkCore;
using SOPRO.Data.Context;
using System;
using System.Data.Common;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Fuente única de verdad del schema SQLite de SOPRO.
    ///
    /// Consolida todo lo que antes estaba disperso en:
    ///   - 20250217_AddModoCalculoPorcentajes.cs
    ///   - MigracionHerramienta.cs
    ///   - MigracionParametrosFSR.cs
    ///   - MigracionProgramacionObra.cs
    ///   - MigracionProgramacionNUMERIC.cs
    ///   - ProjectLifecycleService.TryUpgradeSchema()
    ///   - ProjectLifecycleService.EnsureRibbonColumnPersistence()
    ///   - ProjectLifecycleService.TryCreateFinanciamientoTables()
    ///   - DatabaseMigrationHelper.EnsureTablesExist()
    ///   - DatabaseInitializer.UpgradeSchema()
    ///
    /// Estrategia: idempotencia pura.
    ///   - Las migraciones normales (CREATE IF NOT EXISTS + ALTER ADD COLUMN)
    ///     corren dentro de una transacción.
    ///   - RepararActividadesProgramadas corre FUERA de esa transacción porque
    ///     necesita PRAGMA foreign_keys=OFF, que SQLite no permite dentro de
    ///     una transacción activa.
    ///
    /// Uso:
    ///   SchemaManager.EnsureCurrentSchema(context);
    ///
    /// ─── REGLA DE MANTENIMIENTO ───────────────────────────────────────────────
    /// Todo cambio de schema en SOPRO debe tocar estos 3 artefactos en orden:
    ///   1. Entidad en SOPRO.Core/Entities/
    ///   2. Mapeo en SOPRO.Data/Context/SOPROContext.cs
    ///   3. Migración nueva (M0XX) en este SchemaManager
    ///      + incrementar VersionActual
    ///
    /// Si solo tocas (1) y (2) sin tocar (3), los DBs viejos fallarán con
    /// "no such column" o "NULL at ordinal N" al abrirse.
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class SchemaManager
    {
        /// <summary>
        /// Versión actual del schema. Incrementar cuando se agreguen nuevas migraciones.
        /// Formato: AAAA.MM.revision
        /// </summary>
        public const string VersionActual = "2026.04.2";

        /// <summary>
        /// Aplica todas las migraciones sobre el contexto dado.
        /// Idempotente: se puede llamar múltiples veces sin efectos secundarios.
        /// </summary>
        public static void EnsureCurrentSchema(SOPROContext context)
        {
            var conn = context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();

            try
            {
                // Crear tabla de control de versiones (idempotente, fuera de tx)
                EnsureMigracionesTable(conn);

                // PASO 1 — Migraciones idempotentes normales dentro de una transacción.
                // Todas son CREATE TABLE IF NOT EXISTS o ALTER TABLE ADD COLUMN.
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        M001_Herramientas(conn, tx);
                        M002_ProyectosColumnas(conn, tx);
                        M003_ProgramacionObra(conn, tx);
                        M004_ColumnasTablas(conn, tx);
                        M005_Financiamiento(conn, tx);
                        M006_TitulosReporte(conn, tx);
                        M007_WrapTextoAlineacion(conn, tx);
                        M008_MaquinariaColumnas(conn, tx);
                        M009_ConceptosPresupuesto(conn, tx);
                        M010_ConfigColumnasReporte(conn, tx);
                        M011_SanitizarNullsLegacy(conn, tx);
                        M012_DisenadorEncabezadoPdf(conn, tx);
                        RegistrarVersion(conn, tx);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                // PASO 2 — Reparación especial de ActividadesProgramadas.
                // DEBE correr FUERA de cualquier transacción activa porque necesita
                // PRAGMA foreign_keys=OFF, que SQLite prohíbe dentro de transacciones.
                // Tiene su propia transacción interna.
                RepararActividadesProgramadas(conn, context);
            }
            finally
            {
                if (!wasOpen) conn.Close();
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        // Todos los helpers aceptan DbTransaction? tx para ligarlos a la
        // transacción principal y garantizar atomicidad real de M001-M011.

        private static void Exec(DbConnection conn, string sql, DbTransaction? tx = null)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;
            cmd.ExecuteNonQuery();
        }

        private static void TryExec(DbConnection conn, string sql, DbTransaction? tx = null)
        {
            try { Exec(conn, sql, tx); }
            catch { /* columna o índice ya existe — ignorar */ }
        }

        private static bool TablaExiste(DbConnection conn, string tabla, DbTransaction? tx = null)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tabla}'";
            if (tx != null) cmd.Transaction = tx;
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnaExiste(DbConnection conn, string tabla, string columna, DbTransaction? tx = null)
        {
            if (!TablaExiste(conn, tabla, tx)) return false;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabla}') WHERE name='{columna}'";
            if (tx != null) cmd.Transaction = tx;
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static void AgregarColumna(DbConnection conn, string tabla, string columna, string definicion, DbTransaction? tx = null)
        {
            if (!ColumnaExiste(conn, tabla, columna, tx))
                TryExec(conn, $"ALTER TABLE [{tabla}] ADD COLUMN {columna} {definicion}", tx);
        }

        // ── Versionado de schema ───────────────────────────────────────────────

        private static void EnsureMigracionesTable(DbConnection conn)
        {
            // Corre antes de la transacción principal — no recibe tx.
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS __MigracionesCustom (
                    Clave          TEXT PRIMARY KEY NOT NULL,
                    FechaAplicacion TEXT NOT NULL
                );");
        }

        private static void RegistrarVersion(DbConnection conn, DbTransaction tx)
        {
            // Mantiene UNA SOLA fila de versión. Borra versiones anteriores
            // antes de insertar la actual para no acumular historial innecesario.
            using var cmdDel = conn.CreateCommand();
            cmdDel.Transaction = tx;
            cmdDel.CommandText = "DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%'";
            cmdDel.ExecuteNonQuery();

            using var cmdIns = conn.CreateCommand();
            cmdIns.Transaction = tx;
            cmdIns.CommandText = @"
                INSERT INTO __MigracionesCustom (Clave, FechaAplicacion)
                VALUES (@v, @f)";
            var p1 = cmdIns.CreateParameter(); p1.ParameterName = "@v"; p1.Value = $"SchemaVersion_{VersionActual}"; cmdIns.Parameters.Add(p1);
            var p2 = cmdIns.CreateParameter(); p2.ParameterName = "@f"; p2.Value = DateTime.Now.ToString("o");         cmdIns.Parameters.Add(p2);
            cmdIns.ExecuteNonQuery();
        }

        /// <summary>
        /// Devuelve la versión de schema registrada en el DB, o null si no hay.
        /// Útil para diagnóstico y tests.
        /// </summary>
        public static string? GetVersionRegistrada(SOPROContext context)
        {
            var conn = context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Clave FROM __MigracionesCustom
                    WHERE Clave LIKE 'SchemaVersion_%'
                    ORDER BY FechaAplicacion DESC LIMIT 1";
                var val = cmd.ExecuteScalar() as string;
                return val?.Replace("SchemaVersion_", "");
            }
            catch { return null; }
            finally { if (!wasOpen) conn.Close(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M001 — Herramientas
        // ═══════════════════════════════════════════════════════════════════════
        private static void M001_Herramientas(DbConnection conn, DbTransaction tx)
        {
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS Herramientas (
                    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId        INTEGER NOT NULL,
                    Clave             TEXT    NOT NULL,
                    Descripcion       TEXT    NOT NULL,
                    Unidad            TEXT    NOT NULL,
                    PrecioUnitario    REAL    NOT NULL DEFAULT 0,
                    FechaCreacion     TEXT    NOT NULL,
                    FechaModificacion TEXT    NULL,
                    Origen            INTEGER NOT NULL DEFAULT 1,
                    Notas             TEXT    NULL,
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_Herramientas_ProyectoId ON Herramientas(ProyectoId);", tx);
            AgregarColumna(conn, "ComponentesMatriz", "HerramientaId", "INTEGER NULL", tx);
            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_ComponentesMatriz_HerramientaId ON ComponentesMatriz(HerramientaId);", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M002 — Columnas adicionales en Proyectos
        // ═══════════════════════════════════════════════════════════════════════
        private static void M002_ProyectosColumnas(DbConnection conn, DbTransaction tx)
        {
            AgregarColumna(conn, "Proyectos", "ModoCalculoPorcentajes", "TEXT NOT NULL DEFAULT 'Acumulables'", tx);
            AgregarColumna(conn, "Proyectos", "DecimalesCantidad", "INTEGER NOT NULL DEFAULT 2", tx);
            AgregarColumna(conn, "Proyectos", "DecimalesImporte", "INTEGER NOT NULL DEFAULT 2", tx);
            AgregarColumna(conn, "Proyectos", "DecimalesPorcentaje", "INTEGER NOT NULL DEFAULT 4", tx);
            AgregarColumna(conn, "Proyectos", "ParametrosFSR", "TEXT NULL", tx);
            AgregarColumna(conn, "Proyectos", "BarraLateralColapsada", "INTEGER NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "Proyectos", "NodosMenuExpandidos", "TEXT NULL", tx);
            AgregarColumna(conn, "Proyectos", "PorcentajeCargosAdicionales", "REAL NOT NULL DEFAULT 0", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M003 — Tablas de Programación de Obra
        // ═══════════════════════════════════════════════════════════════════════
        private static void M003_ProgramacionObra(DbConnection conn, DbTransaction tx)
        {
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS CalendariosLaborales (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId  INTEGER NOT NULL,
                    Nombre      TEXT    NOT NULL,
                    Lunes       INTEGER NOT NULL DEFAULT 1,
                    Martes      INTEGER NOT NULL DEFAULT 1,
                    Miercoles   INTEGER NOT NULL DEFAULT 1,
                    Jueves      INTEGER NOT NULL DEFAULT 1,
                    Viernes     INTEGER NOT NULL DEFAULT 1,
                    Sabado      INTEGER NOT NULL DEFAULT 0,
                    Domingo     INTEGER NOT NULL DEFAULT 0,
                    HoraInicio  TEXT    NOT NULL DEFAULT '08:00:00',
                    HoraFin     TEXT    NOT NULL DEFAULT '18:00:00',
                    Activo      INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_CalendariosLaborales_ProyectoId_Nombre ON CalendariosLaborales(ProyectoId, Nombre);", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ProgramasObra (
                    Id                       INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId               INTEGER NOT NULL,
                    Nombre                   TEXT    NOT NULL,
                    Descripcion              TEXT    NULL,
                    FechaInicioPrograma      TEXT    NOT NULL,
                    FechaFinPrograma         TEXT    NULL,
                    TipoPeriodo              INTEGER NOT NULL DEFAULT 2,
                    DuracionPeriodoDias      INTEGER NOT NULL DEFAULT 7,
                    CalendarioLaboralId      INTEGER NULL,
                    Activo                   INTEGER NOT NULL DEFAULT 1,
                    GeneradoDesdePresupuesto INTEGER NOT NULL DEFAULT 1,
                    FechaCreacion            TEXT    NOT NULL,
                    FechaModificacion        TEXT    NOT NULL,
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE,
                    FOREIGN KEY (CalendarioLaboralId) REFERENCES CalendariosLaborales(Id) ON DELETE RESTRICT
                );", tx);
            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_ProgramasObra_ProyectoId_Nombre ON ProgramasObra(ProyectoId, Nombre);", tx);

            // Crear con FK correcta (SET NULL). Si ya existe con FK vieja (RESTRICT),
            // RepararActividadesProgramadas — que corre FUERA de esta transacción — la corrige.
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ActividadesProgramadas (
                    Id                         INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProgramaObraId             INTEGER NOT NULL,
                    ConceptoPresupuestoId      INTEGER NULL,
                    ActividadPadreId           INTEGER NULL,
                    Clave                      TEXT    NULL,
                    Descripcion                TEXT    NOT NULL,
                    Unidad                     TEXT    NULL,
                    EsResumen                  INTEGER NOT NULL DEFAULT 0,
                    EsHito                     INTEGER NOT NULL DEFAULT 0,
                    EsManual                   INTEGER NOT NULL DEFAULT 0,
                    Nivel                      INTEGER NOT NULL DEFAULT 1,
                    Orden                      INTEGER NOT NULL DEFAULT 0,
                    CantidadTotal              REAL    NOT NULL DEFAULT 0,
                    CantidadProgramada         REAL    NOT NULL DEFAULT 0,
                    AvanceProgramadoPorcentaje REAL    NOT NULL DEFAULT 0,
                    PrecioUnitario             REAL    NOT NULL DEFAULT 0,
                    ImporteTotal               REAL    NOT NULL DEFAULT 0,
                    ImporteProgramado          REAL    NOT NULL DEFAULT 0,
                    FechaInicioTemprana        TEXT    NULL,
                    FechaFinTemprana           TEXT    NULL,
                    FechaInicioTardia          TEXT    NULL,
                    FechaFinTardia             TEXT    NULL,
                    FechaInicioProgramada      TEXT    NULL,
                    FechaFinProgramada         TEXT    NULL,
                    DuracionDiasNaturales      INTEGER NOT NULL DEFAULT 0,
                    DuracionDiasHabiles        INTEGER NOT NULL DEFAULT 0,
                    RendimientoDiario          REAL    NOT NULL DEFAULT 0,
                    FrentesTrabajo             INTEGER NOT NULL DEFAULT 1,
                    TipoRestriccion            INTEGER NOT NULL DEFAULT 1,
                    FechaRestriccion           TEXT    NULL,
                    MetodoDistribucion         INTEGER NOT NULL DEFAULT 1,
                    RutaCritica                INTEGER NOT NULL DEFAULT 0,
                    HolguraDias                INTEGER NOT NULL DEFAULT 0,
                    Notas                      TEXT    NULL,
                    FechaCreacion              TEXT    NOT NULL,
                    FechaModificacion          TEXT    NOT NULL,
                    FOREIGN KEY (ProgramaObraId) REFERENCES ProgramasObra(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ConceptoPresupuestoId) REFERENCES ConceptosPresupuesto(Id) ON DELETE SET NULL,
                    FOREIGN KEY (ActividadPadreId) REFERENCES ActividadesProgramadas(Id) ON DELETE SET NULL
                );", tx);
            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_ActividadesProgramadas_ProgramaObraId_Orden ON ActividadesProgramadas(ProgramaObraId, Orden);", tx);
            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_ActividadesProgramadas_ProgramaObraId_ConceptoPresupuestoId ON ActividadesProgramadas(ProgramaObraId, ConceptoPresupuestoId);", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS PeriodosPrograma (
                    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProgramaObraId INTEGER NOT NULL,
                    NumeroPeriodo  INTEGER NOT NULL,
                    Etiqueta       TEXT    NOT NULL,
                    FechaInicio    TEXT    NOT NULL,
                    FechaFin       TEXT    NOT NULL,
                    EsCerrado      INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (ProgramaObraId) REFERENCES ProgramasObra(Id) ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_PeriodosPrograma_ProgramaObraId_NumeroPeriodo ON PeriodosPrograma(ProgramaObraId, NumeroPeriodo);", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS DistribucionesPeriodo (
                    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                    ActividadProgramadaId INTEGER NOT NULL,
                    PeriodoProgramaId     INTEGER NOT NULL,
                    CantidadProgramada    REAL    NOT NULL DEFAULT 0,
                    PorcentajeProgramado  REAL    NOT NULL DEFAULT 0,
                    PrecioUnitario        REAL    NOT NULL DEFAULT 0,
                    ImporteProgramado     REAL    NOT NULL DEFAULT 0,
                    FechaCreacion         TEXT    NOT NULL,
                    FechaModificacion     TEXT    NOT NULL,
                    FOREIGN KEY (ActividadProgramadaId) REFERENCES ActividadesProgramadas(Id) ON DELETE CASCADE,
                    FOREIGN KEY (PeriodoProgramaId)     REFERENCES PeriodosPrograma(Id)       ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_DistribucionesPeriodo_ActividadProgramadaId_PeriodoProgramaId ON DistribucionesPeriodo(ActividadProgramadaId, PeriodoProgramaId);", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS DependenciasActividad (
                    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                    ActividadOrigenId  INTEGER NOT NULL,
                    ActividadDestinoId INTEGER NOT NULL,
                    TipoDependencia    INTEGER NOT NULL DEFAULT 1,
                    DesfaseDias        INTEGER NOT NULL DEFAULT 0,
                    FechaCreacion      TEXT    NOT NULL,
                    FOREIGN KEY (ActividadOrigenId)  REFERENCES ActividadesProgramadas(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ActividadDestinoId) REFERENCES ActividadesProgramadas(Id) ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_DependenciasActividad_ActividadOrigenId_ActividadDestinoId ON DependenciasActividad(ActividadOrigenId, ActividadDestinoId);", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ExcepcionesCalendario (
                    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    CalendarioLaboralId INTEGER NOT NULL,
                    Fecha               TEXT    NOT NULL,
                    Descripcion         TEXT    NULL,
                    Tipo                INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (CalendarioLaboralId) REFERENCES CalendariosLaborales(Id) ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_ExcepcionesCalendario_CalendarioLaboralId_Fecha ON ExcepcionesCalendario(CalendarioLaboralId, Fecha);", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M004 — Tablas Columnas* de catálogos + Rendimiento + Modulo
        // ═══════════════════════════════════════════════════════════════════════
        private static void M004_ColumnasTablas(DbConnection conn, DbTransaction tx)
        {
            void CrearTablaColumnas(string nombre)
            {
                Exec(conn, $@"
                    CREATE TABLE IF NOT EXISTS [{nombre}] (
                        Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProyectoId      INTEGER NOT NULL,
                        Nombre          TEXT    NOT NULL,
                        NombreInterno   TEXT    NOT NULL,
                        Visible         INTEGER NOT NULL DEFAULT 1,
                        Orden           INTEGER NOT NULL DEFAULT 0,
                        AnchoColumna    INTEGER NOT NULL DEFAULT 100,
                        Alineacion      INTEGER NOT NULL DEFAULT 0,
                        FormatoNumerico TEXT    NOT NULL DEFAULT '',
                        NombreFuente    TEXT    NOT NULL DEFAULT 'Segoe UI',
                        TamanoFuente    INTEGER NOT NULL DEFAULT 9,
                        ColorFuente     TEXT    NOT NULL DEFAULT '#000000',
                        ColorFondo      TEXT    NOT NULL DEFAULT '#FFFFFF',
                        Negrita         INTEGER NOT NULL DEFAULT 0,
                        Cursiva         INTEGER NOT NULL DEFAULT 0,
                        FechaModificacion TEXT  NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE,
                        UNIQUE (ProyectoId, NombreInterno)
                    );", tx);
                TryExec(conn, $"CREATE UNIQUE INDEX IF NOT EXISTS IX_{nombre}_ProyectoId_NombreInterno ON [{nombre}](ProyectoId, NombreInterno);", tx);
            }

            CrearTablaColumnas("ColumnasMaterial");
            CrearTablaColumnas("ColumnasManoObra");
            CrearTablaColumnas("ColumnasHerramienta");
            CrearTablaColumnas("ColumnasMaquinaria");
            CrearTablaColumnas("ColumnasMatriz");
            CrearTablaColumnas("ColumnasExplosion");
            CrearTablaColumnas("ColumnasIndirectos");
            CrearTablaColumnas("ColumnasProgramaObra");
            CrearTablaColumnas("ColumnasProgramaInsumos");

            if (!ColumnaExiste(conn, "ComponentesMatriz", "Rendimiento", tx))
            {
                TryExec(conn, "ALTER TABLE ComponentesMatriz ADD COLUMN Rendimiento REAL NOT NULL DEFAULT 0", tx);
                TryExec(conn, @"
                    UPDATE ComponentesMatriz
                    SET Rendimiento = CASE WHEN CAST(Cantidad AS REAL) > 0
                                     THEN 1.0 / CAST(Cantidad AS REAL) ELSE 0 END
                    WHERE TipoComponente = 2", tx);
            }

            AgregarColumna(conn, "ColumnasPersonalizadas", "Modulo", "TEXT NOT NULL DEFAULT 'Presupuesto'", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M005 — Financiamiento
        // ═══════════════════════════════════════════════════════════════════════
        private static void M005_Financiamiento(DbConnection conn, DbTransaction tx)
        {
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ConfiguracionesFinanciamiento (
                    Id                           INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId                   INTEGER NOT NULL,
                    TasaTIIE                     REAL    NOT NULL DEFAULT 11.0,
                    PuntosAdicionales            REAL    NOT NULL DEFAULT 3.0,
                    PorcentajeAnticipo           REAL    NOT NULL DEFAULT 30.0,
                    PeriodosAmortizacionAnticipo INTEGER NOT NULL DEFAULT 1,
                    DesfaseCobro                 INTEGER NOT NULL DEFAULT 1,
                    BaseCalculo                  TEXT    NOT NULL DEFAULT 'Acumulable',
                    InteresesNegativos           REAL    NOT NULL DEFAULT 0,
                    InteresesPositivos           REAL    NOT NULL DEFAULT 0,
                    FinanciamientoNeto           REAL    NOT NULL DEFAULT 0,
                    PorcentajeCalculado          REAL    NOT NULL DEFAULT 0,
                    FechaCalculo                 TEXT    NULL,
                    PorcentajeAmortizacion       REAL    NOT NULL DEFAULT 30.0,
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE
                );", tx);
            AgregarColumna(conn, "ConfiguracionesFinanciamiento", "PeriodosAmortizacionAnticipo", "INTEGER NOT NULL DEFAULT 1", tx);
            AgregarColumna(conn, "ConfiguracionesFinanciamiento", "DesfaseCobro", "INTEGER NOT NULL DEFAULT 1", tx);
            AgregarColumna(conn, "ConfiguracionesFinanciamiento", "BaseCalculo", "TEXT NOT NULL DEFAULT 'Acumulable'", tx);
            AgregarColumna(conn, "ConfiguracionesFinanciamiento", "FechaCalculo", "TEXT NULL", tx);
            AgregarColumna(conn, "ConfiguracionesFinanciamiento", "PorcentajeAmortizacion", "REAL NOT NULL DEFAULT 30.0", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS FilasFlujoCajaFinanciamiento (
                    Id                            INTEGER PRIMARY KEY AUTOINCREMENT,
                    ConfiguracionFinanciamientoId INTEGER NOT NULL,
                    NumeroPeriodo                 INTEGER NOT NULL DEFAULT 0,
                    Etiqueta                      TEXT    NOT NULL DEFAULT '',
                    FechaInicio                   TEXT    NOT NULL DEFAULT '',
                    FechaFin                      TEXT    NOT NULL DEFAULT '',
                    Egresos                       REAL    NOT NULL DEFAULT 0,
                    AnticipoRecibido              REAL    NOT NULL DEFAULT 0,
                    EstimacionCobrada             REAL    NOT NULL DEFAULT 0,
                    AmortizacionAnticipo          REAL    NOT NULL DEFAULT 0,
                    FlujoNeto                     REAL    NOT NULL DEFAULT 0,
                    SaldoAcumulado                REAL    NOT NULL DEFAULT 0,
                    DiasPeriodo                   INTEGER NOT NULL DEFAULT 0,
                    InteresPeriodo                REAL    NOT NULL DEFAULT 0,
                    FOREIGN KEY (ConfiguracionFinanciamientoId) REFERENCES ConfiguracionesFinanciamiento(Id) ON DELETE CASCADE
                );", tx);
            AgregarColumna(conn, "FilasFlujoCajaFinanciamiento", "DiasPeriodo",   "INTEGER NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "FilasFlujoCajaFinanciamiento", "InteresPeriodo", "REAL NOT NULL DEFAULT 0", tx);

            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ColumnasFinanciamiento (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId      INTEGER NOT NULL,
                    Nombre          TEXT    NOT NULL,
                    NombreInterno   TEXT    NOT NULL,
                    Visible         INTEGER NOT NULL DEFAULT 1,
                    Orden           INTEGER NOT NULL DEFAULT 0,
                    AnchoColumna    INTEGER NOT NULL DEFAULT 100,
                    Alineacion      INTEGER NOT NULL DEFAULT 0,
                    FormatoNumerico TEXT    NOT NULL DEFAULT '',
                    NombreFuente    TEXT    NOT NULL DEFAULT 'Segoe UI',
                    TamanoFuente    INTEGER NOT NULL DEFAULT 9,
                    ColorFuente     TEXT    NOT NULL DEFAULT '#000000',
                    ColorFondo      TEXT    NOT NULL DEFAULT '#FFFFFF',
                    Negrita         INTEGER NOT NULL DEFAULT 0,
                    Cursiva         INTEGER NOT NULL DEFAULT 0,
                    FechaModificacion TEXT  NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE,
                    UNIQUE (ProyectoId, NombreInterno)
                );", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M006 — ConfiguracionesTituloReporte
        // ═══════════════════════════════════════════════════════════════════════
        private static void M006_TitulosReporte(DbConnection conn, DbTransaction tx)
        {
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ConfiguracionesTituloReporte (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId   INTEGER NOT NULL,
                    Modulo       TEXT    NOT NULL,
                    TextoTitulo  TEXT    NOT NULL DEFAULT '',
                    NombreFuente TEXT    NOT NULL DEFAULT 'Segoe UI',
                    TamanoFuente REAL    NOT NULL DEFAULT 13,
                    Negrita      INTEGER NOT NULL DEFAULT 1,
                    Cursiva      INTEGER NOT NULL DEFAULT 0,
                    ColorTexto   TEXT    NOT NULL DEFAULT '#FFFFFF',
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE,
                    UNIQUE (ProyectoId, Modulo)
                );", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M007 — WrapTexto + AlineacionVertical en todas las tablas Columnas*
        // ═══════════════════════════════════════════════════════════════════════
        private static void M007_WrapTextoAlineacion(DbConnection conn, DbTransaction tx)
        {
            var tablas = new[]
            {
                "ColumnasPersonalizadas", "ColumnasIndirectos", "ColumnasExplosion",
                "ColumnasMaterial", "ColumnasHerramienta", "ColumnasManoObra",
                "ColumnasMaquinaria", "ColumnasMatriz", "ColumnasProgramaObra",
                "ColumnasProgramaInsumos", "ColumnasFinanciamiento",
            };
            foreach (var tabla in tablas)
            {
                AgregarColumna(conn, tabla, "WrapTexto",          "INTEGER NOT NULL DEFAULT 0", tx);
                AgregarColumna(conn, tabla, "AlineacionVertical", "INTEGER NOT NULL DEFAULT 1", tx);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M008 — Columnas adicionales en Maquinaria
        // ═══════════════════════════════════════════════════════════════════════
        private static void M008_MaquinariaColumnas(DbConnection conn, DbTransaction tx)
        {
            AgregarColumna(conn, "Maquinaria", "TipoCombustible", "INTEGER NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "Maquinaria", "NumeroLlantas", "INTEGER NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "Maquinaria", "EsCostoCalculado", "INTEGER NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "Maquinaria", "FechaCalculoCosto", "TEXT NULL", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M009 — ConceptosPresupuesto: columnas que faltan en DBs viejos
        //
        // CRÍTICO: EF Core falla con "no such column" si estas columnas no existen
        // porque las incluye automáticamente en el SELECT al cargar la entidad.
        // Todas las columnas nuevas de ConceptosPresupuesto deben estar aquí.
        // ═══════════════════════════════════════════════════════════════════════
        private static void M009_ConceptosPresupuesto(DbConnection conn, DbTransaction tx)
        {
            if (!TablaExiste(conn, "ConceptosPresupuesto")) return;

            // Columnas que EF Core requiere y pueden faltar en DBs creados antes
            // de que se agregaran a la entidad
            AgregarColumna(conn, "ConceptosPresupuesto", "Nivel",                     "INTEGER NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "ConceptosPresupuesto", "CargosAdicionales",          "REAL NOT NULL DEFAULT 0", tx);
            AgregarColumna(conn, "ConceptosPresupuesto", "ColumnasPersonalizadasJSON", "TEXT NOT NULL DEFAULT '{}'", tx);
            AgregarColumna(conn, "ConceptosPresupuesto", "Notas",                     "TEXT NOT NULL DEFAULT ''", tx);
            AgregarColumna(conn, "ConceptosPresupuesto", "FechaCreacion",              "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP", tx);
            AgregarColumna(conn, "ConceptosPresupuesto", "FechaModificacion",          "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP", tx);

            TryExec(conn, "CREATE INDEX IF NOT EXISTS IX_ConceptosPresupuesto_MatrizId ON ConceptosPresupuesto(MatrizId);", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M010 — ConfigColumnasReporte
        // ═══════════════════════════════════════════════════════════════════════
        private static void M010_ConfigColumnasReporte(DbConnection conn, DbTransaction tx)
        {
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS ConfigColumnasReporte (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProyectoId    INTEGER NOT NULL,
                    TipoReporte   TEXT    NOT NULL,
                    NombreInterno TEXT    NOT NULL,
                    Encabezado    TEXT    NOT NULL DEFAULT '',
                    Visible       INTEGER NOT NULL DEFAULT 1,
                    Orden         INTEGER NOT NULL DEFAULT 0,
                    Ancho         INTEGER NOT NULL DEFAULT 100,
                    EncFuente     TEXT    NOT NULL DEFAULT 'Segoe UI',
                    EncTamaño     REAL    NOT NULL DEFAULT 9,
                    EncNegrita    INTEGER NOT NULL DEFAULT 1,
                    EncAlineacion TEXT    NOT NULL DEFAULT 'Center',
                    EncColorFondo TEXT    NOT NULL DEFAULT '#2C3E50',
                    EncColorTexto TEXT    NOT NULL DEFAULT '#FFFFFF',
                    ConFuente     TEXT    NOT NULL DEFAULT 'Segoe UI',
                    ConTamaño     REAL    NOT NULL DEFAULT 9,
                    ConNegrita    INTEGER NOT NULL DEFAULT 0,
                    ConAlineacion TEXT    NOT NULL DEFAULT 'Left',
                    ConColorFondo TEXT    NOT NULL DEFAULT '#FFFFFF',
                    ConColorTexto TEXT    NOT NULL DEFAULT '#000000',
                    FormatoNumero TEXT    NOT NULL DEFAULT '',
                    FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE CASCADE
                );", tx);
            TryExec(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_ConfigColumnasReporte_ProyectoId_TipoReporte_NombreInterno ON ConfigColumnasReporte(ProyectoId, TipoReporte, NombreInterno);", tx);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M011 — Sanitizar NULL legacy en columnas string NOT NULL
        //
        // Con <Nullable>enable</Nullable> en .NET 8, EF Core trata "string Notas"
        // (sin ?) como NOT NULL y llama GetString() sin verificar null, produciendo
        // "The data is NULL at ordinal N" al leer registros de DBs viejos donde
        // Notas y otros string se insertaron como NULL.
        //
        // Solución: reemplazar NULL por '' en todas las columnas afectadas.
        // UPDATE idempotente: solo toca filas donde la columna es NULL.
        // ═══════════════════════════════════════════════════════════════════════
        private static void M011_SanitizarNullsLegacy(DbConnection conn, DbTransaction tx)
        {
            // Pares (tabla, columna) donde la entidad C# declara string sin ?
            // y el DB viejo puede tener NULL porque el DDL original era TEXT nullable.
            var columnas = new[]
            {
                // Catálogos core
                ("Materiales",        "Notas"),
                ("ManoDeObra",        "Notas"),
                ("Maquinaria",        "Notas"),
                ("Herramientas",      "Notas"),
                // Matrices y componentes
                ("Matrices",          "Notas"),
                ("ComponentesMatriz", "Notas"),
                // Presupuesto
                ("ConceptosPresupuesto", "Notas"),
                ("ConceptosPresupuesto", "ColumnasPersonalizadasJSON"),
                ("ConceptosPresupuesto", "Clave"),
                ("ConceptosPresupuesto", "Unidad"),
                // Programación
                ("ActividadesProgramadas", "Descripcion"),
            };

            foreach (var (tabla, columna) in columnas)
            {
                if (!TablaExiste(conn, tabla)) continue;
                if (!ColumnaExiste(conn, tabla, columna)) continue;

                TryExec(conn, $@"
                    UPDATE [{tabla}]
                    SET [{columna}] = ''
                    WHERE [{columna}] IS NULL", tx);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PASO 2 — Reparación especial de ActividadesProgramadas
        //
        // Corre FUERA de la transacción principal porque SQLite prohíbe
        // PRAGMA foreign_keys=OFF dentro de una transacción activa.
        // Idempotente: solo actúa si detecta ON DELETE RESTRICT en el DDL real.
        // ═══════════════════════════════════════════════════════════════════════
        private static void RepararActividadesProgramadas(DbConnection conn, SOPROContext context)
        {
            if (!TablaExiste(conn, "ActividadesProgramadas")) return;

            // Limpiar el ChangeTracker de EF antes de recrear la tabla.
            // Evita que EF tenga referencias activas a entidades de una tabla
            // que SQLite está a punto de DROP + recrear.
            context.ChangeTracker.Clear();

            string ddlActual;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='ActividadesProgramadas'";
                ddlActual = cmd.ExecuteScalar() as string ?? string.Empty;
            }

            bool tieneRestrict = ddlActual.IndexOf("ActividadPadreId", StringComparison.OrdinalIgnoreCase) >= 0
                               && ddlActual.IndexOf("ON DELETE RESTRICT", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!tieneRestrict) return; // Ya correcta o tabla nueva — no hacer nada

            // PRAGMA foreign_keys=OFF debe ejecutarse FUERA de cualquier transacción.
            // SQLite ignora silenciosamente este PRAGMA si hay una transacción activa,
            // lo que causa que el DROP TABLE posterior falle por FK violations.
            // Secuencia correcta: PRAGMA OFF → tx → operaciones → tx COMMIT → PRAGMA ON.
            System.Diagnostics.Debug.WriteLine("[SchemaManager] RepararActividadesProgramadas: iniciando reparación.");
            using (var pragmaOff = conn.CreateCommand())
            {
                pragmaOff.CommandText = "PRAGMA foreign_keys = OFF;";
                pragmaOff.ExecuteNonQuery();
            }

            try
            {
                using var tx = conn.BeginTransaction();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;

                    cmd.CommandText = "DROP TABLE IF EXISTS ActividadesProgramadas_FIX;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE ActividadesProgramadas_FIX (
                            Id                         INTEGER PRIMARY KEY AUTOINCREMENT,
                            ProgramaObraId             INTEGER NOT NULL,
                            ConceptoPresupuestoId      INTEGER NULL,
                            ActividadPadreId           INTEGER NULL,
                            Clave                      TEXT    NULL,
                            Descripcion                TEXT    NOT NULL,
                            Unidad                     TEXT    NULL,
                            EsResumen                  INTEGER NOT NULL DEFAULT 0,
                            EsHito                     INTEGER NOT NULL DEFAULT 0,
                            EsManual                   INTEGER NOT NULL DEFAULT 0,
                            Nivel                      INTEGER NOT NULL DEFAULT 1,
                            Orden                      INTEGER NOT NULL DEFAULT 0,
                            CantidadTotal              REAL    NOT NULL DEFAULT 0,
                            CantidadProgramada         REAL    NOT NULL DEFAULT 0,
                            AvanceProgramadoPorcentaje REAL    NOT NULL DEFAULT 0,
                            PrecioUnitario             REAL    NOT NULL DEFAULT 0,
                            ImporteTotal               REAL    NOT NULL DEFAULT 0,
                            ImporteProgramado          REAL    NOT NULL DEFAULT 0,
                            FechaInicioTemprana        TEXT    NULL,
                            FechaFinTemprana           TEXT    NULL,
                            FechaInicioTardia          TEXT    NULL,
                            FechaFinTardia             TEXT    NULL,
                            FechaInicioProgramada      TEXT    NULL,
                            FechaFinProgramada         TEXT    NULL,
                            DuracionDiasNaturales      INTEGER NOT NULL DEFAULT 0,
                            DuracionDiasHabiles        INTEGER NOT NULL DEFAULT 0,
                            RendimientoDiario          REAL    NOT NULL DEFAULT 0,
                            FrentesTrabajo             INTEGER NOT NULL DEFAULT 1,
                            TipoRestriccion            INTEGER NOT NULL DEFAULT 1,
                            FechaRestriccion           TEXT    NULL,
                            MetodoDistribucion         INTEGER NOT NULL DEFAULT 1,
                            RutaCritica                INTEGER NOT NULL DEFAULT 0,
                            HolguraDias                INTEGER NOT NULL DEFAULT 0,
                            Notas                      TEXT    NULL,
                            FechaCreacion              TEXT    NOT NULL,
                            FechaModificacion          TEXT    NOT NULL,
                            FOREIGN KEY (ProgramaObraId) REFERENCES ProgramasObra(Id) ON DELETE CASCADE,
                            FOREIGN KEY (ConceptoPresupuestoId) REFERENCES ConceptosPresupuesto(Id) ON DELETE SET NULL,
                            FOREIGN KEY (ActividadPadreId) REFERENCES ActividadesProgramadas_FIX(Id) ON DELETE SET NULL
                        );";
                    cmd.ExecuteNonQuery();

                    // Copia con columnas explícitas — protege contra drift futuro en orden físico
                    cmd.CommandText = @"
                        INSERT INTO ActividadesProgramadas_FIX (
                            Id, ProgramaObraId, ConceptoPresupuestoId, ActividadPadreId,
                            Clave, Descripcion, Unidad,
                            EsResumen, EsHito, EsManual, Nivel, Orden,
                            CantidadTotal, CantidadProgramada, AvanceProgramadoPorcentaje,
                            PrecioUnitario, ImporteTotal, ImporteProgramado,
                            FechaInicioTemprana, FechaFinTemprana, FechaInicioTardia, FechaFinTardia,
                            FechaInicioProgramada, FechaFinProgramada,
                            DuracionDiasNaturales, DuracionDiasHabiles, RendimientoDiario,
                            FrentesTrabajo, TipoRestriccion, FechaRestriccion,
                            MetodoDistribucion, RutaCritica, HolguraDias,
                            Notas, FechaCreacion, FechaModificacion
                        )
                        SELECT
                            Id, ProgramaObraId, ConceptoPresupuestoId, ActividadPadreId,
                            Clave, Descripcion, Unidad,
                            EsResumen, EsHito, EsManual, Nivel, Orden,
                            CantidadTotal, CantidadProgramada, AvanceProgramadoPorcentaje,
                            PrecioUnitario, ImporteTotal, ImporteProgramado,
                            FechaInicioTemprana, FechaFinTemprana, FechaInicioTardia, FechaFinTardia,
                            FechaInicioProgramada, FechaFinProgramada,
                            DuracionDiasNaturales, DuracionDiasHabiles, RendimientoDiario,
                            FrentesTrabajo, TipoRestriccion, FechaRestriccion,
                            MetodoDistribucion, RutaCritica, HolguraDias,
                            Notas, FechaCreacion, FechaModificacion
                        FROM ActividadesProgramadas;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE ActividadesProgramadas;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "ALTER TABLE ActividadesProgramadas_FIX RENAME TO ActividadesProgramadas;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_ActividadesProgramadas_ProgramaObraId_Orden ON ActividadesProgramadas(ProgramaObraId, Orden);";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_ActividadesProgramadas_ProgramaObraId_ConceptoPresupuestoId ON ActividadesProgramadas(ProgramaObraId, ConceptoPresupuestoId);";
                    cmd.ExecuteNonQuery();

                    tx.Commit();
                    System.Diagnostics.Debug.WriteLine("[SchemaManager] RepararActividadesProgramadas: reparación aplicada correctamente.");
                }
                catch (Exception exReparacion)
                {
                    tx.Rollback();
                    // No relanzar: es mejor abrir el proyecto con el bug de FK
                    // que bloquear la apertura por completo.
                    System.Diagnostics.Debug.WriteLine(
                        $"[SchemaManager] RepararActividadesProgramadas falló: {exReparacion.Message}. " +
                        "El proyecto abre con la FK original (RESTRICT). " +
                        "Esto puede causar errores al eliminar actividades padre en Programación de Obra.");
                }
            }
            finally
            {
                // PRAGMA foreign_keys=ON siempre se reactiva, haya o no haya error.
                // Va en finally para garantizar que la conexión queda en estado limpio.
                using var pragmaOn = conn.CreateCommand();
                pragmaOn.CommandText = "PRAGMA foreign_keys = ON;";
                pragmaOn.ExecuteNonQuery();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // M012 — Diseñador WYSIWYG de Encabezado/Pie de Página (PDF)
        //
        // Crea la tabla PlantillasReporteElementos para el diseñador libre.
        // Cada fila es un elemento posicionado (texto, etiqueta dinámica, imagen)
        // con coordenadas en décimas de mm (dmm).
        //
        // Convive con PlantillaReporte existente:
        //   - PlantillaReporte sigue siendo la fuente para reportes Excel.
        //   - PlantillasReporteElementos es la fuente para reportes PDF diseñados.
        //
        // AlturaEncabezadoDmm y AlturaPieDmm se agregan a PlantillasReporte
        // para que el diseñador persista las alturas de franja independientemente.
        // ═══════════════════════════════════════════════════════════════════════
        private static void M012_DisenadorEncabezadoPdf(DbConnection conn, DbTransaction tx)
        {
            // Tabla principal de elementos del diseñador
            Exec(conn, @"
                CREATE TABLE IF NOT EXISTS PlantillasReporteElementos (
                    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlantillaReporteId  INTEGER NOT NULL
                                        REFERENCES PlantillasReporte(Id) ON DELETE CASCADE,
                    Zona                TEXT NOT NULL DEFAULT 'Encabezado',
                    Tipo                TEXT NOT NULL DEFAULT 'TextoLibre',
                    X                   INTEGER NOT NULL DEFAULT 0,
                    Y                   INTEGER NOT NULL DEFAULT 0,
                    Ancho               INTEGER NOT NULL DEFAULT 500,
                    Alto                INTEGER NOT NULL DEFAULT 100,
                    Contenido           TEXT NOT NULL DEFAULT '',
                    Fuente              TEXT NOT NULL DEFAULT 'Segoe UI',
                    TamanoFuente        REAL NOT NULL DEFAULT 10.0,
                    Negrita             INTEGER NOT NULL DEFAULT 0,
                    Cursiva             INTEGER NOT NULL DEFAULT 0,
                    ColorTextoHex       TEXT NOT NULL DEFAULT '#000000',
                    Alineacion          TEXT NOT NULL DEFAULT 'MiddleLeft',
                    ZOrder              INTEGER NOT NULL DEFAULT 0,
                    ImagenBytes         BLOB,
                    ImagenNombreOrigen  TEXT,
                    ImagenRutaOrigen    TEXT,
                    ImagenMimeType      TEXT
                );", tx);

            // Índice para consultas frecuentes por plantilla y zona
            Exec(conn, @"
                CREATE INDEX IF NOT EXISTS IX_PlantillasReporteElementos_PlantillaZona
                ON PlantillasReporteElementos (PlantillaReporteId, Zona);", tx);

            // Columnas de altura de franja en dmm agregadas a PlantillasReporte
            // (independientes de EncabezadoAltura/PiePaginaAltura que son px para Excel)
            AgregarColumna(conn, "PlantillasReporte", "AlturaEncabezadoDmm",
                "INTEGER NOT NULL DEFAULT 400", tx);   // 400 dmm = 40 mm
            AgregarColumna(conn, "PlantillasReporte", "AlturaPieDmm",
                "INTEGER NOT NULL DEFAULT 200", tx);   // 200 dmm = 20 mm
        }
    }
}
