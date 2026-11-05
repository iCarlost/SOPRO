using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Importacion;

/// <summary>
/// Importación directa al catálogo destino (#75): reutiliza el motor de
/// importación de insumos externos sin crear matrices ni componentes, conserva
/// la procedencia y deja intacta la fuente. También cubre la política de
/// conflictos y la regresión de <see cref="ExternalInsumoImportService.ImportSelected"/>.
/// </summary>
[TestClass]
public class ExternalInsumoCatalogImportTests
{
    [TestMethod]
    public void ImportarVariosMaterialesAlCatalogo_NoCreaMatricesNiComponentes_YLaFuenteNoCambia()
    {
        var fuente = CrearFuente();
        using var destinoCtx = TestDbFactory.CreateContext();
        var destino = CrearProyecto(destinoCtx, "Destino catálogo");
        try
        {
            var hashAntes = Sha256(fuente.Path);
            var opciones = CargarOpciones(fuente.Path, TipoComponenteMatriz.Material);
            Assert.AreEqual(2, opciones.Count);

            var importados = new ExternalInsumoImportService().ImportIntoCatalog(
                destinoCtx, destino.Id, fuente.Path, opciones,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            Assert.AreEqual(2, importados);
            var materiales = destinoCtx.Materiales.AsNoTracking()
                .Where(m => m.ProyectoId == destino.Id)
                .OrderBy(m => m.Clave)
                .ToList();
            Assert.AreEqual(2, materiales.Count);
            Assert.AreEqual("MAT-1", materiales[0].Clave);
            Assert.AreEqual("MAT-2", materiales[1].Clave);
            Assert.IsTrue(materiales.All(m => m.Origen == OrigenInsumo.Proyecto));
            Assert.IsTrue(materiales.All(m => m.MaterialMaestroId == null));
            StringAssert.Contains(materiales[0].Notas ?? string.Empty, "[IMPORTADO DE: Origen catálogo]");
            Assert.AreEqual(0, destinoCtx.Matrices.Count(), "Importar al catálogo no debe crear matrices.");
            Assert.AreEqual(0, destinoCtx.ComponentesMatriz.Count(), "Importar al catálogo no debe crear componentes.");
            Assert.AreEqual(hashAntes, Sha256(fuente.Path), "La fuente debe quedar intacta.");
        }
        finally
        {
            BorrarFuente(fuente.Path);
        }
    }

    [TestMethod]
    public void ImportarHerramientasAlCatalogo_NoCreaMatricesNiComponentes_YLaFuenteNoCambia()
    {
        var fuente = CrearFuente();
        using var destinoCtx = TestDbFactory.CreateContext();
        var destino = CrearProyecto(destinoCtx, "Destino herramientas");
        try
        {
            var hashAntes = Sha256(fuente.Path);
            var opciones = CargarOpciones(fuente.Path, TipoComponenteMatriz.Herramienta);
            Assert.AreEqual(1, opciones.Count);

            var importados = new ExternalInsumoImportService().ImportIntoCatalog(
                destinoCtx, destino.Id, fuente.Path, opciones,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            Assert.AreEqual(1, importados);
            var herramienta = destinoCtx.Herramientas.AsNoTracking().Single(h => h.ProyectoId == destino.Id);
            Assert.AreEqual("HER-1", herramienta.Clave);
            Assert.AreEqual(OrigenInsumo.Proyecto, herramienta.Origen);
            StringAssert.Contains(herramienta.Notas ?? string.Empty, "[IMPORTADO DE: Origen catálogo]");
            Assert.AreEqual(0, destinoCtx.Matrices.Count(), "Importar al catálogo no debe crear matrices.");
            Assert.AreEqual(0, destinoCtx.ComponentesMatriz.Count(), "Importar al catálogo no debe crear componentes.");
            Assert.AreEqual(hashAntes, Sha256(fuente.Path), "La fuente debe quedar intacta.");
        }
        finally
        {
            BorrarFuente(fuente.Path);
        }
    }

    [TestMethod]
    public void ConflictoDeClave_MantenerAmbas_CreaCopiaConClaveTemporalYConservaLaLocal()
    {
        var fuente = CrearFuente();
        using var destinoCtx = TestDbFactory.CreateContext();
        var destino = CrearProyecto(destinoCtx, "Destino mantener ambas");
        try
        {
            destinoCtx.Materiales.Add(new Material
            {
                ProyectoId = destino.Id,
                Clave = "MAT-1",
                Descripcion = "Material local",
                Unidad = "kg",
                PrecioUnitario = 1m,
                Notas = string.Empty,
            });
            destinoCtx.SaveChanges();

            var opcion = CargarOpciones(fuente.Path, TipoComponenteMatriz.Material)
                .Single(o => o.Clave == "MAT-1");

            var importados = new ExternalInsumoImportService().ImportIntoCatalog(
                destinoCtx, destino.Id, fuente.Path, new[] { opcion },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            Assert.AreEqual(1, importados);
            var filas = destinoCtx.Materiales.AsNoTracking()
                .Where(m => m.ProyectoId == destino.Id)
                .ToList();
            Assert.AreEqual(2, filas.Count);
            Assert.AreEqual("Material local", filas.Single(m => m.Clave == "MAT-1").Descripcion);
            var copia = filas.Single(m => m.Clave != "MAT-1");
            StringAssert.StartsWith(copia.Clave, "TEMP-MAT-1");
            Assert.AreEqual("Material 1", copia.Descripcion);
        }
        finally
        {
            BorrarFuente(fuente.Path);
        }
    }

    [TestMethod]
    public void ConflictoDeClave_Reemplazar_ActualizaLaFilaSinDuplicar()
    {
        var fuente = CrearFuente();
        using var destinoCtx = TestDbFactory.CreateContext();
        var destino = CrearProyecto(destinoCtx, "Destino reemplazar");
        try
        {
            destinoCtx.Materiales.Add(new Material
            {
                ProyectoId = destino.Id,
                Clave = "MAT-1",
                Descripcion = "Material local",
                Unidad = "kg",
                PrecioUnitario = 1m,
                Notas = string.Empty,
            });
            destinoCtx.SaveChanges();

            var opcion = CargarOpciones(fuente.Path, TipoComponenteMatriz.Material)
                .Single(o => o.Clave == "MAT-1");

            var importados = new ExternalInsumoImportService().ImportIntoCatalog(
                destinoCtx, destino.Id, fuente.Path, new[] { opcion },
                ExternalMatrixImportConflictPolicy.ReplaceExisting);

            Assert.AreEqual(1, importados);
            var filas = destinoCtx.Materiales.AsNoTracking()
                .Where(m => m.ProyectoId == destino.Id)
                .ToList();
            Assert.AreEqual(1, filas.Count);
            Assert.AreEqual("MAT-1", filas[0].Clave);
            Assert.AreEqual("Material 1", filas[0].Descripcion);
            Assert.AreEqual(11m, filas[0].PrecioUnitario);
            StringAssert.Contains(filas[0].Notas ?? string.Empty, "[IMPORTADO DE: Origen catálogo]");
        }
        finally
        {
            BorrarFuente(fuente.Path);
        }
    }

    [TestMethod]
    public void ImportSelected_ConservaLaCreacionDeComponentes()
    {
        var fuente = CrearFuente();
        using var destinoCtx = TestDbFactory.CreateContext();
        var destino = CrearProyecto(destinoCtx, "Destino matriz");
        try
        {
            var opcion = CargarOpciones(fuente.Path, TipoComponenteMatriz.Material)
                .Single(o => o.Clave == "MAT-1");

            var resultado = new ExternalInsumoImportService().ImportSelected(
                destinoCtx, destino.Id, fuente.Path, new[] { opcion },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, 2m);

            Assert.AreEqual(1, resultado.ImportedComponents.Count);
            Assert.AreEqual(TipoComponenteMatriz.Material, resultado.ImportedComponents[0].TipoComponente);
            Assert.AreEqual(2m, resultado.ImportedComponents[0].Cantidad);
            Assert.AreEqual(22m, resultado.ImportedComponents[0].Importe);
            Assert.AreEqual(1, resultado.ImportedMateriales);
            Assert.AreEqual(0, destinoCtx.Matrices.Count());
        }
        finally
        {
            BorrarFuente(fuente.Path);
        }
    }

    // ── Fixtures ───────────────────────────────────────────────────────────────

    private static List<ExternalProjectInsumoOption> CargarOpciones(string dbPath, TipoComponenteMatriz tipo)
        => new ExternalInsumoImportService().LoadExternalInsumos(dbPath, tipo, null).Items;

    private static (string Path, int Material1Id, int Material2Id, int HerramientaId) CrearFuente()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_catalogo_origen_{Guid.NewGuid():N}.db");
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        SchemaManager.EnsureCurrentSchema(ctx);
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");

        var proyecto = CrearProyecto(ctx, "Origen catálogo");
        var material1 = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-1",
            Descripcion = "Material 1",
            Unidad = "kg",
            PrecioUnitario = 11m,
            Notas = "nota mat 1",
        };
        var material2 = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-2",
            Descripcion = "Material 2",
            Unidad = "m3",
            PrecioUnitario = 12m,
            Notas = "nota mat 2",
        };
        var herramienta = new Herramienta
        {
            ProyectoId = proyecto.Id,
            Clave = "HER-1",
            Descripcion = "Herramienta 1",
            Unidad = "pza",
            PrecioUnitario = 4m,
            Notas = "nota her 1",
        };
        ctx.Materiales.AddRange(material1, material2);
        ctx.Herramientas.Add(herramienta);
        ctx.SaveChanges();

        return (dbPath, material1.Id, material2.Id, herramienta.Id);
    }

    private static Proyecto CrearProyecto(SOPROContext ctx, string nombre)
    {
        var proyecto = new Proyecto
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
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static string Sha256(string path)
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
                    File.Delete(file);
            }
            catch (IOException)
            {
                // Limpieza best-effort del fixture.
            }
        }
    }
}
