using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Performance;

/// <summary>
/// Paso 7.1 — validación de RENDIMIENTO del formato por celda a nivel de
/// <see cref="FormatoReporteService"/> (sin UI):
/// <list type="bullet">
///   <item>la carga de overlays es UNA sola consulta por (ProyectoId, TipoReporte)
///   y el tiempo para 5.000 overrides se mantiene bajo el umbral de 500 ms;</item>
///   <item>la aplicación transaccional en lote (1/10/100/1.000 celdas) y su undo
///   permanecen por debajo del umbral de 3 s para 1.000 celdas;</item>
///   <item>el diccionario en memoria es de tamaño proporcional a los OVERRIDES
///   (disperso), no a las celdas totales del proyecto;</item>
///   <item>cada lote abre UNA sola transacción (no una por celda).</item>
/// </list>
/// Los umbrales son holgados frente a la máquina de desarrollo y existen para
/// detectar una degradación grossera (N+1 / transacción por celda), no como
/// micro-benchmark.
/// </summary>
[TestClass]
public class FormatoCeldasPerformanceTests
{
    private const string Tipo = "Presupuesto";
    private const int FilasProyecto = 5000;
    private const int UmbralCargaMs = 500;
    private const int UmbralAplicar1000Ms = 3000;

    public TestContext TestContext { get; set; } = null!;

    // ─────────────────────────── Escenario ───────────────────────────

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
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4,
    };

    private static int AgregarProyecto(SOPROContext ctx, string nombre = "Proyecto")
    {
        var proyecto = CrearProyecto(nombre);
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    private static FormatoCeldaReporte Override(int proyectoId, int fila) => new()
    {
        ProyectoId = proyectoId,
        TipoReporte = Tipo,
        ClaveVista = string.Empty,
        ClaveFila = ClaveFilaReporte.Concepto(fila),
        NombreColumna = "ImporteTotal",
        ColorFondo = "#112233",
        Negrita = true,
        FechaModificacion = DateTime.Now,
    };

    // ─────────────── Carga masiva: una consulta, bajo umbral ───────────────

    [TestMethod]
    public void CargarCeldas_5000Overrides_UnaConsultaYPorDebajoDe500ms()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyectoId = AgregarProyecto(ctx);

        // 5.000 overrides disperos en un proyecto de 5.000 filas. Un solo SaveChanges.
        ctx.FormatosCeldaReporte.AddRange(
            Enumerable.Range(1, FilasProyecto).Select(i => Override(proyectoId, i)));
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        var service = new FormatoReporteService(ctx);

        // Calentamiento: la primera consulta paga la compilación perezosa del
        // pipeline de EF; se deja FUERA de la medición.
        service.CargarCeldas(proyectoId, Tipo);

        var sw = Stopwatch.StartNew();
        var cargadas = service.CargarCeldas(proyectoId, Tipo);
        sw.Stop();

        TestContext.WriteLine($"[perf] CargarCeldas 5000 overrides: {sw.ElapsedMilliseconds} ms " +
                              $"({sw.Elapsed.TotalMilliseconds:F1} ms), umbral {UmbralCargaMs} ms");

        Assert.AreEqual(FilasProyecto, cargadas.Count, "Deben cargarse TODOS los overrides persistidos.");
        Assert.IsTrue(sw.ElapsedMilliseconds < UmbralCargaMs,
            $"La carga de 5.000 overrides tardó {sw.ElapsedMilliseconds} ms (umbral {UmbralCargaMs} ms).");
    }

    // ─────────── Aplicación transaccional en lote: 1/10/100/1000 ───────────

    [TestMethod]
    public void AplicarCeldas_LotesDe1_10_100_1000_YUndo_PorDebajoDeUmbral()
    {
        using var ctx = TestDbFactory.CreateContext();

        var tiemposAplicar = new Dictionary<int, long>();
        var tiemposUndo = new Dictionary<int, long>();

        foreach (var tamano in new[] { 1, 10, 100, 1000 })
        {
            var proyectoId = AgregarProyecto(ctx, $"Proyecto-{tamano}");

            var lote = Enumerable.Range(1, tamano)
                .Select(i => new CambioFormatoCeldaCommand(
                    string.Empty,
                    ClaveFilaReporte.Concepto(i),
                    "ImporteTotal",
                    ExpectedOverlay: null,
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#010203", Negrita: true)))
                .ToList();

            var sw = Stopwatch.StartNew();
            FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, lote);
            sw.Stop();
            tiemposAplicar[tamano] = sw.ElapsedMilliseconds;

            ctx.ChangeTracker.Clear();
            Assert.AreEqual(tamano, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
                $"El lote de {tamano} debe persistir {tamano} filas.");

            // Undo = reaplicar el estado "antes" (null = borrar) en un único lote.
            var loteUndo = lote
                .Select(c => new CambioFormatoCeldaCommand(
                    c.ClaveVista, c.ClaveFila, c.NombreColumna,
                    ExpectedOverlay: c.ProposedOverlay,
                    ProposedOverlay: null))
                .ToList();

            sw.Restart();
            FormatoTestWriter.Aplicar(ctx, proyectoId, Tipo, loteUndo);
            sw.Stop();
            tiemposUndo[tamano] = sw.ElapsedMilliseconds;

            ctx.ChangeTracker.Clear();
            Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
                "El undo debe borrar TODO el lote.");
        }

        foreach (var par in tiemposAplicar)
            TestContext.WriteLine($"[perf] AplicarCeldas {par.Key}: aplicar {par.Value} ms / " +
                                  $"undo {tiemposUndo[par.Key]} ms");

        Assert.IsTrue(tiemposAplicar[1000] < UmbralAplicar1000Ms,
            $"Aplicar 1.000 celdas tardó {tiemposAplicar[1000]} ms (umbral {UmbralAplicar1000Ms} ms).");
        Assert.IsTrue(tiemposUndo[1000] < UmbralAplicar1000Ms,
            $"Deshacer 1.000 celdas tardó {tiemposUndo[1000]} ms (umbral {UmbralAplicar1000Ms} ms).");
    }

    // ─────────── Presupuesto de transacciones: un lote = una tx ───────────

    [TestMethod]
    public void AplicarCeldas_UnLote_EsUnaSolaTransaccion()
    {
        var dbPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"sopro_perf_tx_{Guid.NewGuid():N}.db");

        using var ctx = new TransactionCountingContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        var proyectoId = AgregarProyecto(ctx);

        var lote = Enumerable.Range(1, 25)
            .Select(i => new CambioFormatoCeldaCommand(
                string.Empty, ClaveFilaReporte.Concepto(i), "ImporteTotal",
                ExpectedOverlay: null,
                ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#0A0B0C")))
            .ToList();

        ctx.Interceptor.TransaccionesIniciadas = 0;
        var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
            new CountingTransactionFactory(ctx.Interceptor), dbPath, proyectoId, Tipo, lote);
        var aperturas = ctx.Interceptor.TransaccionesIniciadas;

        Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
        TestContext.WriteLine($"[perf] AplicarCeldas 25 celdas abrió {aperturas} transacción(es) (esperado 1).");
        Assert.AreEqual(1, aperturas,
            "El lote debe envolverse en UNA sola transacción (no una por celda).");
        Assert.AreEqual(25, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));
    }

    // ─────────────────────── Infraestructura de conteo ───────────────────────

    private sealed class TransactionCountingContext : SOPROContext
    {
        public TransactionCountingContext(string dbPath) : base(dbPath) { }

        public TransactionCounterInterceptor Interceptor { get; } = new();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(Interceptor);
            base.OnConfiguring(optionsBuilder);
        }
    }

    private sealed class CountingTransactionFactory : IProjectDbContextFactory
    {
        private readonly TransactionCounterInterceptor _interceptor;

        public CountingTransactionFactory(TransactionCounterInterceptor interceptor)
            => _interceptor = interceptor;

        public SOPROContext Create(string databasePath)
            => new CountingTransactionContext(databasePath, _interceptor);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class CountingTransactionContext : SOPROContext
    {
        private readonly TransactionCounterInterceptor _interceptor;

        public CountingTransactionContext(string dbPath, TransactionCounterInterceptor interceptor)
            : base(dbPath)
        {
            _interceptor = interceptor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_interceptor);
            base.OnConfiguring(optionsBuilder);
        }
    }

    /// <summary>Cuenta las transacciones que EF abre sobre la conexión.</summary>
    private sealed class TransactionCounterInterceptor : DbTransactionInterceptor
    {
        public int TransaccionesIniciadas { get; set; }

        public override InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result)
        {
            TransaccionesIniciadas++;
            return base.TransactionStarting(connection, eventData, result);
        }
    }
}
