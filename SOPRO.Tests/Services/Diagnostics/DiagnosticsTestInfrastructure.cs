using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

internal sealed class DiagnosticsTempDirectory : IDisposable
{
    public DiagnosticsTempDirectory()
    {
        Root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sopro-diagnostics-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathFor(string fileName) => System.IO.Path.Combine(Root, fileName);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(25);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(25);
            }
        }
    }
}

internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public MutableTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta)
    {
        _utcNow += delta;
    }
}

/// <summary>
/// Seam de E/S que falla de forma determinista en la operación indicada.
/// </summary>
internal sealed class FailingDiagnosticsFileStore : IDiagnosticsFileStore
{
    private readonly DiagnosticsFileStore _inner = new();

    public bool FailCreateDirectory { get; init; }

    public bool FailAppend { get; set; }

    public bool FailLock { get; set; }

    /// <summary>
    /// Falla el candado con un <see cref="IOException"/> permanente (sin código de
    /// contención) para distinguirlo del timeout reintentable.
    /// </summary>
    public bool FailLockPermanent { get; set; }

    /// <summary>
    /// Denegación permanente de candado (permisos), distinta del timeout por contención.
    /// </summary>
    public bool DenyLock { get; set; }

    /// <summary>
    /// Fallos de candado restantes antes de permitir la adquisición. Permite simular
    /// contención transitoria de forma determinista sin depender de esperas reales.
    /// </summary>
    public int RemainingLockFailures { get; set; }

    public bool FailEnumerate { get; set; }

    public bool FailDelete { get; set; }

    public string? FailDeleteFileName { get; set; }

    public string? ReparsePath { get; set; }

    public int EnumerateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public int LockCount { get; private set; }

    public bool FailRead { get; set; }

    public bool FailWrite { get; set; }

    public Action<string>? AfterDelete { get; set; }

    public void CreateDirectory(string path)
    {
        if (FailCreateDirectory)
        {
            throw new UnauthorizedAccessException("create-directory inyectado");
        }

        _inner.CreateDirectory(path);
    }

    public bool FileExists(string path) => _inner.FileExists(path);

    public long GetLength(string path) => _inner.GetLength(path);

    public FileStream OpenAppend(string path) =>
        FailAppend ? throw new IOException("append inyectado") : _inner.OpenAppend(path);

    public FileStream OpenExclusiveLock(string path)
    {
        LockCount++;
        if (FailLock)
        {
            throw CreateLockContentionIOException();
        }

        if (FailLockPermanent)
        {
            throw new IOException("lock io permanente");
        }

        if (DenyLock)
        {
            throw new UnauthorizedAccessException("lock denegado");
        }

        if (RemainingLockFailures > 0)
        {
            RemainingLockFailures--;
            throw CreateLockContentionIOException();
        }

        return _inner.OpenExclusiveLock(path);
    }

    private static IOException CreateLockContentionIOException()
    {
        var hresult = OperatingSystem.IsWindows()
            ? unchecked((int)0x80070020)
            : unchecked((int)0x8007000B);
        return new LockContentionIOException(hresult);
    }

    private sealed class LockContentionIOException : IOException
    {
        public LockContentionIOException(int hresult)
            : base("lock contention inyectado")
        {
            HResult = hresult;
        }
    }

    public void Move(string source, string destination) => _inner.Move(source, destination);

    public void Delete(string path)
    {
        if (FailDelete || string.Equals(Path.GetFileName(path), FailDeleteFileName, StringComparison.Ordinal)) throw new IOException("texto de fallo de borrado");
        _inner.Delete(path);
        DeleteCount++;
        AfterDelete?.Invoke(path);
    }

    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
        FailEnumerate
            ? throw new IOException("enumerate inyectado")
            : EnumerateAndCount(directory, searchPattern);

    private IReadOnlyList<string> EnumerateAndCount(string directory, string searchPattern)
    {
        EnumerateCount++;
        return _inner.EnumerateFiles(directory, searchPattern);
    }

    public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);

    public byte[] ReadAllBytes(string path) => FailRead ? throw new IOException("texto de fallo de lectura") : _inner.ReadAllBytes(path);

    public bool IsReparsePoint(string path) =>
        string.Equals(System.IO.Path.GetFullPath(path), ReparsePath is null ? null : System.IO.Path.GetFullPath(ReparsePath), StringComparison.OrdinalIgnoreCase)
            || _inner.IsReparsePoint(path);

    public void WriteNewFileAndFlush(string path, byte[] bytes)
    {
        if (FailWrite) throw new UnauthorizedAccessException("texto de fallo de escritura");
        _inner.WriteNewFileAndFlush(path, bytes);
    }

    public void MoveNew(string source, string destination) => _inner.MoveNew(source, destination);
}
