using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SOPRO.Application.Contracts;

namespace SOPRO.Application.Services;

/// <summary>
/// Sink de archivo local en formato JSON Lines (UTF-8, un evento por línea).
/// Es thread-safe dentro del proceso y coordina append, rotación y purga entre
/// instancias con un candado de archivo exclusivo de espera acotada. Si el candado
/// no se obtiene dentro del plazo, la línea ya serializada queda en una cola FIFO
/// por instancia —acotada en cantidad y bytes— y se vuelca al siguiente candado
/// disponible, sin prometer orden global entre instancias. Un fallo de E/S del
/// propio sink nunca se propaga: se notifica, como máximo, mediante un delegado
/// sin texto libre.
/// </summary>
public sealed class FileDiagnosticsSink : IDiagnosticsSink, IDisposable
{
    internal const long DefaultMaxFileBytes = 1024 * 1024;
    internal const int DefaultMaxFiles = 10;
    internal const int DefaultRetentionDays = 7;
    internal const int DefaultMaxPendingLines = 64;
    internal const long DefaultMaxPendingBytes = 256 * 1024;
    internal static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromMilliseconds(750);

    private const int LockRetryDelayMilliseconds = 10;
    private const int DrainMaxLinesPerAttempt = 32;
    private const long DrainMaxBytesPerAttempt = 128 * 1024;
    private const int WindowsSharingViolation = 0x20;
    private const int WindowsLockViolation = 0x21;
    private const int UnixWouldBlock = 11;
    private const int UnixAgainMacOs = 35;
    private const string LogSearchPattern = "sopro-*.log";
    private const string LockFileName = "sopro-diagnostics.lock";

    [ThreadStatic]
    private static bool s_reportingFailure;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly IDiagnosticsFileStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly string _rootDirectory;
    private readonly long _maxFileBytes;
    private readonly int _maxFiles;
    private readonly int _retentionDays;
    private readonly TimeSpan _lockTimeout;
    private readonly Action? _onSinkFailure;
    private readonly int _maxPendingLines;
    private readonly long _maxPendingBytes;
    private readonly Queue<byte[]> _pending = new();

    private long _pendingBytes;
    private bool _purgePending = true;
    private string? _lastPurgedPath;
    private bool _disposed;

    private enum LockAcquisition
    {
        Acquired,
        Busy,
        Denied,
        Failed
    }

    public FileDiagnosticsSink(
        string? rootDirectory = null,
        TimeProvider? timeProvider = null,
        long maxFileBytes = DefaultMaxFileBytes,
        int maxFiles = DefaultMaxFiles,
        int retentionDays = DefaultRetentionDays,
        TimeSpan? lockTimeout = null,
        Action? onSinkFailure = null,
        int maxPendingLines = DefaultMaxPendingLines,
        long maxPendingBytes = DefaultMaxPendingBytes)
        : this(
            new DiagnosticsFileStore(),
            rootDirectory ?? Path.Combine(WorkspacePaths.LocalDataFolder, "logs"),
            timeProvider ?? TimeProvider.System,
            maxFileBytes,
            maxFiles,
            retentionDays,
            lockTimeout ?? DefaultLockTimeout,
            onSinkFailure,
            maxPendingLines,
            maxPendingBytes)
    {
    }

    internal FileDiagnosticsSink(
        IDiagnosticsFileStore store,
        string rootDirectory,
        TimeProvider timeProvider,
        long maxFileBytes,
        int maxFiles,
        int retentionDays,
        TimeSpan lockTimeout,
        Action? onSinkFailure,
        int maxPendingLines = DefaultMaxPendingLines,
        long maxPendingBytes = DefaultMaxPendingBytes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionDays);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingBytes);

        _store = store;
        _rootDirectory = rootDirectory;
        _timeProvider = timeProvider;
        _maxFileBytes = maxFileBytes;
        _maxFiles = maxFiles;
        _retentionDays = retentionDays;
        _lockTimeout = lockTimeout < TimeSpan.Zero ? TimeSpan.Zero : lockTimeout;
        _onSinkFailure = onSinkFailure;
        _maxPendingLines = maxPendingLines;
        _maxPendingBytes = maxPendingBytes;
    }

    public void Write(DiagnosticsEvent diagnosticsEvent)
    {
        if (diagnosticsEvent is null)
        {
            return;
        }

        try
        {
            var line = Serialize(diagnosticsEvent);
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                WriteLine(line);
            }
        }
        catch (Exception)
        {
            ReportFailure();
        }
    }

    /// <summary>
    /// Intenta un único volcado de la cola pendiente, acotado por los límites de la
    /// propia cola. Nunca lanza ni espera más allá del plazo de candado configurado.
    /// </summary>
    public void Flush()
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || _pending.Count == 0)
                {
                    return;
                }

                _store.CreateDirectory(_rootDirectory);
                var acquisition = TryAcquireProcessLock(out var processLock);
                using var processLockScope = processLock;
                if (acquisition is LockAcquisition.Denied or LockAcquisition.Failed)
                {
                    ReportFailure();
                    return;
                }

                if (acquisition != LockAcquisition.Acquired)
                {
                    return;
                }

                DrainPending(_maxPendingLines, _maxPendingBytes);
            }
        }
        catch (Exception)
        {
            ReportFailure();
        }
    }

    public void Dispose()
    {
        try
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                DrainPendingOnDispose();
                var discarded = _pending.Count > 0;
                _disposed = true;
                _pending.Clear();
                _pendingBytes = 0;

                if (discarded)
                {
                    ReportFailure();
                }
            }
        }
        catch (Exception)
        {
            ReportFailure();
        }
    }

    private void DrainPendingOnDispose()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        try
        {
            _store.CreateDirectory(_rootDirectory);
            var acquisition = TryAcquireProcessLock(out var processLock);
            using var processLockScope = processLock;
            if (acquisition is LockAcquisition.Denied or LockAcquisition.Failed)
            {
                ReportFailure();
                return;
            }

            if (acquisition == LockAcquisition.Acquired)
            {
                DrainPending(_maxPendingLines, _maxPendingBytes);
            }
        }
        catch (Exception)
        {
            ReportFailure();
        }
    }

    private void WriteLine(byte[] line)
    {
        _store.CreateDirectory(_rootDirectory);

        var acquisition = TryAcquireProcessLock(out var processLock);
        using var processLockScope = processLock;
        if (acquisition is LockAcquisition.Denied or LockAcquisition.Failed)
        {
            ReportFailure();
            return;
        }

        if (acquisition != LockAcquisition.Acquired)
        {
            EnqueuePending(line);
            return;
        }

        if (!DrainPending(DrainMaxLinesPerAttempt, DrainMaxBytesPerAttempt))
        {
            // El volcado se cortó por un fallo ambiguo: no se anexa la línea actual
            // sobre una cola que pudo quedar parcialmente escrita.
            return;
        }

        // Si el volcado se detuvo por el tope del intento, la línea actual se
        // encola detrás del resto para no alterar el FIFO por instancia.
        if (_pending.Count > 0)
        {
            EnqueuePending(line);
            return;
        }

        AppendLine(ComposeTodayPathUtc(), line);
    }

    private void AppendLine(string destinationPath, byte[] line)
    {
        EnsurePurged(destinationPath);

        if (RotateIfNeeded(destinationPath, line.Length))
        {
            Purge(destinationPath);
            _lastPurgedPath = destinationPath;
        }

        using var stream = _store.OpenAppend(destinationPath);
        stream.Write(line, 0, line.Length);
        stream.Flush(flushToDisk: true);
    }

    private void EnsurePurged(string destinationPath)
    {
        if (_purgePending || !string.Equals(_lastPurgedPath, destinationPath, StringComparison.Ordinal))
        {
            Purge(destinationPath);
            _lastPurgedPath = destinationPath;
            _purgePending = false;
        }
    }

    private void EnqueuePending(byte[] line)
    {
        if (_pending.Count >= _maxPendingLines || _pendingBytes + line.Length > _maxPendingBytes)
        {
            ReportFailure();
            return;
        }

        _pending.Enqueue(line);
        _pendingBytes += line.Length;
    }

    /// <summary>
    /// Vuelca la cola en orden FIFO hasta el tope indicado, usando el archivo del día
    /// activo al momento del volcado. Solo el timeout de candado garantiza reintento;
    /// un fallo de append/rotación durante el volcado es ambiguo (pudo quedar una
    /// escritura parcial), así que se notifica, se descarta esa línea y se detiene el
    /// lote. Devuelve <c>false</c> cuando el lote se cortó por ese fallo.
    /// </summary>
    private bool DrainPending(int maxLines, long maxBytes)
    {
        if (_pending.Count == 0)
        {
            return true;
        }

        var destinationPath = ComposeTodayPathUtc();
        var processedLines = 0;
        var processedBytes = 0L;

        while (_pending.Count > 0
               && processedLines < maxLines
               && processedBytes < maxBytes)
        {
            var line = _pending.Peek();
            processedLines++;
            processedBytes += line.Length;

            try
            {
                AppendLine(destinationPath, line);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // La escritura pudo quedar parcial: se descarta esa línea y se corta
                // el lote para no anexar posteriores sobre una cola dudosa.
                _pending.Dequeue();
                _pendingBytes -= line.Length;
                ReportFailure();
                return false;
            }

            _pending.Dequeue();
            _pendingBytes -= line.Length;
        }

        return true;
    }

    private string ComposeTodayPathUtc()
    {
        var day = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(_rootDirectory, $"sopro-{day}.log");
    }

    private LockAcquisition TryAcquireProcessLock(out FileStream? processLock)
    {
        processLock = null;
        var lockPath = Path.Combine(_rootDirectory, LockFileName);
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                processLock = _store.OpenExclusiveLock(lockPath);
                return LockAcquisition.Acquired;
            }
            catch (IOException exception)
            {
                if (!IsLockContention(exception))
                {
                    return LockAcquisition.Failed;
                }

                if (stopwatch.Elapsed >= _lockTimeout)
                {
                    return LockAcquisition.Busy;
                }

                Thread.Sleep(LockRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                return LockAcquisition.Denied;
            }
        }
    }

    /// <summary>
    /// Solo los códigos nativos de contención de archivo justifican reintentar y
    /// encolar. Cualquier otro <see cref="IOException"/> es permanente y se reporta
    /// sin retención. No se inspeccionan mensajes.
    /// </summary>
    private static bool IsLockContention(IOException exception)
    {
        var code = exception.HResult & 0xFFFF;
        return OperatingSystem.IsWindows()
            ? code is WindowsSharingViolation or WindowsLockViolation
            : code is UnixWouldBlock or UnixAgainMacOs;
    }

    /// <summary>
    /// Rota renombrando el archivo del día a <c>.1.log</c> y desplazando los
    /// segmentos existentes. El número de segmento crece con la antigüedad: el
    /// archivo base es el más reciente. Devuelve <c>true</c> si hubo rotación.
    /// </summary>
    private bool RotateIfNeeded(string currentPath, int incomingBytes)
    {
        long currentLength;
        try
        {
            currentLength = _store.FileExists(currentPath) ? _store.GetLength(currentPath) : 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (currentLength == 0 || currentLength + incomingBytes <= _maxFileBytes)
        {
            return false;
        }

        if (_maxFiles <= 1)
        {
            TryDelete(currentPath);
            return true;
        }

        var segmentCapacity = _maxFiles - 1;
        var existingSegments = FindExistingSegments(currentPath);

        foreach (var segment in existingSegments.Where(segment => segment >= segmentCapacity))
        {
            TryDelete(SegmentPath(currentPath, segment));
        }

        for (var segment = segmentCapacity - 1; segment >= 1; segment--)
        {
            var source = SegmentPath(currentPath, segment);
            if (_store.FileExists(source))
            {
                TryMove(source, SegmentPath(currentPath, segment + 1));
            }
        }

        TryMove(currentPath, SegmentPath(currentPath, 1));
        return true;
    }

    private List<int> FindExistingSegments(string currentPath)
    {
        var directory = Path.GetDirectoryName(currentPath) ?? _rootDirectory;
        var baseName = Path.GetFileNameWithoutExtension(currentPath);
        var segments = new List<int>();

        foreach (var path in _store.EnumerateFiles(directory, LogSearchPattern))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith(baseName + ".", StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = name[(baseName.Length + 1)..];
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var segment) && segment > 0)
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    private static string SegmentPath(string currentPath, int segment)
    {
        var directory = Path.GetDirectoryName(currentPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(currentPath);
        return Path.Combine(directory, $"{baseName}.{segment}.log");
    }

    /// <summary>
    /// Aplica retención por antigüedad y por número de archivos. El archivo del
    /// evento en curso (<paramref name="currentPath"/>) nunca se elimina, aunque su
    /// mtime sea antiguo, y ocupa uno de los huecos del máximo configurado. Así el
    /// conjunto completo de segmentos respeta el límite incluyendo el archivo activo.
    /// </summary>
    private void Purge(string currentPath)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var retention = _retentionDays > 0 ? TimeSpan.FromDays(_retentionDays) : (TimeSpan?)null;
        var candidates = new List<(string Path, DateTime LastWriteUtc)>();

        foreach (var path in _store.EnumerateFiles(_rootDirectory, LogSearchPattern))
        {
            if (!IsOwnLogFile(path))
            {
                continue;
            }

            try
            {
                var lastWrite = _store.GetLastWriteTimeUtc(path);
                if (retention is not null
                    && now - lastWrite > retention.Value
                    && !IsSamePath(path, currentPath))
                {
                    TryDelete(path);
                    continue;
                }

                candidates.Add((path, lastWrite));
            }
            catch (IOException)
            {
                // Best-effort: un archivo ilegible se deja como está.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // El archivo activo ocupa siempre uno de los huecos; con maxFiles=1 no se
        // conserva ningún otro archivo (keep=0), ni siquiera el del día anterior.
        var keepOthers = Math.Max(0, _maxFiles - 1);
        foreach (var candidate in candidates
                     .Where(item => !IsSamePath(item.Path, currentPath))
                     .OrderByDescending(item => item.LastWriteUtc)
                     .Skip(keepOthers))
        {
            TryDelete(candidate.Path);
        }
    }

    private static bool IsSamePath(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private static bool IsOwnLogFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith("sopro-", StringComparison.Ordinal))
        {
            return false;
        }

        var datePart = name.AsSpan("sopro-".Length);
        var separator = datePart.IndexOf('.');
        if (separator >= 0)
        {
            datePart = datePart[..separator];
        }

        return datePart.Length == 8
               && int.TryParse(datePart, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private void TryMove(string source, string destination)
    {
        try
        {
            _store.Move(source, destination);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure();
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            _store.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure();
        }
    }

    private void ReportFailure()
    {
        if (_onSinkFailure is null || s_reportingFailure)
        {
            return;
        }

        s_reportingFailure = true;
        try
        {
            _onSinkFailure();
        }
        catch (Exception)
        {
            // El sink jamás debe fallar por su propio reporte.
        }
        finally
        {
            s_reportingFailure = false;
        }
    }

    private static byte[] Serialize(DiagnosticsEvent diagnosticsEvent)
    {
        var payload = new DiagnosticsPayload
        {
            Timestamp = diagnosticsEvent.TimestampUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Level = SeverityName(diagnosticsEvent.Severity),
            Source = DiagnosticsSanitizer.Sanitize(diagnosticsEvent.Source),
            Operation = DiagnosticsSanitizer.Sanitize(diagnosticsEvent.Operation),
            ProductVersion = DiagnosticsSanitizer.Sanitize(diagnosticsEvent.ProductVersion),
            SchemaVersion = SanitizeOptional(diagnosticsEvent.SchemaVersion),
            SchemaStage = SanitizeOptional(diagnosticsEvent.SchemaStage),
            CorrelationId = DiagnosticsSanitizer.Sanitize(diagnosticsEvent.CorrelationId),
            ExceptionType = SanitizeOptional(diagnosticsEvent.ExceptionType),
            HResult = diagnosticsEvent.HResult,
            InnerExceptionTypes = SanitizeTypes(diagnosticsEvent.InnerExceptionTypes)
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return Encoding.UTF8.GetBytes(json + "\n");
    }

    private static string SeverityName(DiagnosticsSeverity severity) => severity switch
    {
        DiagnosticsSeverity.Info => "Info",
        DiagnosticsSeverity.Warning => "Warning",
        DiagnosticsSeverity.Error => "Error",
        DiagnosticsSeverity.Fatal => "Fatal",
        _ => "Info"
    };

    private static string? SanitizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DiagnosticsSanitizer.Sanitize(value);

    private static string[]? SanitizeTypes(IReadOnlyList<string> types)
    {
        if (types.Count == 0)
        {
            return null;
        }

        return types
            .Take(8)
            .Select(DiagnosticsSanitizer.Sanitize)
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private sealed class DiagnosticsPayload
    {
        [JsonPropertyName("ts")]
        public string Timestamp { get; init; } = string.Empty;

        [JsonPropertyName("level")]
        public string Level { get; init; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; init; } = string.Empty;

        [JsonPropertyName("operation")]
        public string Operation { get; init; } = string.Empty;

        [JsonPropertyName("product")]
        public string ProductVersion { get; init; } = string.Empty;

        [JsonPropertyName("schema")]
        public string? SchemaVersion { get; init; }

        [JsonPropertyName("stage")]
        public string? SchemaStage { get; init; }

        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; init; } = string.Empty;

        [JsonPropertyName("exceptionType")]
        public string? ExceptionType { get; init; }

        [JsonPropertyName("hresult")]
        public int? HResult { get; init; }

        [JsonPropertyName("innerExceptionTypes")]
        public string[]? InnerExceptionTypes { get; init; }
    }
}
