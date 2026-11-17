using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SOPRO.Application.Contracts;
using WinFormsApplication = System.Windows.Forms.Application;

namespace SOPRO.WinForms.Diagnostics
{
    /// <summary>
    /// Coordinador de diagnóstico global: suscribe los hooks de excepciones no
    /// controladas, registra el evento en el sink, fuerza el vaciado síncrono y
    /// aplica la política de cierre del ADR-0002. Los fallos del propio sink o
    /// del presentador nunca escapan del manejador.
    /// <para>
    /// La transición a fatal es única por proceso: el primer hook que la gana
    /// registra y termina; los siguientes se descartan para no duplicar
    /// escrituras ni salidas. Los manejadores son invocables directamente para
    /// probarlos sin instalar los eventos globales.
    /// </para>
    /// </summary>
    internal sealed class GlobalExceptionHooks : IDisposable
    {
        internal const string Source = "SOPRO.WinForms";
        internal const string OperationUiException = "ExcepcionNoControladaUI";
        internal const string OperationAppDomainException = "ExcepcionNoControladaAplicacion";
        internal const string OperationUnobservedTaskException = "ExcepcionDeTareaNoObservada";
        internal const string OperationStartupFailure = "FalloDeArranque";

        internal const int CorrelationIdLength = 8;
        internal const int ExitCodeFatal = 1;

        /// <summary>Alfabeto sin caracteres ambiguos (I, O, 0, 1) para dictar el código por teléfono.</summary>
        internal const string CorrelationIdAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>Código de respaldo cuando la factoría y el generador fallan; legible y de 8 caracteres.</summary>
        internal const string FallbackCorrelationId = "SPRA2AAA";

        private readonly IDiagnosticsSink _sink;
        private readonly IFatalErrorPresenter _presenter;
        private readonly string _productVersion;
        private readonly string _logsDirectory;
        private readonly Func<DateTimeOffset> _clock;
        private readonly Func<string> _correlationIdFactory;
        private readonly Action<int> _terminate;

        private int _uiThreadId;
        private int _fatalTransitionTaken;
        private int _installed;
        private volatile bool _disposed;

        internal GlobalExceptionHooks(
            IDiagnosticsSink sink,
            IFatalErrorPresenter presenter,
            string productVersion,
            string logsDirectory,
            Func<DateTimeOffset>? clock = null,
            Func<string>? correlationIdFactory = null,
            Action<int>? terminate = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _productVersion = productVersion ?? throw new ArgumentNullException(nameof(productVersion));
            _logsDirectory = logsDirectory ?? throw new ArgumentNullException(nameof(logsDirectory));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            _correlationIdFactory = correlationIdFactory ?? GenerateCorrelationId;
            _terminate = terminate ?? (code => Environment.Exit(code));
            _uiThreadId = Environment.CurrentManagedThreadId;
        }

        internal bool IsInstalled => Volatile.Read(ref _installed) == 1;

        internal bool IsUiThread => Environment.CurrentManagedThreadId == Volatile.Read(ref _uiThreadId);

        /// <summary>
        /// Suscribe los hooks globales. Debe llamarse antes de crear cualquier
        /// ventana y una sola vez por proceso; es idempotente.
        /// </summary>
        public void Install()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Interlocked.Exchange(ref _installed, 1) == 1)
            {
                return;
            }

            Volatile.Write(ref _uiThreadId, Environment.CurrentManagedThreadId);

            WinFormsApplication.SetUnhandledExceptionMode(
                System.Windows.Forms.UnhandledExceptionMode.CatchException);
            WinFormsApplication.ThreadException += HandleThreadException;
            AppDomain.CurrentDomain.UnhandledException += HandleAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (Interlocked.Exchange(ref _installed, 0) == 0)
            {
                return;
            }

            try
            {
                WinFormsApplication.ThreadException -= HandleThreadException;
            }
            catch
            {
                // La desuscripción nunca debe romper el cierre.
            }

            try
            {
                AppDomain.CurrentDomain.UnhandledException -= HandleAppDomainUnhandledException;
            }
            catch
            {
            }

            try
            {
                TaskScheduler.UnobservedTaskException -= HandleUnobservedTaskException;
            }
            catch
            {
            }
        }

        /// <summary>
        /// Excepción no controlada en el bucle de mensajes: fatal, con diálogo
        /// propio solo si se atiende en el hilo de UI, y cierre con código 1.
        /// </summary>
        internal void HandleThreadException(object? sender, ThreadExceptionEventArgs e)
        {
            try
            {
                HandleThreadExceptionCore(e);
            }
            catch
            {
                // Última red: cualquier fallo inyectado en el reloj o en la
                // construcción del evento no puede saltarse la política.
                TerminateSafely();
            }
        }

        /// <summary>
        /// Excepción no controlada del dominio de aplicación. Si el runtime ya
        /// está terminando solo se registra (no hay segunda salida ni diálogo);
        /// si no, se registra como error y se cierra de forma segura.
        /// </summary>
        internal void HandleAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e is null)
            {
                return;
            }

            bool esTerminante;
            try
            {
                esTerminante = e.IsTerminating;
            }
            catch
            {
                // Sin información fiable se asume que el runtime ya termina:
                // nunca se añade una segunda salida.
                esTerminante = true;
            }

            try
            {
                if (_disposed || !TryTakeFatalTransition())
                {
                    return;
                }

                var severity = esTerminante ? DiagnosticsSeverity.Fatal : DiagnosticsSeverity.Error;
                WriteEventSafely(severity, OperationAppDomainException, TryGetExceptionObject(e));

                if (!esTerminante)
                {
                    TerminateSafely();
                }
            }
            catch
            {
                if (!esTerminante)
                {
                    TerminateSafely();
                }
            }
        }

        /// <summary>
        /// Tarea no observada: se registra como advertencia, se marca observada
        /// siempre y nunca muestra diálogo ni termina el proceso.
        /// </summary>
        internal void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e is null)
            {
                return;
            }

            try
            {
                e.SetObserved();
            }
            catch
            {
                // El marcado es best-effort; la política ya no depende de él.
            }

            try
            {
                if (_disposed)
                {
                    return;
                }

                WriteEventSafely(DiagnosticsSeverity.Warning, OperationUnobservedTaskException, e.Exception);
            }
            catch
            {
                // Ni diálogo ni terminación: un fallo del hook no cambia la política.
            }
        }

        /// <summary>
        /// Fallo de arranque con estado desconocido: fatal, se presenta en el
        /// hilo de arranque (aún no hay bucle de mensajes) y se cierra con 1.
        /// </summary>
        internal void HandleStartupFailure(Exception exception)
        {
            try
            {
                if (_disposed || !TryTakeFatalTransition())
                {
                    return;
                }

                var correlationId = WriteEventSafely(DiagnosticsSeverity.Fatal, OperationStartupFailure, exception);
                PresentSafely(correlationId);
                TerminateSafely();
            }
            catch
            {
                // El arranque nunca continúa con estado desconocido.
                TerminateSafely();
            }
        }

        private void HandleThreadExceptionCore(ThreadExceptionEventArgs? e)
        {
            if (_disposed || e is null || !TryTakeFatalTransition())
            {
                return;
            }

            var correlationId = WriteEventSafely(DiagnosticsSeverity.Fatal, OperationUiException, e.Exception);

            if (IsUiThreadSafely())
            {
                PresentSafely(correlationId);
            }

            TerminateSafely();
        }

        private bool TryTakeFatalTransition() =>
            Interlocked.Exchange(ref _fatalTransitionTaken, 1) == 0;

        private bool IsUiThreadSafely()
        {
            try
            {
                return IsUiThread;
            }
            catch
            {
                return false;
            }
        }

        private static Exception? TryGetExceptionObject(UnhandledExceptionEventArgs e)
        {
            try
            {
                return e.ExceptionObject as Exception;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Registra y vacía sin permitir que reloj, factoría ni construcción del
        /// evento escapen. Si no puede construir el evento, igualmente sigue la
        /// política del manejador (presentar/terminar).
        /// </summary>
        private string WriteEventSafely(DiagnosticsSeverity severity, string operation, Exception? exception)
        {
            var correlationId = CreateCorrelationIdSafely();
            var diagnosticsEvent = TryBuildEvent(severity, operation, exception, correlationId);

            if (diagnosticsEvent is not null)
            {
                try
                {
                    _sink.Write(diagnosticsEvent);
                }
                catch
                {
                    // Un sink roto no puede ocultar el cierre ni propagarse al runtime.
                }
            }

            try
            {
                _sink.Flush();
            }
            catch
            {
            }

            return correlationId;
        }

        private DiagnosticsEvent? TryBuildEvent(
            DiagnosticsSeverity severity,
            string operation,
            Exception? exception,
            string correlationId)
        {
            try
            {
                return BuildEvent(severity, operation, exception, correlationId);
            }
            catch
            {
                // Reloj u obtención de datos estructurales fallidos: evento mínimo.
            }

            try
            {
                return BuildMinimalEvent(severity, operation, exception, correlationId);
            }
            catch
            {
                return null;
            }
        }

        private DiagnosticsEvent BuildEvent(
            DiagnosticsSeverity severity,
            string operation,
            Exception? exception,
            string correlationId)
        {
            var timestamp = _clock();

            if (exception is null)
            {
                return new DiagnosticsEvent(
                    timestamp,
                    severity,
                    Source,
                    operation,
                    _productVersion,
                    correlationId);
            }

            return DiagnosticsEvent.FromException(
                timestamp,
                severity,
                Source,
                operation,
                _productVersion,
                correlationId,
                exception);
        }

        private DiagnosticsEvent BuildMinimalEvent(
            DiagnosticsSeverity severity,
            string operation,
            Exception? exception,
            string correlationId)
        {
            return new DiagnosticsEvent(
                DateTimeOffset.UtcNow,
                severity,
                Source,
                operation,
                _productVersion,
                correlationId,
                exceptionType: TryGetExceptionTypeName(exception),
                hResult: TryGetHResult(exception));
        }

        private static string? TryGetExceptionTypeName(Exception? exception)
        {
            try
            {
                return exception?.GetType().FullName ?? exception?.GetType().Name;
            }
            catch
            {
                return null;
            }
        }

        private static int? TryGetHResult(Exception? exception)
        {
            try
            {
                return exception?.HResult;
            }
            catch
            {
                return null;
            }
        }

        private string CreateCorrelationIdSafely()
        {
            try
            {
                return CreateCorrelationId();
            }
            catch
            {
                return FallbackCorrelationId;
            }
        }

        private string CreateCorrelationId()
        {
            try
            {
                var correlationId = _correlationIdFactory();
                if (IsValidCorrelationId(correlationId))
                {
                    return correlationId;
                }
            }
            catch
            {
            }

            return GenerateCorrelationId();
        }

        private static bool IsValidCorrelationId(string? correlationId)
        {
            if (correlationId is null || correlationId.Length != CorrelationIdLength)
            {
                return false;
            }

            foreach (var caracter in correlationId)
            {
                if (!CorrelationIdAlphabet.Contains(caracter))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GenerateCorrelationId()
        {
            try
            {
                return new(RandomNumberGenerator.GetItems<char>(CorrelationIdAlphabet, CorrelationIdLength));
            }
            catch
            {
                return FallbackCorrelationId;
            }
        }

        private void PresentSafely(string correlationId)
        {
            try
            {
                _presenter.Show(correlationId, _logsDirectory);
            }
            catch
            {
                // El diálogo es informativo: su fallo no impide el cierre.
            }
        }

        private void TerminateSafely()
        {
            try
            {
                _terminate(ExitCodeFatal);
            }
            catch
            {
            }
        }
    }
}
