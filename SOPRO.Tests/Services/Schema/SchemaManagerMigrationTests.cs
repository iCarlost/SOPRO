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

    // ═══════════════════════════════════════════════════════════════════════
    // M014 — Persistencia relacional de estilos (FormatosCeldaReporte +
    //        ConfiguracionesEstiloFilaReporte)
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Migracion014_CreacionDesdeBaseVacia_CreaTablasYVersion()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                Assert.IsTrue(TablaExiste(ctx, "FormatosCeldaReporte"),
                    "FormatosCeldaReporte debe existir tras M014.");
                Assert.IsTrue(TablaExiste(ctx, "ConfiguracionesEstiloFilaReporte"),
                    "ConfiguracionesEstiloFilaReporte debe existir tras M014.");

                Assert.AreEqual("2026.04.4", SchemaManager.GetVersionRegistrada(ctx),
                    "M014 debe registrar la versión 2026.04.4 (bump de VersionActual).");
                Assert.AreEqual(SchemaManager.VersionActual,
                    SchemaManager.GetVersionRegistrada(ctx),
                    "La versión registrada debe coincidir con VersionActual.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void Migracion014_UpgradeDesdeM013_SinPerderDatos()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            int proyectoId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();

                // Simular una DB legacy creada con el schema M013: el modelo
                // conoce las tablas nuevas pero el archivo físico no las tiene.
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS FormatosCeldaReporte;");
                ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ConfiguracionesEstiloFilaReporte;");

                var proyecto = CrearProyecto("Proyecto M013");
                ctx.Proyectos.Add(proyecto);
                ctx.SaveChanges();
                proyectoId = proyecto.Id;

                // Dato pre-existente que NO debe perderse con el upgrade.
                ctx.ConfiguracionesTituloReporte.Add(new ConfiguracionTituloReporte
                {
                    ProyectoId = proyectoId,
                    Modulo = "Presupuesto",
                    TextoTitulo = "PRESUPUESTO DE OBRA"
                });
                ctx.SaveChanges();
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                SchemaManager.EnsureCurrentSchema(ctx);

                Assert.IsTrue(TablaExiste(ctx, "FormatosCeldaReporte"), "Falta FormatosCeldaReporte.");
                Assert.IsTrue(TablaExiste(ctx, "ConfiguracionesEstiloFilaReporte"), "Falta ConfiguracionesEstiloFilaReporte.");
                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx));

                // Datos previos intactos
                Assert.IsTrue(ctx.Proyectos.Any(p => p.Id == proyectoId), "El proyecto previo desapareció.");
                var titulo = ctx.ConfiguracionesTituloReporte.Single(t => t.ProyectoId == proyectoId);
                Assert.AreEqual("PRESUPUESTO DE OBRA", titulo.TextoTitulo,
                    "Los datos previos al upgrade deben conservarse.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void Migracion014_EsIdempotente_DobleAplicacionNoDuplica()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);
                SchemaManager.EnsureCurrentSchema(ctx);
                SchemaManager.EnsureCurrentSchema(ctx);

                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx));

                Assert.IsTrue(TablaExiste(ctx, "FormatosCeldaReporte"));
                Assert.IsTrue(TablaExiste(ctx, "ConfiguracionesEstiloFilaReporte"));

                // Sin tablas ni índices duplicados
                Assert.AreEqual(1, ContarObjeto(ctx, "table", "FormatosCeldaReporte"),
                    "No debe duplicarse la tabla FormatosCeldaReporte.");
                Assert.AreEqual(1, ContarObjeto(ctx, "table", "ConfiguracionesEstiloFilaReporte"),
                    "No debe duplicarse la tabla ConfiguracionesEstiloFilaReporte.");
                Assert.AreEqual(1, ContarObjeto(ctx, "index",
                    "IX_FormatosCeldaReporte_ProyectoId_TipoReporte_ClaveVista_ClaveFila_NombreColumna"),
                    "No debe duplicarse el índice único de FormatosCeldaReporte.");
                Assert.AreEqual(1, ContarObjeto(ctx, "index",
                    "IX_ConfiguracionesEstiloFilaReporte_ProyectoId_TipoReporte_RolFila"),
                    "No debe duplicarse el índice único de ConfiguracionesEstiloFilaReporte.");

                // Una sola fila de versión tras múltiples aplicaciones
                Assert.AreEqual(1, ContarVersionRegistrada(ctx),
                    "RegistrarVersion debe mantener una sola fila de versión.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void FormatosCeldaReporte_IndiceUnico_RechazaDuplicadoYAdmiteClaveVistaVacia()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proyecto = CrearProyecto("Proyecto Formatos");
                ctx.Proyectos.Add(proyecto);
                ctx.SaveChanges();

                var formato = new FormatoCeldaReporte
                {
                    ProyectoId = proyecto.Id,
                    TipoReporte = "Presupuesto",
                    ClaveVista = "",                 // vista por defecto: nunca NULL
                    ClaveFila = "1.2.3",
                    NombreColumna = "ImporteTotal",
                    ColorFondo = "#FF0000"
                };
                ctx.FormatosCeldaReporte.Add(formato);
                ctx.SaveChanges();

                Assert.AreEqual(string.Empty, formato.ClaveVista,
                    "ClaveVista \"\" debe persistirse como string vacío, no NULL.");

                // Duplicado exacto de la clave única → debe lanzar
                ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
                {
                    ProyectoId = proyecto.Id,
                    TipoReporte = "Presupuesto",
                    ClaveVista = "",
                    ClaveFila = "1.2.3",
                    NombreColumna = "ImporteTotal"
                });
                Assert.ThrowsExactly<DbUpdateException>(() => ctx.SaveChanges());

                // Otra vista (ClaveVista distinta) → permitido
                ctx.ChangeTracker.Clear();
                ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
                {
                    ProyectoId = proyecto.Id,
                    TipoReporte = "Presupuesto",
                    ClaveVista = "VistaA",
                    ClaveFila = "1.2.3",
                    NombreColumna = "ImporteTotal"
                });
                ctx.SaveChanges();

                Assert.AreEqual(2, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyecto.Id));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void ConfiguracionEstiloFilaReporte_IndiceUnico_RechazaDuplicado()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proyecto = CrearProyecto("Proyecto Estilos");
                ctx.Proyectos.Add(proyecto);
                ctx.SaveChanges();

                ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
                {
                    ProyectoId = proyecto.Id,
                    TipoReporte = "Presupuesto",
                    RolFila = "Total",
                    ColorFondo = "#2C3E50"
                });
                ctx.SaveChanges();

                // Mismo (ProyectoId, TipoReporte, RolFila) → debe lanzar
                ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
                {
                    ProyectoId = proyecto.Id,
                    TipoReporte = "Presupuesto",
                    RolFila = "Total"
                });
                Assert.ThrowsExactly<DbUpdateException>(() => ctx.SaveChanges());

                // Distinto rol → permitido
                ctx.ChangeTracker.Clear();
                ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
                {
                    ProyectoId = proyecto.Id,
                    TipoReporte = "Presupuesto",
                    RolFila = "Subtotal"
                });
                ctx.SaveChanges();

                Assert.AreEqual(2, ctx.ConfiguracionesEstiloFilaReporte.Count(c => c.ProyectoId == proyecto.Id));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void Migracion014_CascadaPorProyecto_EliminaFormatosYEstilos()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proyecto = CrearProyecto("Proyecto Cascada");
                ctx.Proyectos.Add(proyecto);
                ctx.SaveChanges();
                var proyectoId = proyecto.Id;

                ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
                {
                    ProyectoId = proyectoId,
                    TipoReporte = "Presupuesto",
                    ClaveVista = "",
                    ClaveFila = "1",
                    NombreColumna = "ImporteTotal"
                });
                ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
                {
                    ProyectoId = proyectoId,
                    TipoReporte = "Presupuesto",
                    RolFila = "Total"
                });
                ctx.SaveChanges();

                // Borrado a nivel DB sobre la MISMA conexión con FK activas:
                // verifica el ON DELETE CASCADE declarado en M014.
                var conn = ctx.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON;";
                    pragma.ExecuteNonQuery();
                }
                using (var del = conn.CreateCommand())
                {
                    del.CommandText = $"DELETE FROM Proyectos WHERE Id = {proyectoId};";
                    del.ExecuteNonQuery();
                }

                ctx.ChangeTracker.Clear();
                Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
                    "El cascade debe borrar los formatos de celda del proyecto.");
                Assert.AreEqual(0, ctx.ConfiguracionesEstiloFilaReporte.Count(c => c.ProyectoId == proyectoId),
                    "El cascade debe borrar los estilos de fila del proyecto.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void FormatosYEstilos_RoundTripEf_InsertReadUpdateDelete()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_schema_{Guid.NewGuid():N}.db");
        try
        {
            int proyectoId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proyecto = CrearProyecto("Proyecto RoundTrip");
                ctx.Proyectos.Add(proyecto);
                ctx.SaveChanges();
                proyectoId = proyecto.Id;

                ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
                {
                    ProyectoId = proyectoId,
                    TipoReporte = "Presupuesto",
                    ClaveVista = "",
                    ClaveFila = "1",
                    NombreColumna = "ImporteTotal",
                    Fuente = "Segoe UI",
                    TamanoFuente = 9f,
                    Negrita = true,
                    Cursiva = false,
                    ColorFuente = "#000000",
                    ColorFondo = "#FFEE00",
                    AlineacionHorizontal = "Right",
                    AlineacionVertical = "Middle",
                    Wrap = true,
                    FormatoNumerico = "C2"
                });
                ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
                {
                    ProyectoId = proyectoId,
                    TipoReporte = "Presupuesto",
                    RolFila = "Total",
                    Negrita = true,
                    ColorFondo = "#2C3E50"
                });
                ctx.SaveChanges();
            }

            // Read + Update
            using (var ctx = new SOPROContext(dbPath))
            {
                var formato = ctx.FormatosCeldaReporte.Single(f => f.ProyectoId == proyectoId);
                Assert.AreEqual("#FFEE00", formato.ColorFondo);
                Assert.AreEqual(9f, formato.TamanoFuente);
                Assert.AreEqual(true, formato.Wrap);
                Assert.AreEqual("C2", formato.FormatoNumerico);

                formato.ColorFondo = "#00FF00";
                formato.Negrita = false;

                var estilo = ctx.ConfiguracionesEstiloFilaReporte.Single(c => c.ProyectoId == proyectoId);
                Assert.AreEqual("Total", estilo.RolFila);
                Assert.IsNull(estilo.TamanoFuente); // null = heredar

                ctx.SaveChanges();
            }

            // Read updated + Delete
            using (var ctx = new SOPROContext(dbPath))
            {
                var formato = ctx.FormatosCeldaReporte.Single(f => f.ProyectoId == proyectoId);
                Assert.AreEqual("#00FF00", formato.ColorFondo);
                Assert.AreEqual(false, formato.Negrita);

                ctx.FormatosCeldaReporte.Remove(formato);
                ctx.ConfiguracionesEstiloFilaReporte.RemoveRange(
                    ctx.ConfiguracionesEstiloFilaReporte.Where(c => c.ProyectoId == proyectoId));
                ctx.SaveChanges();

                Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));
                Assert.AreEqual(0, ctx.ConfiguracionesEstiloFilaReporte.Count(c => c.ProyectoId == proyectoId));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

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

    private static int ContarObjeto(SOPROContext ctx, string tipo, string nombre)
    {
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type=$1 AND name=$2";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "$1"; p1.Value = tipo; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "$2"; p2.Value = nombre; cmd.Parameters.Add(p2);
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            cmd.Connection.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static int ContarVersionRegistrada(SOPROContext ctx)
    {
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%'";
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            cmd.Connection.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static bool TablaExiste(SOPROContext ctx, string nombre)
    {
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = Sql;
        var p = cmd.CreateParameter();
        p.ParameterName = "$1";
        p.Value = nombre;
        cmd.Parameters.Add(p);
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            cmd.Connection.Open();
        return cmd.ExecuteScalar() != null;
    }
}
