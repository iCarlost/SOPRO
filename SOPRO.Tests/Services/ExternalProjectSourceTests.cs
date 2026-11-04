using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System.Data;
using System.Security.Cryptography;

namespace SOPRO.Tests.Services;

/// <summary>
/// Contrato de la fuente externa de solo lectura (paso 1, sin integración con
/// importadores): esquema actual ⇒ contexto read-only sobre el original; esquema
/// legado/sin versión ⇒ copia temporal migrada, origen intacto; esquema futuro ⇒
/// rechazo. La copia temporal se elimina al liberar el arrendamiento.
/// </summary>
[TestClass]
public class ExternalProjectSourceTests
{
    private static readonly IProjectDbContextFactory Factory = new ProjectDbContextFactory();

    [TestMethod]
    public void Open_EsquemaActual_UsaContextoSoloLecturaYNoModificaElOrigen()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto actual");
            var hashAntes = Sha256(dbPath);
            var escrituraAntes = File.GetLastWriteTimeUtc(dbPath);

            using (var lease = ExternalProjectSource.Open(Factory, dbPath))
            {
                Assert.IsFalse(lease.IsSnapshot, "Un esquema actual no debe copiarse.");
                Assert.IsTrue(lease.Context.IsReadOnly, "El contexto debe abrir la base en solo lectura.");
                Assert.AreEqual(Path.GetFullPath(dbPath), lease.OriginalPath);
                Assert.AreEqual(Path.GetFullPath(dbPath), lease.Context.DatabasePath);

                var nombres = lease.Context.Proyectos
                    .AsNoTracking()
                    .Select(p => p.Nombre)
                    .ToList();
                CollectionAssert.AreEqual(new[] { "Proyecto actual" }, nombres);

                Assert.ThrowsExactly<InvalidOperationException>(
                    () => SchemaManager.EnsureCurrentSchema(lease.Context),
                    "EnsureCurrentSchema debe rechazar un contexto de solo lectura.");

                var escribio = true;
                try
                {
                    lease.Context.Database.ExecuteSqlRaw("CREATE TABLE __escritura_prohibida (Id INTEGER);");
                }
                catch (SqliteException)
                {
                    escribio = false;
                }

                Assert.IsFalse(escribio, "La base original no debe aceptar escrituras.");
            }

            Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
            Assert.AreEqual(escrituraAntes, File.GetLastWriteTimeUtc(dbPath), "La fecha del archivo no debe cambiar.");
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_EsquemaLegado_MigraSoloLaCopiaYAbreElOriginalIntacto()
    {
        var dbPath = NuevaRutaTemporal();
        string? rutaCopia = null;
        string? directorioCopia = null;
        try
        {
            CrearBaseActual(dbPath, "Proyecto legado");

            // Simular una base legada: sin versión registrada y sin la tabla de M013.
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS VistasPresupuesto;");
                ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(dbPath);
            var escrituraAntes = File.GetLastWriteTimeUtc(dbPath);

            using (var lease = ExternalProjectSource.Open(Factory, dbPath))
            {
                Assert.IsTrue(lease.IsSnapshot, "Una base legada debe migrarse sobre una copia.");
                rutaCopia = lease.Context.DatabasePath;
                directorioCopia = Path.GetDirectoryName(rutaCopia);
                Assert.AreNotEqual(Path.GetFullPath(dbPath), rutaCopia);
                Assert.IsTrue(File.Exists(rutaCopia), "La copia temporal debe existir mientras el lease vive.");
                Assert.AreEqual(Path.GetFullPath(dbPath), lease.OriginalPath);

                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(lease.Context));
                Assert.AreEqual(1, lease.Context.Proyectos.Count(), "La copia debe conservar los datos.");
                Assert.AreEqual(0, lease.Context.VistasPresupuesto.Count(), "La copia debe tener la tabla de M013.");
            }

            Assert.IsFalse(File.Exists(rutaCopia), "La copia temporal debe eliminarse al liberar el lease.");
            Assert.IsFalse(Directory.Exists(directorioCopia), "El directorio temporal debe eliminarse.");

            Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
            Assert.AreEqual(escrituraAntes, File.GetLastWriteTimeUtc(dbPath), "La fecha del archivo no debe cambiar.");

            using var verificacion = Factory.CreateReadOnly(dbPath);
            Assert.IsNull(SchemaManager.GetVersionRegistrada(verificacion), "El origen no debe quedar migrado.");
            Assert.IsFalse(TablaExiste(verificacion, "VistasPresupuesto"), "El origen no debe recibir la migración.");
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_EsquemaFuturo_SeRechazaSinCopiarNiEscribir()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto futuro");
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ('SchemaVersion_2999.01.1', datetime('now'));");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(dbPath);
            var copiasAntes = ContarDirectoriosDeCopias();

            Assert.ThrowsExactly<InvalidOperationException>(
                () => ExternalProjectSource.Open(Factory, dbPath),
                "Un esquema más reciente que el actual debe rechazarse.");

            Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
            Assert.AreEqual(copiasAntes, ContarDirectoriosDeCopias(), "Un rechazo no debe dejar copias temporales.");
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_VersionNoReconocida_SeRechazaSinCopiarNiEscribir()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto con versión ilegible");
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ('SchemaVersion_legacy-sin-formato', datetime('now'));");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(dbPath);
            var copiasAntes = ContarDirectoriosDeCopias();

            Assert.ThrowsExactly<InvalidOperationException>(
                () => ExternalProjectSource.Open(Factory, dbPath),
                "Una versión registrada ilegible debe fallar cerrado, no migrarse.");

            Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
            Assert.AreEqual(copiasAntes, ContarDirectoriosDeCopias(), "Un rechazo no debe dejar copias temporales.");
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    [DataRow("2026.04.4.0")]
    [DataRow("+2026.04.4")]
    [DataRow(" 2026.04.4")]
    [DataRow("2026.04")]
    [DataRow("2026.4.4")]
    [DataRow("26.04.4")]
    public void Open_VersionConFormatoFueraDeContrato_SeRechazaSinCopiarNiEscribir(string versionRegistrada)
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto con versión fuera de contrato");
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ({0}, datetime('now'));",
                    $"SchemaVersion_{versionRegistrada}");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(dbPath);
            var copiasAntes = ContarDirectoriosDeCopias();

            Assert.ThrowsExactly<InvalidOperationException>(
                () => ExternalProjectSource.Open(Factory, dbPath),
                $"La versión registrada '{versionRegistrada}' no cumple AAAA.MM.revision: debe rechazarse, no migrarse.");

            Assert.AreEqual(hashAntes, Sha256(dbPath), "El archivo original no debe cambiar.");
            Assert.AreEqual(copiasAntes, ContarDirectoriosDeCopias(), "Un rechazo no debe dejar copias temporales.");
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_RutaInexistente_LanzaFileNotFound()
    {
        var dbPath = NuevaRutaTemporal();

        var ex = Assert.ThrowsExactly<FileNotFoundException>(
            () => ExternalProjectSource.Open(Factory, dbPath));

        Assert.AreEqual(Path.GetFullPath(dbPath), ex.FileName);
        Assert.IsFalse(File.Exists(dbPath), "No debe crearse un archivo vacío.");
    }

    [TestMethod]
    public void EnsureCurrentSchema_ContextoSoloLectura_RechazaAntesDeAbrirLaConexion()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto actual");
            var hashAntes = Sha256(dbPath);

            using var context = Factory.CreateReadOnly(dbPath);

            Assert.ThrowsExactly<InvalidOperationException>(() => SchemaManager.EnsureCurrentSchema(context));
            Assert.AreEqual(ConnectionState.Closed, context.Database.GetDbConnection().State,
                "El rechazo ocurre antes de abrir la conexión.");
            Assert.AreEqual(hashAntes, Sha256(dbPath));
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_FalloAlMigrarLaCopia_LimpiaElTemporalYDejaElOrigenIntacto()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto con migración rota");

            // Versión antigua legible + tabla de control sin la columna que usa
            // RegistrarVersion: el backup funciona y la migración de la COPIA falla.
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.ExecuteSqlRaw("DROP TABLE __MigracionesCustom;");
                ctx.Database.ExecuteSqlRaw("CREATE TABLE __MigracionesCustom (Clave TEXT PRIMARY KEY NOT NULL);");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave) VALUES ('SchemaVersion_2020.01.1');");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(dbPath);
            var copiasAntes = ContarDirectoriosDeCopias();

            Exception? fallo = null;
            try
            {
                ExternalProjectSource.Open(Factory, dbPath);
            }
            catch (Exception ex)
            {
                fallo = ex;
            }

            Assert.IsNotNull(fallo, "La migración sobre la copia debía fallar.");
            Assert.IsInstanceOfType<SqliteException>(fallo);
            Assert.AreEqual(copiasAntes, ContarDirectoriosDeCopias(),
                "Un fallo durante la migración no debe dejar copias temporales.");
            Assert.AreEqual(hashAntes, Sha256(dbPath),
                "El origen no debe cambiar aunque la migración de la copia falle.");
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_FuenteEnWal_NoModificaElArchivoNiElLogPendiente()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto en WAL");

            // Escritor vivo: garantiza -wal con contenido sin checkpoint y -shm creado,
            // el escenario en el que una lectura ingenua podría tocar sidecars.
            using var escritor = AbrirEscritorWal(dbPath);
            var walPath = dbPath + "-wal";
            Assert.IsTrue(File.Exists(walPath), "El escenario requiere un -wal con contenido pendiente.");

            var hashDbAntes = Sha256Compartido(dbPath);
            var hashWalAntes = Sha256Compartido(walPath);

            using (var lease = ExternalProjectSource.Open(Factory, dbPath))
            {
                Assert.IsFalse(lease.IsSnapshot, "Un esquema actual no debe copiarse aunque la fuente esté en WAL.");
                Assert.AreEqual(1, lease.Context.Proyectos.Count());

                Assert.AreEqual(hashDbAntes, Sha256Compartido(dbPath),
                    "La conexión de solo lectura no debe tocar el archivo principal.");
                Assert.AreEqual(hashWalAntes, Sha256Compartido(walPath),
                    "La conexión de solo lectura no debe tocar el WAL pendiente.");
            }

            Assert.AreEqual(hashDbAntes, Sha256Compartido(dbPath));
            Assert.AreEqual(hashWalAntes, Sha256Compartido(walPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarBase(dbPath);
        }
    }

    [TestMethod]
    public void Open_ArchivoMarcadoSoloLectura_LeeSinModificarElArchivo()
    {
        var dbPath = NuevaRutaTemporal();
        try
        {
            CrearBaseActual(dbPath, "Proyecto de archivo inmutable");
            var hashAntes = Sha256(dbPath);

            File.SetAttributes(dbPath, FileAttributes.ReadOnly);
            try
            {
                using var lease = ExternalProjectSource.Open(Factory, dbPath);

                Assert.IsFalse(lease.IsSnapshot);
                Assert.AreEqual(1, lease.Context.Proyectos.Count());
                Assert.IsTrue((File.GetAttributes(dbPath) & FileAttributes.ReadOnly) != 0,
                    "El atributo de solo lectura debe conservarse.");
            }
            finally
            {
                File.SetAttributes(dbPath, FileAttributes.Normal);
            }

            Assert.AreEqual(hashAntes, Sha256(dbPath));
        }
        finally
        {
            BorrarBase(dbPath);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string NuevaRutaTemporal()
        => Path.Combine(Path.GetTempPath(), $"sopro_externo_{Guid.NewGuid():N}.db");

    private static void CrearBaseActual(string dbPath, string nombreProyecto)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureCreated();

        ctx.Proyectos.Add(new Proyecto
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
        });
        ctx.SaveChanges();

        SchemaManager.EnsureCurrentSchema(ctx);
        SqliteConnection.ClearAllPools();
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    /// <summary>
    /// Hash con compartición de lectura/escritura: permite medir un archivo que
    /// SQLite mantiene abierto con acceso de escritura (WAL).
    /// </summary>
    private static string Sha256Compartido(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static SqliteConnection AbrirEscritorWal(string dbPath)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();

        Ejecutar(connection, "PRAGMA journal_mode=WAL;");
        Ejecutar(connection, "PRAGMA wal_autocheckpoint=0;");
        Ejecutar(connection, "CREATE TABLE IF NOT EXISTS __wal_probe (Id INTEGER NOT NULL);");
        Ejecutar(connection, "INSERT INTO __wal_probe (Id) VALUES (1);");

        return connection;
    }

    private static void Ejecutar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool TablaExiste(SOPROContext ctx, string tabla)
    {
        var conn = ctx.Database.GetDbConnection();
        var estabaAbierta = conn.State == ConnectionState.Open;
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

    private static void BorrarBase(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", ".lock" })
        {
            var file = dbPath + suffix;
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch (IOException)
            {
                // Best-effort de limpieza del test.
            }
        }
    }
}
