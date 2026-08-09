using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Tests.Services.Schema;

/// <summary>
/// Tests pin de la Fase 2 — Persistencia y migraciones.
/// A1 — Las tablas GruposIndirectos, ConceptosIndirectos, ConfiguracionesIndirectos
///      y VistasPresupuesto NO tenían migración en SchemaManager (solo esistían
///      vía EnsureCreated, que no toca DBs existentes).
/// Deben FALLAR (rojo) sin M013 y QUEDAR VERDES con el fix.
/// </summary>
[TestClass]
public class SchemaManagerMigrationTests
{
    private const string Sql = @"SELECT name FROM sqlite_master WHERE type='table' AND name = $1";

    [TestMethod]
    public void Migracion013_CreaTablasIndirectosYVistas_EnDbLegacySinEllas()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            int proyectoId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();

                // Simular una DB legacy creada antes de que existieran estas tablas:
                // el modelo las conoce pero el archivo físico no las tiene.
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS VistasPresupuesto;");
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ConceptosIndirectos;");
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ConfiguracionesIndirectos;");
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS GruposIndirectos;");

                var proyecto = new Proyecto
                {
                    Nombre = "Proyecto Legacy",
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
                proyectoId = proyecto.Id;
            }

            // Abrir con SchemaManager debe corregir el schema legacy
            using (var ctx = new SOPROContext(dbPath))
            {
                SchemaManager.EnsureCurrentSchema(ctx);

                foreach (var tabla in new[]
                             {
                                 "GruposIndirectos",
                                 "ConceptosIndirectos",
                                 "ConfiguracionesIndirectos",
                                 "VistasPresupuesto"
                             })
                    Assert.IsTrue(TablaExiste(ctx, tabla), $"La tabla {tabla} debe existir tras M013.");

                Assert.AreEqual(SchemaManager.VersionActual,
                    SchemaManager.GetVersionRegistrada(ctx),
                    "La versión registrada debe coincidir con VersionActual.");
            }

            // Round-trip: el modelo EF debe poder insertar/leer desde las tablas creadas
            using (var ctx = new SOPROContext(dbPath))
            {
                var grupo = new GrupoIndirecto
                {
                    ProyectoId = proyectoId,
                    Nombre = "HONORARIOS",
                    Tipo = TipoIndirecto.OficinaCentral,
                    Orden = 1
                };
                grupo.Conceptos.Add(new ConceptoIndirecto
                {
                    Concepto = "Gerente General",
                    Tipo = TipoIndirecto.OficinaCentral,
                    ImporteMensual = 10000m,
                    DuracionMeses = 12,
                    Orden = 1,
                    Activo = true
                });
                ctx.GruposIndirectos.Add(grupo);
                ctx.ConfiguracionesIndirectos.Add(new ConfiguracionIndirectos
                {
                    ProyectoId = proyectoId,
                    VolumenAnualObra = 1000000m,
                    CostoDirectoObra = 800000m,
                    TotalOficinaCentralAnual = 50000m,
                    TotalCampo = 20000m,
                    PorcentajeOficinaCentral = 5m,
                    PorcentajeCampo = 2.5m
                });
                ctx.VistasPresupuesto.Add(new VistaPresupuesto
                {
                    ProyectoId = proyectoId,
                    Nombre = "Vista Base",
                    Descripcion = string.Empty,
                    EsVistaPorDefecto = true,
                    ConfiguracionColumnasJSON = "[]"
                });
                ctx.SaveChanges();

                var leido = ctx.GruposIndirectos.Include(g => g.Conceptos).First();
                if (leido is null) throw new InvalidOperationException("No se pudo leer el grupo.");
                var concepto = leido.Conceptos.Single();
                Assert.AreEqual("HONORARIOS", leido.Nombre);
                Assert.AreEqual(10000m, concepto.ImporteMensual);
                Assert.IsTrue(ctx.ConfiguracionesIndirectos.Any(c => c.ProyectoId == proyectoId));
                Assert.IsTrue(ctx.VistasPresupuesto.Any(v => v.ProyectoId == proyectoId));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void Migracion013_EsIdempotente_DobleAplicacionNoFallaNiDuplica()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);
            }

            // Segunda pasada: sin excepción y sin cambios en el conteo de versión
            using (var ctx = new SOPROContext(dbPath))
            {
                SchemaManager.EnsureCurrentSchema(ctx);
                SchemaManager.EnsureCurrentSchema(ctx);

                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx));

                foreach (var tabla in new[]
                             {
                                 "GruposIndirectos",
                                 "ConceptosIndirectos",
                                 "ConfiguracionesIndirectos",
                                 "VistasPresupuesto"
                             })
                    Assert.IsTrue(TablaExiste(ctx, tabla), $"Tabla {tabla} debe seguir existiendo.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static bool TablaExiste(SOPROContext ctx, string nombre)
    {
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = Sql;
        var p = cmd.CreateParameter();
        p.ParameterName = "$1";
        p.Value = nombre;
        cmd.Parameters.Add(p);
        if (cmd.Connection.State != System.Data.ConnectionState.Open)
            cmd.Connection.Open();
        return cmd.ExecuteScalar() != null;
    }
}