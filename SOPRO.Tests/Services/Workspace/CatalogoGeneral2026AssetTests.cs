using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Tests.Services.Workspace;

[TestClass]
public sealed class CatalogoGeneral2026AssetTests
{
    private const string NombreAsset = "CATALOGO GENERAL 2026.soproproj";
    private const string NombreProyecto = "CATALOGO GENERAL 2026";
    private const long TamanoMaximoBytes = 2L * 1024 * 1024;

    private static readonly byte[] CabeceraSqlite = Encoding.ASCII.GetBytes("SQLite format 3\0");
    private static readonly string[] SufijosColaterales = { "-wal", "-shm", ".lock" };

    private string _baseDir = null!;
    private string _soproFolder = null!;
    private string _localDataFolder = null!;

    private static string AssetPath => Path.Combine(AppContext.BaseDirectory, "TestData", NombreAsset);

    [TestInitialize]
    public void Setup()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "sopro_catalogo_2026_" + Guid.NewGuid().ToString("N"));
        _soproFolder = Path.Combine(_baseDir, "Documentos", "SOPRO");
        _localDataFolder = Path.Combine(_baseDir, "LocalAppData", "SOPRO");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();

        for (int intento = 0; intento < 5 && Directory.Exists(_baseDir); intento++)
        {
            try
            {
                Directory.Delete(_baseDir, recursive: true);
            }
            catch (IOException) when (intento < 4)
            {
                Thread.Sleep(200);
                SqliteConnection.ClearAllPools();
            }
        }
    }

    [TestMethod]
    public void AssetEnSalida_ExisteConCabeceraSqliteYTamanoValido()
    {
        Assert.IsTrue(File.Exists(AssetPath), $"No se encontró el asset en la salida de pruebas: {AssetPath}");

        FileInfo info = new(AssetPath);
        Assert.IsTrue(info.Length > 0, "El asset no debe estar vacío.");
        Assert.IsTrue(info.Length <= TamanoMaximoBytes,
            $"El asset supera el límite de 2 MiB: {info.Length} bytes.");

        byte[] cabecera = new byte[CabeceraSqlite.Length];
        using (FileStream stream = File.OpenRead(AssetPath))
        {
            int leidos = stream.Read(cabecera, 0, cabecera.Length);
            Assert.AreEqual(CabeceraSqlite.Length, leidos, "El archivo es más corto que la cabecera SQLite.");
        }

        CollectionAssert.AreEqual(CabeceraSqlite, cabecera,
            "La cabecera debe ser exactamente 'SQLite format 3\\0'.");
    }

    [TestMethod]
    public void AssetEnSalidaYSusFuentes_NoTienenArchivosColateralesSqliteNiCandado()
    {
        AssertSinColaterales(AssetPath);

        string? assetFuente = BuscarAssetEnFuente();
        if (assetFuente is not null)
        {
            AssertSinColaterales(assetFuente);
        }
    }

    [TestMethod]
    public void AssetEnSalida_IntegridadSqliteOk_SinCrearColaterales()
    {
        using (SOPROContext context = AbrirAssetSoloLectura())
        {
            Assert.AreEqual("ok", LeerIntegridad(context), "PRAGMA integrity_check debe devolver 'ok'.");
        }

        AssertSinColaterales(AssetPath);
    }

    [TestMethod]
    public void AssetEnSalida_ContieneExactamenteUnProyectoConElNombreEsperado()
    {
        using SOPROContext context = AbrirAssetSoloLectura();

        List<Proyecto> proyectos = context.Proyectos.ToList();

        Assert.AreEqual(1, proyectos.Count, "El catálogo debe contener exactamente un proyecto.");
        Assert.AreEqual(NombreProyecto, proyectos[0].Nombre);
    }

    [TestMethod]
    public void AssetEnSalida_VersionDeEsquemaRegistradaEsLaActual()
    {
        using SOPROContext context = AbrirAssetSoloLectura();

        string? registrada = SchemaManager.GetVersionRegistrada(context);

        Assert.IsNotNull(registrada, "El asset debe tener una versión de esquema registrada.");
        Assert.IsTrue(VersionEsquemaCoincide(registrada, SchemaManager.VersionActual),
            $"La versión registrada '{registrada}' debe coincidir con SchemaManager.VersionActual '{SchemaManager.VersionActual}'.");
    }

    [TestMethod]
    public void AssetEnSalida_CatalogosClaveTienenDatos()
    {
        using SOPROContext context = AbrirAssetSoloLectura();

        Assert.IsTrue(context.Materiales.Any(), "Materiales no debe estar vacío.");
        Assert.IsTrue(context.ManoDeObra.Any(), "ManoDeObra no debe estar vacío.");
        Assert.IsTrue(context.Maquinaria.Any(), "Maquinaria no debe estar vacío.");
        Assert.IsTrue(context.Herramientas.Any(), "Herramientas no debe estar vacío.");
        Assert.IsTrue(context.Matrices.Any(), "Matrices no debe estar vacío.");
    }

    [TestMethod]
    public void CopiaTemporal_AbreConElCicloDeVidaReal_YNoModificaElAsset()
    {
        string? assetFuente = BuscarAssetEnFuente();
        string? hashAntes = assetFuente is null ? null : CalcularSha256(assetFuente);

        Directory.CreateDirectory(_soproFolder);
        Directory.CreateDirectory(_localDataFolder);

        string copiaPath = Path.Combine(_soproFolder, NombreAsset);
        File.Copy(AssetPath, copiaPath, overwrite: true);
        File.SetAttributes(copiaPath, FileAttributes.Normal);

        AssertSinColaterales(copiaPath);

        ProjectWorkspaceService workspace = new(_soproFolder, _localDataFolder);
        ProjectLifecycleService lifecycle = new(workspace, new ProjectDbContextFactory());

        using (ProjectSession sesion = lifecycle.OpenProject(copiaPath))
        {
            Assert.AreEqual(copiaPath, sesion.DatabasePath);
            Assert.AreEqual(NombreProyecto, sesion.Project.Nombre);
            Assert.AreEqual(1, sesion.Context.Proyectos.Count());
            Assert.IsTrue(sesion.Context.Materiales.Any(), "La sesión debe permitir consultar los catálogos.");
            Assert.IsTrue(VersionEsquemaCoincide(
                SchemaManager.GetVersionRegistrada(sesion.Context),
                SchemaManager.VersionActual));
        }

        Assert.IsFalse(File.Exists(copiaPath + ".lock"), "El candado debe liberarse al cerrar la sesión.");
        AssertSinColaterales(copiaPath);

        if (assetFuente is not null)
        {
            Assert.AreEqual(hashAntes, CalcularSha256(assetFuente),
                "El asset fuente no debe modificarse al abrir una copia temporal.");
        }
    }

    private static SOPROContext AbrirAssetSoloLectura()
        => new ProjectDbContextFactory().CreateReadOnly(AssetPath);

    private static string LeerIntegridad(SOPROContext context)
    {
        DbConnection connection = context.Database.GetDbConnection();
        bool abrir = connection.State != ConnectionState.Open;
        if (abrir)
        {
            connection.Open();
        }

        try
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return (string?)command.ExecuteScalar() ?? string.Empty;
        }
        finally
        {
            if (abrir)
            {
                connection.Close();
            }
        }
    }

    private static void AssertSinColaterales(string dbPath)
    {
        foreach (string sufijo in SufijosColaterales)
        {
            string colateral = dbPath + sufijo;
            Assert.IsFalse(File.Exists(colateral), $"No debe existir el archivo colateral: {colateral}");
        }
    }

    private static string? BuscarAssetEnFuente()
    {
        for (DirectoryInfo? actual = new(AppContext.BaseDirectory); actual is not null; actual = actual.Parent)
        {
            string candidato = Path.Combine(actual.FullName, "installer", "assets", "proyectos", NombreAsset);
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        return null;
    }

    private static string CalcularSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool VersionEsquemaCoincide(string? registrada, string esperada)
    {
        if (string.IsNullOrWhiteSpace(registrada))
        {
            return false;
        }

        string valor = registrada.Trim();
        const string prefijo = "SchemaVersion_";
        if (valor.StartsWith(prefijo, StringComparison.Ordinal))
        {
            valor = valor[prefijo.Length..];
        }

        string? normalizada = NormalizarVersion(valor);
        string? esperadaNormalizada = NormalizarVersion(esperada);
        return normalizada is not null && normalizada == esperadaNormalizada;
    }

    private static string? NormalizarVersion(string valor)
    {
        string[] partes = valor.Split('.', StringSplitOptions.TrimEntries);
        if (partes.Length != 3)
        {
            return null;
        }

        int[] numeros = new int[partes.Length];
        for (int i = 0; i < partes.Length; i++)
        {
            if (!int.TryParse(partes[i], NumberStyles.None, CultureInfo.InvariantCulture, out numeros[i]))
            {
                return null;
            }
        }

        return string.Join('.', numeros);
    }
}
