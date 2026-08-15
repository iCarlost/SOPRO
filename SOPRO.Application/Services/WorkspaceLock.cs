using System.IO;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Candado exclusivo de workspace (decisión N4): un archivo .lock junto a la
    /// base de datos, retenido con FileShare.None mientras la sesión está abierta.
    /// Impide que dos instancias (o dos ventanas) editen el mismo proyecto a la vez.
    /// El candado es el handle abierto: un .lock sobrante de una sesión abortada no
    /// bloquea la siguiente apertura.
    /// </summary>
    public interface IWorkspaceLock : IDisposable
    {
        string LockFilePath { get; }
    }

    public sealed class WorkspaceLock : IWorkspaceLock
    {
        private readonly FileStream? _stream;
        private string? _lockFilePath;

        private WorkspaceLock(string lockFilePath, FileStream stream)
        {
            _lockFilePath = lockFilePath;
            _stream = stream;
        }

        public string LockFilePath => _lockFilePath ?? string.Empty;

        public static IWorkspaceLock Acquire(string databasePath)
        {
            var lockPath = databasePath + ".lock";
            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new WorkspaceLock(lockPath, stream);
            }
            catch (IOException ex) when (ex is not DirectoryNotFoundException)
            {
                // Violación de uso compartido: otra sesión/instancia retiene el candado.
                throw new WorkspaceLockedException(lockPath);
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();

            if (_lockFilePath is not null)
            {
                try
                {
                    File.Delete(_lockFilePath);
                }
                catch (IOException)
                {
                    // Best-effort: si otra instancia ya lo reabrió, el archivo es suyo.
                }

                _lockFilePath = null;
            }
        }
    }

    /// <summary>
    /// El proyecto ya está abierto en otra ventana o instancia de SOPRO.
    /// </summary>
    public sealed class WorkspaceLockedException : IOException
    {
        public WorkspaceLockedException(string lockFilePath)
            : base(
                "El proyecto ya está abierto en otra ventana o instancia.\n"
                + $"Para editarlo, ciérralo ahí primero. (Archivo de bloqueo: {lockFilePath})")
        {
        }
    }
}