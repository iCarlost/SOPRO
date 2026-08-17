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
/// desde mÃºltiples hilos. El patrÃ³n prohibido es el contexto COMPARTIDO dentro
/// de <c>Task.Run</c>; el patrÃ³n adoptado es contexto propio por operaciÃ³n
/// (abierto con la fÃ¡brica y descartado al terminar el bloque) mientras el
/// contexto de sesiÃ³n se queda exclusivamente en el hilo de UI.
///
/// El <see cref="ThreadAccessGuardInterceptor"/> es el contador de accesos
/// concurrentes: por instancia registra si dos comandos se ejecutan a la vez
/// desde hilos distintos. EF Core ademÃ¡s detecta el abuso en la misma instancia
/// ("A second operation was started on this context instance") y lo lanza como
/// <see cref="InvalidOperationException"/>, asÃ­ que ambos mecanismos cubren
/// los dos patrones.
/// </summary>
[TestClass]
public class DbContextThreadAccessGuardTests
{
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // SEAM: contador de accesos concurrentes por instancia de contexto
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Interceptor que cuenta superposiciones de comandos en la MISMA instancia
    /// desde hilos distintos (contador de accesos concurrentes). Opcionalmente
    /// detiene el primer comando (gate) para forzar la superposiciÃ³n de forma
    /// determinista y seÃ±aliza cuando ese primer comando entrÃ³.
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

        /// <summary>NÃºmero de accesos concurrentes detectados en esta instancia.</summary>
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
    /// (via <c>OnConfiguring</c>, sin tocar la producciÃ³n).
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

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // PRUEBAS
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// PatrÃ³n prohibido (antes de N4-5): la MISMA instancia usada desde dos
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
        // del interceptor: el comando estÃ¡ en vuelo pero la base no se toca.
        var t1 = Task.Run(() => gateContext.Proyectos.Count());
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
            "El primer comando del contexto no entrÃ³ al interceptor.");

        // El hilo 2 intenta usar la MISMA instancia mientras el hilo 1 tiene
        // una operaciÃ³n en vuelo: EF Core rechaza el acceso concurrente.
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

        Assert.IsNotNull(error, "EF Core debiÃ³ rechazar el acceso concurrente a la misma instancia.");
        Assert.IsInstanceOfType<InvalidOperationException>(error);
        Assert.IsTrue(error!.Message.Contains("second operation", StringComparison.OrdinalIgnoreCase)
                      || error.Message.Contains("concurrently", StringComparison.OrdinalIgnoreCase),
            $"El mensaje debe ser el de contexto en uso: {error.Message}");

        // El contexto compartido quedÃ³ servible: la lectura original completÃ³.
        Assert.AreEqual(1, compartido.Proyectos.Count());
    }

    /// <summary>
    /// PatrÃ³n N4-5: la operaciÃ³n de segundo plano usa su PROPIO contexto
    /// (fÃ¡brica, transient) mientras el contexto de sesiÃ³n permanece en el
    /// hilo de UI. Con la superposiciÃ³n FORZADA entre ambos (gate) el contador
    /// de accesos concurrentes debe quedar en cero en las dos instancias.
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

        // "Task.Run": la operaciÃ³n abre su contexto con la fÃ¡brica (transient),
        // lo usa y lo descarta DENTRO del bloque. El gate detiene su primer
        // comando para garantizar la superposiciÃ³n temporal con la UI.
        var tFondo = Task.Run(() =>
        {
            using var propio = new GuardedContext(dbPath, new ThreadAccessGuardInterceptor(gate, entered));
            propio.Proyectos.Add(CrearProyecto("P de fondo"));
            propio.SaveChanges();
        });

        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
            "El comando del contexto de fondo no entrÃ³ al interceptor.");
        try
        {
            // Mientras el fondo estÃ¡ en vuelo, la UI lee del contexto compartido.
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
            "La operaciÃ³n de fondo no completÃ³.");
        tFondo.GetAwaiter().GetResult();

        Assert.AreEqual(0, compartido.Violations,
            "El contexto de sesiÃ³n fue usado desde mÃ¡s de un hilo.");
        Assert.IsTrue(tFondo.IsCompletedSuccessfully,
            "La operaciÃ³n de fondo fallÃ³.");
    }

    /// <summary>
    /// Carga de trabajo real (forma de btnRecalcular): el cÃ¡lculo de programa
    /// (recalcular + regenerar periodos + distribuir) corre en segundo plano con
    /// contexto propio mientras la UI consulta el contexto compartido. La
    /// operaciÃ³n completa, los resultados persisten y el contador queda en cero.
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
            "El primer comando del cÃ¡lculo de fondo no entrÃ³ al interceptor.");
        try
        {
            // La UI consulta el contexto compartido mientras el cÃ¡lculo estÃ¡ en vuelo.
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
            "El recÃ¡lculo de fondo no completÃ³.");
        tFondo.GetAwaiter().GetResult();

        Assert.AreEqual(0, compartido.Violations,
            "El contexto de sesiÃ³n fue usado desde mÃ¡s de un hilo durante el recÃ¡lculo.");
        Assert.IsTrue(tFondo.IsCompletedSuccessfully,
            "El cÃ¡lculo de fondo fallÃ³ (la operaciÃ³n no debe lanzar).");

        // Resultados persistidos visibles con lectura fresca del contexto compartido.
        var actividades = compartido.ActividadesProgramadas
            .Where(a => a.ProgramaObraId == programaId)
            .ToList();
        Assert.AreEqual(2, actividades.Count);
        Assert.IsTrue(actividades.All(a => a.FechaInicioProgramada.HasValue && a.FechaFinProgramada.HasValue),
            "El recÃ¡lculo debe dejar fechas programadas a todas las hojas.");
        Assert.IsTrue(compartido.PeriodosPrograma.Count(p => p.ProgramaObraId == programaId) > 0,
            "La regeneraciÃ³n debe crear periodos del programa.");
        Assert.IsTrue(compartido.DistribucionesPeriodo.Count(d => d.ActividadProgramada.ProgramaObraId == programaId) > 0,
            "La distribuciÃ³n debe repartir importes en los periodos.");
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
