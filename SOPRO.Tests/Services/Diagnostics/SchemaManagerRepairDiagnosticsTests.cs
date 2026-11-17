using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Tests.Services.Diagnostics;

/// <summary>
/// Issue #142: la reparación de ActividadesProgramadas registra dos eventos Info
/// fijos (inicio y confirmación) solo cuando detecta ON DELETE RESTRICT y la
/// ejecuta. Comparten correlación, versión de producto y etapa de esquema con la
/// operación de apertura. El sink es best-effort: sus fallos no alteran la
/// migración y una reparación fallida nunca deja confirmación.
/// </summary>
[TestClass]
public sealed class SchemaManagerRepairDiagnosticsTests
{
    private const string ProductVersion = "1.19.2";
    private const string CorrelationId = "ABCD2345";

    private string _baseDir = null!;
    private string _soproFolder = null!;
    private string _localDataFolder = null!;

    [TestInitialize]
    public void Setup()
    {
        _baseDir = Path.Combine(
            Path.GetTempPath(), "sopro_schema_diag_" + Guid.NewGuid().ToString("N"));
        _soproFolder = Path.Combine(_baseDir, "Documentos", "SOPRO");
        _localDataFolder = Path.Combine(_baseDir, "LocalAppData", "SOPRO");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();

        for (var attempt = 0; attempt < 5 && Directory.Exists(_baseDir); attempt++)
        {
            try
            {
                Directory.Delete(_baseDir, recursive: true);
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(200);
                SqliteConnection.ClearAllPools();
            }
        }
    }

    [TestMethod]
    public void EnsureCurrentSchema_ReparacionEjecutada_EmiteInfoDeInicioYConfirmacionConMismaCorrelacion()
    {
        var dbPath = CrearRutaTemporal();
        try
        {
            using (var contexto = new SOPROContext(dbPath))
            {
                contexto.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(contexto);
            }

            ConvertirActividadesALegacyRestrict(dbPath);

            var sink = new InMemoryDiagnosticsSink();
            using var ctx = new SOPROContext(dbPath);
            SchemaManager.EnsureCurrentSchema(
                ctx, null, sink, ProductVersion, CorrelationId);

            var eventos = sink.Snapshot();
            Assert.AreEqual(2, eventos.Count,
                "una reparación ejecutada debe dejar exactamente inicio y confirmación.");

            var inicio = eventos[0];
            var confirmado = eventos[1];

            Assert.AreEqual(SchemaManager.OperationReparacionIniciada, inicio.Operation);
            Assert.AreEqual(SchemaManager.OperationReparacionConfirmada, confirmado.Operation);

            foreach (var evento in eventos)
            {
                Assert.AreEqual(DiagnosticsSeverity.Info, evento.Severity);
                Assert.AreEqual(SchemaManager.DiagnosticsSource, evento.Source);
                Assert.AreEqual(ProductVersion, evento.ProductVersion);
                Assert.AreEqual(SchemaManager.VersionActual, evento.SchemaVersion);
                Assert.AreEqual(SchemaManager.SchemaStageReparacionActividades, evento.SchemaStage);
                Assert.AreEqual(CorrelationId, evento.CorrelationId);
                Assert.IsNull(evento.ExceptionType);
                Assert.IsNull(evento.HResult);
                Assert.AreEqual(0, evento.InnerExceptionTypes.Count);
            }

            Assert.AreEqual(inicio.CorrelationId, confirmado.CorrelationId,
                "inicio y confirmación deben compartir la correlación de la operación.");

            Assert.IsFalse(LeerDdlActividades(dbPath).Contains(
                    "ON DELETE RESTRICT", StringComparison.OrdinalIgnoreCase),
                "la reparación debe haberse aplicado.");
            Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void EnsureCurrentSchema_SinReparacion_NoEmiteEventosDeReparacion()
    {
        var dbPath = CrearRutaTemporal();
        try
        {
            var sink = new InMemoryDiagnosticsSink();
            using var ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureCreated();

            SchemaManager.EnsureCurrentSchema(ctx, null, sink, ProductVersion, CorrelationId);

            Assert.AreEqual(0, sink.Count,
                "un esquema ya correcto no debe registrar inicio ni confirmación.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void EnsureCurrentSchema_SinkQueLanza_NoAlteraLaMigracion()
    {
        var dbPath = CrearRutaTemporal();
        try
        {
            using (var contexto = new SOPROContext(dbPath))
            {
                contexto.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(contexto);
            }

            ConvertirActividadesALegacyRestrict(dbPath);

            var sink = new SinkQueLanza();
            using var ctx = new SOPROContext(dbPath);

            SchemaManager.EnsureCurrentSchema(ctx, null, sink, ProductVersion, CorrelationId);

            Assert.AreEqual(2, sink.Writes, "el sink debe invocarse aunque falle.");
            Assert.AreEqual(2, sink.Flushes, "cada escritura se vacía por separado, aunque falle.");
            Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx),
                "el fallo del sink no debe impedir el sello de versión.");
            Assert.IsFalse(LeerDdlActividades(dbPath).Contains(
                    "ON DELETE RESTRICT", StringComparison.OrdinalIgnoreCase),
                "el fallo del sink no debe revertir la reparación.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void EnsureCurrentSchema_FalloDeReparacion_NoEmiteConfirmacion()
    {
        var dbPath = CrearRutaTemporal();
        try
        {
            using (var contexto = new SOPROContext(dbPath))
            {
                contexto.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(contexto);
            }

            // Tabla legacy con RESTRICT y sin columnas que la reparación copia:
            // el INSERT falla y la reparación se revierte.
            Ejecutar(dbPath, @"
                PRAGMA foreign_keys = OFF;
                DROP TABLE ActividadesProgramadas;
                CREATE TABLE ActividadesProgramadas (
                    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProgramaObraId        INTEGER NOT NULL,
                    ConceptoPresupuestoId INTEGER NULL,
                    ActividadPadreId      INTEGER NULL,
                    Descripcion           TEXT NOT NULL,
                    Orden                 INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (ActividadPadreId) REFERENCES ActividadesProgramadas(Id) ON DELETE RESTRICT
                );
                DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';
                PRAGMA foreign_keys = ON;");

            var sink = new InMemoryDiagnosticsSink();
            using var ctx = new SOPROContext(dbPath);

            var ex = Assert.ThrowsExactly<SqliteException>(
                () => SchemaManager.EnsureCurrentSchema(
                    ctx, null, sink, ProductVersion, CorrelationId));

            StringAssert.Contains(ex.Message, "no such column");

            var eventos = sink.Snapshot();
            Assert.AreEqual(1, eventos.Count,
                "una reparación fallida solo puede dejar el evento de inicio.");
            Assert.AreEqual(SchemaManager.OperationReparacionIniciada, eventos[0].Operation);
            Assert.IsFalse(
                eventos.Any(e => e.Operation == SchemaManager.OperationReparacionConfirmada),
                "no debe emitirse confirmación si la reparación falla.");
            Assert.IsNull(SchemaManager.GetVersionRegistrada(ctx),
                "una reparación fallida no debe sellar la versión.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void OpenProject_ReparacionEjecutada_HeredaVersionYCorrelacionDeLaApertura()
    {
        var workspace = new ProjectWorkspaceService(_soproFolder, _localDataFolder);
        using (var creacion = new ProjectLifecycleService(workspace, new ProjectDbContextFactory())
            .CreateProject(CrearProyecto("ProyectoReparacion")))
        {
        }

        SqliteConnection.ClearAllPools();
        var dbPath = workspace.BuildProjectDatabasePath("ProyectoReparacion");
        ConvertirActividadesALegacyRestrict(dbPath);

        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = new ProjectLifecycleService(
            workspace, new ProjectDbContextFactory(), sink, ProductVersion);

        using var sesion = lifecycle.OpenProject(dbPath);
        Assert.AreEqual("ProyectoReparacion", sesion.Project.Nombre);

        var eventos = sink.Snapshot()
            .Where(e => e.Operation == SchemaManager.OperationReparacionIniciada
                || e.Operation == SchemaManager.OperationReparacionConfirmada)
            .ToList();

        Assert.AreEqual(2, eventos.Count,
            "la apertura debe propagar el sink a la reparación.");
        Assert.AreEqual(SchemaManager.OperationReparacionIniciada, eventos[0].Operation);
        Assert.AreEqual(SchemaManager.OperationReparacionConfirmada, eventos[1].Operation);
        Assert.AreEqual(ProductVersion, eventos[0].ProductVersion);
        Assert.AreEqual(SchemaManager.VersionActual, eventos[0].SchemaVersion);
        Assert.AreEqual(SchemaManager.SchemaStageReparacionActividades, eventos[0].SchemaStage);
        Assert.AreEqual(eventos[0].CorrelationId, eventos[1].CorrelationId,
            "ambos eventos deben quedar en el mismo contexto de operación.");
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(eventos[0].CorrelationId));
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static string CrearRutaTemporal()
        => Path.Combine(Path.GetTempPath(), $"sopro_schema_diag_{Guid.NewGuid():N}.db");

    private static Proyecto CrearProyecto(string nombre) => new()
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31
    };

    private static void Ejecutar(string dbPath, string sql)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string LeerDdlActividades(string dbPath)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='ActividadesProgramadas'";
        return command.ExecuteScalar() as string ?? string.Empty;
    }

    /// <summary>
    /// Recrea ActividadesProgramadas conservando todas sus columnas pero con la FK
    /// de ActividadPadreId en ON DELETE RESTRICT, que es lo que dispara la reparación.
    /// </summary>
    private static void ConvertirActividadesALegacyRestrict(string dbPath)
    {
        var lineas = LeerDdlActividades(dbPath).Split('\n');
        var reemplazado = false;

        for (var i = 0; i < lineas.Length; i++)
        {
            if (lineas[i].Contains("ActividadPadreId", StringComparison.OrdinalIgnoreCase)
                && lineas[i].Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                && lineas[i].Contains("ON DELETE SET NULL", StringComparison.OrdinalIgnoreCase))
            {
                lineas[i] = lineas[i].Replace(
                    "ON DELETE SET NULL", "ON DELETE RESTRICT",
                    StringComparison.OrdinalIgnoreCase);
                reemplazado = true;
            }
        }

        Assert.IsTrue(reemplazado,
            "la fixture no encontró la FK de ActividadPadreId con ON DELETE SET NULL.");

        var ddlLegacy = string.Join('\n', lineas);
        Ejecutar(dbPath, $@"
            PRAGMA foreign_keys = OFF;
            DROP TABLE ActividadesProgramadas;
            {ddlLegacy};
            PRAGMA foreign_keys = ON;");
    }

    private sealed class SinkQueLanza : IDiagnosticsSink
    {
        public int Writes { get; private set; }

        public int Flushes { get; private set; }

        public void Write(DiagnosticsEvent diagnosticsEvent)
        {
            Writes++;
            throw new InvalidOperationException("sink inyectado");
        }

        public void Flush()
        {
            Flushes++;
            throw new IOException("flush inyectado");
        }
    }
}
