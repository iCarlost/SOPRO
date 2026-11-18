using System.Text;

namespace SOPRO.Application.Services;

/// <summary>Persistencia local e independiente de la carpeta de logs del consentimiento de privacidad.</summary>
public sealed class DiagnosticsPrivacyNoticeStore
{
    public const string CurrentVersion = "1";
    private readonly string _markerPath;

    public DiagnosticsPrivacyNoticeStore(string? localDataDirectory = null)
    {
        var root = localDataDirectory ?? SOPRO.Application.Contracts.WorkspacePaths.LocalDataFolder;
        _markerPath = Path.Combine(root, "diagnostics-privacy-notice.version");
    }

    public bool IsAcknowledged
    {
        get
        {
            try { return File.Exists(_markerPath) && File.ReadAllText(_markerPath, Encoding.UTF8) == CurrentVersion; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    public void Acknowledge()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_markerPath)!);
        var temporary = _markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(CurrentVersion);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _markerPath, overwrite: true);
        }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
