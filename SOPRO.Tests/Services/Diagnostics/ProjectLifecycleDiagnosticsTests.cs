using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Diagnostics;

/// <summary>
/// Issue #138: la apertura y creación de proyectos registran un evento
/// estructurado con correlación cuando fallan, sin persistir rutas, mensajes,
/// SQL, trazas ni datos del proyecto. El sink es best-effort y no altera la
/// excepción original, el candado ni el gating de versiones.
/// </summary>
[TestClass]
public sealed class ProjectLifecycleDiagnosticsTests
{
    private const string ProductVersion = "1.19.2";

    private string _baseDir = null!;
    private string _soproFolder = null!;
    private string _localDataFolder = null!;

    [TestInitialize]
    public void Setup()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "sopro_lifecycle_diag_" + Guid.NewGuid().ToString("N"));
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

    private ProjectWorkspaceService CrearWorkspace() => new(_soproFolder, _localDataFolder);

    private static ProjectLifecycleService CrearServicio(
        ProjectWorkspaceService workspace,
        IDiagnosticsSink sink,
        IProjectDbContextFactory? factory = null)
        => new(workspace, factory ?? new ProjectDbContextFactory(), sink, ProductVersion);

    private static string CrearRutaTemporal(string extension = ".soproproj")
        => Path.Combine(Path.GetTempPath(), $"sopro_lifecycle_diag_{Guid.NewGuid():N}{extension}");

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

    private static void CrearProyectoValido(string dbPath, string nombre)
    {
        using var contexto = TestDbFactory.CreateContextAt(dbPath);
        contexto.Proyectos.Add(CrearProyecto(nombre));
        contexto.SaveChanges();
    }

    private static void Ejecutar(string dbPath, string sql)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AssertSinDatosSensibles(
        DiagnosticsEvent evento,
        IEnumerable<string> prohibidos)
    {
        var textos = new List<string?>
        {
            evento.Source,
            evento.Operation,
            evento.ProductVersion,
            evento.SchemaVersion,
            evento.SchemaStage,
            evento.CorrelationId,
            evento.ExceptionType
        };
        textos.AddRange(evento.InnerExceptionTypes);

        var texto = string.Join('\n', textos.Where(valor => !string.IsNullOrEmpty(valor)));

        Assert.IsFalse(texto.Contains("SELECT ", StringComparison.OrdinalIgnoreCase),
            "el evento no debe contener SQL");
        Assert.IsFalse(texto.Contains("CREATE ", StringComparison.OrdinalIgnoreCase),
            "el evento no debe contener SQL");
        Assert.IsFalse(texto.Contains("   at ", StringComparison.Ordinal),
            "el evento no debe contener stack traces");

        foreach (var prohibido in prohibidos.Where(valor => !string.IsNullOrWhiteSpace(valor)))
        {
            Assert.IsFalse(
                texto.Contains(prohibido, StringComparison.OrdinalIgnoreCase),
                $"el evento no debe contener '{prohibido}': {texto}");
        }
    }

    [TestMethod]
    public void OpenProject_ArchivoAusente_RegistraWarningSinRutaNiCrearArchivo()
    {
        var workspace = CrearWorkspace();
        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);
        var dbPath = CrearRutaTemporal();

        var ex = Assert.ThrowsExactly<FileNotFoundException>(
            () => lifecycle.OpenProject(dbPath));

        Assert.IsTrue(ex.Message.Contains("No se encontró el archivo", StringComparison.Ordinal),
            "la excepción original debe conservarse");
        Assert.AreEqual(1, sink.Count);

        var evento = sink.Snapshot()[0];
        Assert.AreEqual(DiagnosticsSeverity.Warning, evento.Severity);
        Assert.AreEqual(ProjectLifecycleService.OperationOpenProject, evento.Operation);
        Assert.AreEqual(ProjectLifecycleService.DiagnosticsSource, evento.Source);
        Assert.AreEqual(ProductVersion, evento.ProductVersion);
        Assert.AreEqual(typeof(FileNotFoundException).FullName, evento.ExceptionType);
        Assert.IsNull(evento.SchemaVersion);
        Assert.IsNull(evento.SchemaStage);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));

        AssertSinDatosSensibles(evento, new[]
        {
            dbPath,
            Path.GetFileName(dbPath),
            "No se encontró"
        });

        Assert.IsFalse(File.Exists(dbPath), "no debe crearse una base vacía");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "no debe crearse el candado");
    }

    [TestMethod]
    public void OpenProject_RutaVacia_RegistraWarningSinDatosSensibles()
    {
        var workspace = CrearWorkspace();
        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);

        Assert.ThrowsExactly<ArgumentException>(
            () => lifecycle.OpenProject("   "));

        Assert.AreEqual(1, sink.Count);
        var evento = sink.Snapshot()[0];
        Assert.AreEqual(DiagnosticsSeverity.Warning, evento.Severity);
        Assert.AreEqual(ProjectLifecycleService.OperationOpenProject, evento.Operation);
        Assert.AreEqual(typeof(ArgumentException).FullName, evento.ExceptionType);
        Assert.IsNull(evento.SchemaVersion);
        Assert.IsNull(evento.SchemaStage);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));

        AssertSinDatosSensibles(evento, new[] { "Debe indicar la ruta" });
    }

    [TestMethod]
    public void OpenProject_ArchivoNoSopro_RegistraWarningSinRutaNiSql()
    {
        var workspace = CrearWorkspace();
        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);
        var dbPath = CrearRutaTemporal(".db");
        Ejecutar(dbPath, "CREATE TABLE OtraTabla (Id INTEGER PRIMARY KEY, Valor TEXT);");
        Ejecutar(dbPath, "INSERT INTO OtraTabla (Valor) VALUES ('ajeno');");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => lifecycle.OpenProject(dbPath));

        Assert.AreEqual(1, sink.Count);
        var evento = sink.Snapshot()[0];
        Assert.AreEqual(DiagnosticsSeverity.Warning, evento.Severity);
        Assert.AreEqual(ProjectLifecycleService.OperationOpenProject, evento.Operation);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType);
        Assert.IsNull(evento.SchemaVersion);
        Assert.IsNull(evento.SchemaStage);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));

        AssertSinDatosSensibles(evento, new[]
        {
            dbPath,
            Path.GetFileName(dbPath),
            "El archivo no contiene un proyecto válido"
        });

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_FalloDeMigracion_IdentificaEtapaYConservaLaExcepcionOriginal()
    {
        var workspace = CrearWorkspace();
        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);
        var dbPath = CrearRutaTemporal();
        CrearProyectoValido(dbPath, "Proyecto migracion");

        // Una vista con el nombre de la tabla de M010 hace fallar esa etapa sin
        // tocar las tablas core que exige el preflight.
        Ejecutar(dbPath, "DROP TABLE ConfigColumnasReporte;");
        Ejecutar(dbPath, "CREATE VIEW ConfigColumnasReporte AS SELECT 1 AS Id;");

        var ex = Assert.ThrowsExactly<SqliteException>(
            () => lifecycle.OpenProject(dbPath));

        Assert.AreEqual(1, ex.SqliteErrorCode,
            "la excepción original de SQLite debe conservarse sin envolver");
        Assert.AreEqual(1, sink.Count);

        var evento = sink.Snapshot()[0];
        Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
        Assert.AreEqual(ProjectLifecycleService.OperationOpenProject, evento.Operation);
        Assert.AreEqual(typeof(SqliteException).FullName, evento.ExceptionType);
        Assert.IsNotNull(evento.SchemaStage);
        StringAssert.Contains(evento.SchemaStage!, "M010");
        Assert.IsNull(evento.SchemaVersion);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));

        AssertSinDatosSensibles(evento, new[]
        {
            dbPath,
            Path.GetFileName(dbPath),
            "Proyecto migracion",
            ex.Message
        });

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el fallo");
    }

    [TestMethod]
    public void OpenProject_VersionFutura_RegistraWarningYNoRompeElGating()
    {
        var workspace = CrearWorkspace();
        var dbPath = CrearRutaTemporal();
        CrearProyectoValido(dbPath, "Proyecto version futura");

        using (var sesion = new ProjectLifecycleService(
            workspace, new ProjectDbContextFactory()).OpenProject(dbPath))
        {
            Assert.AreEqual("Proyecto version futura", sesion.Project.Nombre);
        }

        Ejecutar(dbPath,
            "DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
        Ejecutar(dbPath,
            "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) "
            + "VALUES ('SchemaVersion_2999.01.1', '2999-01-01T00:00:00.0000000');");

        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => lifecycle.OpenProject(dbPath));

        StringAssert.Contains(ex.Message, "más reciente");
        Assert.AreEqual(1, sink.Count);

        var evento = sink.Snapshot()[0];
        Assert.AreEqual(DiagnosticsSeverity.Warning, evento.Severity);
        Assert.AreEqual(ProjectLifecycleService.OperationOpenProject, evento.Operation);
        Assert.AreEqual("2999.01.1", evento.SchemaVersion);
        Assert.IsNull(evento.SchemaStage);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));

        AssertSinDatosSensibles(evento, new[]
        {
            dbPath,
            Path.GetFileName(dbPath),
            "Proyecto version futura"
        });

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");

        using var verificacion = new ProjectDbContextFactory().CreateReadOnly(dbPath);
        Assert.AreEqual(
            "2999.01.1",
            SchemaManager.GetVersionRegistrada(verificacion),
            "el gating rechazado no debe borrar ni reemplazar la versión futura");
    }

    [TestMethod]
    public void OpenProject_SinkQueLanza_NoOcultaNiReemplazaLaExcepcionOriginal()
    {
        var workspace = CrearWorkspace();
        var sink = new ThrowingDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);
        var dbPath = CrearRutaTemporal(".db");
        Ejecutar(dbPath, "CREATE TABLE OtraTabla (Id INTEGER PRIMARY KEY);");

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => lifecycle.OpenProject(dbPath));

        StringAssert.Contains(ex.Message, "no contiene un proyecto válido");
        Assert.AreEqual(1, sink.Writes, "el sink debe haberse invocado una vez");
        Assert.AreEqual(1, sink.Flushes, "el sink debe vaciarse tras escribir, aunque falle");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse aunque el sink falle");
    }

    [TestMethod]
    public void OpenProject_Exitoso_NoRegistraEventosYConservaElSello()
    {
        var workspace = CrearWorkspace();
        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);
        var dbPath = CrearRutaTemporal();
        CrearProyectoValido(dbPath, "Proyecto exitoso");

        using (var sesion = lifecycle.OpenProject(dbPath))
        {
            Assert.AreEqual("Proyecto exitoso", sesion.Project.Nombre);
        }

        Assert.AreEqual(0, sink.Count, "una apertura correcta no debe registrar eventos");

        using var verificacion = new ProjectDbContextFactory().CreateReadOnly(dbPath);
        Assert.AreEqual(
            SchemaManager.VersionActual,
            SchemaManager.GetVersionRegistrada(verificacion),
            "la apertura exitosa debe conservar el sello de esquema");
    }

    [TestMethod]
    public void CreateProject_NombreExistente_RegistraWarningSinDatosDelProyecto()
    {
        var workspace = CrearWorkspace();
        var lifecycleSinSink = new ProjectLifecycleService(workspace, new ProjectDbContextFactory());
        using (lifecycleSinSink.CreateProject(CrearProyecto("Obra sensible")))
        {
        }

        SqliteConnection.ClearAllPools();
        var dbPath = workspace.BuildProjectDatabasePath("Obra sensible");

        var sink = new InMemoryDiagnosticsSink();
        var lifecycle = CrearServicio(workspace, sink);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => lifecycle.CreateProject(CrearProyecto("Obra sensible")));

        StringAssert.Contains(ex.Message, "Abrir proyecto");
        Assert.AreEqual(1, sink.Count);

        var evento = sink.Snapshot()[0];
        Assert.AreEqual(DiagnosticsSeverity.Warning, evento.Severity);
        Assert.AreEqual(ProjectLifecycleService.OperationCreateProject, evento.Operation);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType);
        Assert.IsNull(evento.SchemaVersion);
        Assert.IsNull(evento.SchemaStage);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));

        AssertSinDatosSensibles(evento, new[]
        {
            dbPath,
            Path.GetFileName(dbPath),
            "Obra sensible",
            "Abrir proyecto"
        });

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el error");
    }

    private sealed class ThrowingDiagnosticsSink : IDiagnosticsSink
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
