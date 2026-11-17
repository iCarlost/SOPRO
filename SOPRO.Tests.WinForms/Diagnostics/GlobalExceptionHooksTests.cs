using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.WinForms.Diagnostics;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Pruebas del coordinador de diagnóstico global. Se invocan los manejadores
/// directamente con sink, presentador y terminación falsos: no se instalan los
/// eventos globales reales, no se llama a <c>Program.Main</c> y el runner nunca
/// se termina.
/// </summary>
[STATestClass]
[DoNotParallelize] // Transiciones fatales y estado estático de WinForms compartido.
public class GlobalExceptionHooksTests
{
    private const string VersionPrueba = "1.19.2.0";
    private const string LogsPrueba = @"C:\logs-sinteticos";
    private const string CorrelationPrueba = "ABCD2345";
    private static readonly DateTimeOffset RelojFijo = new(2026, 11, 17, 12, 34, 56, TimeSpan.Zero);

    [TestMethod]
    public void ThreadException_RegistraFatalPresentaDialogoYTermina()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = CrearHooks(sink, presenter, codigo => salidas.Add(codigo));

        var excepcion = new InvalidOperationException("MENSAJE-SECRETO de usuario");
        try
        {
            throw excepcion;
        }
        catch (InvalidOperationException capturada)
        {
            hooks.HandleThreadException(null, new ThreadExceptionEventArgs(capturada));
        }

        Assert.AreEqual(1, sink.Eventos.Count);
        var evento = sink.Eventos[0];
        Assert.AreEqual(DiagnosticsSeverity.Fatal, evento.Severity);
        Assert.AreEqual(GlobalExceptionHooks.Source, evento.Source);
        Assert.AreEqual(GlobalExceptionHooks.OperationUiException, evento.Operation);
        Assert.AreEqual(VersionPrueba, evento.ProductVersion);
        Assert.AreEqual(CorrelationPrueba, evento.CorrelationId);
        Assert.AreEqual(RelojFijo, evento.TimestampUtc);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType);
        Assert.AreEqual(excepcion.HResult, evento.HResult);
        Assert.AreEqual(1, sink.FlushCalls);
        AssertNoExponeTextoLibreNiTraza(evento, "MENSAJE-SECRETO");

        Assert.AreEqual(1, presenter.Llamadas.Count);
        Assert.AreEqual(CorrelationPrueba, presenter.Llamadas[0].CorrelationId);
        Assert.AreEqual(LogsPrueba, presenter.Llamadas[0].LogsDirectory);
        CollectionAssert.AreEqual(new[] { GlobalExceptionHooks.ExitCodeFatal }, salidas);
    }

    [TestMethod]
    public void ThreadException_FueraDelHiloUi_NoPresentaDialogoPeroTermina()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = CrearHooks(sink, presenter, codigo => salidas.Add(codigo));

        Exception? fuga = null;
        var hilo = new Thread(() =>
        {
            try
            {
                hooks.HandleThreadException(
                    null,
                    new ThreadExceptionEventArgs(new InvalidOperationException()));
            }
            catch (Exception ex)
            {
                fuga = ex;
            }
        });
        hilo.SetApartmentState(ApartmentState.MTA);
        hilo.Start();
        hilo.Join();

        Assert.IsNull(fuga);
        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(DiagnosticsSeverity.Fatal, sink.Eventos[0].Severity);
        Assert.AreEqual(0, presenter.Llamadas.Count);
        CollectionAssert.AreEqual(new[] { GlobalExceptionHooks.ExitCodeFatal }, salidas);
    }

    [TestMethod]
    public void ThreadException_ReentranteDesdeElSink_MantieneUnaSolaTransicionFatal()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        int terminaciones = 0;
        using var hooks = CrearHooks(
            sink,
            presenter,
            _ => Interlocked.Increment(ref terminaciones));

        sink.AlEscribir = () => hooks.HandleThreadException(
            null,
            new ThreadExceptionEventArgs(new InvalidOperationException()));

        hooks.HandleThreadException(
            null,
            new ThreadExceptionEventArgs(new InvalidOperationException()));

        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(1, terminaciones);
        Assert.AreEqual(1, presenter.Llamadas.Count);
    }

    [TestMethod]
    public void ThreadException_Concurrente_MantieneUnaSolaTransicionFatal()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        int terminaciones = 0;
        using var hooks = CrearHooks(
            sink,
            presenter,
            _ => Interlocked.Increment(ref terminaciones));

        Parallel.For(0, 16, _ => hooks.HandleThreadException(
            null,
            new ThreadExceptionEventArgs(new InvalidOperationException())));

        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(DiagnosticsSeverity.Fatal, sink.Eventos[0].Severity);
        Assert.AreEqual(1, terminaciones);
        Assert.IsTrue(presenter.Llamadas.Count <= 1, "El diálogo no debe abrirse más de una vez.");
    }

    [TestMethod]
    public void AppDomainTerminante_RegistraFatalSinDialogoNiSegundaSalida()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = CrearHooks(sink, presenter, codigo => salidas.Add(codigo));

        hooks.HandleAppDomainUnhandledException(
            null,
            new UnhandledExceptionEventArgs(new InvalidOperationException(), isTerminating: true));

        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(DiagnosticsSeverity.Fatal, sink.Eventos[0].Severity);
        Assert.AreEqual(GlobalExceptionHooks.OperationAppDomainException, sink.Eventos[0].Operation);
        Assert.AreEqual(1, sink.FlushCalls);
        Assert.AreEqual(0, presenter.Llamadas.Count);
        Assert.AreEqual(0, salidas.Count);
    }

    [TestMethod]
    public void AppDomainNoTerminante_RegistraErrorYTerminaSinDialogo()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = CrearHooks(sink, presenter, codigo => salidas.Add(codigo));

        hooks.HandleAppDomainUnhandledException(
            null,
            new UnhandledExceptionEventArgs(new InvalidOperationException(), isTerminating: false));

        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(DiagnosticsSeverity.Error, sink.Eventos[0].Severity);
        Assert.AreEqual(1, sink.FlushCalls);
        Assert.AreEqual(0, presenter.Llamadas.Count);
        CollectionAssert.AreEqual(new[] { GlobalExceptionHooks.ExitCodeFatal }, salidas);
    }

    [TestMethod]
    public void UnobservedTaskException_RegistraAdvertenciaYMarcaObservado()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = CrearHooks(sink, presenter, codigo => salidas.Add(codigo));

        var argumentos = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException()));

        hooks.HandleUnobservedTaskException(null, argumentos);

        Assert.IsTrue(argumentos.Observed, "La tarea debe quedar observada.");
        Assert.AreEqual(1, sink.Eventos.Count);
        var evento = sink.Eventos[0];
        Assert.AreEqual(DiagnosticsSeverity.Warning, evento.Severity);
        Assert.AreEqual(GlobalExceptionHooks.OperationUnobservedTaskException, evento.Operation);
        Assert.AreEqual(typeof(AggregateException).FullName, evento.ExceptionType);
        Assert.IsTrue(
            evento.InnerExceptionTypes.Contains(typeof(InvalidOperationException).FullName!),
            "El tipo de la excepción interna debe quedar registrado.");
        Assert.AreEqual(1, sink.FlushCalls);
        Assert.AreEqual(0, presenter.Llamadas.Count);
        Assert.AreEqual(0, salidas.Count);
    }

    [TestMethod]
    public void UnobservedTaskException_ConSinkQueLanzaNoPropagaYMarcaObservado()
    {
        var sink = new FakeDiagnosticsSink { LanzarEnWrite = true, LanzarEnFlush = true };
        var presenter = new FakeFatalErrorPresenter();
        int terminaciones = 0;
        using var hooks = CrearHooks(
            sink,
            presenter,
            _ => Interlocked.Increment(ref terminaciones));

        var argumentos = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException()));

        Exception? fuga = null;
        try
        {
            hooks.HandleUnobservedTaskException(null, argumentos);
        }
        catch (Exception ex)
        {
            fuga = ex;
        }

        Assert.IsNull(fuga);
        Assert.IsTrue(argumentos.Observed, "Debe marcarse observado incluso si el sink falla.");
        Assert.AreEqual(1, sink.FlushCalls);
        Assert.AreEqual(0, presenter.Llamadas.Count);
        Assert.AreEqual(0, terminaciones);
    }

    [TestMethod]
    public void SinkYPresenterQueLanzan_NoPropaganYTerminan()
    {
        var sink = new FakeDiagnosticsSink { LanzarEnWrite = true, LanzarEnFlush = true };
        var presenter = new FakeFatalErrorPresenter { Lanzar = true };
        int terminaciones = 0;
        using var hooks = CrearHooks(
            sink,
            presenter,
            _ => Interlocked.Increment(ref terminaciones));

        Exception? fuga = null;
        try
        {
            hooks.HandleThreadException(
                null,
                new ThreadExceptionEventArgs(new InvalidOperationException()));
        }
        catch (Exception ex)
        {
            fuga = ex;
        }

        Assert.IsNull(fuga, "Ni el fallo del sink ni el del presentador deben escapar.");
        Assert.AreEqual(1, sink.FlushCalls, "El flush se intenta aunque falle la escritura.");
        Assert.AreEqual(1, presenter.Llamadas.Count);
        Assert.AreEqual(1, terminaciones);
    }

    [TestMethod]
    public void FalloDeArranque_RegistraFatalPresentaEnElHiloDeArranqueYTermina()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = CrearHooks(sink, presenter, codigo => salidas.Add(codigo));

        hooks.HandleStartupFailure(new InvalidOperationException());

        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(DiagnosticsSeverity.Fatal, sink.Eventos[0].Severity);
        Assert.AreEqual(GlobalExceptionHooks.OperationStartupFailure, sink.Eventos[0].Operation);
        Assert.AreEqual(1, presenter.Llamadas.Count);
        Assert.AreEqual(CorrelationPrueba, presenter.Llamadas[0].CorrelationId);
        CollectionAssert.AreEqual(new[] { GlobalExceptionHooks.ExitCodeFatal }, salidas);
    }

    [TestMethod]
    public void CorrelationPorDefecto_EsOchoCaracteresLegibles()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        using var hooks = new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => RelojFijo,
            terminate: _ => { });

        hooks.HandleUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException())));
        hooks.HandleUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException())));

        var correlaciones = sink.Eventos.Select(evento => evento.CorrelationId).ToArray();
        Assert.AreEqual(2, correlaciones.Length);
        foreach (var correlacion in correlaciones)
        {
            Assert.AreEqual(GlobalExceptionHooks.CorrelationIdLength, correlacion.Length);
            Assert.IsTrue(
                correlacion.All(caracter => GlobalExceptionHooks.CorrelationIdAlphabet.Contains(caracter)),
                $"El código {correlacion} contiene caracteres no legibles.");
        }
    }

    [TestMethod]
    public void CorrelationFactoryQueFalla_UsaCodigoGeneradoDeRespaldo()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        using var hooks = new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => RelojFijo,
            () => throw new InvalidOperationException("fábrica rota"),
            terminate: _ => { });

        hooks.HandleUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException())));

        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(GlobalExceptionHooks.CorrelationIdLength, sink.Eventos[0].CorrelationId.Length);
    }

    [TestMethod]
    public void FallbackCorrelationId_EsDeOchoCaracteresDelAlfabeto()
    {
        Assert.AreEqual(GlobalExceptionHooks.CorrelationIdLength, GlobalExceptionHooks.FallbackCorrelationId.Length);
        Assert.IsTrue(
            GlobalExceptionHooks.FallbackCorrelationId
                .All(caracter => GlobalExceptionHooks.CorrelationIdAlphabet.Contains(caracter)),
            "El código de respaldo debe ser legible según el alfabeto declarado.");
    }

    [DataTestMethod]
    [DataRow("ABCD234")]   // 7 caracteres
    [DataRow("ABCD23456")] // 9 caracteres
    [DataRow("ABCD234O")]  // O excluida del alfabeto
    [DataRow("ABCD2340")]  // 0 excluido del alfabeto
    [DataRow("ABCD 234")]  // espacio no admitido
    [DataRow("abcd2345")]  // minúsculas no admitidas
    public void CorrelationFactoryInvalida_GeneraUnCodigoLegible(string correlacionInvalida)
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        using var hooks = new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => RelojFijo,
            () => correlacionInvalida,
            terminate: _ => { });

        hooks.HandleUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException())));

        Assert.AreEqual(1, sink.Eventos.Count);
        AssertCorrelacionLegible(sink.Eventos[0].CorrelationId);
        Assert.AreNotEqual(correlacionInvalida, sink.Eventos[0].CorrelationId);
    }

    [TestMethod]
    public void RelojQueLanza_RegistraEventoMinimoPresentaYTermina()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        var salidas = new List<int>();
        using var hooks = new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => throw new InvalidOperationException("reloj roto"),
            () => CorrelationPrueba,
            codigo => salidas.Add(codigo));

        hooks.HandleThreadException(
            null,
            new ThreadExceptionEventArgs(new InvalidOperationException()));

        Assert.AreEqual(1, sink.Eventos.Count);
        var evento = sink.Eventos[0];
        Assert.AreEqual(DiagnosticsSeverity.Fatal, evento.Severity);
        Assert.AreEqual(GlobalExceptionHooks.Source, evento.Source);
        Assert.AreEqual(GlobalExceptionHooks.OperationUiException, evento.Operation);
        Assert.AreEqual(CorrelationPrueba, evento.CorrelationId);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType);
        Assert.AreEqual(1, sink.FlushCalls);
        Assert.AreEqual(1, presenter.Llamadas.Count);
        Assert.AreEqual(CorrelationPrueba, presenter.Llamadas[0].CorrelationId);
        CollectionAssert.AreEqual(new[] { GlobalExceptionHooks.ExitCodeFatal }, salidas);
    }

    [TestMethod]
    public void RelojYCorrelacionQueLanzan_UsaCorrelacionGeneradaYEventoMinimoYTermina()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        int terminaciones = 0;
        using var hooks = new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => throw new InvalidOperationException("reloj roto"),
            () => throw new InvalidOperationException("fábrica rota"),
            _ => Interlocked.Increment(ref terminaciones));

        hooks.HandleThreadException(
            null,
            new ThreadExceptionEventArgs(new InvalidOperationException()));

        Assert.AreEqual(1, sink.Eventos.Count);
        var correlacion = sink.Eventos[0].CorrelationId;
        Assert.AreEqual(GlobalExceptionHooks.CorrelationIdLength, correlacion.Length);
        Assert.IsTrue(
            correlacion.All(caracter => GlobalExceptionHooks.CorrelationIdAlphabet.Contains(caracter)),
            $"El código de respaldo {correlacion} no es legible.");
        Assert.AreEqual(1, presenter.Llamadas.Count);
        Assert.AreEqual(correlacion, presenter.Llamadas[0].CorrelationId);
        Assert.AreEqual(1, terminaciones);
    }

    [TestMethod]
    public void UnobservedTaskException_ConRelojQueLanzaNoPropagaYMarcaObservado()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        using var hooks = new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => throw new InvalidOperationException("reloj roto"),
            () => CorrelationPrueba,
            terminate: _ => { });

        var argumentos = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException()));

        hooks.HandleUnobservedTaskException(null, argumentos);

        Assert.IsTrue(argumentos.Observed);
        Assert.AreEqual(1, sink.Eventos.Count);
        Assert.AreEqual(DiagnosticsSeverity.Warning, sink.Eventos[0].Severity);
        Assert.AreEqual(CorrelationPrueba, sink.Eventos[0].CorrelationId);
        Assert.AreEqual(1, sink.FlushCalls);
        Assert.AreEqual(0, presenter.Llamadas.Count);
    }

    [TestMethod]
    public void Dispose_DejaLosManejadoresInertesYNoSePuedeReinstalar()
    {
        var sink = new FakeDiagnosticsSink();
        var presenter = new FakeFatalErrorPresenter();
        int terminaciones = 0;
        var hooks = CrearHooks(sink, presenter, _ => Interlocked.Increment(ref terminaciones));

        Assert.IsFalse(hooks.IsInstalled);

        hooks.Dispose();
        hooks.Dispose();

        Assert.IsFalse(hooks.IsInstalled);
        Assert.ThrowsExactly<ObjectDisposedException>(() => hooks.Install());

        hooks.HandleThreadException(
            null,
            new ThreadExceptionEventArgs(new InvalidOperationException()));
        hooks.HandleAppDomainUnhandledException(
            null,
            new UnhandledExceptionEventArgs(new InvalidOperationException(), isTerminating: true));
        hooks.HandleUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException())));
        hooks.HandleStartupFailure(new InvalidOperationException());

        Assert.AreEqual(0, sink.Eventos.Count);
        Assert.AreEqual(0, sink.FlushCalls);
        Assert.AreEqual(0, presenter.Llamadas.Count);
        Assert.AreEqual(0, terminaciones);
    }

    private static GlobalExceptionHooks CrearHooks(
        FakeDiagnosticsSink sink,
        FakeFatalErrorPresenter presenter,
        Action<int> terminate)
    {
        return new GlobalExceptionHooks(
            sink,
            presenter,
            VersionPrueba,
            LogsPrueba,
            () => RelojFijo,
            () => CorrelationPrueba,
            terminate);
    }

    private static void AssertCorrelacionLegible(string correlacion)
    {
        Assert.AreEqual(GlobalExceptionHooks.CorrelationIdLength, correlacion.Length);
        Assert.IsTrue(
            correlacion.All(caracter => GlobalExceptionHooks.CorrelationIdAlphabet.Contains(caracter)),
            $"El código {correlacion} debe ser legible según el alfabeto declarado.");
    }

    private static void AssertNoExponeTextoLibreNiTraza(DiagnosticsEvent evento, string textoProhibido)
    {
        foreach (var propiedad in typeof(DiagnosticsEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (propiedad.PropertyType != typeof(string))
            {
                continue;
            }

            if (propiedad.GetValue(evento) is not string valor)
            {
                continue;
            }

            Assert.IsFalse(
                valor.Contains(textoProhibido, StringComparison.Ordinal),
                $"La propiedad {propiedad.Name} filtró el mensaje de la excepción.");
            Assert.IsFalse(
                valor.Contains("   at ", StringComparison.Ordinal),
                $"La propiedad {propiedad.Name} filtró la traza de la excepción.");
        }
    }

    private sealed class FakeDiagnosticsSink : IDiagnosticsSink
    {
        private readonly object _gate = new();
        private readonly List<DiagnosticsEvent> _eventos = new();

        public Action? AlEscribir { get; set; }

        public bool LanzarEnWrite { get; set; }

        public bool LanzarEnFlush { get; set; }

        public int FlushCalls { get; private set; }

        public IReadOnlyList<DiagnosticsEvent> Eventos
        {
            get
            {
                lock (_gate)
                {
                    return _eventos.ToArray();
                }
            }
        }

        public void Write(DiagnosticsEvent diagnosticsEvent)
        {
            if (LanzarEnWrite)
            {
                throw new IOException("fallo sintético de escritura");
            }

            lock (_gate)
            {
                _eventos.Add(diagnosticsEvent);
            }

            AlEscribir?.Invoke();
        }

        public void Flush()
        {
            lock (_gate)
            {
                FlushCalls++;
            }

            if (LanzarEnFlush)
            {
                throw new IOException("fallo sintético de flush");
            }
        }
    }

    private sealed class FakeFatalErrorPresenter : IFatalErrorPresenter
    {
        private readonly object _gate = new();
        private readonly List<(string CorrelationId, string LogsDirectory)> _llamadas = new();

        public bool Lanzar { get; set; }

        public IReadOnlyList<(string CorrelationId, string LogsDirectory)> Llamadas
        {
            get
            {
                lock (_gate)
                {
                    return _llamadas.ToArray();
                }
            }
        }

        public void Show(string correlationId, string logsDirectory)
        {
            lock (_gate)
            {
                _llamadas.Add((correlationId, logsDirectory));
            }

            if (Lanzar)
            {
                throw new InvalidOperationException("fallo sintético del presentador");
            }
        }
    }
}
