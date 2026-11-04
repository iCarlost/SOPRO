using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Importacion;

/// <summary>
/// La fuente externa es de solo lectura para los importadores: listar,
/// previsualizar e importar (insumo directo, herramienta y árbol de matrices) no
/// realiza escrituras de la aplicación ni cambios de esquema/versión sobre el
/// proyecto externo; tampoco lo migra. Un esquema legado fluye por la copia
/// temporal y solo se migra la copia. Las filas destino conservan el proyecto
/// actual, el origen <see cref="OrigenInsumo.Proyecto"/>, sin vínculo maestro y
/// con el sello de procedencia.
///
/// Sobre los sidecars: SQLite puede crear operacionalmente <c>-wal</c>/<c>-shm</c>
/// vacíos junto a una fuente en modo WAL al abrirla en solo lectura, aunque el
/// contenido (`.db`, esquema y WAL pendiente) no cambie; los servicios no borran
/// sidecars porque un escritor vivo puede ser su dueño. Por eso las aserciones
/// deterministas de "sin sidecars" se limitan a la fuente en modo DELETE que usa
/// el fixture y al caso de fuente inexistente. El escenario WAL real se cubre
/// comparando los bytes del `.db` y del `-wal` pendiente.
/// </summary>
[TestClass]
public class ExternalImportSourceImmutabilityTests
{
    private static readonly IProjectDbContextFactory Factory = new ProjectDbContextFactory();

    [TestMethod]
    public void ListarInsumosYMatrices_NoModificaLaFuenteNiDejaSidecars()
    {
        var dbPath = NuevaRutaFuente();
        try
        {
            CrearFuenteActual(dbPath, "Origen lista");
            var antes = HuellaFuente.Capturar(dbPath);

            var insumos = new ExternalInsumoImportService().LoadExternalInsumos(dbPath, TipoComponenteMatriz.Material, null);
            var herramientas = new ExternalInsumoImportService().LoadExternalInsumos(dbPath, TipoComponenteMatriz.Herramienta, null);
            var matrices = new ExternalMatrixImportService().LoadExternalMatrices(dbPath);

            Assert.AreEqual(1, insumos.Items.Count);
            Assert.AreEqual(1, herramientas.Items.Count, "La ruta de herramientas (antes SQL crudo) debe leer la fuente.");
            Assert.AreEqual(2, matrices.Matrices.Count);
            Assert.AreEqual(Path.GetFullPath(dbPath), insumos.ProjectPath);
            Assert.AreEqual(Path.GetFullPath(dbPath), matrices.ProjectPath);

            antes.AssertSinCambios(dbPath);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportarInsumoDirectoYHerramienta_NoModificaLaFuenteYDestinoConservaProyectoYProcedencia()
    {
        var dbPath = NuevaRutaFuente();
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto(current, "Destino insumos");
        int materialId, herramientaId;
        try
        {
            (materialId, herramientaId, _, _) = CrearFuenteActual(dbPath, "Origen insumos");
            var antes = HuellaFuente.Capturar(dbPath);

            var resultado = new ExternalInsumoImportService().ImportSelected(
                current,
                destino.Id,
                dbPath,
                new[]
                {
                    new ExternalProjectInsumoOption { ItemId = materialId, TipoComponente = TipoComponenteMatriz.Material },
                    new ExternalProjectInsumoOption { ItemId = herramientaId, TipoComponente = TipoComponenteMatriz.Herramienta }
                },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey,
                1m);

            Assert.AreEqual(1, resultado.ImportedMateriales);
            Assert.AreEqual(1, resultado.ImportedHerramientas);

            var material = current.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-FUENTE");
            Assert.AreEqual(destino.Id, material.ProyectoId);
            Assert.AreEqual(OrigenInsumo.Proyecto, material.Origen);
            Assert.IsNull(material.MaterialMaestroId, "El vínculo maestro no debe viajar de la fuente.");
            Assert.AreEqual("[IMPORTADO DE: Origen insumos] nota fuente", material.Notas);

            var herramienta = current.Herramientas.AsNoTracking().Single(h => h.Clave == "HER-FUENTE");
            Assert.AreEqual(destino.Id, herramienta.ProyectoId);
            Assert.AreEqual(OrigenInsumo.Proyecto, herramienta.Origen);
            Assert.AreEqual("[IMPORTADO DE: Origen insumos] nota herramienta", herramienta.Notas);

            antes.AssertSinCambios(dbPath);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportarArbolDeMatrices_PorRutaYPorArrendamiento_NoModificaLaFuente()
    {
        var dbPath = NuevaRutaFuente();
        try
        {
            var (_, _, apuId, _) = CrearFuenteActual(dbPath, "Origen matrices");
            var antes = HuellaFuente.Capturar(dbPath);

            using (var current = TestDbFactory.CreateContext())
            {
                var destino = CrearProyecto(current, "Destino matrices ruta");
                var resultado = new ExternalMatrixImportService().ImportMatrixTree(
                    current, destino.Id, dbPath, apuId,
                    ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);
                VerificarArbolImportado(current, destino.Id, resultado);
            }

            using (var current = TestDbFactory.CreateContext())
            {
                var destino = CrearProyecto(current, "Destino matrices lease");
                using var lease = ExternalProjectSource.Open(Factory, dbPath);
                Assert.IsFalse(lease.IsSnapshot);

                var resultado = new ExternalMatrixImportService().ImportMatrixTree(
                    current, destino.Id, lease, apuId,
                    ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);
                VerificarArbolImportado(current, destino.Id, resultado);
            }

            antes.AssertSinCambios(dbPath);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportarBasico_ReutilizaElArrendamientoSinReabrirLaFuente()
    {
        var dbPath = NuevaRutaFuente();
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto(current, "Destino básico");
        try
        {
            var (_, _, _, basicoId) = CrearFuenteActual(dbPath, "Origen básico");
            var antes = HuellaFuente.Capturar(dbPath);
            var factory = new FabricaContadora(Factory);

            var resultado = new ExternalInsumoImportService(factory).ImportSelected(
                current,
                destino.Id,
                dbPath,
                new[] { new ExternalProjectInsumoOption { ItemId = basicoId, TipoComponente = TipoComponenteMatriz.Auxiliar } },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey,
                1m);

            Assert.AreEqual(1, resultado.ImportedComponents.Count);
            Assert.IsTrue(resultado.ImportedMatrices >= 1);

            // Un arrendamiento actual consume dos lecturas (versión registrada y
            // contexto). Reabrir la fuente para el básico duplicaría el conteo.
            Assert.AreEqual(0, factory.CreadosLecturaEscritura,
                "La fuente actual nunca debe abrirse en modo lectura/escritura.");
            Assert.AreEqual(2, factory.CreadosSoloLectura,
                "El básico debe reutilizar el arrendamiento ya abierto, no reabrir la fuente.");

            var basico = current.Matrices.AsNoTracking().Single(m => m.Clave == "BAS-FUENTE");
            Assert.AreEqual(destino.Id, basico.ProyectoId);
            Assert.AreEqual(OrigenInsumo.Proyecto, basico.Origen);
            Assert.IsNull(basico.MatrizMaestraId);
            Assert.AreEqual("[IMPORTADO DE: Origen básico]", basico.Notas);

            antes.AssertSinCambios(dbPath);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void FuenteLegada_FluyePorCopia_MigraSoloLaCopiaYSeImporta()
    {
        var dbPath = NuevaRutaFuente();
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto(current, "Destino legado");
        try
        {
            var (materialId, herramientaId, _, _) = CrearFuenteActual(dbPath, "Origen legado");
            MarcarComoLegada(dbPath);
            var antes = HuellaFuente.Capturar(dbPath);

            using (var lease = ExternalProjectSource.Open(Factory, dbPath))
            {
                Assert.IsTrue(lease.IsSnapshot, "Un esquema legado debe leerse sobre la copia migrada.");
            }

            var herramientas = new ExternalInsumoImportService().LoadExternalInsumos(dbPath, TipoComponenteMatriz.Herramienta, null);
            Assert.AreEqual(1, herramientas.Items.Count);
            Assert.AreEqual(Path.GetFullPath(dbPath), herramientas.ProjectPath, "La ruta mostrada debe ser la original, no la copia.");

            new ExternalInsumoImportService().ImportSelected(
                current,
                destino.Id,
                dbPath,
                new[]
                {
                    new ExternalProjectInsumoOption { ItemId = materialId, TipoComponente = TipoComponenteMatriz.Material },
                    new ExternalProjectInsumoOption { ItemId = herramientaId, TipoComponente = TipoComponenteMatriz.Herramienta }
                },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey,
                1m);

            Assert.AreEqual(destino.Id, current.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-FUENTE").ProyectoId);
            Assert.AreEqual(destino.Id, current.Herramientas.AsNoTracking().Single(h => h.Clave == "HER-FUENTE").ProyectoId);

            antes.AssertSinCambios(dbPath);

            using var verificacion = Factory.CreateReadOnly(dbPath);
            Assert.IsNull(SchemaManager.GetVersionRegistrada(verificacion),
                "La fuente legada no debe quedar migrada por la importación.");
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void FuenteLegadaMarcadaSoloLectura_SeImportaSinEscribirElOrigen()
    {
        var dbPath = NuevaRutaFuente();
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto(current, "Destino inmutable");
        try
        {
            var (_, _, apuId, _) = CrearFuenteActual(dbPath, "Origen inmutable");
            MarcarComoLegada(dbPath);
            var antes = HuellaFuente.Capturar(dbPath);

            File.SetAttributes(dbPath, FileAttributes.ReadOnly);
            try
            {
                // Con el archivo de solo lectura, migrar el origen fallaría: el flujo
                // probado migra únicamente la copia temporal.
                var resultado = new ExternalMatrixImportService().ImportMatrixTree(
                    current, destino.Id, dbPath, apuId,
                    ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

                VerificarArbolImportado(current, destino.Id, resultado);
                Assert.IsTrue((File.GetAttributes(dbPath) & FileAttributes.ReadOnly) != 0);
            }
            finally
            {
                File.SetAttributes(dbPath, FileAttributes.Normal);
            }

            antes.AssertSinCambios(dbPath);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void FuenteEnWal_ListarEImportar_NoTocanElArchivoNiElLogPendiente()
    {
        var dbPath = NuevaRutaFuente();
        try
        {
            var (materialId, _, _, _) = CrearFuenteActual(dbPath, "Origen WAL");

            // Escritor vivo: deja -wal con contenido pendiente y -shm, el escenario en
            // el que una lectura ingenua podría checkpointear o tocar el log.
            using var escritor = AbrirEscritorWal(dbPath);
            var walPath = dbPath + "-wal";
            Assert.IsTrue(File.Exists(walPath));

            // Tras el cambio de journal_mode (que sí escribe la cabecera del .db) se
            // fija la huella del archivo principal; a partir de aquí ninguna escritura
            // debe llegar al .db.
            var hashDbAntes = Sha256Compartido(dbPath);

            // Escritura confirmada solo en el WAL pendiente (wal_autocheckpoint=0).
            const string descripcionEnWal = "Material fuente (pendiente en WAL)";
            ActualizarDescripcionMaterial(escritor, materialId, descripcionEnWal);
            var hashWalAntes = Sha256Compartido(walPath);

            Assert.AreEqual(hashDbAntes, Sha256Compartido(dbPath),
                "La actualización del escritor debe vivir solo en el WAL pendiente.");

            var items = new ExternalInsumoImportService().LoadExternalInsumos(dbPath, TipoComponenteMatriz.Material, null);
            Assert.AreEqual(1, items.Items.Count);
            Assert.AreEqual(descripcionEnWal, items.Items[0].Descripcion,
                "La lectura del importador debe ver la versión pendiente en el WAL.");

            using var current = TestDbFactory.CreateContext();
            var destino = CrearProyecto(current, "Destino WAL");
            new ExternalInsumoImportService().ImportSelected(
                current,
                destino.Id,
                dbPath,
                new[] { new ExternalProjectInsumoOption { ItemId = materialId, TipoComponente = TipoComponenteMatriz.Material } },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey,
                1m);

            Assert.AreEqual(descripcionEnWal,
                current.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-FUENTE").Descripcion,
                "La importación debe copiar la versión pendiente en el WAL.");

            Assert.AreEqual(hashDbAntes, Sha256Compartido(dbPath), "La fuente WAL no debe cambiar.");
            Assert.AreEqual(hashWalAntes, Sha256Compartido(walPath), "El log pendiente no debe tocarse.");

            using var verificacion = Factory.CreateReadOnly(dbPath);
            Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(verificacion),
                "La importación no debe migrar ni reescribir el esquema de la fuente.");
            Assert.AreEqual(1, verificacion.Materiales.Count(m => m.Clave == "MAT-FUENTE"));
        }
        finally
        {
            // El escritor vive en el ámbito del try; al salir se cierra y solo entonces
            // se limpia el fixture (nunca se borran sidecars de una fuente con dueño).
            SqliteConnection.ClearAllPools();
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void FuenteInexistente_LanzaFileNotFoundSinCrearArchivo()
    {
        var dbPath = NuevaRutaFuente();
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto(current, "Destino sin fuente");

        Assert.ThrowsExactly<FileNotFoundException>(
            () => new ExternalInsumoImportService().LoadExternalInsumos(dbPath, TipoComponenteMatriz.Material, null));
        Assert.ThrowsExactly<FileNotFoundException>(
            () => new ExternalMatrixImportService().LoadExternalMatrices(dbPath));
        Assert.ThrowsExactly<FileNotFoundException>(
            () => new ExternalInsumoImportService().ImportSelected(
                current, destino.Id, dbPath,
                new[] { new ExternalProjectInsumoOption { ItemId = 1, TipoComponente = TipoComponenteMatriz.Material } },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, 1m));
        Assert.ThrowsExactly<FileNotFoundException>(
            () => new ExternalMatrixImportService().ImportMatrixTree(
                current, destino.Id, dbPath, 1,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey));

        Assert.IsFalse(File.Exists(dbPath), "No debe crearse un archivo vacío en la ruta indicada.");
        Assert.IsFalse(File.Exists(dbPath + "-wal"), "No deben crearse sidecars de una fuente inexistente.");
        Assert.IsFalse(File.Exists(dbPath + "-shm"), "No deben crearse sidecars de una fuente inexistente.");
    }

    // ── Verificaciones ─────────────────────────────────────────────────────────

    private static void VerificarArbolImportado(SOPROContext current, int proyectoDestino, ExternalMatrixImportResult resultado)
    {
        var raiz = current.Matrices.AsNoTracking().Single(m => m.Id == resultado.RootMatrixId);
        Assert.AreEqual(proyectoDestino, raiz.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, raiz.Origen);
        Assert.IsNull(raiz.MatrizMaestraId);
        StringAssert.Contains(raiz.Notas ?? string.Empty, "[IMPORTADO DE: Origen");

        var basico = current.Matrices.AsNoTracking().Single(m => m.Clave == "BAS-FUENTE");
        Assert.AreEqual(proyectoDestino, basico.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, basico.Origen);

        var material = current.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-FUENTE");
        Assert.AreEqual(proyectoDestino, material.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, material.Origen);
        Assert.IsNull(material.MaterialMaestroId);
    }

    // ── Fixtures ───────────────────────────────────────────────────────────────

    private static string NuevaRutaFuente()
        => Path.Combine(Path.GetTempPath(), $"sopro_inmutable_{Guid.NewGuid():N}.db");

    private static (int MaterialId, int HerramientaId, int ApuId, int BasicoId) CrearFuenteActual(string dbPath, string nombreProyecto)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        SchemaManager.EnsureCurrentSchema(ctx);
        // EF Core crea la base en WAL. Para que la huella de sidecars sea determinista
        // el fixture deja la fuente en journal DELETE; el caso WAL real se cubre en
        // FuenteEnWal_ListarEImportar_NoTocanElArchivoNiElLogPendiente.
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");

        var proy = CrearProyecto(ctx, nombreProyecto);

        var material = new Material
        {
            ProyectoId = proy.Id,
            Clave = "MAT-FUENTE",
            Descripcion = "Material fuente",
            Unidad = "kg",
            PrecioUnitario = 10m,
            MaterialMaestroId = 99,
            Notas = "nota fuente"
        };
        var herramienta = new Herramienta
        {
            ProyectoId = proy.Id,
            Clave = "HER-FUENTE",
            Descripcion = "Herramienta fuente",
            Unidad = "pza",
            PrecioUnitario = 4m,
            Notas = "nota herramienta"
        };
        ctx.Materiales.Add(material);
        ctx.Herramientas.Add(herramienta);
        ctx.SaveChanges();

        var basico = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = "BAS-FUENTE",
            Descripcion = "Básico fuente",
            Unidad = "m2",
            Tipo = TipoMatriz.Basico,
            Notas = string.Empty
        };
        var apu = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = "APU-FUENTE",
            Descripcion = "APU fuente",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        ctx.Matrices.AddRange(basico, apu);
        ctx.SaveChanges();

        ctx.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = apu.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = basico.Id,
                Cantidad = 1m,
                Importe = 1m,
                Orden = 1,
                Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = basico.Id,
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = material.Id,
                Cantidad = 2m,
                Importe = 20m,
                Orden = 1,
                Notas = string.Empty
            });
        ctx.SaveChanges();

        return (material.Id, herramienta.Id, apu.Id, basico.Id);
    }

    private static void MarcarComoLegada(string dbPath)
    {
        using (var ctx = new SOPROContext(dbPath))
        {
            ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS VistasPresupuesto;");
            ctx.Database.ExecuteSqlRaw("DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
        }

        SqliteConnection.ClearAllPools();
    }

    private static Proyecto CrearProyecto(SOPROContext ctx, string nombre)    {
        var p = new Proyecto
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        ctx.Proyectos.Add(p);
        ctx.SaveChanges();
        return p;
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

    /// <summary>Actualización confirmada por el escritor vivo (queda en el WAL pendiente).</summary>
    private static void ActualizarDescripcionMaterial(SqliteConnection connection, int materialId, string descripcion)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Materiales SET Descripcion = $descripcion WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$descripcion", descripcion);
        cmd.Parameters.AddWithValue("$id", materialId);
        Assert.AreEqual(1, cmd.ExecuteNonQuery(), "El escritor debía actualizar exactamente un material.");
    }

    /// <summary>Hash con compartición total: permite medir un archivo que SQLite tiene abierto.</summary>
    private static string Sha256Compartido(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void BorrarFuente(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", ".lock" })
        {
            var file = dbPath + suffix;
            try
            {
                if (File.Exists(file))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // Best-effort de limpieza del test.
            }
        }
    }

    private sealed record HuellaFuente(string Hash, DateTime Escritura, IReadOnlyList<string> Archivos)
    {
        public static HuellaFuente Capturar(string dbPath)
        {
            var archivos = Directory
                .GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*")
                .Select(path => Path.GetFileName(path)!)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new HuellaFuente(Sha256(dbPath), File.GetLastWriteTimeUtc(dbPath), archivos);
        }

        public void AssertSinCambios(string dbPath)
        {
            Assert.AreEqual(Hash, Sha256(dbPath), "El archivo externo no debe cambiar.");
            Assert.AreEqual(Escritura, File.GetLastWriteTimeUtc(dbPath), "La fecha del archivo externo no debe cambiar.");
            var despues = Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*")
                .Select(path => Path.GetFileName(path)!)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            CollectionAssert.AreEqual(
                Archivos.ToArray(),
                despues,
                $"La fuente externa no debe ganar ni perder sidecars. antes=[{string.Join(",", Archivos)}] despues=[{string.Join(",", despues)}]");
        }

        private static string Sha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
    }

    /// <summary>Fábrica que cuenta aperturas para probar que la fuente no se reabre.</summary>
    private sealed class FabricaContadora : IProjectDbContextFactory
    {
        private readonly IProjectDbContextFactory _inner;

        public FabricaContadora(IProjectDbContextFactory inner) => _inner = inner;

        public int CreadosLecturaEscritura { get; private set; }
        public int CreadosSoloLectura { get; private set; }

        public SOPROContext Create(string databasePath)
        {
            CreadosLecturaEscritura++;
            return _inner.Create(databasePath);
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
        {
            CreadosLecturaEscritura++;
            return _inner.CreateAsync(databasePath, cancellationToken);
        }

        public SOPROContext CreateReadOnly(string databasePath)
        {
            CreadosSoloLectura++;
            return _inner.CreateReadOnly(databasePath);
        }
    }
}
