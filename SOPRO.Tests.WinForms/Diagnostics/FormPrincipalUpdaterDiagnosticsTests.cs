using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.WinForms.Diagnostics;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Pruebas del diagnóstico del updater en <see cref="FormPrincipal"/>. Se
/// inyectan comprobador, confirmación y lanzador falsos: no hay red real, no se
/// abren procesos ni modales y la comprobación se espera directamente.
/// </summary>
[STATestClass]
[DoNotParallelize] // Formularios WinForms sobre un hilo STA dedicado.
public class FormPrincipalUpdaterDiagnosticsTests
{
    private const string VersionPrueba = "1.19.2.0";
    private const string UrlInstalador = "https://example.test/descarga/setup-secreto.exe";
    private const string UrlRelease = "https://example.test/release/1.20.0";
    private const string MensajeSecreto = "MENSAJE-SECRETO-QUE-NO-DEBE-PERSISTIRSE";

    [TestMethod]
    public async Task SinActualizacion_RegistraInfoUnicoSinUrlNiModal()
    {
        var confirmaciones = 0;
        var lanzamientos = 0;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Available(
                hasUpdate: false,
                latestTag: "v1.19.2",
                latestVersion: new Version(1, 19, 2),
                installerUrl: UrlInstalador,
                releaseUrl: UrlRelease)),
            updateConfirmation: (_, _) =>
            {
                confirmaciones++;
                return false;
            },
            installerLauncher: _ => lanzamientos++);

        var eventos = sink.Snapshot();
        Assert.AreEqual(1, eventos.Count);
        AssertEvento(eventos[0], DiagnosticsSeverity.Info, FormPrincipal.OperationUpdateCheck);
        Assert.AreEqual(0, confirmaciones);
        Assert.AreEqual(0, lanzamientos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ActualizacionAceptada_RegistraInfoYDecisionYLanzaInstalador()
    {
        string? urlLanzada = null;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Available(
                hasUpdate: true,
                latestTag: "v1.20.0",
                latestVersion: new Version(1, 20, 0),
                installerUrl: UrlInstalador,
                releaseUrl: UrlRelease)),
            updateConfirmation: (_, _) => true,
            installerLauncher: url => urlLanzada = url);

        var eventos = sink.Snapshot();
        CollectionAssert.AreEqual(
            new[] { FormPrincipal.OperationUpdateAvailable, FormPrincipal.OperationUpdateDecision },
            eventos.Select(evento => evento.Operation).ToArray());
        Assert.IsTrue(eventos.All(evento => evento.Severity == DiagnosticsSeverity.Info));
        Assert.AreEqual(UrlInstalador, urlLanzada);
        AssertUnaSolaCorrelacion(eventos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ActualizacionRechazada_RegistraInfoDeDecisionSinLanzar()
    {
        var lanzamientos = 0;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Available(
                hasUpdate: true,
                latestTag: "v1.20.0",
                latestVersion: new Version(1, 20, 0),
                installerUrl: UrlInstalador,
                releaseUrl: UrlRelease)),
            updateConfirmation: (_, _) => false,
            installerLauncher: _ => lanzamientos++);

        var eventos = sink.Snapshot();
        CollectionAssert.AreEqual(
            new[] { FormPrincipal.OperationUpdateAvailable, FormPrincipal.OperationUpdateDecision },
            eventos.Select(evento => evento.Operation).ToArray());
        Assert.AreEqual(0, lanzamientos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ResultadoUnavailable_RegistraUnWarningSinModalNiErrorMessage()
    {
        var confirmaciones = 0;
        var lanzamientos = 0;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Unavailable(
                $"{MensajeSecreto} {UrlInstalador}")),
            updateConfirmation: (_, _) =>
            {
                confirmaciones++;
                return true;
            },
            installerLauncher: _ => lanzamientos++);

        var eventos = sink.Snapshot();
        Assert.AreEqual(1, eventos.Count);
        AssertEvento(eventos[0], DiagnosticsSeverity.Warning, FormPrincipal.OperationUpdateUnavailable);
        Assert.AreEqual(0, confirmaciones);
        Assert.AreEqual(0, lanzamientos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ReleaseSinAssetExe_RegistraWarningYUsaLaUrlDelRelease()
    {
        string? urlLanzada = null;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Available(
                hasUpdate: true,
                latestTag: "v1.20.0",
                latestVersion: new Version(1, 20, 0),
                installerUrl: UrlRelease,
                releaseUrl: UrlRelease)),
            updateConfirmation: (_, _) => true,
            installerLauncher: url => urlLanzada = url);

        var eventos = sink.Snapshot();
        CollectionAssert.AreEqual(
            new[]
            {
                FormPrincipal.OperationUpdateAvailable,
                FormPrincipal.OperationUpdateMissingInstaller,
                FormPrincipal.OperationUpdateDecision
            },
            eventos.Select(evento => evento.Operation).ToArray());
        Assert.AreEqual(DiagnosticsSeverity.Warning, eventos[1].Severity);
        Assert.AreEqual(UrlRelease, urlLanzada);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ReleaseSinUrl_RegistraWarningYNoLanza()
    {
        var lanzamientos = 0;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Available(
                hasUpdate: true,
                latestTag: "v1.20.0",
                latestVersion: new Version(1, 20, 0),
                installerUrl: string.Empty,
                releaseUrl: string.Empty)),
            updateConfirmation: (_, _) => true,
            installerLauncher: _ => lanzamientos++);

        var eventos = sink.Snapshot();
        CollectionAssert.AreEqual(
            new[]
            {
                FormPrincipal.OperationUpdateAvailable,
                FormPrincipal.OperationUpdateMissingUrl,
                FormPrincipal.OperationUpdateDecision
            },
            eventos.Select(evento => evento.Operation).ToArray());
        Assert.AreEqual(DiagnosticsSeverity.Warning, eventos[1].Severity);
        Assert.AreEqual(0, lanzamientos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task FalloDeProcessStart_RegistraErrorSinPropagarNiModal()
    {
        var sink = await EjecutarComprobacionAsync(
            (_, _) => Task.FromResult(UpdateCheckResult.Available(
                hasUpdate: true,
                latestTag: "v1.20.0",
                latestVersion: new Version(1, 20, 0),
                installerUrl: UrlInstalador,
                releaseUrl: UrlRelease)),
            updateConfirmation: (_, _) => true,
            installerLauncher: _ => throw new InvalidOperationException($"{MensajeSecreto} {UrlInstalador}"));

        var eventos = sink.Snapshot();
        var error = eventos.Single(evento => evento.Severity == DiagnosticsSeverity.Error);
        AssertEvento(error, DiagnosticsSeverity.Error, FormPrincipal.OperationUpdateLaunchFailed);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, error.ExceptionType);
        Assert.AreEqual(DiagnosticsSeverity.Info, eventos[0].Severity);
        Assert.AreEqual(DiagnosticsSeverity.Info, eventos[1].Severity);
        AssertUnaSolaCorrelacion(eventos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ExcepcionDelComprobador_RegistraErrorContenidoSinModal()
    {
        var confirmaciones = 0;
        var lanzamientos = 0;

        var sink = await EjecutarComprobacionAsync(
            (_, _) => throw new InvalidOperationException($"{MensajeSecreto} {UrlInstalador}"),
            updateConfirmation: (_, _) =>
            {
                confirmaciones++;
                return true;
            },
            installerLauncher: _ => lanzamientos++);

        var eventos = sink.Snapshot();
        Assert.AreEqual(1, eventos.Count);
        AssertEvento(eventos[0], DiagnosticsSeverity.Error, FormPrincipal.OperationUpdateCheck);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, eventos[0].ExceptionType);
        Assert.AreEqual(0, confirmaciones);
        Assert.AreEqual(0, lanzamientos);
        AssertSinDatosSensibles(eventos, MensajeSecreto);
    }

    [TestMethod]
    public async Task ComprobacionRepetida_SeEjecutaUnaSolaVez()
    {
        var comprobaciones = 0;

        var sink = await EjecutarComprobacionAsync(
            (_, _) =>
            {
                comprobaciones++;
                return Task.FromResult(UpdateCheckResult.Available(
                    hasUpdate: false,
                    latestTag: "v1.19.2",
                    latestVersion: new Version(1, 19, 2),
                    installerUrl: string.Empty,
                    releaseUrl: UrlRelease));
            },
            updateConfirmation: (_, _) => false,
            installerLauncher: _ => { },
            llamadas: 2);

        Assert.AreEqual(1, comprobaciones);
        Assert.AreEqual(1, sink.Snapshot().Count);
    }

    private static async Task<InMemoryDiagnosticsSink> EjecutarComprobacionAsync(
        Func<Version, CancellationToken, Task<UpdateCheckResult>> updateChecker,
        Func<string, string, bool> updateConfirmation,
        Action<string> installerLauncher,
        int llamadas = 1)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "sopro-updater-diagnostics",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var workspace = new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local"));
            var sink = new InMemoryDiagnosticsSink();

            using var form = new FormPrincipal(
                workspace,
                new ProjectLifecycleService(workspace),
                initialProjectPath: null,
                diagnosticsSink: sink,
                productVersion: VersionPrueba,
                updateChecker: updateChecker,
                updateConfirmation: updateConfirmation,
                installerLauncher: installerLauncher);

            for (var i = 0; i < llamadas; i++)
            {
                await form.CheckForUpdatesOnStartupAsync();
            }

            return sink;
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void AssertEvento(DiagnosticsEvent evento, DiagnosticsSeverity severity, string operation)
    {
        Assert.AreEqual(severity, evento.Severity);
        Assert.AreEqual(operation, evento.Operation);
        Assert.AreEqual(GlobalExceptionHooks.Source, evento.Source);
        Assert.AreEqual(VersionPrueba, evento.ProductVersion);
        Assert.IsTrue(
            DiagnosticsCorrelationId.IsValid(evento.CorrelationId),
            $"La correlación {evento.CorrelationId} no es válida.");
    }

    private static void AssertUnaSolaCorrelacion(IReadOnlyList<DiagnosticsEvent> eventos)
    {
        Assert.AreEqual(1, eventos.Select(evento => evento.CorrelationId).Distinct().Count());
    }

    private static void AssertSinDatosSensibles(IReadOnlyList<DiagnosticsEvent> eventos, params string[] prohibidos)
    {
        foreach (var evento in eventos)
        {
            foreach (var propiedad in typeof(DiagnosticsEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (propiedad.PropertyType != typeof(string)
                    || propiedad.GetValue(evento) is not string valor)
                {
                    continue;
                }

                Assert.IsFalse(
                    valor.Contains("http://", StringComparison.OrdinalIgnoreCase)
                    || valor.Contains("https://", StringComparison.OrdinalIgnoreCase),
                    $"La propiedad {propiedad.Name} filtró una URL.");
                Assert.IsFalse(
                    valor.Contains("   at ", StringComparison.Ordinal),
                    $"La propiedad {propiedad.Name} filtró una traza.");

                foreach (var prohibido in prohibidos)
                {
                    Assert.IsFalse(
                        valor.Contains(prohibido, StringComparison.Ordinal),
                        $"La propiedad {propiedad.Name} filtró texto prohibido.");
                }
            }

            Assert.IsFalse(
                evento.InnerExceptionTypes.Any(tipo =>
                    tipo.Contains("http://", StringComparison.OrdinalIgnoreCase)
                    || tipo.Contains("https://", StringComparison.OrdinalIgnoreCase)
                    || prohibidos.Any(prohibido => tipo.Contains(prohibido, StringComparison.Ordinal))),
                "Los tipos internos no deben contener datos de la respuesta.");
        }
    }
}
