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

    public bool FailAppend { get; init; }

    public bool FailLock { get; init; }

    public bool FailEnumerate { get; set; }

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

    public FileStream OpenExclusiveLock(string path) =>
        FailLock ? throw new IOException("lock inyectado") : _inner.OpenExclusiveLock(path);

    public void Move(string source, string destination) => _inner.Move(source, destination);

    public void Delete(string path) => _inner.Delete(path);

    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
        FailEnumerate
            ? throw new IOException("enumerate inyectado")
            : _inner.EnumerateFiles(directory, searchPattern);

    public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);
}
