using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class FileDiagnosticsSinkTests
{
    private static readonly DateTimeOffset ClockBase = new(2026, 11, 17, 10, 20, 30, TimeSpan.Zero);

    [TestMethod]
    public void Write_GeneraJsonLineUtf8ConPayloadConservador()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase.AddTicks(1234567));
        var failures = new List<int>();
        var sink = CreateSink(temp, clock, failures: failures);

        var exception = new InvalidOperationException(
            "mensaje-secreto-no-debe-persistirse",
            new IOException("inner-secreto-no-debe-persistirse"));
        var evento = DiagnosticsEvent.FromException(
            clock.GetUtcNow(),
            DiagnosticsSeverity.Error,
            "SOPRO.Tests",
            "AbrirProyecto",
            "1.11.0",
            "corr-1",
            exception,
            "2026.04.5");

        sink.Write(evento);

        var path = temp.PathFor("sopro-20261117.log");
        Assert.IsTrue(File.Exists(path), "Debe crearse el archivo diario en UTC.");
        Assert.AreEqual(0, failures.Count);

        var bytes = File.ReadAllBytes(path);
        Assert.AreEqual((byte)'{', bytes[0], "UTF-8 sin BOM: el primer byte debe ser '{'.");
        Assert.AreEqual((byte)'\n', bytes[^1], "Cada evento termina en salto de línea.");

        var contenido = File.ReadAllText(path);
        Assert.AreEqual(1, contenido.Count(c => c == '\n'), "Un evento por línea.");
        Assert.IsFalse(contenido.Contains("mensaje-secreto", StringComparison.Ordinal));
        Assert.IsFalse(contenido.Contains("inner-secreto", StringComparison.Ordinal));

        var lines = ReadJsonLines(path);
        Assert.AreEqual(1, lines.Count);
        var root = lines[0];

        Assert.AreEqual("2026-11-17T10:20:30.1234567Z", root.GetProperty("ts").GetString());
        Assert.AreEqual("Error", root.GetProperty("level").GetString());
        Assert.AreEqual("SOPRO.Tests", root.GetProperty("source").GetString());
        Assert.AreEqual("AbrirProyecto", root.GetProperty("operation").GetString());
        Assert.AreEqual("1.11.0", root.GetProperty("product").GetString());
        Assert.AreEqual("2026.04.5", root.GetProperty("schema").GetString());
        Assert.AreEqual("corr-1", root.GetProperty("correlationId").GetString());
        Assert.AreEqual(typeof(InvalidOperationException).FullName, root.GetProperty("exceptionType").GetString());
        Assert.AreEqual(exception.HResult, root.GetProperty("hresult").GetInt32());

        var innerTypes = root.GetProperty("innerExceptionTypes").EnumerateArray().Select(item => item.GetString()).ToList();
        CollectionAssert.AreEqual(new[] { typeof(IOException).FullName }, innerTypes);

        Assert.IsFalse(root.TryGetProperty("metadata", out _), "El contrato no admite metadatos libres.");
        Assert.IsFalse(root.TryGetProperty("message", out _), "El payload no debe incluir mensajes de excepción.");
        Assert.IsFalse(root.TryGetProperty("stackTrace", out _), "El payload no debe incluir trazas.");
        Assert.IsFalse(root.TryGetProperty("exception", out _), "No debe existir un ToString() de la excepción.");
    }

    [TestMethod]
    public void Write_UsaElRelojInyectadoParaNombreYTimestamp()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock);

        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-1"));
        clock.Advance(TimeSpan.FromDays(1));
        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-2"));

        var first = temp.PathFor("sopro-20261117.log");
        var second = temp.PathFor("sopro-20261118.log");
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(second));

        Assert.AreEqual("2026-11-17T10:20:30.0000000Z", ReadJsonLines(first)[0].GetProperty("ts").GetString());
        Assert.AreEqual("2026-11-18T10:20:30.0000000Z", ReadJsonLines(second)[0].GetProperty("ts").GetString());
    }

    [TestMethod]
    public void Write_AcumulaEventosPorAppendSinSobrescribir()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock);

        sink.Write(CreateEvent(clock.GetUtcNow(), "uno"));
        sink.Write(CreateEvent(clock.GetUtcNow(), "dos"));
        sink.Write(CreateEvent(clock.GetUtcNow(), "tres"));

        var path = temp.PathFor("sopro-20261117.log");
        var ids = ReadJsonLines(path)
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();

        CollectionAssert.AreEqual(new[] { "uno", "dos", "tres" }, ids);
    }

    [TestMethod]
    public void Write_SanitizaPerfilYSecretosEnLosCamposFijos()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock);

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile));

        var evento = new DiagnosticsEvent(
            clock.GetUtcNow(),
            DiagnosticsSeverity.Warning,
            "token=abc123456789",
            $"Abrir {System.IO.Path.Combine(profile, "Documentos", "privado.soproproj")}",
            "1.11.0",
            "Bearer zzzzzzzzzz");

        sink.Write(evento);

        var path = temp.PathFor("sopro-20261117.log");
        var contenido = File.ReadAllText(path);
        Assert.IsFalse(contenido.Contains(profile, StringComparison.OrdinalIgnoreCase), contenido);
        Assert.IsFalse(contenido.Contains("abc123456789", StringComparison.Ordinal), contenido);
        Assert.IsFalse(contenido.Contains("zzzzzzzzzz", StringComparison.Ordinal), contenido);
        StringAssert.Contains(contenido, "%USERPROFILE%");
        StringAssert.Contains(contenido, "[REDACTED]");

        var root = ReadJsonLines(path)[0];
        StringAssert.Contains(root.GetProperty("operation").GetString(), "%USERPROFILE%");
    }

    [TestMethod]
    public void Write_ConcurrenteEnUnaInstancia_NoPierdeNiCorrompeLineas()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var sink = CreateSink(temp, clock, failures: failures);

        const int workers = 8;
        const int perWorker = 50;
        var tasks = Enumerable.Range(0, workers)
            .Select(worker => Task.Run(() =>
            {
                for (var i = 0; i < perWorker; i++)
                {
                    sink.Write(CreateEvent(clock.GetUtcNow(), $"w{worker}-{i:D3}"));
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        var lines = ReadJsonLines(temp.PathFor("sopro-20261117.log"));
        Assert.AreEqual(workers * perWorker, lines.Count);
        Assert.AreEqual(workers * perWorker, lines.Select(line => line.GetProperty("correlationId").GetString()).Distinct().Count());
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void Write_DosInstanciasSobreElMismoRoot_NoCorrompenElArchivo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var timeout = TimeSpan.FromSeconds(10);
        var sinkA = CreateSink(temp, clock, lockTimeout: timeout, failures: failures);
        var sinkB = CreateSink(temp, clock, lockTimeout: timeout, failures: failures);

        const int perSink = 50;
        var tasks = new[]
        {
            Task.Run(() =>
            {
                for (var i = 0; i < perSink; i++)
                {
                    sinkA.Write(CreateEvent(clock.GetUtcNow(), $"a-{i:D3}"));
                }
            }),
            Task.Run(() =>
            {
                for (var i = 0; i < perSink; i++)
                {
                    sinkB.Write(CreateEvent(clock.GetUtcNow(), $"b-{i:D3}"));
                }
            })
        };

        Task.WaitAll(tasks);
        sinkA.Flush();
        sinkB.Flush();
        sinkA.Dispose();
        sinkB.Dispose();

        var lines = ReadJsonLines(temp.PathFor("sopro-20261117.log"));
        Assert.AreEqual(perSink * 2, lines.Count);
        Assert.AreEqual(perSink * 2, lines.Select(line => line.GetProperty("correlationId").GetString()).Distinct().Count());
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void RotacionPorTamano_GeneraSegmentosYConservaElOrden()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock, maxFileBytes: 400, maxFiles: 10);

        for (var i = 0; i < 30; i++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"{i:D4}-{new string('x', 60)}"));
        }

        var files = Directory.GetFiles(temp.Root, "sopro-*.log");
        Assert.IsTrue(files.Length >= 2, $"Debe haber rotación; archivos: {files.Length}");
        Assert.IsTrue(File.Exists(temp.PathFor("sopro-20261117.1.log")), "El primer segmento es .1.log.");

        foreach (var file in files)
        {
            Assert.IsTrue(ReadJsonLines(file).Count > 0, $"El archivo {System.IO.Path.GetFileName(file)} quedó vacío.");
        }

        var baseIds = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => CorrelationIndex(line))
            .ToList();
        var firstSegmentIds = ReadJsonLines(temp.PathFor("sopro-20261117.1.log"))
            .Select(line => CorrelationIndex(line))
            .ToList();

        Assert.IsTrue(baseIds.Count > 0 && firstSegmentIds.Count > 0);
        Assert.IsTrue(
            firstSegmentIds.Max() < baseIds.Min(),
            "El segmento .1.log es más antiguo que el archivo base.");
    }

    [TestMethod]
    public void Rotacion_RespetaElMaximoDeSegmentosSobreElConjunto()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock, maxFileBytes: 200, maxFiles: 3);

        for (var i = 0; i < 40; i++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"{i:D4}-{new string('y', 40)}"));
        }

        var files = Directory.GetFiles(temp.Root, "sopro-*.log");
        Assert.IsTrue(files.Length <= 3, $"El máximo aplica al conjunto de segmentos; encontrados: {files.Length}");

        foreach (var file in files)
        {
            Assert.IsTrue(ReadJsonLines(file).Count > 0, $"El archivo {System.IO.Path.GetFileName(file)} quedó vacío.");
        }
    }

    [TestMethod]
    public void Rotacion_ConMaxFilesUno_NoGeneraSegmentos()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock, maxFileBytes: 100, maxFiles: 1);

        for (var i = 0; i < 5; i++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"n-{i}"));
        }

        var files = Directory.GetFiles(temp.Root, "sopro-*.log");
        Assert.AreEqual(1, files.Length);
        Assert.AreEqual("sopro-20261117.log", System.IO.Path.GetFileName(files[0]));
        Assert.AreEqual(1, ReadJsonLines(files[0]).Count);
    }

    [TestMethod]
    public void Purga_EliminaArchivosMasAntiguosQueLaRetencion()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var oldPath = temp.PathFor("sopro-20200101.log");
        File.WriteAllText(oldPath, "{\"ts\":\"2020-01-01T00:00:00.0000000Z\"}\n");
        File.SetLastWriteTimeUtc(oldPath, clock.GetUtcNow().UtcDateTime.AddDays(-30));

        var sink = CreateSink(temp, clock, retentionDays: 7);
        sink.Write(CreateEvent(clock.GetUtcNow(), "corr-1"));

        Assert.IsFalse(File.Exists(oldPath), "Los archivos fuera de retención deben eliminarse.");
        Assert.IsTrue(File.Exists(temp.PathFor("sopro-20261117.log")));
    }

    [TestMethod]
    public void Purga_AlCambiarDeDia_RespetaElMaximoIncluidoElArchivoNuevo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock, maxFiles: 1, retentionDays: 30);

        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-1"));
        clock.Advance(TimeSpan.FromDays(1));
        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-2"));

        var files = Directory.GetFiles(temp.Root, "sopro-*.log");
        Assert.AreEqual(1, files.Length, "Con maxFiles=1 solo debe quedar el archivo del día activo.");
        Assert.AreEqual("sopro-20261118.log", System.IO.Path.GetFileName(files[0]));
        Assert.AreEqual("dia-2", ReadJsonLines(files[0])[0].GetProperty("correlationId").GetString());
    }

    [TestMethod]
    public void Purga_AlCambiarDeDia_ConservaComoMaximoElConjuntoConfigurado()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var sink = CreateSink(temp, clock, maxFiles: 2, retentionDays: 30);

        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-1"));
        clock.Advance(TimeSpan.FromDays(1));
        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-2"));

        var files = Directory.GetFiles(temp.Root, "sopro-*.log")
            .Select(System.IO.Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "sopro-20261117.log", "sopro-20261118.log" },
            files);
    }

    [TestMethod]
    public void Purga_SiNoCompleta_NoSeMarcaYReintentaEnLaSiguienteEscritura()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore();
        var sink = CreateSink(temp, clock, maxFiles: 1, retentionDays: 30, store: store, failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-1"));
        Assert.IsTrue(File.Exists(temp.PathFor("sopro-20261117.log")));

        clock.Advance(TimeSpan.FromDays(1));
        store.FailEnumerate = true;

        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-2-fallida"));
        Assert.IsTrue(failures.Count >= 1);
        Assert.IsFalse(File.Exists(temp.PathFor("sopro-20261118.log")));

        store.FailEnumerate = false;
        sink.Write(CreateEvent(clock.GetUtcNow(), "dia-2"));

        var files = Directory.GetFiles(temp.Root, "sopro-*.log");
        Assert.AreEqual(1, files.Length, "La purga debe reintentarse y dejar solo el archivo activo.");
        Assert.AreEqual("sopro-20261118.log", System.IO.Path.GetFileName(files[0]));
    }

    [TestMethod]
    public void Purga_NoEliminaElArchivoActivoAunqueSuMtimeSeaAntiguo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var timeout = TimeSpan.FromMilliseconds(500);

        var firstSink = CreateSink(temp, clock, retentionDays: 7, lockTimeout: timeout);
        firstSink.Write(CreateEvent(clock.GetUtcNow(), "primero"));

        var activePath = temp.PathFor("sopro-20261117.log");
        File.SetLastWriteTimeUtc(activePath, clock.GetUtcNow().UtcDateTime.AddDays(-30));

        // Una instancia nueva purga al arrancar: el archivo del día debe sobrevivir.
        var secondSink = CreateSink(temp, clock, retentionDays: 7, lockTimeout: timeout);
        secondSink.Write(CreateEvent(clock.GetUtcNow(), "segundo"));

        Assert.IsTrue(File.Exists(activePath), "La purga no debe eliminar el archivo activo del día.");
        var ids = ReadJsonLines(activePath)
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "primero", "segundo" }, ids);
    }

    [TestMethod]
    public void Purga_ConservaLosArchivosMasRecientesHastaElMaximo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);

        for (var day = 10; day <= 15; day++)
        {
            var path = temp.PathFor($"sopro-202611{day}.log");
            File.WriteAllText(path, "{\"ts\":\"2026-11-01T00:00:00.0000000Z\"}\n");
            File.SetLastWriteTimeUtc(path, clock.GetUtcNow().UtcDateTime.AddDays(day - 16));
        }

        var sink = CreateSink(temp, clock, maxFiles: 4, retentionDays: 30);
        sink.Write(CreateEvent(clock.GetUtcNow(), "corr-1"));

        var names = Directory.GetFiles(temp.Root, "sopro-*.log")
            .Select(System.IO.Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "sopro-20261113.log", "sopro-20261114.log", "sopro-20261115.log", "sopro-20261117.log" },
            names);
    }

    [TestMethod]
    public void Write_ConFalloDeAppend_NoPropagaYNotifica()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailAppend = true };
        var sink = CreateSink(temp, clock, store: store, failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "corr-1"));
        sink.Flush();

        Assert.IsTrue(failures.Count >= 1, "El fallo inyectado debe notificarse.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);
    }

    [TestMethod]
    public void Write_ConFalloDeCreacionDeDirectorio_NoPropagaYNotifica()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailCreateDirectory = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            failures: failures,
            root: System.IO.Path.Combine(temp.Root, "no-creable"));

        sink.Write(CreateEvent(clock.GetUtcNow(), "corr-1"));

        Assert.IsTrue(failures.Count >= 1);
    }

    [TestMethod]
    public void Write_ConCandadoOcupado_EsperaDeFormaAcotadaYNoPropaga()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(40),
            failures: failures);

        var stopwatch = Stopwatch.StartNew();
        sink.Write(CreateEvent(clock.GetUtcNow(), "corr-1"));
        stopwatch.Stop();

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Espera no acotada: {stopwatch.Elapsed}");
        Assert.AreEqual(0, failures.Count, "Encolar por timeout no es un fallo: la línea se conserva.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);

        store.FailLock = false;
        sink.Flush();

        var ids = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "corr-1" }, ids);
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void Write_TrasTimeoutDeCandado_RecuperaAmbasLineasEnFifo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { RemainingLockFailures = 100 };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(30),
            failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "uno"));
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length, "La línea debe quedar pendiente.");

        store.RemainingLockFailures = 0;
        sink.Write(CreateEvent(clock.GetUtcNow(), "dos"));

        var ids = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "uno", "dos" }, ids);
        Assert.AreEqual(0, failures.Count, "La recuperación transitoria no debe reportarse como fallo.");
    }

    [TestMethod]
    public void Flush_TrasTimeoutDeCandado_VolcaLaColaUnaSolaVez()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { RemainingLockFailures = 100 };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(30),
            failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "pendiente"));
        sink.Flush();
        Assert.AreEqual(0, failures.Count, "El timeout de Flush conserva la línea: no hay descarte.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);

        store.RemainingLockFailures = 0;

        sink.Flush();
        sink.Flush();

        var ids = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "pendiente" }, ids);
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void Write_ConContencionRealDeCandado_EncolaYRecuperaAlLiberar()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var sink = CreateSink(temp, clock, lockTimeout: TimeSpan.FromMilliseconds(30), failures: failures);

        var lockPath = temp.PathFor("sopro-diagnostics.lock");
        using (var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), "bloqueada"));
            Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);
        }

        sink.Flush();

        var ids = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "bloqueada" }, ids);
        Assert.AreEqual(0, failures.Count, "La contención transitoria recuperada no es un fallo.");
    }

    [TestMethod]
    public void Write_ConCandadoDenegado_NotificaYNoEncola()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { DenyLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(20),
            failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "denegada"));

        Assert.AreEqual(1, failures.Count, "La denegación permanente se notifica una vez.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);

        store.DenyLock = false;
        sink.Flush();

        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length, "Una denegación no debe encolar la línea.");
        Assert.AreEqual(1, failures.Count);
    }

    [TestMethod]
    public void Write_ConFalloPermanenteDeIO_NotificaYNoEncola()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLockPermanent = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(20),
            failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "permanente"));

        Assert.AreEqual(1, failures.Count, "Un IOException permanente se notifica una vez.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);

        store.FailLockPermanent = false;
        sink.Flush();

        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length, "Un fallo permanente no debe encolarse ni reintentarse.");
        Assert.AreEqual(1, failures.Count);
    }

    [TestMethod]
    public void Write_ConFalloDeAppendEnCola_NoAnexaLaLineaActual()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(20),
            failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "pendiente"));
        Assert.AreEqual(0, failures.Count);

        store.FailLock = false;
        store.FailAppend = true;
        sink.Write(CreateEvent(clock.GetUtcNow(), "actual"));

        Assert.AreEqual(1, failures.Count, "El fallo ambiguo del volcado se notifica una vez.");

        store.FailAppend = false;
        sink.Flush();

        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length, "La línea actual no debe anexarse tras un lote cortado.");
        Assert.AreEqual(1, failures.Count);
    }

    [TestMethod]
    public void Write_AlCambiarDeDia_ElVolcadoYLaLineaActualUsanElDiaActivo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { RemainingLockFailures = 100 };
        var sink = CreateSink(
            temp,
            clock,
            maxFiles: 1,
            retentionDays: 30,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(30),
            failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "vieja"));
        store.RemainingLockFailures = 0;
        clock.Advance(TimeSpan.FromDays(1));
        sink.Write(CreateEvent(clock.GetUtcNow(), "nueva"));

        var files = Directory.GetFiles(temp.Root, "sopro-*.log");
        Assert.AreEqual(1, files.Length, "maxFiles=1 deja solo el archivo activo.");
        Assert.AreEqual("sopro-20261118.log", System.IO.Path.GetFileName(files[0]));

        var ids = ReadJsonLines(temp.PathFor("sopro-20261118.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "vieja", "nueva" }, ids);
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void Write_ConCandadoPermanenteYColaLlena_DescartaElMasNuevoYNotifica()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(20),
            failures: failures,
            maxPendingLines: 2);

        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 5; index++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"e{index}"));
        }

        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Escritura no acotada: {stopwatch.Elapsed}");
        Assert.AreEqual(3, failures.Count, "Solo los tres rechazos por overflow deben notificarse.");

        store.FailLock = false;
        sink.Flush();

        var ids = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "e0", "e1" }, ids);
        Assert.AreEqual(3, failures.Count);
    }

    [TestMethod]
    public void Write_ConLimiteDeBytesPendientes_RechazaElMasNuevo()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(20),
            failures: failures,
            maxPendingLines: 10,
            maxPendingBytes: 64);

        for (var index = 0; index < 3; index++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"b{index}"));
        }

        Assert.AreEqual(3, failures.Count, "Cada rechazo por límite de bytes debe notificarse.");

        store.FailLock = false;
        sink.Flush();

        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length, "Ninguna línea cabe en el límite de bytes.");
        Assert.AreEqual(3, failures.Count);
    }

    [TestMethod]
    public void Write_LineasPendientesAplicanLaRotacionPorTamanoAlVolcarse()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var store = new FailingDiagnosticsFileStore { RemainingLockFailures = 100 };
        var sink = CreateSink(
            temp,
            clock,
            maxFileBytes: 200,
            maxFiles: 3,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(30));

        sink.Write(CreateEvent(clock.GetUtcNow(), "primera"));
        sink.Write(CreateEvent(clock.GetUtcNow(), "segunda"));
        store.RemainingLockFailures = 0;
        sink.Flush();

        Assert.AreEqual(
            "primera",
            ReadJsonLines(temp.PathFor("sopro-20261117.1.log"))[0].GetProperty("correlationId").GetString());
        Assert.AreEqual(
            "segunda",
            ReadJsonLines(temp.PathFor("sopro-20261117.log"))[0].GetProperty("correlationId").GetString());
    }

    [TestMethod]
    public void Dispose_VolcaLaColaCompletaDe33LineasMasAllaDelTopeDeWrite()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(10),
            failures: failures);

        const int count = 33;
        for (var index = 0; index < count; index++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"p{index:D2}"));
        }

        Assert.AreEqual(0, failures.Count);
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);

        store.FailLock = false;
        sink.Dispose();

        var lines = ReadJsonLines(temp.PathFor("sopro-20261117.log"));
        Assert.AreEqual(count, lines.Count, "Dispose debe volcar toda la cola, no solo el tope de Write.");
        Assert.AreEqual(count, lines.Select(line => line.GetProperty("correlationId").GetString()).Distinct().Count());
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void Flush_VolcaLaColaCompletaDe64Lineas()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(
            temp,
            clock,
            store: store,
            lockTimeout: TimeSpan.FromMilliseconds(10),
            failures: failures);

        const int count = 64;
        for (var index = 0; index < count; index++)
        {
            sink.Write(CreateEvent(clock.GetUtcNow(), $"f{index:D2}"));
        }

        Assert.AreEqual(0, failures.Count, "La cola admite exactamente 64 líneas.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);

        store.FailLock = false;
        sink.Flush();

        var lines = ReadJsonLines(temp.PathFor("sopro-20261117.log"));
        Assert.AreEqual(count, lines.Count, "Flush debe volcar toda la cola acotada.");
        Assert.AreEqual(count, lines.Select(line => line.GetProperty("correlationId").GetString()).Distinct().Count());
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void Dispose_TrasTimeoutDeCandado_VolcaLaColaYNoLanza()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { RemainingLockFailures = 100 };
        var sink = CreateSink(temp, clock, store: store, lockTimeout: TimeSpan.FromMilliseconds(30), failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "dispose"));
        Assert.AreEqual(0, failures.Count);
        store.RemainingLockFailures = 0;
        sink.Dispose();
        sink.Dispose();

        var ids = ReadJsonLines(temp.PathFor("sopro-20261117.log"))
            .Select(line => line.GetProperty("correlationId").GetString())
            .ToList();
        CollectionAssert.AreEqual(new[] { "dispose" }, ids);
        Assert.AreEqual(0, failures.Count, "Sin descarte no hay notificación.");
    }

    [TestMethod]
    public void Dispose_ConCandadoPermanente_NoLanzaYNotificaElDescarte()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var failures = new List<int>();
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var sink = CreateSink(temp, clock, store: store, lockTimeout: TimeSpan.FromMilliseconds(20), failures: failures);

        sink.Write(CreateEvent(clock.GetUtcNow(), "uno"));
        sink.Write(CreateEvent(clock.GetUtcNow(), "dos"));
        Assert.AreEqual(0, failures.Count, "Las líneas encoladas aún no se han descartado.");

        var stopwatch = Stopwatch.StartNew();
        sink.Dispose();
        stopwatch.Stop();

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Dispose no acotado: {stopwatch.Elapsed}");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);
        Assert.AreEqual(1, failures.Count, "El descarte de pendientes en Dispose se notifica una vez.");
    }

    [TestMethod]
    public void Write_ConCallbackDeFalloRecursivo_NoSeReentraSinControl()
    {
        using var temp = new DiagnosticsTempDirectory();
        var clock = new MutableTimeProvider(ClockBase);
        var store = new FailingDiagnosticsFileStore { FailLock = true };
        var callbackCount = 0;
        FileDiagnosticsSink? sink = null;
        sink = new FileDiagnosticsSink(
            store,
            temp.Root,
            clock,
            1024 * 1024,
            10,
            7,
            TimeSpan.FromMilliseconds(20),
            () =>
            {
                callbackCount++;
                sink!.Write(CreateEvent(clock.GetUtcNow(), "reentrante"));
            },
            maxPendingLines: 1);

        sink.Write(CreateEvent(clock.GetUtcNow(), "uno"));
        Assert.AreEqual(0, callbackCount, "Encolar no notifica.");

        sink.Write(CreateEvent(clock.GetUtcNow(), "dos"));
        Assert.AreEqual(1, callbackCount, "El rechazo por overflow notifica una vez, sin reentrada en cascada.");

        sink.Dispose();
        Assert.AreEqual(2, callbackCount, "El descarte en Dispose notifica una vez, sin reentrada.");
        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);
    }

    [TestMethod]
    public void Write_EventoNulo_NoHaceNada()
    {
        using var temp = new DiagnosticsTempDirectory();
        var sink = CreateSink(temp);

        sink.Write(null!);

        Assert.AreEqual(0, Directory.GetFiles(temp.Root, "sopro-*.log").Length);
    }

    private static FileDiagnosticsSink CreateSink(
        DiagnosticsTempDirectory temp,
        TimeProvider? clock = null,
        long maxFileBytes = 1024 * 1024,
        int maxFiles = 10,
        int retentionDays = 7,
        TimeSpan? lockTimeout = null,
        List<int>? failures = null,
        IDiagnosticsFileStore? store = null,
        string? root = null,
        int maxPendingLines = FileDiagnosticsSink.DefaultMaxPendingLines,
        long maxPendingBytes = FileDiagnosticsSink.DefaultMaxPendingBytes)
    {
        return new FileDiagnosticsSink(
            store ?? new DiagnosticsFileStore(),
            root ?? temp.Root,
            clock ?? new MutableTimeProvider(ClockBase),
            maxFileBytes,
            maxFiles,
            retentionDays,
            lockTimeout ?? TimeSpan.FromMilliseconds(500),
            failures is null
                ? null
                : () =>
                {
                    lock (failures)
                    {
                        failures.Add(1);
                    }
                },
            maxPendingLines,
            maxPendingBytes);
    }

    private static DiagnosticsEvent CreateEvent(
        DateTimeOffset timestamp,
        string correlationId,
        DiagnosticsSeverity severity = DiagnosticsSeverity.Info)
    {
        return new DiagnosticsEvent(
            timestamp,
            severity,
            "SOPRO.Tests",
            "Operacion",
            "1.11.0",
            correlationId);
    }

    private static int CorrelationIndex(JsonElement line) =>
        int.Parse(line.GetProperty("correlationId").GetString()![..4], System.Globalization.CultureInfo.InvariantCulture);

    private static List<JsonElement> ReadJsonLines(string path)
    {
        var result = new List<JsonElement>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            result.Add(document.RootElement.Clone());
        }

        return result;
    }
}
