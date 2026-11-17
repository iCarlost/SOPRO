namespace SOPRO.Application.Services;

/// <summary>
/// Seam interno de E/S del sink de archivo. Existe para inyectar fallos en pruebas
/// sin tocar el sistema de archivos real ni depender de permisos.
/// </summary>
internal interface IDiagnosticsFileStore
{
    void CreateDirectory(string path);

    bool FileExists(string path);

    long GetLength(string path);

    FileStream OpenAppend(string path);

    FileStream OpenExclusiveLock(string path);

    void Move(string source, string destination);

    void Delete(string path);

    IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern);

    DateTime GetLastWriteTimeUtc(string path);
}

internal sealed class DiagnosticsFileStore : IDiagnosticsFileStore
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool FileExists(string path) => File.Exists(path);

    public long GetLength(string path) => new FileInfo(path).Length;

    public FileStream OpenAppend(string path) =>
        new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.None);

    public FileStream OpenExclusiveLock(string path) =>
        new FileStream(
            path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.None);

    public void Move(string source, string destination) => File.Move(source, destination, overwrite: true);

    public void Delete(string path) => File.Delete(path);

    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory, searchPattern)
            : Array.Empty<string>();

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
}
