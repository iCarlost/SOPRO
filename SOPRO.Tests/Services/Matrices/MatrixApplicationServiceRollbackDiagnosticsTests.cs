using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Matrices;

/// <summary>
/// Issue #144: cuando la reversión de la guarda de matriz falla, la operación emite
/// exactamente un evento estructurado (<c>GuardarMatrizRollbackFallido</c>) con la
/// correlación de la guarda y conserva la excepción primaria original. El sink es
/// best-effort: no altera la transacción, el rollback ni el throw. Una reversión
/// correcta y una guarda exitosa no emiten nada. Los fallos se inducen con triggers
/// de SQLite y con <see cref="TransactionProbeInterceptor"/>.
/// </summary>
[TestClass]
public class MatrixApplicationServiceRollbackDiagnosticsTests
{
    private const string VersionPrueba = "1.19.2";
    private const string MensajeFalloPrimario = "fallo inyectado al actualizar la matriz";
    private const string FuenteEsperada = "SOPRO.Application";
    private const string OperacionEsperada = "GuardarMatrizRollbackFallido";
    private const decimal CostoDirectoOriginal = 100m;
    private const decimal CostoDirectoEditado = 350m;

    [TestMethod]
    public async Task SaveAsync_FalloPrimarioYDeRollback_EmiteUnSoloErrorYConservaLaExcepcionPrimaria()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, crearTriggerDeFallo: true);
            const string mensajeRollbackPrivado = "rollback_datos_privados";
            var probe = new TransactionProbeInterceptor
            {
                FailRollback = true,
                RollbackFailureMessage = mensajeRollbackPrivado
            };
            using var operationContext = new ProbeContext(dbPath, probe);
            var sink = new InMemoryDiagnosticsSink();

            var ex = await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
                MatrixApplicationService.SaveAsync(
                    new FixedContextFactory(operationContext),
                    dbPath,
                    escenario.Dto,
                    escenario.Matriz,
                    sessionContext: null,
                    diagnosticsSink: sink,
                    productVersion: VersionPrueba));

            Assert.AreEqual(1, probe.IntentosDeRollback,
                "La operación debe intentar la reversión una sola vez.");
            Assert.AreEqual(0, probe.Commits);
            StringAssert.Contains(ex.InnerException?.Message ?? string.Empty, MensajeFalloPrimario,
                "La excepción primaria original debe llegar al llamador.");

            var evento = sink.Snapshot().Single();
            Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
            Assert.AreEqual(FuenteEsperada, evento.Source);
            Assert.AreEqual(OperacionEsperada, evento.Operation);
            Assert.AreEqual(VersionPrueba, evento.ProductVersion);
            Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId),
                $"Correlación inválida: '{evento.CorrelationId}'.");
            Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType,
                "El evento describe el fallo de reversión, no el primario.");

            var payload = JsonSerializer.Serialize(evento);
            foreach (var textoPrivado in new[]
            {
                mensajeRollbackPrivado,
                MensajeFalloPrimario,
                escenario.Dto.Clave,
                escenario.Dto.Descripcion,
                dbPath
            })
            {
                Assert.IsFalse(payload.Contains(textoPrivado, StringComparison.Ordinal),
                    $"El evento no debe contener texto de excepción, negocio ni ruta: {textoPrivado}.");
            }
        }
        finally
        {
            EliminarBase(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_FalloPrimarioConRollbackCorrecto_NoEmiteEventos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, crearTriggerDeFallo: true);
            var probe = new TransactionProbeInterceptor();
            using var operationContext = new ProbeContext(dbPath, probe);
            var sink = new InMemoryDiagnosticsSink();

            await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
                MatrixApplicationService.SaveAsync(
                    new FixedContextFactory(operationContext),
                    dbPath,
                    escenario.Dto,
                    escenario.Matriz,
                    sessionContext: null,
                    diagnosticsSink: sink,
                    productVersion: VersionPrueba));

            Assert.AreEqual(1, probe.Rollbacks, "La reversión debe completarse.");
            Assert.AreEqual(0, sink.Count,
                "Una reversión confirmada no es una incidencia de diagnóstico.");
        }
        finally
        {
            EliminarBase(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_GuardaExitosa_NoEmiteEventos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, crearTriggerDeFallo: false);
            var probe = new TransactionProbeInterceptor();
            using var operationContext = new ProbeContext(dbPath, probe);
            var sink = new InMemoryDiagnosticsSink();

            var result = await MatrixApplicationService.SaveAsync(
                new FixedContextFactory(operationContext),
                dbPath,
                escenario.Dto,
                escenario.Matriz,
                sessionContext: null,
                diagnosticsSink: sink,
                productVersion: VersionPrueba);

            Assert.AreEqual(escenario.Matriz.Id, result.MatrixId);
            Assert.AreEqual(1, probe.Commits);
            Assert.AreEqual(0, sink.Count, "Una guarda exitosa no emite eventos.");
        }
        finally
        {
            EliminarBase(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_SinkQueLanza_NoAlteraLaExcepcionPrimariaNiLaReversion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            var escenario = CrearEscenario(dbPath, crearTriggerDeFallo: true);
            var probe = new TransactionProbeInterceptor { FailRollback = true };
            using var operationContext = new ProbeContext(dbPath, probe);
            var sink = new ThrowingDiagnosticsSink();

            var ex = await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
                MatrixApplicationService.SaveAsync(
                    new FixedContextFactory(operationContext),
                    dbPath,
                    escenario.Dto,
                    escenario.Matriz,
                    sessionContext: null,
                    diagnosticsSink: sink,
                    productVersion: VersionPrueba));

            StringAssert.Contains(ex.InnerException?.Message ?? string.Empty, MensajeFalloPrimario,
                "Un sink defectuoso nunca reemplaza la excepción primaria.");
            Assert.AreEqual(1, probe.IntentosDeRollback);
            Assert.AreEqual(0, probe.Commits);
            Assert.AreEqual(1, sink.Escrituras,
                "El evento se intenta escribir una sola vez.");
            Assert.AreEqual(1, sink.Vaciados,
                "El vaciado es best-effort e independiente de la escritura.");
        }
        finally
        {
            EliminarBase(dbPath);
        }
    }

    // ── Escenario ─────────────────────────────────────────────────────────────

    private sealed record Escenario(Matriz Matriz, MatrixEditDto Dto);

    private static Escenario CrearEscenario(string dbPath, bool crearTriggerDeFallo)
    {
        int proyectoId;
        int matrizId;
        int materialId;

        using (var setup = TestDbFactory.CreateContextAt(dbPath))
        {
            var proyecto = new Proyecto
            {
                Nombre = "Proyecto diagnóstico rollback",
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

            var material = new Material
            {
                Clave = "MAT-DIAG",
                Descripcion = string.Empty,
                Unidad = "pza",
                PrecioUnitario = 100m,
                Notas = string.Empty
            };

            setup.Proyectos.Add(proyecto);
            setup.Materiales.Add(material);
            setup.SaveChanges();

            proyectoId = proyecto.Id;
            materialId = material.Id;

            var matriz = new Matriz
            {
                ProyectoId = proyectoId,
                Clave = "APU-DIAG",
                Descripcion = "A original",
                Unidad = "m3",
                Tipo = TipoMatriz.APU,
                CostoDirecto = CostoDirectoOriginal,
                Notas = string.Empty
            };
            setup.Matrices.Add(matriz);
            setup.SaveChanges();
            matrizId = matriz.Id;

            setup.ComponentesMatriz.Add(new ComponenteMatriz
            {
                MatrizId = matrizId,
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = materialId,
                Cantidad = 1m,
                Importe = CostoDirectoOriginal,
                Orden = 1,
                Notas = string.Empty
            });
            setup.SaveChanges();

            if (crearTriggerDeFallo)
            {
                setup.Database.ExecuteSqlRaw(
                    $@"CREATE TRIGGER fail_matriz_update
                       BEFORE UPDATE ON Matrices
                       WHEN NEW.Id = {matrizId}
                       BEGIN
                           SELECT RAISE(ABORT, '{MensajeFalloPrimario}');
                       END;");
            }
        }

        var dto = new MatrixEditDto
        {
            ProyectoId = proyectoId,
            Clave = "APU-DIAG",
            Descripcion = "A editada",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = CostoDirectoEditado,
            Componentes =
            {
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = materialId,
                    Cantidad = 2m,
                    Importe = 200m,
                    Orden = 1,
                    Notas = string.Empty
                }
            }
        };

        var soloIdentidad = new Matriz { Id = matrizId, ProyectoId = proyectoId };
        return new Escenario(soloIdentidad, dto);
    }

    private static void EliminarBase(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        for (var intento = 0; intento < 5; intento++)
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }

                return;
            }
            catch (IOException) when (intento < 4)
            {
                Thread.Sleep(150);
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            }
        }
    }

    // ── Infraestructura de inyección ──────────────────────────────────────────

    private sealed class FixedContextFactory : IProjectDbContextFactory
    {
        private readonly SOPROContext _context;

        public FixedContextFactory(SOPROContext context) => _context = context;

        public SOPROContext Create(string databasePath) => _context;

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_context);
    }

    private sealed class ProbeContext : SOPROContext
    {
        private readonly TransactionProbeInterceptor _probe;

        public ProbeContext(string dbPath, TransactionProbeInterceptor probe)
            : base(dbPath)
        {
            _probe = probe;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_probe);
            base.OnConfiguring(optionsBuilder);
        }
    }

    /// <summary>
    /// Cuenta aperturas, commits y reversiones reales de transacción y permite
    /// simular el fallo de la reversión (donde la operación queda incierta).
    /// </summary>
    private sealed class TransactionProbeInterceptor : DbTransactionInterceptor
    {
        public int Commits { get; private set; }

        public int Rollbacks { get; private set; }

        public int IntentosDeRollback { get; private set; }

        public bool FailRollback { get; set; }

        public string RollbackFailureMessage { get; set; } = "rollback_simulado";

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            Commits++;
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.CompletedTask;
        }

        public override InterceptionResult TransactionRollingBack(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
            => ContarIntentoDeRollback(result);

        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
            => new(ContarIntentoDeRollback(result));

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            Rollbacks++;
        }

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Rollbacks++;
            return Task.CompletedTask;
        }

        private InterceptionResult ContarIntentoDeRollback(InterceptionResult result)
        {
            IntentosDeRollback++;
            if (FailRollback)
                throw new InvalidOperationException(RollbackFailureMessage);

            return result;
        }
    }

    private sealed class ThrowingDiagnosticsSink : IDiagnosticsSink
    {
        public int Escrituras { get; private set; }

        public int Vaciados { get; private set; }

        public void Write(DiagnosticsEvent diagnosticsEvent)
        {
            Escrituras++;
            throw new IOException("write_simulado");
        }

        public void Flush()
        {
            Vaciados++;
            throw new IOException("flush_simulado");
        }
    }
}
