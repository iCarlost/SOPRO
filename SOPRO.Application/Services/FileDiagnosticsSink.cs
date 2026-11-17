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
/// instancias con un candado de archivo exclusivo de espera acotada. Un fallo de
/// E/S del propio sink nunca se propaga: se notifica, como máximo, mediante un
/// delegado sin texto libre.
/// </summary>
public sealed class FileDiagnosticsSink : IDiagnosticsSink, IDisposable
{
    internal const long DefaultMaxFileBytes = 1024 * 1024;
    internal const int DefaultMaxFiles = 10;
    internal const int DefaultRetentionDays = 7;
    internal static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromMilliseconds(750);

    private const int LockRetryDelayMilliseconds = 10;
    private const string LogSearchPattern = "sopro-*.log";
    private const string LockFileName = "sopro-diagnostics.lock";

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

    private bool _purgePending = true;
    private string? _lastPurgedPath;
    private bool _disposed;

    public FileDiagnosticsSink(
        string? rootDirectory = null,
        TimeProvider? timeProvider = null,
        long maxFileBytes = DefaultMaxFileBytes,
        int maxFiles = DefaultMaxFiles,
        int retentionDays = DefaultRetentionDays,
        TimeSpan? lockTimeout = null,
        Action? onSinkFailure = null)
        : this(
            new DiagnosticsFileStore(),
            rootDirectory ?? Path.Combine(WorkspacePaths.LocalDataFolder, "logs"),
            timeProvider ?? TimeProvider.System,
            maxFileBytes,
            maxFiles,
            retentionDays,
            lockTimeout ?? DefaultLockTimeout,
            onSinkFailure)
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
        Action? onSinkFailure)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionDays);

        _store = store;
        _rootDirectory = rootDirectory;
        _timeProvider = timeProvider;
        _maxFileBytes = maxFileBytes;
        _maxFiles = maxFiles;
        _retentionDays = retentionDays;
        _lockTimeout = lockTimeout < TimeSpan.Zero ? TimeSpan.Zero : lockTimeout;
        _onSinkFailure = onSinkFailure;
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
    /// Cada escritura se vacía a disco de forma sincrónica, por lo que no queda
    /// estado pendiente. El método existe para cumplir el contrato y nunca lanza.
    /// </summary>
    public void Flush()
    {
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }

    private void WriteLine(byte[] line)
    {
        var logPath = ComposeTodayPathUtc();

        _store.CreateDirectory(_rootDirectory);

        using var processLock = TryAcquireProcessLock();
        if (processLock is null)
        {
            ReportFailure();
            return;
        }

        if (_purgePending || !string.Equals(_lastPurgedPath, logPath, StringComparison.Ordinal))
        {
            Purge(logPath);
            _lastPurgedPath = logPath;
            _purgePending = false;
        }

        if (RotateIfNeeded(logPath, line.Length))
        {
            Purge(logPath);
            _lastPurgedPath = logPath;
        }

        using var stream = _store.OpenAppend(logPath);
        stream.Write(line, 0, line.Length);
        stream.Flush(flushToDisk: true);
    }

    private string ComposeTodayPathUtc()
    {
        var day = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(_rootDirectory, $"sopro-{day}.log");
    }

    private FileStream? TryAcquireProcessLock()
    {
        var lockPath = Path.Combine(_rootDirectory, LockFileName);
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                return _store.OpenExclusiveLock(lockPath);
            }
            catch (IOException)
            {
                if (stopwatch.Elapsed >= _lockTimeout)
                {
                    return null;
                }

                Thread.Sleep(LockRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
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
        if (_onSinkFailure is null)
        {
            return;
        }

        try
        {
            _onSinkFailure();
        }
        catch (Exception)
        {
            // El sink jamás debe fallar por su propio reporte.
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
