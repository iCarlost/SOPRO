using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Workspace;

/// <summary>
/// Fase 2.1: la creación de proyectos usa la extensión canónica
/// <c>.soproproj</c> y la apertura sigue admitiendo el formato legacy
/// <c>.db</c>. El <em>seam</em> de rutas apunta a una carpeta temporal para no
/// escribir en <c>%USERPROFILE%\Documents\SOPRO</c>.
/// </summary>
[TestClass]
public sealed class ProjectWorkspaceServiceTests
{
    private string _baseDir = null!;
    private string _soproFolder = null!;
    private string _localDataFolder = null!;

    [TestInitialize]
    public void Setup()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "sopro_ws_" + Guid.NewGuid().ToString("N"));
        _soproFolder = Path.Combine(_baseDir, "Documentos", "SOPRO");
        _localDataFolder = Path.Combine(_baseDir, "LocalAppData", "SOPRO");
    }

    [TestCleanup]
    public void Cleanup()
    {
        // El pool de conexiones SQLite puede retener el archivo; se libera antes de borrar.
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

    [TestMethod]
    public void ConstructorPorDefecto_UsaLasCarpetasDeLaConvencion()
    {
        var workspace = new ProjectWorkspaceService();

        Assert.AreEqual(Path.GetFullPath(WorkspacePaths.SoproFolder), workspace.SoproFolder);
        Assert.AreEqual(Path.GetFullPath(WorkspacePaths.LocalDataFolder), workspace.LocalDataFolder);
        Assert.AreEqual(Path.Combine(workspace.SoproFolder, "Proyectos"), workspace.ProjectsFolder);
    }

    [TestMethod]
    public void BuildProjectDatabasePath_GeneraLaExtensionCanonica()
    {
        var workspace = CrearWorkspace();

        var path = workspace.BuildProjectDatabasePath("Obra Central");

        Assert.AreEqual("Obra Central" + ProjectFileExtensions.Canonical, Path.GetFileName(path));
        Assert.IsTrue(path.EndsWith(ProjectFileExtensions.Canonical, StringComparison.Ordinal));
        Assert.AreEqual(
            Path.Combine(workspace.ProjectsFolder, "Obra Central" + ProjectFileExtensions.Canonical),
            path);
    }

    [TestMethod]
    public void CreateProject_CreaElArchivoConExtensionCanonica()
    {
        var workspace = CrearWorkspace();
        var lifecycle = new ProjectLifecycleService(workspace, new ProjectDbContextFactory());

        using var sesion = lifecycle.CreateProject(CrearProyecto("Obra nueva"));

        Assert.IsNotNull(sesion.DatabasePath);
        Assert.IsTrue(
            sesion.DatabasePath!.EndsWith(ProjectFileExtensions.Canonical, StringComparison.Ordinal),
            $"el archivo creado debe usar la extensión canónica, fue: {sesion.DatabasePath}");
        Assert.IsTrue(File.Exists(sesion.DatabasePath));
        Assert.AreEqual(
            Path.Combine(workspace.ProjectsFolder, "Obra nueva" + ProjectFileExtensions.Canonical),
            sesion.DatabasePath);
    }

    [TestMethod]
    public void OpenProject_AbraUnArchivoDbLegado()
    {
        var workspace = CrearWorkspace();
        workspace.EnsureWorkspaceExists();

        var dbPath = Path.Combine(workspace.ProjectsFolder, "Legado.db");
        using (var contexto = TestDbFactory.CreateContextAt(dbPath))
        {
            contexto.Proyectos.Add(CrearProyecto("Legado"));
            contexto.SaveChanges();
        }

        Assert.IsTrue(ProjectFileExtensions.IsSupportedForOpen(dbPath));

        var lifecycle = new ProjectLifecycleService(workspace, new ProjectDbContextFactory());
        using var sesion = lifecycle.OpenProject(dbPath);

        Assert.AreEqual(dbPath, sesion.DatabasePath);
        Assert.AreEqual("Legado", sesion.Project.Nombre);
    }

    [TestMethod]
    public void GetRecentProjects_DescubreCanonicaLegacyYTransicional_SinDuplicadosYOrdenadas()
    {
        var workspace = CrearWorkspace();
        workspace.EnsureWorkspaceExists();

        CrearArchivo(workspace.ProjectsFolder, "Canonica.soproproj", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        CrearArchivo(workspace.ProjectsFolder, "Legado.db", new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        CrearArchivo(workspace.ProjectsFolder, "Transicional.sopro", new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        CrearArchivo(workspace.ProjectsFolder, "Ignorado.txt", new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc));

        var primera = workspace.GetRecentProjects(50);
        var segunda = workspace.GetRecentProjects(50);

        Assert.AreEqual(3, primera.Count);
        CollectionAssert.AreEqual(
            new[] { "Transicional", "Legado", "Canonica" },
            primera.Select(p => p.Name).ToList());
        Assert.AreEqual(3, primera.Select(p => p.FilePath.ToLowerInvariant()).Distinct().Count(), "sin duplicados");
        CollectionAssert.AreEqual(
            primera.Select(p => p.FilePath).ToList(),
            segunda.Select(p => p.FilePath).ToList(),
            "el orden debe ser estable entre llamadas");
    }

    [TestMethod]
    public void CreateProject_ConNombreYaExistente_TrasCerrarSesion_FallaSinAlterarElArchivo()
    {
        var workspace = CrearWorkspace();
        var lifecycle = new ProjectLifecycleService(workspace, new ProjectDbContextFactory());

        using (var sesion = lifecycle.CreateProject(CrearProyecto("Obra repetida")))
        {
            Assert.IsTrue(File.Exists(sesion.DatabasePath));
        }

        var dbPath = workspace.BuildProjectDatabasePath("Obra repetida");
        SqliteConnection.ClearAllPools();
        var contenidoOriginal = File.ReadAllBytes(dbPath);

        var excepcion = Assert.ThrowsExactly<InvalidOperationException>(
            () => lifecycle.CreateProject(CrearProyecto("Obra repetida")));

        StringAssert.Contains(excepcion.Message, "Abrir proyecto");
        SqliteConnection.ClearAllPools();
        CollectionAssert.AreEqual(contenidoOriginal, File.ReadAllBytes(dbPath), "el archivo original no debe cambiar");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el error");

        using (var contexto = new SOPROContext(dbPath))
        {
            Assert.AreEqual(1, contexto.Proyectos.Count(), "no debe añadirse una segunda fila al proyecto existente");
        }

        using var reabierto = lifecycle.OpenProject(dbPath);
        Assert.AreEqual("Obra repetida", reabierto.Project.Nombre);
    }

    [TestMethod]
    public void CreateProject_ConNombreQueColisionaTrasSaneamiento_FallaYConservaElOriginal()
    {
        var workspace = CrearWorkspace();
        var lifecycle = new ProjectLifecycleService(workspace, new ProjectDbContextFactory());

        // '/' es carácter inválido tanto en Windows como en Unix: ambos nombres
        // se sanean al mismo archivo "Obra_Norte.soproproj".
        var rutaSaneada = workspace.BuildProjectDatabasePath("Obra/Norte");
        Assert.AreEqual(rutaSaneada, workspace.BuildProjectDatabasePath("Obra_Norte"));

        using (var sesion = lifecycle.CreateProject(CrearProyecto("Obra/Norte")))
        {
            Assert.IsTrue(File.Exists(sesion.DatabasePath));
        }

        SqliteConnection.ClearAllPools();
        var contenidoOriginal = File.ReadAllBytes(rutaSaneada);

        var excepcion = Assert.ThrowsExactly<InvalidOperationException>(
            () => lifecycle.CreateProject(CrearProyecto("Obra_Norte")));

        StringAssert.Contains(excepcion.Message, "Elige otro nombre");
        SqliteConnection.ClearAllPools();
        CollectionAssert.AreEqual(contenidoOriginal, File.ReadAllBytes(rutaSaneada), "el archivo original no debe cambiar");
        Assert.IsFalse(File.Exists(rutaSaneada + ".lock"), "el candado debe liberarse tras el error");

        using (var contexto = new SOPROContext(rutaSaneada))
        {
            Assert.AreEqual(1, contexto.Proyectos.Count(), "no debe añadirse una segunda fila al proyecto existente");
        }

        using var reabierto = lifecycle.OpenProject(rutaSaneada);
        Assert.AreEqual("Obra/Norte", reabierto.Project.Nombre);
    }

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

    private static string CrearArchivo(string carpeta, string nombre, DateTime lastWriteUtc)
    {
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, string.Empty);
        File.SetLastWriteTimeUtc(ruta, lastWriteUtc);
        return ruta;
    }
}
