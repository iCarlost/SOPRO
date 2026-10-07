using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Workspace;

/// <summary>
/// Fase 2.1: el descubrimiento del selector de proyectos debe reconocer las tres
/// extensiones admitidas (<c>.soproproj</c>, <c>.db</c> y <c>.sopro</c>) tanto en
/// la carpeta de proyectos como junto al proyecto actual, sin duplicados y con
/// orden estable.
/// </summary>
[TestClass]
public sealed class ProjectIndexServiceDualExtensionTests
{
    private string _baseDir = null!;
    private string _soproFolder = null!;
    private string _localDataFolder = null!;

    [TestInitialize]
    public void Setup()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "sopro_idx_" + Guid.NewGuid().ToString("N"));
        _soproFolder = Path.Combine(_baseDir, "Documentos", "SOPRO");
        _localDataFolder = Path.Combine(_baseDir, "LocalAppData", "SOPRO");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_baseDir))
        {
            Directory.Delete(_baseDir, recursive: true);
        }
    }

    private ProjectWorkspaceService CrearWorkspace() => new(_soproFolder, _localDataFolder);

    [TestMethod]
    public void RefreshKnownProjects_CarpetaMixta_ListaTodasLasExtensiones_SinDuplicadosYOrdenEstable()
    {
        var workspace = CrearWorkspace();
        workspace.EnsureWorkspaceExists();

        CrearArchivo(workspace.ProjectsFolder, "Alfa.soproproj", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        CrearArchivo(workspace.ProjectsFolder, "Beta.db", new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        CrearArchivo(workspace.ProjectsFolder, "Gamma.sopro", new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        CrearArchivo(workspace.ProjectsFolder, "Notas.txt", new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc));

        var index = new ProjectIndexService(workspace);
        index.RefreshKnownProjects(force: true);

        var primera = index.GetRecentProjects(50);
        var segunda = index.GetRecentProjects(50);

        Assert.AreEqual(3, primera.Count, "solo deben listarse las extensiones de proyecto");
        CollectionAssert.AreEqual(
            new[] { "Gamma", "Beta", "Alfa" },
            primera.Select(p => p.Name).ToList(),
            "orden por última modificación descendente");
        Assert.AreEqual(3, primera.Select(p => p.FilePath.ToLowerInvariant()).Distinct().Count(), "sin duplicados");
        CollectionAssert.AreEqual(
            primera.Select(p => p.FilePath).ToList(),
            segunda.Select(p => p.FilePath).ToList(),
            "el orden debe ser estable entre llamadas");
    }

    [TestMethod]
    public void RefreshKnownProjects_CarpetaDelProyectoActual_DescubreHermanosDeTodasLasExtensiones()
    {
        var workspace = CrearWorkspace();
        workspace.EnsureWorkspaceExists();

        var carpetaObra = Path.Combine(_baseDir, "Obra");
        var actual = CrearArchivo(carpetaObra, "Actual.soproproj", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpetaObra, "HermanoDb.db", new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpetaObra, "HermanoSopro.sopro", new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpetaObra, "HermanoCanonico.soproproj", new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpetaObra, "HermanoIgnorado.txt", new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc));

        var index = new ProjectIndexService(workspace);
        index.RefreshKnownProjects(actual, force: true);

        var recientes = index.GetRecentProjects(50);
        var nombres = recientes.Select(p => p.Name).ToList();

        Assert.AreEqual(4, recientes.Count, "el proyecto actual + tres hermanos de proyecto");
        CollectionAssert.Contains(nombres, "Actual");
        CollectionAssert.Contains(nombres, "HermanoDb");
        CollectionAssert.Contains(nombres, "HermanoSopro");
        CollectionAssert.Contains(nombres, "HermanoCanonico");
        CollectionAssert.DoesNotContain(nombres, "HermanoIgnorado");
        Assert.AreEqual(4, recientes.Select(p => p.FilePath.ToLowerInvariant()).Distinct().Count(), "sin duplicados");
    }

    [TestMethod]
    public void EnumerateProjectFiles_Mezcla_DevuelveSoloProyectosOrdenados()
    {
        var carpeta = Path.Combine(_baseDir, "Mixta");
        Directory.CreateDirectory(carpeta);
        CrearArchivo(carpeta, "b.db", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpeta, "a.soproproj", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpeta, "c.sopro", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        CrearArchivo(carpeta, "d.txt", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        var archivos = ProjectWorkspaceService.EnumerateProjectFiles(carpeta)
            .Select(Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new[] { "a.soproproj", "b.db", "c.sopro" }, archivos);
    }

    private static string CrearArchivo(string carpeta, string nombre, DateTime lastWriteUtc)
    {
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, string.Empty);
        File.SetLastWriteTimeUtc(ruta, lastWriteUtc);
        return ruta;
    }
}
