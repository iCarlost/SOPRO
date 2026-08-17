using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Threading;

/// <summary>
/// Pruebas de hilo del Gate N4 (N4-5): cero <see cref="SOPROContext"/> usado
/// desde múltiples hilos. El patrón prohibido es el contexto COMPARTIDO dentro
/// de <c>Task.Run</c>; el patrón adoptado es contexto propio por operación
/// (abierto con la fábrica y descartado al terminar el bloque) mientras el
/// contexto de sesión se queda exclusivamente en el hilo de UI.
///
/// El <see cref="ThreadAccessGuardInterceptor"/> es el contador de accesos
/// concurrentes: por instancia registra si dos comandos se ejecutan a la vez
/// desde hilos distintos. EF Core además detecta el abuso en la misma instancia
/// ("A second operation was started on this context instance") y lo lanza como
/// <see cref="InvalidOperationException"/>, así que ambos mecanismos cubren
/// los dos patrones.
/// </summary>
[TestClass]
public class DbContextThreadAccessGuardTests
{
    // ────────────────────────────────────────────────────────────────────────
    // SEAM: contador de accesos concurrentes por instancia de contexto
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Interceptor que cuenta superposiciones de comandos en la MISMA instancia
    /// desde hilos distintos (contador de accesos concurrentes). Opcionalmente
    /// detiene el primer comando (gate) para forzar la superposición de forma
    /// determinista y señaliza cuando ese primer comando entró.
    /// </summary>
    private sealed class ThreadAccessGuardInterceptor : DbCommandInterceptor
    {
        private int _activeThreadId = -1;
        private long _violations;
        private readonly ManualResetEventSlim? _gate;
        private readonly ManualResetEventSlim? _entered;
        private int _firstCommandArmed;

        public ThreadAccessGuardInterceptor(
            ManualResetEventSlim? gate = null,
            ManualResetEventSlim? entered = null)
        {
            _gate = gate;
            _entered = entered;
        }

        /// <summary>Número de accesos concurrentes detectados en esta instancia.</summary>
        public long Violations => Interlocked.Read(ref _violations);

        private void Enter()
        {
            var threadId = Environment.CurrentManagedThreadId;
            var previous = Interlocked.Exchange(ref _activeThreadId, threadId);
            if (previous != -1 && previous != threadId)
            {
                Interlocked.Increment(ref _violations);
            }

            if (_gate == null) return;
            if (Interlocked.Exchange(ref _firstCommandArmed, 1) == 0)
            {
                _entered?.Set();
                _gate.Wait();
            }
        }

        private void Exit()
        {
            Interlocked.Exchange(ref _activeThreadId, -1);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Enter();
            return base.ReaderExecuting(command, eventData, result);
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Exit();
            return base.ReaderExecuted(command, eventData, result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Enter();
            return base.ScalarExecuting(command, eventData, result);
        }

        public override object ScalarExecuted(
            DbCommand command, CommandExecutedEventData eventData, object result)
        {
            Exit();
            return base.ScalarExecuted(command, eventData, result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Enter();
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override int NonQueryExecuted(
            DbCommand command, CommandExecutedEventData eventData, int result)
        {
            Exit();
            return base.NonQueryExecuted(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Enter();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Exit();
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Enter();
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<object> ScalarExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, object result,
            CancellationToken cancellationToken = default)
        {
            Exit();
            return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Enter();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Exit();
            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>
    /// Contexto con el contador de accesos concurrentes conectado
    /// (via <c>OnConfiguring</c>, sin tocar la producción).
    /// </summary>
    private sealed class GuardedContext : SOPROContext
    {
        private readonly ThreadAccessGuardInterceptor _guard;

        public GuardedContext(string dbPath, ThreadAccessGuardInterceptor guard) : base(dbPath)
        {
            _guard = guard;
        }

        public long Violations => _guard.Violations;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.AddInterceptors(_guard);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // PRUEBAS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Patrón prohibido (antes de N4-5): la MISMA instancia usada desde dos
    /// hilos a la vez. EF Core lo detecta y lo rechaza de forma determinista:
    /// es la prueba de que el contador y el guard tienen algo que medir.
    /// </summary>
    [TestMethod]
    public void ContextoCompartido_EnDosHilos_EsRechazadoPorEFCore()
    {
        using var seed = TestDbFactory.CreateContext();
        seed.Proyectos.Add(CrearProyecto("P compartido"));
        seed.SaveChanges();
        var dbPath = seed.DatabasePath;

        using var compartido = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor());

        var gate = new ManualResetEventSlim(false);
        var entered = new ManualResetEventSlim(false);
        using var gateContext = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor(gate, entered));

        // El hilo 1 inicia un comando sobre gateContext y queda DETENIDO dentro
        // del interceptor: el comando está en vuelo pero la base no se toca.
        var t1 = Task.Run(() => gateContext.Proyectos.Count());
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
            "El primer comando del contexto no entró al interceptor.");

        // El hilo 2 intenta usar la MISMA instancia mientras el hilo 1 tiene
        // una operación en vuelo: EF Core rechaza el acceso concurrente.
        var t2 = Task.Run(() => gateContext.Proyectos.Count());
        Exception? error = null;
        try
        {
            t2.Wait(TimeSpan.FromSeconds(10));
        }
        catch (AggregateException ae)
        {
            error = ae.InnerException;
        }

        gate.Set();
        t1.Wait(TimeSpan.FromSeconds(10));

        Assert.IsNotNull(error, "EF Core debió rechazar el acceso concurrente a la misma instancia.");
        Assert.IsInstanceOfType<InvalidOperationException>(error);
        Assert.IsTrue(error!.Message.Contains("second operation", StringComparison.OrdinalIgnoreCase)
                      || error.Message.Contains("concurrently", StringComparison.OrdinalIgnoreCase),
            $"El mensaje debe ser el de contexto en uso: {error.Message}");

        // El contexto compartido quedó servible: la lectura original completó.
        Assert.AreEqual(1, compartido.Proyectos.Count());
    }

    /// <summary>
    /// Patrón N4-5: la operación de segundo plano usa su PROPIO contexto
    /// (fábrica, transient) mientras el contexto de sesión permanece en el
    /// hilo de UI. Con la superposición FORZADA entre ambos (gate) el contador
    /// de accesos concurrentes debe quedar en cero en el contexto de sesión
    /// (el contexto propio se descarta dentro del bloque y no es asertable).
    /// </summary>
    [TestMethod]
    public void OperacionEnSegundoPlano_ConContextoPropio_CeroAccesosConcurrentes()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        using (var seed = TestDbFactory.CreateContextAt(dbPath))
        {
            seed.Proyectos.Add(CrearProyecto("P semilla"));
            seed.SaveChanges();
        }

        using var compartido = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor());
        var gate = new ManualResetEventSlim(false);
        var entered = new ManualResetEventSlim(false);

        // "Task.Run": la operación abre su contexto con la fábrica (transient),
        // lo usa y lo descarta DENTRO del bloque. El gate detiene su primer
        // comando para garantizar la superposición temporal con la UI.
        var tFondo = Task.Run(() =>
        {
            using var propio = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor(gate, entered));
            propio.Proyectos.Add(CrearProyecto("P de fondo"));
            propio.SaveChanges();
        });

        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
            "El comando del contexto de fondo no entró al interceptor.");
        try
        {
            // Mientras el fondo está en vuelo, la UI lee del contexto compartido.
            for (int i = 0; i < 25; i++)
            {
                _ = compartido.Proyectos.Count();
                _ = compartido.Proyectos.FirstOrDefault(p => p.Nombre.StartsWith("P semilla"));
            }
        }
        finally
        {
            gate.Set();
        }

        Assert.IsTrue(tFondo.Wait(TimeSpan.FromSeconds(20)),
            "La operación de fondo no completó.");
        tFondo.GetAwaiter().GetResult();

        Assert.AreEqual(0, compartido.Violations,
            "El contexto de sesión fue usado desde más de un hilo.");
        Assert.IsTrue(tFondo.IsCompletedSuccessfully,
            "La operación de fondo falló.");
    }

    /// <summary>
    /// Carga de trabajo real (forma de btnRecalcular): el cálculo de programa
    /// (recalcular + regenerar periodos + distribuir) corre en segundo plano con
    /// contexto propio mientras la UI consulta el contexto compartido. La
    /// operación completa, los resultados persisten y el contador queda en cero.
    /// </summary>
    [TestMethod]
    public void RecalcularProgramaEnSegundoPlano_ConContextoPropio_CompletaSinViolaciones()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        int programaId;
        using (var seed = TestDbFactory.CreateContextAt(dbPath))
        {
            var proyecto = CrearProyecto("P programa");
            seed.Proyectos.Add(proyecto);
            seed.SaveChanges();

            var programa = new ProgramaObra
            {
                ProyectoId = proyecto.Id,
                Nombre = "Programa N4-5",
                FechaInicioPrograma = new DateTime(2026, 1, 5),
                TipoPeriodo = TipoPeriodoPrograma.Semana,
                DuracionPeriodoDias = 7,
                Activo = true
            };
            seed.ProgramasObra.Add(programa);
            seed.SaveChanges();

            foreach (var clave in new[] { "A-01", "A-02" })
            {
                seed.ActividadesProgramadas.Add(new ActividadProgramada
                {
                    ProgramaObraId = programa.Id,
                    EsResumen = false,
                    EsManual = false,
                    Clave = clave,
                    Descripcion = $"Actividad {clave}",
                    DuracionDiasHabiles = 10,
                    CantidadTotal = 100m,
                    PrecioUnitario = 25m,
                    MetodoDistribucion = MetodoDistribucionActividad.Uniforme
                });
            }
            seed.SaveChanges();
            programaId = programa.Id;
        }

        using var compartido = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor());
        var gate = new ManualResetEventSlim(false);
        var entered = new ManualResetEventSlim(false);

        var tFondo = Task.Run(() =>
        {
            using var propio = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor(gate, entered));
            var calculo = new ProgramacionCalculationService();
            var generacion = new ProgramacionGenerationService();
            var distribucion = new ProgramacionDistributionService();

            // Forma exacta de RegenerarPeriodosYDistribuciones (FormProgramaObra)
            calculo.RecalculateProgram(propio, programaId);
            generacion.RegeneratePeriodsFromProgramRange(propio, programaId, TipoPeriodoPrograma.Semana);
            distribucion.DistributeUniformBatch(propio, programaId);
            calculo.RecalculateProgram(propio, programaId);
        });

        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
            "El primer comando del cálculo de fondo no entró al interceptor.");
        try
        {
            // La UI consulta el contexto compartido mientras el cálculo está en vuelo.
            for (int i = 0; i < 40 && !tFondo.IsCompleted; i++)
            {
                _ = compartido.ActividadesProgramadas.Count(a => a.ProgramaObraId == programaId);
                _ = compartido.ProgramasObra.FirstOrDefault(p => p.Id == programaId);
            }
        }
        finally
        {
            gate.Set();
        }

        Assert.IsTrue(tFondo.Wait(TimeSpan.FromSeconds(30)),
            "El recálculo de fondo no completó.");
        tFondo.GetAwaiter().GetResult();

        Assert.AreEqual(0, compartido.Violations,
            "El contexto de sesión fue usado desde más de un hilo durante el recálculo.");
        Assert.IsTrue(tFondo.IsCompletedSuccessfully,
            "El cálculo de fondo falló (la operación no debe lanzar).");

        // Resultados persistidos visibles con lectura fresca del contexto compartido.
        var actividades = compartido.ActividadesProgramadas
            .Where(a => a.ProgramaObraId == programaId)
            .ToList();
        Assert.AreEqual(2, actividades.Count);
        Assert.IsTrue(actividades.All(a => a.FechaInicioProgramada.HasValue && a.FechaFinProgramada.HasValue),
            "El recálculo debe dejar fechas programadas a todas las hojas.");
        Assert.IsTrue(compartido.PeriodosPrograma.Count(p => p.ProgramaObraId == programaId) > 0,
            "La regeneración debe crear periodos del programa.");
        Assert.IsTrue(compartido.DistribucionesPeriodo.Count(d => d.ActividadProgramada.ProgramaObraId == programaId) > 0,
            "La distribución debe repartir importes en los periodos.");
    }

    private static Proyecto CrearProyecto(string nombre) => new Proyecto
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 12, 31),
        PlazoEjecucion = 365
    };
}
