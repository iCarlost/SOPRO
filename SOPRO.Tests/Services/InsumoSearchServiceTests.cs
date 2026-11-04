using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Selector;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System.Security.Cryptography;

namespace SOPRO.Tests.Services;

/// <summary>
/// El índice de insumos de proyectos externos se construye con el arrendamiento
/// <see cref="ExternalProjectSource"/>: esquema actual ⇒ lectura del original;
/// esquema legado ⇒ copia temporal migrada. La identidad del índice es la ruta
/// ORIGINAL y el proyecto ajeno queda intacto (bytes, fecha y esquema).
/// </summary>
[TestClass]
public class InsumoSearchServiceTests
{
    private static readonly IProjectDbContextFactory Factory = new ProjectDbContextFactory();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string _root = string.Empty;
    private string _workspaceSopro = string.Empty;
    private string _workspaceLocal = string.Empty;
    private ProjectWorkspaceService _workspace = null!;
    private ProjectIndexService _projectIndexService = null!;
    private InsumoSearchService _service = null!;

    private string InsumoIndexFolder => Path.Combine(_workspaceLocal, "selector-insumo-index");

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_insumos_{Guid.NewGuid():N}");
        _workspaceSopro = Path.Combine(_root, "workspace", "sopro");
        _workspaceLocal = Path.Combine(_root, "workspace", "local");
        Directory.CreateDirectory(_root);

        _workspace = new ProjectWorkspaceService(_workspaceSopro, _workspaceLocal);
        _projectIndexService = new ProjectIndexService(_workspace);
        _service = new InsumoSearchService(
            _projectIndexService,
            new ProjectUsageService(_workspace),
            _workspace,
            Factory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    public void RebuildProjectInsumoIndex_EsquemaActual_IndexaElOriginalSinModificarlo()
    {
        var dbPath = RutaProyecto("actual", ".soproproj");
        CrearBaseActual(dbPath, "Proyecto insumos");
        var hashAntes = Sha256(dbPath);
        var escrituraAntes = File.GetLastWriteTimeUtc(dbPath);
        var copiasAntes = ContarDirectoriosDeCopias();

        _service.RebuildProjectInsumoIndex(dbPath);

        var index = CargarUnicoIndiceInsumo();
        Assert.AreEqual(Path.GetFullPath(dbPath), index.ProjectPath, "La identidad del índice es la ruta original.");
        Assert.AreEqual("Proyecto insumos", index.ProjectName);
        Assert.AreEqual(File.GetLastWriteTimeUtc(dbPath), index.ProjectLastWriteUtc,
            "La fecha de referencia del índice debe ser la del archivo original.");
        Assert.AreEqual(1, index.Insumos.Count(i => i.TipoComponente == TipoComponenteMatriz.Material),
            "Los materiales se indexan como Material.");
        Assert.AreEqual(1, index.Insumos.Count(i =>
                i.TipoComponente == TipoComponenteMatriz.ManoDeObra && i.Tag == "Individual"),
            "La mano de obra se indexa como Individual.");
        Assert.AreEqual(1, index.Insumos.Count(i =>
                i.TipoComponente == TipoComponenteMatriz.Auxiliar && i.Tag == "Cuadrilla"),
            "Las cuadrillas se indexan como Auxiliar con Tag Cuadrilla.");
        Assert.AreEqual(1, index.Insumos.Count(i =>
                i.TipoComponente == TipoComponenteMatriz.Auxiliar && string.IsNullOrEmpty(i.Tag)),
            "Los básicos se indexan como Auxiliar sin tag.");
        Assert.AreEqual(4, index.Insumos.Count, "Contenido del índice sin filtros ni duplicados.");

        Assert.AreEqual(hashAntes, Sha256(dbPath), "El proyecto indexado no debe cambiar.");
        Assert.AreEqual(escrituraAntes, File.GetLastWriteTimeUtc(dbPath), "La fecha del archivo no debe cambiar.");
        Assert.AreEqual(copiasAntes, ContarDirectoriosDeCopias(), "Un esquema actual no debe copiarse.");
    }

    [TestMethod]
    public void RebuildProjectInsumoIndex_EsquemaLegado_MigraSoloLaCopiaYDejaElOrigenIntacto()
    {
        var dbPath = RutaProyecto("legado", ".soprodemo");
        CrearBaseActual(dbPath, "Proyecto legado");
        ConvertirEnLegado(dbPath);

        var hashAntes = Sha256(dbPath);
        var escrituraAntes = File.GetLastWriteTimeUtc(dbPath);
        var copiasAntes = ContarDirectoriosDeCopias();

        _service.RebuildProjectInsumoIndex(dbPath);

        var index = CargarUnicoIndiceInsumo();
        Assert.AreEqual(Path.GetFullPath(dbPath), index.ProjectPath,
            "El índice NUNCA debe apuntar a la copia temporal.");
        Assert.AreEqual("Proyecto legado", index.ProjectName);
        Assert.AreEqual(File.GetLastWriteTimeUtc(dbPath), index.ProjectLastWriteUtc);
        Assert.AreEqual(4, index.Insumos.Count, "La copia migrada conserva los insumos del proyecto.");

        Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
        Assert.AreEqual(escrituraAntes, File.GetLastWriteTimeUtc(dbPath), "La fecha del archivo no debe cambiar.");
        Assert.AreEqual(copiasAntes, ContarDirectoriosDeCopias(), "La copia temporal debe eliminarse al liberar el lease.");

        using var original = Factory.CreateReadOnly(dbPath);
        Assert.IsNull(SchemaManager.GetVersionRegistrada(original),
            "El origen no debe quedar migrado: no se llama EnsureCurrentSchema sobre la fuente.");
        Assert.IsFalse(TablaExiste(original, "VistasPresupuesto"),
            "El origen no debe recibir la migración del esquema.");
    }

    [TestMethod]
    public void RebuildProjectInsumoIndex_EsquemaFuturo_FallaCerradoSinEscribirNiIndexar()
    {
        var dbPath = RutaProyecto("futuro", ".soproproj");
        CrearBaseActual(dbPath, "Proyecto futuro");
        ConvertirEnFuturo(dbPath);

        var hashAntes = Sha256(dbPath);
        var escrituraAntes = File.GetLastWriteTimeUtc(dbPath);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _service.RebuildProjectInsumoIndex(dbPath));

        Assert.IsFalse(DirectorioTieneIndices(), "Un esquema no soportado no debe producir índice.");
        Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
        Assert.AreEqual(escrituraAntes, File.GetLastWriteTimeUtc(dbPath), "La fecha del archivo no debe cambiar.");
    }

    [TestMethod]
    public void SearchInsumos_ProyectoLegado_DevuelveResultadosConLaRutaOriginal()
    {
        var currentPath = RutaProyecto("actual", ".soproproj");
        CrearBaseActual(currentPath, "Proyecto actual");

        var externalPath = RutaProyecto("externo", ".soprodemo");
        CrearBaseActual(externalPath, "Proyecto externo legado");
        ConvertirEnLegado(externalPath);
        var hashExternoAntes = Sha256(externalPath);

        using var current = Factory.Create(currentPath);

        var results = _service.SearchInsumos(
            current,
            proyectoId: 1,
            tipoComponente: TipoComponenteMatriz.Material,
            query: "cemento",
            includeCurrentProject: false,
            includeRecentProjects: false,
            includeFavoriteProjects: false,
            maxResults: 10,
            specificProjectPaths: new[] { externalPath });

        Assert.AreEqual(1, results.Count, "La búsqueda debe encontrar el material del proyecto legado.");
        var result = results[0];
        Assert.AreEqual(Path.GetFullPath(externalPath), result.RutaProyecto,
            "El resultado debe exponer la ruta original, no la copia temporal.");
        Assert.AreEqual("Proyecto externo legado", result.NombreProyecto);
        Assert.AreEqual("MAT-01", result.Clave);
        Assert.IsFalse(result.EsActual);

        Assert.AreEqual(hashExternoAntes, Sha256(externalPath), "La búsqueda no debe modificar el proyecto externo.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string RutaProyecto(string carpeta, string extension)
    {
        var directory = Path.Combine(_root, carpeta);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"proyecto{extension}");
    }

    private static void CrearBaseActual(string dbPath, string nombreProyecto)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureCreated();

        var proyecto = new Proyecto
        {
            Nombre = nombreProyecto,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        ctx.Materiales.Add(new Material
        {
            Clave = "MAT-01",
            Descripcion = "Cemento gris 50 kg",
            Unidad = "saco",
            PrecioUnitario = 210m,
            Notas = string.Empty,
            ProyectoId = proyecto.Id
        });
        ctx.ManoDeObra.Add(new ManoDeObra
        {
            Clave = "MO-01",
            Descripcion = "Peón",
            Unidad = "jor",
            SalarioBase = 300m,
            SalarioReal = 496.29m,
            Notas = string.Empty,
            ProyectoId = proyecto.Id
        });
        ctx.Matrices.Add(new Matriz
        {
            Clave = "M-002",
            Descripcion = "Concreto f'c 250",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 1500m,
            ProyectoId = proyecto.Id
        });
        ctx.Matrices.Add(new Matriz
        {
            Clave = "CU-01",
            Descripcion = "Cuadrilla de albañilería",
            Unidad = "jor",
            Tipo = TipoMatriz.Cuadrilla,
            CostoDirecto = 900m,
            ProyectoId = proyecto.Id
        });
        ctx.SaveChanges();

        SchemaManager.EnsureCurrentSchema(ctx);
        SqliteConnection.ClearAllPools();
    }

    private ProjectInsumoIndexFile CargarUnicoIndiceInsumo()
    {
        Assert.IsTrue(Directory.Exists(InsumoIndexFolder), "Debe existir la carpeta del índice de insumos.");
        var files = Directory.GetFiles(InsumoIndexFolder, "*.json", SearchOption.TopDirectoryOnly);
        Assert.AreEqual(1, files.Length, "Debe generarse un único índice para el proyecto indexado.");
        var index = JsonSerializer.Deserialize<ProjectInsumoIndexFile>(File.ReadAllText(files[0]), JsonOptions);
        Assert.IsNotNull(index);
        return index!;
    }

    private bool DirectorioTieneIndices()
        => Directory.Exists(InsumoIndexFolder)
           && Directory.GetFiles(InsumoIndexFolder, "*.json", SearchOption.TopDirectoryOnly).Length > 0;

    private static void ConvertirEnLegado(string dbPath)
    {
        using (var ctx = new SOPROContext(dbPath))
        {
            ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS VistasPresupuesto;");
            ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
        }

        SqliteConnection.ClearAllPools();
    }

    private static void ConvertirEnFuturo(string dbPath)
    {
        using (var ctx = new SOPROContext(dbPath))
        {
            ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
            ctx.Database.ExecuteSqlRaw(
                "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ('SchemaVersion_2999.01.1', datetime('now'));");
        }

        SqliteConnection.ClearAllPools();
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static bool TablaExiste(SOPROContext ctx, string tabla)
    {
        var conn = ctx.Database.GetDbConnection();
        var estabaAbierta = conn.State == System.Data.ConnectionState.Open;
        if (!estabaAbierta) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
            var p = cmd.CreateParameter();
            p.ParameterName = "$n";
            p.Value = tabla;
            cmd.Parameters.Add(p);
            return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (!estabaAbierta) conn.Close();
        }
    }

    private static int ContarDirectoriosDeCopias()
    {
        var root = Path.Combine(Path.GetTempPath(), "SOPRO", "external-snapshots");
        return Directory.Exists(root) ? Directory.GetDirectories(root).Length : 0;
    }
}
