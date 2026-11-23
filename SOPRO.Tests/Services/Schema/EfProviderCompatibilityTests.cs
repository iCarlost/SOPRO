using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Schema;

/// <summary>
/// Caracterización del proveedor EF Core 8 + SQLite previa al salto a EF Core 10.
/// Fija la semántica observada de fechas TEXT (sin offset, con offset y NULL),
/// la apertura de copias sintéticas actuales/legacy y el rollback transaccional.
/// Si EF Core 10 cambia alguna de estas semánticas, estas pruebas deben fallar
/// para que la decisión sea explícita y no silenciosa.
/// <para>
/// Política temporal: el producto fija
/// <c>Microsoft.Data.Sqlite.Pre10TimeZoneHandling</c> al arrancar, antes del
/// primer uso de SQLite. Este proceso de pruebas hace lo mismo en
/// <see cref="ConfigurarPoliticaTemporalPrevia"/> para caracterizar la misma
/// semántica; el switch es global al proceso y no debe alternarse después.
/// </para>
/// </summary>
[TestClass]
public class EfProviderCompatibilityTests
{
    private const string SwitchTemporalSqlite = "Microsoft.Data.Sqlite.Pre10TimeZoneHandling";
    private const string FixtureSintetico = "proyecto-sintetico-vial-demo.db";
    private const string ColumnaFechaInicio = nameof(Proyecto.FechaInicio);
    private const string ColumnaFechaCalculoFsr = nameof(Proyecto.FechaCalculoFSR);

    private static readonly string[] ColumnasM015Maquinaria =
    {
        "MaterialCombustibleId",
        "MaterialAceiteId",
        "ManoDeObraOperadorId"
    };

    private const string FixtureNombre = "PROYECTO SINTETICO VIAL DEMO";
    private const string FixtureDescripcion = "OBRA: REPAVIMENTACION SINTETICA DEMO";
    private const string FixtureTextoFechaInicio = "2026-04-30 14:33:07.534";
    private static readonly DateTime FixtureFechaInicio =
        new(2026, 4, 30, 14, 33, 7, 534, DateTimeKind.Unspecified);
    private static readonly DateTime FixtureFechaTermino =
        new(2026, 8, 27, 14, 33, 7, 543, DateTimeKind.Unspecified);
    private static readonly DateTime FixtureFechaCreacion =
        new DateTime(2026, 4, 16, 14, 33, 7, 467, DateTimeKind.Unspecified).AddTicks(765);

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// El switch debe quedar fijado antes de que cualquier prueba abra SQLite.
    /// No se alterna durante el proceso: se fija aquí una sola vez.
    /// </summary>
    [AssemblyInitialize]
    public static void ConfigurarPoliticaTemporalPrevia(TestContext context)
    {
        _ = context;
        AppContext.SetSwitch(SwitchTemporalSqlite, isEnabled: true);
    }

    [TestMethod]
    public void PoliticaTemporal_ProcesoUsaSemanticaPreviaDeSqlite()
    {
        Assert.IsTrue(
            AppContext.TryGetSwitch(SwitchTemporalSqlite, out var activo) && activo,
            "El proceso de pruebas debe fijar el switch antes del primer uso de SQLite.");

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT '2026-04-15 13:45:30.1234567'";
        using var reader = cmd.ExecuteReader();
        Assert.IsTrue(reader.Read());

        var valor = reader.GetDateTimeOffset(0);
        var reloj = new DateTime(2026, 4, 15, 13, 45, 30, 123).AddTicks(4567);
        Assert.AreEqual(reloj.Ticks, valor.DateTime.Ticks,
            "Sin offset, la semántica previa conserva el reloj del texto.");
        Assert.AreEqual(TimeZoneInfo.Local.GetUtcOffset(reloj), valor.Offset,
            "Sin offset, la semántica previa asume la zona local de la máquina; " +
            $"observado: {valor.Offset} en zona {TimeZoneInfo.Local.Id}.");
    }

    [TestMethod]
    [DataRow(DateTimeKind.Unspecified)]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Utc)]
    public void EscrituraEf_FechaTexto_SeAlmacenaSinOffsetYLeeKindUnspecified(DateTimeKind kind)
    {
        var fecha = new DateTime(2026, 4, 15, 13, 45, 30, 123, kind);
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                ctx.Proyectos.Add(CrearProyecto(fecha));
                ctx.SaveChanges();
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaInicio);

                Assert.AreEqual("text", tipo, "EF8 debe persistir DateTime como TEXT.");
                Assert.IsNotNull(texto, "La fecha escrita no debe quedar NULL.");
                Assert.IsFalse(ContieneOffset(texto),
                    $"EF8 almacena DateTime sin offset; texto observado: '{texto}'.");

                Assert.AreEqual(fecha.Ticks, proyecto.FechaInicio.Ticks,
                    "El valor de reloj escrito debe conservarse exactamente.");
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaInicio.Kind,
                    "EF8 pierde el Kind al leer una fecha TEXT sin offset.");

                TestContext.WriteLine(
                    $"[EF8] Kind escrito={kind}; TEXT='{texto}'; Kind leído={proyecto.FechaInicio.Kind}; ticks={proyecto.FechaInicio.Ticks}");
            }
        }
        finally
        {
            BorrarDb(dbPath);
        }
    }

    [TestMethod]
    public void LecturaEf_FechaTextoConOffset_ConservaInstanteYReconoceZona()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                ctx.Proyectos.Add(CrearProyecto(new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Unspecified)));
                ctx.SaveChanges();
            }

            // 13:45:30.1234567+02:00 == 11:45:30.1234567Z; EF8 lo convierte a la zona local de la máquina.
            var esperadoUtc = new DateTime(2026, 4, 15, 11, 45, 30, 123, DateTimeKind.Utc).AddTicks(4567);
            var esperadoLocal = esperadoUtc.ToLocalTime();
            var formatos = new[]
            {
                "2026-04-15 13:45:30.1234567+02:00",
                "2026-04-15T13:45:30.1234567+02:00"
            };

            foreach (var textoConOffset in formatos)
            {
                using var ctx = new SOPROContext(dbPath);
                var proyectoId = ctx.Proyectos.Select(p => p.Id).Single();
                EjecutarSql(ctx, "UPDATE Proyectos SET FechaInicio = $fecha WHERE Id = $id",
                    ("$fecha", textoConOffset), ("$id", proyectoId));
                ctx.ChangeTracker.Clear();

                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.AreEqual(esperadoLocal.Ticks, proyecto.FechaInicio.Ticks,
                    $"EF8 convierte '{textoConOffset}' a la hora local de la máquina.");
                Assert.AreEqual(DateTimeKind.Local, proyecto.FechaInicio.Kind,
                    $"EF8 marca la fecha con offset como Local; observado: {proyecto.FechaInicio.Kind}.");

                TestContext.WriteLine(
                    $"[EF8] TEXT='{textoConOffset}'; leído={proyecto.FechaInicio:O}; Kind={proyecto.FechaInicio.Kind}; local esperado={esperadoLocal:O}");
            }
        }
        finally
        {
            BorrarDb(dbPath);
        }
    }

    [TestMethod]
    public void FechaNullable_NullYValor_RoundTripSinOffset()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                ctx.Proyectos.Add(CrearProyecto(
                    new DateTime(2026, 4, 15, 13, 45, 30, DateTimeKind.Unspecified),
                    fechaCalculoFsr: null));
                ctx.SaveChanges();
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                var proyecto = ctx.Proyectos.Single();
                Assert.IsNull(proyecto.FechaCalculoFSR);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaCalculoFsr);
                Assert.IsNull(texto);
                Assert.AreEqual("null", tipo);

                proyecto.FechaCalculoFSR =
                    new DateTime(2026, 4, 20, 9, 10, 11, 222, DateTimeKind.Unspecified);
                ctx.SaveChanges();
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.IsNotNull(proyecto.FechaCalculoFSR);
                Assert.AreEqual(new DateTime(2026, 4, 20, 9, 10, 11, 222).Ticks,
                    proyecto.FechaCalculoFSR.Value.Ticks);
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaCalculoFSR.Value.Kind);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaCalculoFsr);
                Assert.AreEqual("text", tipo);
                Assert.IsNotNull(texto);
                Assert.IsFalse(ContieneOffset(texto));
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                var proyectoId = ctx.Proyectos.Select(p => p.Id).Single();
                EjecutarSql(ctx, "UPDATE Proyectos SET FechaCalculoFSR = NULL WHERE Id = $id",
                    ("$id", proyectoId));
                ctx.ChangeTracker.Clear();

                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.IsNull(proyecto.FechaCalculoFSR);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaCalculoFsr);
                Assert.IsNull(texto);
                Assert.AreEqual("null", tipo);
            }
        }
        finally
        {
            BorrarDb(dbPath);
        }
    }

    [TestMethod]
    public void FixtureSintetica_CoincideConBaselineDeterminista()
    {
        var rutaCopiaFixture = (string?)null;

        try
        {
            using (var origen = ProyectoSinteticoFixture.OpenReadOnlyCopy())
            {
                rutaCopiaFixture = origen.DatabasePath;
                var proyecto = origen.Proyectos.AsNoTracking().Single();

                Assert.AreEqual(1, origen.Proyectos.Count());
                Assert.AreEqual(FixtureNombre, proyecto.Nombre);
                Assert.AreEqual(FixtureDescripcion, proyecto.Descripcion);
                Assert.AreEqual(FixtureFechaInicio.Ticks, proyecto.FechaInicio.Ticks);
                Assert.AreEqual(FixtureFechaTermino.Ticks, proyecto.FechaTermino.Ticks);
                Assert.AreEqual(FixtureFechaCreacion.Ticks, proyecto.FechaCreacion.Ticks);
                Assert.IsNull(proyecto.FechaCalculoFSR);
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaInicio.Kind);
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaCreacion.Kind);

                var (texto, tipo) = LeerFecha(origen, proyecto.Id, ColumnaFechaInicio);
                Assert.AreEqual("text", tipo);
                Assert.AreEqual(FixtureTextoFechaInicio, texto);
            }
        }
        finally
        {
            if (rutaCopiaFixture is not null)
                BorrarDb(rutaCopiaFixture);
        }
    }

    [TestMethod]
    public void CopiaSinteticaActual_AbreConIntegridadVersionYFechasYAdmiteEscritura()
    {
        var rutaFuente = RutaFixtureSintetico();
        var hashFuenteAntes = Sha256(rutaFuente);
        var rutaCopiaFixture = (string?)null;
        var rutaCopia = TestDbFactory.CreateTempDbPath();

        try
        {
            using (var origen = ProyectoSinteticoFixture.OpenReadOnlyCopy())
            {
                rutaCopiaFixture = origen.DatabasePath;
                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(origen),
                    "La copia actual debe estar sellada con la versión de esquema vigente.");
                Assert.AreEqual("ok", Integridad(origen),
                    "El fixture sintético rastreado debe estar íntegro.");
            }

            File.Copy(rutaFuente, rutaCopia, overwrite: true);

            using (var ctx = new SOPROContext(rutaCopia))
            {
                SchemaManager.EnsureCurrentSchema(ctx);

                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx),
                    "Una copia actual debe abrir sin degradar ni re-sellar la versión.");
                Assert.AreEqual("ok", Integridad(ctx), "La copia actual debe seguir íntegra.");

                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.AreEqual(FixtureNombre, proyecto.Nombre);
                Assert.AreEqual(FixtureDescripcion, proyecto.Descripcion);
                Assert.AreEqual(FixtureFechaInicio.Ticks, proyecto.FechaInicio.Ticks);
                Assert.AreEqual(FixtureFechaTermino.Ticks, proyecto.FechaTermino.Ticks);
                Assert.AreEqual(FixtureFechaCreacion.Ticks, proyecto.FechaCreacion.Ticks);
                Assert.IsNull(proyecto.FechaCalculoFSR);
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaInicio.Kind);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaInicio);
                Assert.AreEqual("text", tipo);
                Assert.AreEqual(FixtureTextoFechaInicio, texto,
                    "La copia debe conservar el TEXT exacto de la fecha del fixture.");
            }

            var fechaModificacionNueva =
                new DateTime(2026, 5, 1, 8, 30, 15, 250, DateTimeKind.Unspecified);
            var descripcionNueva = "Copia sintetica actual modificada";
            using (var ctx = new SOPROContext(rutaCopia))
            {
                var proyecto = ctx.Proyectos.Single();
                proyecto.Descripcion = descripcionNueva;
                proyecto.FechaModificacion = fechaModificacionNueva;
                ctx.SaveChanges();
            }

            using (var ctx = new SOPROContext(rutaCopia))
            {
                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.AreEqual(descripcionNueva, proyecto.Descripcion,
                    "La copia debe admitir escritura sin afectar al fixture fuente.");
                Assert.AreEqual(fechaModificacionNueva.Ticks, proyecto.FechaModificacion.Ticks);
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaModificacion.Kind);
                Assert.AreEqual(FixtureFechaInicio.Ticks, proyecto.FechaInicio.Ticks,
                    "La escritura de otro campo no debe alterar FechaInicio.");
                Assert.AreEqual("ok", Integridad(ctx));
            }

            Assert.AreEqual(hashFuenteAntes, Sha256(rutaFuente),
                "El fixture sintético rastreado no debe mutarse al abrir copias.");
        }
        finally
        {
            BorrarDb(rutaCopia);
            if (rutaCopiaFixture is not null)
                BorrarDb(rutaCopiaFixture);
        }
    }

    [TestMethod]
    public void CopiaSinteticaLegacy_MigraConservandoIntegridadDatosYFechas()
    {
        var rutaFuente = RutaFixtureSintetico();
        var rutaCopia = TestDbFactory.CreateTempDbPath();
        const string versionLegacy = "2026.04.4";

        try
        {
            File.Copy(rutaFuente, rutaCopia, overwrite: true);

            // Las comparaciones antes/después solo verifican preservación durante la
            // migración; los valores canónicos están fijados en el test de baseline.
            DateTime fechaInicioAntes;
            DateTime fechaTerminoAntes;
            DateTime? fechaCalculoFsrAntes;
            string? textoFechaInicioAntes;
            string descripcionAntes;
            int maquinariaAntes;
            int maquinariaId;
            int maquinariaMaxId;

            using (var ctx = new SOPROContext(rutaCopia))
            {
                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                fechaInicioAntes = proyecto.FechaInicio;
                fechaTerminoAntes = proyecto.FechaTermino;
                fechaCalculoFsrAntes = proyecto.FechaCalculoFSR;
                descripcionAntes = proyecto.Descripcion;
                maquinariaAntes = ctx.Maquinaria.Count();
                maquinariaId = ctx.Maquinaria.AsNoTracking()
                    .OrderBy(m => m.Id)
                    .Select(m => m.Id)
                    .First();
                maquinariaMaxId = ctx.Maquinaria.Max(m => m.Id);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaInicio);
                textoFechaInicioAntes = texto;
                Assert.AreEqual("text", tipo);
                Assert.IsNotNull(texto);
            }

            SimularEsquemaLegacy(rutaCopia, versionLegacy);

            using (var ctx = new SOPROContext(rutaCopia))
            {
                Assert.AreEqual(versionLegacy, SchemaManager.GetVersionRegistrada(ctx),
                    "La copia debe quedar sellada con la versión legacy antes de migrar.");
                Assert.IsFalse(TablaExiste(ctx, "FormatosCeldaReporte"));
                Assert.IsFalse(TablaExiste(ctx, "ConfiguracionesEstiloFilaReporte"));
                Assert.IsFalse(ColumnaExiste(ctx, "Maquinaria", "MaterialCombustibleId"));
                Assert.IsFalse(ColumnaExiste(ctx, "Maquinaria", "MaterialAceiteId"));
                Assert.IsFalse(ColumnaExiste(ctx, "Maquinaria", "ManoDeObraOperadorId"));

                Assert.IsTrue(EsClavePrimaria(ctx, "Maquinaria", "Id"),
                    "La recreación legacy debe conservar la PK de Maquinaria.");
                Assert.IsTrue(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_ProyectoId_Clave"),
                    "La recreación legacy debe conservar el índice único existente.");
                Assert.IsTrue(EsIndiceUnico(ctx, "Maquinaria", "IX_Maquinaria_ProyectoId_Clave"));
                Assert.IsFalse(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_MaterialCombustibleId"));
                Assert.IsFalse(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_MaterialAceiteId"));
                Assert.IsFalse(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_ManoDeObraOperadorId"));

                var clavesForaneasLegacy = LeerClavesForaneas(ctx, "Maquinaria");
                Assert.IsTrue(clavesForaneasLegacy.Contains(("Proyectos", "ProyectoId", "CASCADE")),
                    "La recreación legacy debe conservar la FK a Proyectos con CASCADE.");
                Assert.IsFalse(clavesForaneasLegacy.Any(f =>
                        f.Columna is "MaterialCombustibleId" or "MaterialAceiteId" or "ManoDeObraOperadorId"),
                    "La recreación legacy no debe conservar las FK de las columnas M015 ausentes.");
                Assert.AreEqual(0, ContarViolacionesForeignKeys(ctx));

                Assert.AreEqual(maquinariaAntes, ContarFilas(ctx, "Maquinaria"),
                    "La simulación legacy no debe perder filas de Maquinaria.");
                Assert.AreEqual("ok", Integridad(ctx));

                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.AreEqual(fechaInicioAntes.Ticks, proyecto.FechaInicio.Ticks);
            }

            using (var ctx = new SOPROContext(rutaCopia))
            {
                SchemaManager.EnsureCurrentSchema(ctx);

                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(ctx),
                    "La apertura de la copia legacy debe sellar la versión vigente.");
                Assert.AreEqual(1, ContarVersion(ctx),
                    "No debe quedar más de una fila de versión tras migrar la copia legacy.");
                Assert.IsTrue(TablaExiste(ctx, "FormatosCeldaReporte"));
                Assert.IsTrue(TablaExiste(ctx, "ConfiguracionesEstiloFilaReporte"));
                Assert.IsTrue(ColumnaExiste(ctx, "Maquinaria", "MaterialCombustibleId"));
                Assert.IsTrue(ColumnaExiste(ctx, "Maquinaria", "MaterialAceiteId"));
                Assert.IsTrue(ColumnaExiste(ctx, "Maquinaria", "ManoDeObraOperadorId"));

                Assert.IsTrue(EsClavePrimaria(ctx, "Maquinaria", "Id"),
                    "La migración debe conservar la PK de Maquinaria.");
                Assert.IsTrue(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_ProyectoId_Clave"));
                Assert.IsTrue(EsIndiceUnico(ctx, "Maquinaria", "IX_Maquinaria_ProyectoId_Clave"));
                Assert.IsTrue(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_MaterialCombustibleId"));
                Assert.IsTrue(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_MaterialAceiteId"));
                Assert.IsTrue(IndiceExiste(ctx, "Maquinaria", "IX_Maquinaria_ManoDeObraOperadorId"));

                var clavesForaneas = LeerClavesForaneas(ctx, "Maquinaria");
                Assert.IsTrue(clavesForaneas.Contains(("Proyectos", "ProyectoId", "CASCADE")));
                Assert.IsTrue(clavesForaneas.Contains(("Materiales", "MaterialCombustibleId", "RESTRICT")));
                Assert.IsTrue(clavesForaneas.Contains(("Materiales", "MaterialAceiteId", "RESTRICT")));
                Assert.IsTrue(clavesForaneas.Contains(("ManoDeObra", "ManoDeObraOperadorId", "RESTRICT")));
                Assert.AreEqual(0, ContarViolacionesForeignKeys(ctx));
                Assert.AreEqual("ok", Integridad(ctx));

                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.AreEqual(descripcionAntes, proyecto.Descripcion,
                    "La migración de la copia legacy debe conservar los datos del proyecto.");
                Assert.AreEqual(fechaInicioAntes.Ticks, proyecto.FechaInicio.Ticks);
                Assert.AreEqual(fechaTerminoAntes.Ticks, proyecto.FechaTermino.Ticks);
                Assert.AreEqual(fechaCalculoFsrAntes, proyecto.FechaCalculoFSR);
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaInicio.Kind);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaInicio);
                Assert.AreEqual("text", tipo);
                Assert.AreEqual(textoFechaInicioAntes, texto,
                    "La migración no debe reescribir el TEXT de la fecha.");

                Assert.AreEqual(maquinariaAntes, ctx.Maquinaria.Count(),
                    "La migración debe conservar las filas de Maquinaria.");

                var maquinaria = ctx.Maquinaria.Single(m => m.Id == maquinariaId);
                maquinaria.Descripcion = "Maquinaria legacy editada";
                maquinaria.Notas = "Escritura posterior a migracion";
                maquinaria.FechaModificacion =
                    new DateTime(2026, 5, 2, 9, 0, 0, DateTimeKind.Unspecified);

                var maquinariaNueva = new Maquinaria
                {
                    Clave = "LEGACY-WRITE",
                    Descripcion = "Maquinaria insertada tras migrar",
                    Notas = string.Empty,
                    ProyectoId = maquinaria.ProyectoId
                };
                ctx.Maquinaria.Add(maquinariaNueva);
                ctx.SaveChanges();
                Assert.IsTrue(maquinariaNueva.Id > maquinariaMaxId,
                    "La PK AUTOINCREMENT de Maquinaria debe seguir asignando Id sobre el máximo previo.");
            }

            using (var ctx = new SOPROContext(rutaCopia))
            {
                var editada = ctx.Maquinaria.AsNoTracking().Single(m => m.Id == maquinariaId);
                Assert.AreEqual("Maquinaria legacy editada", editada.Descripcion);
                Assert.AreEqual("Escritura posterior a migracion", editada.Notas);
                Assert.AreEqual(DateTimeKind.Unspecified, editada.FechaModificacion.Kind);

                var insertada = ctx.Maquinaria.AsNoTracking().Single(m => m.Clave == "LEGACY-WRITE");
                Assert.AreEqual("Maquinaria insertada tras migrar", insertada.Descripcion);
                Assert.AreEqual(maquinariaAntes + 1, ctx.Maquinaria.Count());
                Assert.AreEqual(0, ContarViolacionesForeignKeys(ctx));
                Assert.AreEqual("ok", Integridad(ctx));
            }
        }
        finally
        {
            BorrarDb(rutaCopia);
        }
    }

    [TestMethod]
    public void TransaccionEfRollback_NoPersisteCambiosDeFechaNiFilas()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        var fechaOriginal = new DateTime(2026, 4, 15, 13, 45, 30, 123, DateTimeKind.Unspecified);
        var fechaEnTransaccion = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);

        try
        {
            string? textoOriginal;
            int proyectoId;

            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureCreated();
                var proyecto = CrearProyecto(fechaOriginal, nombre: "Proyecto rollback");
                ctx.Proyectos.Add(proyecto);
                ctx.SaveChanges();
                proyectoId = proyecto.Id;

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaInicio);
                textoOriginal = texto;
                Assert.AreEqual("text", tipo);
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                using var transaccion = ctx.Database.BeginTransaction();
                var proyecto = ctx.Proyectos.Single(p => p.Id == proyectoId);
                proyecto.FechaInicio = fechaEnTransaccion;
                ctx.Proyectos.Add(CrearProyecto(
                    fechaEnTransaccion, nombre: "Proyecto solo en transaccion"));
                ctx.SaveChanges();
                transaccion.Rollback();
            }

            using (var ctx = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, ctx.Proyectos.Count(),
                    "El rollback no debe dejar filas insertadas en la transacción.");

                var proyecto = ctx.Proyectos.AsNoTracking().Single();
                Assert.AreEqual(fechaOriginal.Ticks, proyecto.FechaInicio.Ticks,
                    "El rollback debe restaurar la fecha original.");
                Assert.AreEqual(DateTimeKind.Unspecified, proyecto.FechaInicio.Kind);

                var (texto, tipo) = LeerFecha(ctx, proyecto.Id, ColumnaFechaInicio);
                Assert.AreEqual("text", tipo);
                Assert.AreEqual(textoOriginal, texto,
                    "El TEXT de la fecha debe volver al valor confirmado antes de la transacción.");
                Assert.AreEqual("ok", Integridad(ctx));
            }
        }
        finally
        {
            BorrarDb(dbPath);
        }
    }

    private static Proyecto CrearProyecto(
        DateTime fechaInicio,
        DateTime? fechaCalculoFsr = null,
        string nombre = "Proyecto compatibilidad EF")
        => new()
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = fechaInicio,
            FechaTermino = new DateTime(2026, 8, 27, 14, 33, 7, 543, DateTimeKind.Unspecified),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCalculoFSR = fechaCalculoFsr,
            FechaCreacion = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified),
            FechaModificacion = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified)
        };

    private static string RutaFixtureSintetico()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "TestData", FixtureSintetico);
        Assert.IsTrue(File.Exists(ruta), $"No se encontró el fixture sintético: {ruta}");
        return ruta;
    }

    private static (string? Texto, string Tipo) LeerFecha(
        SOPROContext ctx, int proyectoId, string columna)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT {columna}, typeof({columna}) FROM Proyectos WHERE Id = $id";
            var parametro = cmd.CreateParameter();
            parametro.ParameterName = "$id";
            parametro.Value = proyectoId;
            cmd.Parameters.Add(parametro);

            using var reader = cmd.ExecuteReader();
            Assert.IsTrue(reader.Read(), $"No se encontró el proyecto {proyectoId}.");
            var texto = reader.IsDBNull(0) ? null : reader.GetString(0);
            return (texto, reader.GetString(1));
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static void EjecutarSql(
        SOPROContext ctx, string sql, params (string Nombre, object Valor)[] parametros)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (nombre, valor) in parametros)
            {
                var parametro = cmd.CreateParameter();
                parametro.ParameterName = nombre;
                parametro.Value = valor;
                cmd.Parameters.Add(parametro);
            }

            cmd.ExecuteNonQuery();
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static string Integridad(SOPROContext ctx)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static int ContarVersion(SOPROContext ctx)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%'";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static int ContarFilas(SOPROContext ctx, string tabla)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [{tabla}]";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static bool TablaExiste(SOPROContext ctx, string tabla)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$nombre";
            var parametro = cmd.CreateParameter();
            parametro.ParameterName = "$nombre";
            parametro.Value = tabla;
            cmd.Parameters.Add(parametro);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static bool ColumnaExiste(SOPROContext ctx, string tabla, string columna)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info($tabla) WHERE name=$columna";
            var pTabla = cmd.CreateParameter();
            pTabla.ParameterName = "$tabla";
            pTabla.Value = tabla;
            cmd.Parameters.Add(pTabla);
            var pColumna = cmd.CreateParameter();
            pColumna.ParameterName = "$columna";
            pColumna.Value = columna;
            cmd.Parameters.Add(pColumna);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static void SimularEsquemaLegacy(string dbPath, string versionLegacy)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();

        var columnas = new List<string>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM pragma_table_info('Maquinaria') ORDER BY cid";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var nombre = reader.GetString(0);
                if (ColumnasM015Maquinaria.Contains(nombre)) continue;
                columnas.Add(nombre);
            }
        }

        string ddl;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Maquinaria'";
            ddl = cmd.ExecuteScalar() as string
                ?? throw new InvalidOperationException("No se encontró el DDL de Maquinaria.");
        }

        var indices = LeerIndicesValidos(connection, ColumnasM015Maquinaria);
        var columnasSql = string.Join(", ", columnas.Select(ColumnaSql));
        var ddlLegacy = ConstruirDdlLegacyMaquinaria(ddl, ColumnasM015Maquinaria);

        EjecutarComando(connection, "PRAGMA foreign_keys = OFF;");
        using (var transaccion = connection.BeginTransaction())
        {
            EjecutarComando(connection, "DROP TABLE IF EXISTS FormatosCeldaReporte;", transaccion);
            EjecutarComando(connection, "DROP TABLE IF EXISTS ConfiguracionesEstiloFilaReporte;", transaccion);
            EjecutarComando(connection, "DROP TABLE IF EXISTS \"Maquinaria_SIN_VINCULOS\";", transaccion);
            EjecutarComando(connection, ddlLegacy, transaccion);
            EjecutarComando(connection,
                $"INSERT INTO \"Maquinaria_SIN_VINCULOS\" ({columnasSql}) SELECT {columnasSql} FROM \"Maquinaria\";",
                transaccion);
            EjecutarComando(connection, "DROP TABLE \"Maquinaria\";", transaccion);
            EjecutarComando(connection,
                "ALTER TABLE \"Maquinaria_SIN_VINCULOS\" RENAME TO \"Maquinaria\";", transaccion);
            foreach (var indice in indices)
                EjecutarComando(connection, indice, transaccion);
            EjecutarComando(connection,
                "DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';", transaccion);
            EjecutarComando(connection,
                "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ($version, '2026-04-04T00:00:00.0000000');",
                transaccion, ("$version", $"SchemaVersion_{versionLegacy}"));
            transaccion.Commit();
        }
        EjecutarComando(connection, "PRAGMA foreign_keys = ON;");
    }

    private static string ConstruirDdlLegacyMaquinaria(
        string ddl, IReadOnlyCollection<string> columnasExcluidas)
    {
        var apertura = ddl.IndexOf('(');
        var cierre = ddl.LastIndexOf(')');
        Assert.IsTrue(apertura > 0 && cierre > apertura,
            "No se pudo interpretar el DDL de Maquinaria.");

        var cuerpo = ddl.Substring(apertura + 1, cierre - apertura - 1);
        var definiciones = SepararDefinicionesDdl(cuerpo)
            .Where(definicion => definicion.Length > 0)
            .Where(definicion => !columnasExcluidas.Any(columna =>
                EsDefinicionDeColumna(definicion, columna)))
            .ToList();
        Assert.IsTrue(definiciones.Count > 0, "El DDL legacy quedó sin definiciones.");

        return $"CREATE TABLE \"Maquinaria_SIN_VINCULOS\" ({string.Join(",\n    ", definiciones)});";
    }

    private static List<string> SepararDefinicionesDdl(string cuerpo)
    {
        var definiciones = new List<string>();
        var inicio = 0;
        var profundidad = 0;
        var comilla = '\0';

        for (var i = 0; i < cuerpo.Length; i++)
        {
            var actual = cuerpo[i];
            if (comilla != '\0')
            {
                if (actual != comilla) continue;
                if (i + 1 < cuerpo.Length && cuerpo[i + 1] == comilla)
                {
                    i++;
                    continue;
                }
                comilla = '\0';
                continue;
            }

            if (actual is '\'' or '"' or '`')
            {
                comilla = actual;
                continue;
            }
            if (actual == '[')
            {
                comilla = ']';
                continue;
            }

            if (actual == '(') profundidad++;
            else if (actual == ')') profundidad--;
            else if (actual == ',' && profundidad == 0)
            {
                definiciones.Add(cuerpo.Substring(inicio, i - inicio).Trim());
                inicio = i + 1;
            }
        }

        definiciones.Add(cuerpo.Substring(inicio).Trim());
        return definiciones;
    }

    private static bool EsDefinicionDeColumna(string definicion, string columna)
    {
        var texto = definicion.TrimStart();
        return texto.StartsWith($"\"{columna}\"", StringComparison.OrdinalIgnoreCase)
            || texto.StartsWith($"[{columna}]", StringComparison.OrdinalIgnoreCase)
            || texto.StartsWith($"`{columna}`", StringComparison.OrdinalIgnoreCase)
            || texto.StartsWith(columna + " ", StringComparison.OrdinalIgnoreCase)
            || texto.StartsWith(columna + "\t", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> LeerIndicesValidos(
        SqliteConnection connection, IReadOnlyCollection<string> columnasExcluidas)
    {
        var definiciones = new List<(string Nombre, string Sql)>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT name, sql FROM sqlite_master " +
                "WHERE type='index' AND tbl_name='Maquinaria' AND sql IS NOT NULL ORDER BY name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                definiciones.Add((reader.GetString(0), reader.GetString(1)));
        }

        var indices = new List<string>();
        foreach (var (nombre, sql) in definiciones)
        {
            if (IndiceReferenciaColumnas(connection, nombre, columnasExcluidas)) continue;
            indices.Add(sql);
        }
        return indices;
    }

    private static bool IndiceReferenciaColumnas(
        SqliteConnection connection, string indice, IReadOnlyCollection<string> columnas)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_index_info($indice)";
        var parametro = cmd.CreateParameter();
        parametro.ParameterName = "$indice";
        parametro.Value = indice;
        cmd.Parameters.Add(parametro);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0)) continue;
            if (columnas.Contains(reader.GetString(0))) return true;
        }
        return false;
    }

    private static string ColumnaSql(string nombre)
        => "[" + nombre.Replace("]", "]]") + "]";

    private static bool EsClavePrimaria(SOPROContext ctx, string tabla, string columna)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info($tabla) WHERE name=$columna AND pk > 0";
            var pTabla = cmd.CreateParameter();
            pTabla.ParameterName = "$tabla";
            pTabla.Value = tabla;
            cmd.Parameters.Add(pTabla);
            var pColumna = cmd.CreateParameter();
            pColumna.ParameterName = "$columna";
            pColumna.Value = columna;
            cmd.Parameters.Add(pColumna);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static bool IndiceExiste(SOPROContext ctx, string tabla, string indice)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND tbl_name=$tabla AND name=$indice";
            var pTabla = cmd.CreateParameter();
            pTabla.ParameterName = "$tabla";
            pTabla.Value = tabla;
            cmd.Parameters.Add(pTabla);
            var pIndice = cmd.CreateParameter();
            pIndice.ParameterName = "$indice";
            pIndice.Value = indice;
            cmd.Parameters.Add(pIndice);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static bool EsIndiceUnico(SOPROContext ctx, string tabla, string indice)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM pragma_index_list($tabla) WHERE name=$indice AND [unique] = 1";
            var pTabla = cmd.CreateParameter();
            pTabla.ParameterName = "$tabla";
            pTabla.Value = tabla;
            cmd.Parameters.Add(pTabla);
            var pIndice = cmd.CreateParameter();
            pIndice.ParameterName = "$indice";
            pIndice.Value = indice;
            cmd.Parameters.Add(pIndice);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static List<(string Tabla, string Columna, string OnDelete)> LeerClavesForaneas(
        SOPROContext ctx, string tabla)
    {
        var claves = new List<(string Tabla, string Columna, string OnDelete)>();
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT [table], [from], on_delete FROM pragma_foreign_key_list($tabla)";
            var parametro = cmd.CreateParameter();
            parametro.ParameterName = "$tabla";
            parametro.Value = tabla;
            cmd.Parameters.Add(parametro);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                claves.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        finally
        {
            if (abrir) connection.Close();
        }

        return claves;
    }

    private static int ContarViolacionesForeignKeys(SOPROContext ctx)
    {
        var connection = ctx.Database.GetDbConnection();
        var abrir = connection.State != System.Data.ConnectionState.Open;
        if (abrir) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_foreign_key_check;";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (abrir) connection.Close();
        }
    }

    private static void EjecutarComando(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? transaccion = null,
        params (string Nombre, object Valor)[] parametros)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaccion;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            var parametro = cmd.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor;
            cmd.Parameters.Add(parametro);
        }
        cmd.ExecuteNonQuery();
    }

    private static bool ContieneOffset(string texto)
    {
        if (texto.EndsWith("Z", StringComparison.Ordinal)) return true;
        if (texto.Contains('+')) return true;

        var inicioHora = texto.IndexOf('T');
        if (inicioHora < 0) inicioHora = texto.IndexOf(' ');
        if (inicioHora < 0) return false;

        return texto.IndexOf('-', inicioHora + 1) >= 0;
    }

    private static string Sha256(string ruta)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta)));

    private static void BorrarDb(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var sufijo in new[] { string.Empty, "-wal", "-shm" })
        {
            var ruta = dbPath + sufijo;
            if (!File.Exists(ruta)) continue;
            File.SetAttributes(ruta, FileAttributes.Normal);
            File.Delete(ruta);
        }
    }
}
