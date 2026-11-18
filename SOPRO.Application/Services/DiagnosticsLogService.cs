using System.Diagnostics;
using System.Globalization;
using System.Text;
using SOPRO.Application.Contracts;

namespace SOPRO.Application.Services;

public enum DiagnosticsOperationStatus { Success, Partial, Cancelled, Busy, Failed, NotFound, AlreadyExists, TooLarge, InvalidData, InvalidDestination }

public sealed record DiagnosticsOperationResult(DiagnosticsOperationStatus Status, int DeletedCount = 0)
{
    public bool Succeeded => Status == DiagnosticsOperationStatus.Success;
}

public sealed record DiagnosticsLogEntry(string Id, string FileName, DateTime LastWriteTimeUtc, long Length, string PreviewText);

public sealed class DiagnosticsLogSnapshot
{
    internal DiagnosticsLogSnapshot(IReadOnlyList<DiagnosticsLogEntry> entries, IReadOnlyDictionary<string, byte[]> bytes)
    {
        Entries = Array.AsReadOnly(entries.ToArray());
        Bytes = bytes;
    }
    public IReadOnlyList<DiagnosticsLogEntry> Entries { get; }
    internal IReadOnlyDictionary<string, byte[]> Bytes { get; }
}

public sealed record DiagnosticsSnapshotResult(DiagnosticsOperationStatus Status, DiagnosticsLogSnapshot? Snapshot)
{
    public bool Succeeded => Status == DiagnosticsOperationStatus.Success;
}

/// <summary>Lectura/exportación/borrado coordinados con el sink de diagnósticos.</summary>
public sealed class DiagnosticsLogService
{
    public const long MaximumSnapshotBytes = 20 * 1024 * 1024;
    private const int RetryDelayMilliseconds = 10;
    private readonly IDiagnosticsFileStore _store;
    private readonly string _logsDirectory;
    private readonly TimeSpan _lockTimeout;

    public DiagnosticsLogService(string? logsDirectory = null, TimeSpan? lockTimeout = null)
        : this(new DiagnosticsFileStore(), logsDirectory ?? Path.Combine(WorkspacePaths.LocalDataFolder, "logs"), lockTimeout ?? FileDiagnosticsSink.DefaultLockTimeout) { }

    internal DiagnosticsLogService(IDiagnosticsFileStore store, string logsDirectory, TimeSpan lockTimeout)
    {
        _store = store;
        _logsDirectory = Path.GetFullPath(logsDirectory);
        _lockTimeout = lockTimeout < TimeSpan.Zero ? TimeSpan.Zero : lockTimeout;
    }

    public DiagnosticsSnapshotResult CaptureSnapshot(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(_logsDirectory)) return new(DiagnosticsOperationStatus.Success, new DiagnosticsLogSnapshot(Array.Empty<DiagnosticsLogEntry>(), new Dictionary<string, byte[]>()));
            if (_store.IsReparsePoint(_logsDirectory)) return new(DiagnosticsOperationStatus.InvalidDestination, null);
            using var processLock = AcquireLock(cancellationToken);
            if (processLock is null) return new(DiagnosticsOperationStatus.Busy, null);
            var entries = new List<DiagnosticsLogEntry>();
            var contents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long total = 0;
            foreach (var path in _store.EnumerateFiles(_logsDirectory, "sopro-*.log"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsValidLogName(Path.GetFileName(path)) || _store.IsReparsePoint(path)) continue;
                var length = _store.GetLength(path);
                if (length > MaximumSnapshotBytes - total) return new(DiagnosticsOperationStatus.TooLarge, null);
                var bytes = _store.ReadAllBytes(path);
                total += bytes.Length;
                if (total > MaximumSnapshotBytes) return new(DiagnosticsOperationStatus.TooLarge, null);
                var preview = new UTF8Encoding(false, true).GetString(bytes);
                var id = Guid.NewGuid().ToString("N");
                contents.Add(id, bytes);
                entries.Add(new DiagnosticsLogEntry(id, Path.GetFileName(path), _store.GetLastWriteTimeUtc(path), bytes.Length, preview));
            }
            return new(DiagnosticsOperationStatus.Success, new DiagnosticsLogSnapshot(entries.AsReadOnly(), contents));
        }
        catch (OperationCanceledException) { return new(DiagnosticsOperationStatus.Cancelled, null); }
        catch (DecoderFallbackException) { return new(DiagnosticsOperationStatus.InvalidData, null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return new(DiagnosticsOperationStatus.Failed, null); }
    }

    public DiagnosticsOperationResult ExportEntry(DiagnosticsLogSnapshot snapshot, string entryId, string destination, bool overwriteConfirmed, CancellationToken cancellationToken = default)
    {
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!snapshot.Bytes.TryGetValue(entryId, out var bytes)) return new(DiagnosticsOperationStatus.NotFound);
            var fullDestination = Path.GetFullPath(destination);
            if (IsWithinLogs(fullDestination)) return new(DiagnosticsOperationStatus.InvalidDestination);
            if (File.Exists(fullDestination) && !overwriteConfirmed) return new(DiagnosticsOperationStatus.AlreadyExists);
            var parent = Path.GetDirectoryName(fullDestination);
            if (string.IsNullOrEmpty(parent)) return new(DiagnosticsOperationStatus.InvalidDestination);
            Directory.CreateDirectory(parent);
            cancellationToken.ThrowIfCancellationRequested();
            temporary = Path.Combine(parent, ".sopro-export-" + Guid.NewGuid().ToString("N") + ".tmp");
            _store.WriteNewFileAndFlush(temporary, bytes);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullDestination))
            {
                if (!overwriteConfirmed) return new(DiagnosticsOperationStatus.AlreadyExists);
                File.Move(temporary, fullDestination, true);
            }
            else _store.MoveNew(temporary, fullDestination);
            temporary = null;
            return new(DiagnosticsOperationStatus.Success);
        }
        catch (OperationCanceledException) { return new(DiagnosticsOperationStatus.Cancelled); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return new(DiagnosticsOperationStatus.Failed); }
        finally { if (temporary is not null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
    }

    public DiagnosticsOperationResult DeleteLogs(CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;
        var failed = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(_logsDirectory)) return new(DiagnosticsOperationStatus.Success, 0);
            if (_store.IsReparsePoint(_logsDirectory)) return new(DiagnosticsOperationStatus.InvalidDestination, 0);
            using var processLock = AcquireLock(cancellationToken);
            if (processLock is null) return new(DiagnosticsOperationStatus.Busy, 0);
            foreach (var path in _store.EnumerateFiles(_logsDirectory, "sopro-*.log"))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    failed = true;
                    break;
                }
                if (!IsValidLogName(Path.GetFileName(path))) continue;
                try
                {
                    if (_store.IsReparsePoint(path)) continue;
                    _store.Delete(path);
                    deletedCount++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    failed = true;
                }
            }
            return new(failed ? deletedCount > 0 ? DiagnosticsOperationStatus.Partial : cancellationToken.IsCancellationRequested ? DiagnosticsOperationStatus.Cancelled : DiagnosticsOperationStatus.Failed : DiagnosticsOperationStatus.Success, deletedCount);
        }
        catch (OperationCanceledException) { return new(deletedCount > 0 ? DiagnosticsOperationStatus.Partial : DiagnosticsOperationStatus.Cancelled, deletedCount); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return new(deletedCount > 0 ? DiagnosticsOperationStatus.Partial : DiagnosticsOperationStatus.Failed, deletedCount); }
    }

    private FileStream? AcquireLock(CancellationToken token)
    {
        var path = Path.Combine(_logsDirectory, "sopro-diagnostics.lock");
        var timer = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return _store.OpenExclusiveLock(path); }
            catch (IOException)
            {
                if (timer.Elapsed >= _lockTimeout) return null;
                token.WaitHandle.WaitOne(Math.Min(RetryDelayMilliseconds, Math.Max(1, (int)(_lockTimeout - timer.Elapsed).TotalMilliseconds)));
            }
            catch (UnauthorizedAccessException) { return null; }
        }
    }

    private bool IsWithinLogs(string path)
    {
        var relative = Path.GetRelativePath(_logsDirectory, path);
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static bool IsValidLogName(string name)
    {
        if (!name.StartsWith("sopro-", StringComparison.Ordinal) || !name.EndsWith(".log", StringComparison.Ordinal)) return false;
        var stem = name.AsSpan(6, name.Length - 10);
        var dot = stem.IndexOf('.');
        var date = dot < 0 ? stem : stem[..dot];
        if (date.Length != 8 || !DateOnly.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
        if (dot < 0) return true;
        var segment = stem[(dot + 1)..];
        return segment.Length > 0 && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 && value.ToString(CultureInfo.InvariantCulture) == segment.ToString();
    }
}
