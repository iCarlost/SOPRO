using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.WinForms.Diagnostics;
using SOPRO.WinForms.Forms;
using System;
using System.IO;
using System.Windows.Forms;

namespace SOPRO.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            FileDiagnosticsSink? sink = null;
            GlobalExceptionHooks? hooks = null;

            // El try/catch cubre también la composición observable: si algo falla
            // antes de instalar los hooks, no se continúa con estado desconocido.
            try
            {
                ApplicationConfiguration.Initialize();
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

                // Fase 2 de diagnóstico: los logs viven junto a los datos locales,
                // nunca junto al proyecto ni en la carpeta de instalación.
                var logsDirectory = Path.Combine(WorkspacePaths.LocalDataFolder, "logs");
                var productVersion = ObtenerVersionProducto();
                sink = new FileDiagnosticsSink(logsDirectory);
                hooks = new GlobalExceptionHooks(
                    sink,
                    new FatalErrorDialogPresenter(),
                    productVersion,
                    logsDirectory);

                // Los hooks se instalan una sola vez y antes de crear cualquier
                // ventana para no perder fallos de arranque ni dejar excepciones
                // sin política.
                hooks.Install();

                MostrarAvisoPrivacidadDiagnosticos();

                EjecutarArranque(args, sink, productVersion);
            }
            catch (Exception ex)
            {
                if (hooks is not null)
                {
                    // Registra, presenta el diálogo fatal en el hilo de arranque
                    // y termina con código distinto de cero.
                    hooks.HandleStartupFailure(ex);

                    // Solo alcanzable si la terminación inyectada no saliera del
                    // proceso: el arranque nunca continúa con estado desconocido.
                    Environment.Exit(GlobalExceptionHooks.ExitCodeFatal);
                }

                TerminarSinCoordinador(sink, ex);
            }
            finally
            {
                hooks?.Dispose();
                sink?.Dispose();
            }
        }

        private static void MostrarAvisoPrivacidadDiagnosticos()
        {
            var store = new DiagnosticsPrivacyNoticeStore();
            if (store.IsAcknowledged) return;
            using var notice = new FormAvisoPrivacidadDiagnosticos();
            notice.ShowDialog();
            if (!notice.Reconocido) return;
            try { store.Acknowledge(); }
            catch (Exception) { }
            if (notice.Revisar)
            {
                using var logs = new FormRegistrosLocales();
                logs.ShowDialog();
            }
        }

        /// <summary>
        /// Fallback mínimo cuando la composición de diagnóstico falló antes de
        /// tener coordinador: se registra lo que permita el sink disponible y se
        /// cierra con código 1, sin MessageBox y sin continuar.
        /// <para>
        /// Límite conocido: si el fallo ocurre antes de crear el sink (por
        /// ejemplo en <see cref="ApplicationConfiguration.Initialize"/>), no
        /// queda evidencia local; solo el código de salida.
        /// </para>
        /// </summary>
        private static void TerminarSinCoordinador(IDiagnosticsSink? sink, Exception exception)
        {
            if (sink is not null)
            {
                try
                {
                    sink.Write(DiagnosticsEvent.FromException(
                        DateTimeOffset.UtcNow,
                        DiagnosticsSeverity.Fatal,
                        GlobalExceptionHooks.Source,
                        GlobalExceptionHooks.OperationStartupFailure,
                        ObtenerVersionProducto(),
                        GlobalExceptionHooks.FallbackCorrelationId,
                        exception));
                }
                catch
                {
                }

                try
                {
                    sink.Flush();
                }
                catch
                {
                }
            }

            Environment.Exit(GlobalExceptionHooks.ExitCodeFatal);
        }

        private static void EjecutarArranque(
            string[] args,
            IDiagnosticsSink diagnosticsSink,
            string productVersion)
        {
            var workspaceService = new ProjectWorkspaceService();
            workspaceService.EnsureWorkspaceExists();

            // Fase 2.2: apertura por shell / doble clic del archivo de proyecto.
            // Sin argumentos válidos el arranque conserva el comportamiento actual.
            CommandLineProjectPathParser.TryParse(args, out var projectPath);

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                // Arranque directo a un proyecto: se abre ANTES de mostrar
                // cualquier ventana, para que el selector (FormPrincipal) no se
                // pinte. Si la apertura falla, se cae al selector con el error.
                var principal = new FormPrincipal(
                    workspaceService,
                    new ProjectLifecycleService(
                        workspaceService,
                        diagnosticsSink: diagnosticsSink,
                        productVersion: productVersion),
                    initialProjectPath: null,
                    diagnosticsSink: diagnosticsSink,
                    productVersion: productVersion);
                var projectForm = principal.TryOpenProject(projectPath);

                if (projectForm != null)
                {
                    // La comprobación de actualizaciones se lanza ya dentro del
                    // bucle de mensajes (hilo de UI), igual que en el arranque
                    // normal desde el selector.
                    projectForm.BeginInvoke((Action)principal.StartStartupUpdateCheck);
                    System.Windows.Forms.Application.Run(new ProjectStartupContext(principal, projectForm));
                    return;
                }

                System.Windows.Forms.Application.Run(principal);
                return;
            }

            System.Windows.Forms.Application.Run(new FormPrincipal(
                workspaceService,
                new ProjectLifecycleService(
                    workspaceService,
                    diagnosticsSink: diagnosticsSink,
                    productVersion: productVersion),
                projectPath,
                diagnosticsSink: diagnosticsSink,
                productVersion: productVersion));
        }

        private static string ObtenerVersionProducto()
        {
            var version = typeof(Program).Assembly.GetName().Version;
            return version?.ToString() ?? "0.0.0";
        }
    }
}
