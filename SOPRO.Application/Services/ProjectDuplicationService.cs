using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace SOPRO.Application.Services;

/// <summary>
/// Prepara y publica una copia de un proyecto a partir de una fuente que puede
/// estar abierta en otra ventana/instancia e incluso usar una extensión legacy
/// (<c>.db</c> / <c>.sopro</c>).
///
/// Flujo:
///   1. La fuente nunca se abre con SQLite. Se adquieren handles exclusivos
///      (<c>FileShare.None</c>) del archivo principal y de todos los sidecars
///      existentes; así una conexión SQLite activa impide la copia. Se copian el
///      principal y los sidecars de datos (WAL/journal), pero no el índice <c>-shm</c>.
///   2. Se copian solo mientras todos los handles exclusivos siguen abiertos, por
///      lo que ningún proceso SQLite puede cambiar el conjunto durante el snapshot.
///   3. La versión registrada se valida sobre el temporal ya copiado: una versión
///      futura o ilegible se rechaza (fallo cerrado). Si es anterior o no tiene
///      versión, se migra SOLO el temporal.
///   4. El resultado es un <see cref="ProjectDuplicationDraft"/> con metadata
///      <see cref="Proyecto"/> desacoplada para el diálogo.
///   5. <see cref="ProjectDuplicationDraft.Publish"/> toma el candado del destino,
///      aplica únicamente la metadata editable, conserva los valores originales
///      de IVA/decimales, cierra/checkpointa el WAL, exige que no queden sidecars
///      temporales y mueve el temporal al nombre canónico <c>.soproproj</c> sin
///      sobrescribir.
/// </summary>
public sealed class ProjectDuplicationService
{
    private static readonly string[] SidecarSuffixes = { "-wal", "-shm", "-journal" };

    private readonly ProjectWorkspaceService _workspaceService;
    private readonly IProjectDbContextFactory _dbContextFactory;
    private readonly IProjectDraftFileOperations _fileOperations;
    private readonly Func<string, IWorkspaceLock> _acquireDraftLock;

    public ProjectDuplicationService(
        ProjectWorkspaceService workspaceService,
        IProjectDbContextFactory? dbContextFactory = null)
        : this(workspaceService, dbContextFactory, fileOperations: null)
    {
    }

    /// <summary>
    /// Seam interno para pruebas: permite observar/forzar fallos de eliminación
    /// sin tocar archivos reales. La composición de producción usa el constructor
    /// público y las operaciones de archivo por defecto.
    /// </summary>
    internal ProjectDuplicationService(
        ProjectWorkspaceService workspaceService,
        IProjectDbContextFactory? dbContextFactory,
        IProjectDraftFileOperations? fileOperations)
        : this(workspaceService, dbContextFactory, fileOperations, lockFactory: null)
    {
    }

    /// <summary>
    /// Seam interno adicional para pruebas: permite inyectar un candado cuyo
    /// <see cref="IDisposable.Dispose"/> falle y comprobar que la limpieza de
    /// archivos se intenta igualmente y que ambos fallos se exponen.
    /// </summary>
    internal ProjectDuplicationService(
        ProjectWorkspaceService workspaceService,
        IProjectDbContextFactory? dbContextFactory,
        IProjectDraftFileOperations? fileOperations,
        Func<string, IWorkspaceLock>? lockFactory)
    {
        _workspaceService = workspaceService
            ?? throw new ArgumentNullException(nameof(workspaceService));
        _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
        _fileOperations = fileOperations ?? ProjectDraftFileOperations.Default;
        _acquireDraftLock = lockFactory ?? WorkspaceLock.Acquire;
    }

    /// <summary>
    /// Abre un borrador aislado del proyecto <paramref name="sourceProjectPath"/>.
    /// El origen solo se lee a nivel de archivo. Mientras el borrador no se
    /// publique, dispose lo elimina junto con sus sidecars y su candado.
    /// </summary>
    /// <exception cref="ArgumentException">Ruta vacía o extensión no admitida.</exception>
    /// <exception cref="FileNotFoundException">El origen no existe.</exception>
    /// <exception cref="InvalidOperationException">
    /// La versión de esquema del origen es más reciente que la soportada o no es
    /// reconocible (fallo cerrado), el origen no contiene un único proyecto, o no
    /// no fue posible bloquear el origen exclusivamente para una instantánea segura.
    /// </exception>
    public ProjectDuplicationDraft PrepareDuplicate(string sourceProjectPath)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectPath))
            throw new ArgumentException("Debe indicar la ruta del proyecto de origen.", nameof(sourceProjectPath));

        var sourcePath = Path.GetFullPath(sourceProjectPath.Trim());

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("No se encontró el proyecto de origen.", sourcePath);

        if (!ProjectFileExtensions.IsSupportedForOpen(sourcePath))
            throw new ArgumentException(
                "La ruta indicada no es un archivo de proyecto SOPRO admitido.", nameof(sourceProjectPath));

        _workspaceService.EnsureWorkspaceExists();

        var draftPath = Path.Combine(
            _workspaceService.ProjectsFolder,
            $".sopro-duplicado-{Guid.NewGuid():N}.tmp");

        // El candado del borrador vive en la carpeta de destino y se toma ANTES
        // de escribir el temporal. El candado del origen nunca se toca.
        var draftLock = _acquireDraftLock(draftPath);
        try
        {
            CreateDraftSnapshot(sourcePath, draftPath);
            var draftNeedsMigration = ValidateSourceVersion(ReadRegisteredVersion(draftPath));

            Proyecto project;
            using (var context = _dbContextFactory.Create(draftPath))
            {
                if (draftNeedsMigration)
                    SchemaManager.EnsureCurrentSchema(context);

                project = ReadSingleProject(context);
            }

            CheckpointAndClose(draftPath);
            DeleteSidecars(draftPath, _fileOperations);

            return new ProjectDuplicationDraft(
                _workspaceService,
                _dbContextFactory,
                _fileOperations,
                draftPath,
                draftLock,
                project);
        }
        catch (Exception original)
        {
            // La limpieza de archivos se intenta SIEMPRE, incluso si el candado
            // falla al liberarse. Nunca se oculta una ruta residual detrás del
            // error del candado.
            Exception? lockError = null;
            Exception? cleanupError = null;
            IReadOnlyList<string> residual = Array.Empty<string>();

            try
            {
                residual = TryDeleteDraftFiles(draftPath, _fileOperations);
                if (residual.Count > 0)
                    cleanupError = new ProjectDraftCleanupException(residual);
            }
            catch (Exception ex)
            {
                cleanupError = ex;
                residual = GetDraftFileCandidates(draftPath);
            }
            finally
            {
                try
                {
                    draftLock.Dispose();
                }
                catch (Exception ex)
                {
                    lockError = ex;
                }
            }

            if (lockError is not null || residual.Count > 0 || cleanupError is not null)
            {
                var inner = cleanupError is null
                    ? original
                    : new AggregateException(original, cleanupError);
                throw new ProjectDraftCleanupException(residual, inner, lockError);
            }

            throw;
        }
    }

    private string? ReadRegisteredVersion(string databasePath)
    {
        // Se abre el temporal (nunca el origen) en modo lectura/escritura para
        // que SQLite recupere el WAL/journal copiado si fuera necesario.
        using var context = _dbContextFactory.Create(databasePath);
        return SchemaManager.GetVersionRegistrada(context);
    }

    private static bool ValidateSourceVersion(string? registeredVersion)
    {
        if (registeredVersion is null)
            return true;

        var comparison = CompareVersions(registeredVersion, SchemaManager.VersionActual);
        if (comparison is null)
        {
            throw new InvalidOperationException(
                "El proyecto de origen registra una versión de esquema no reconocida "
                + $"({registeredVersion}). Por seguridad no se duplica: ábralo con una "
                + "versión de SOPRO que reconozca ese esquema.");
        }

        if (comparison > 0)
        {
            throw new InvalidOperationException(
                "El proyecto de origen usa una versión de esquema más reciente "
                + $"({registeredVersion}) que la soportada por esta instalación "
                + $"({SchemaManager.VersionActual}). Actualice SOPRO para duplicarlo.");
        }

        return comparison < 0;
    }

    private static Proyecto ReadSingleProject(SOPROContext context)
    {
        var projects = context.Proyectos
            .AsNoTracking()
            .Take(2)
            .ToList();

        if (projects.Count != 1)
        {
            throw new InvalidOperationException(
                "El archivo no contiene exactamente un proyecto válido para duplicar.");
        }

        return projects[0];
    }

    /// <summary>
    /// Crea el temporal a partir de una instantánea de archivos bloqueada contra
    /// aperturas SQLite concurrentes. Se adquieren handles exclusivos del archivo
    /// principal y de cada sidecar existente; luego se copian el principal, WAL y
    /// journal (nunca el shm, que es un índice reconstruible). Si otro proceso
    /// mantiene abierto el proyecto, la adquisición exclusiva falla cerrada.
    /// </summary>
    private void CreateDraftSnapshot(string sourcePath, string destinationPath)
    {
        // Microsoft.Data.Sqlite usa pooling por defecto en los contextos de
        // proyecto. Al cerrar una sesión, el handle puede permanecer en el pool
        // e impedir el handle exclusivo aunque ya no haya ninguna ventana abierta.
        // Limpiar solo el pool de esta fuente libera conexiones inactivas; una
        // conexión todavía activa seguirá bloqueando la copia de forma segura.
        ClearPool(sourcePath);

        using var main = OpenExclusiveRead(sourcePath);
        using var wal = OpenOptionalExclusiveRead(sourcePath + "-wal");
        using var journal = OpenOptionalExclusiveRead(sourcePath + "-journal");
        using var shm = OpenOptionalExclusiveRead(sourcePath + "-shm");

        CopyStream(main, destinationPath);
        if (wal is not null)
            CopyStream(wal, destinationPath + "-wal");
        if (journal is not null)
            CopyStream(journal, destinationPath + "-journal");

        // El -shm contiene índices y marcas de lectura, no páginas de datos;
        // nunca se copia al borrador ni se abre el origen con SQLite.
        DeleteIfExists(destinationPath + "-shm");

    }

    private static FileStream? OpenOptionalExclusiveRead(string path)
        => File.Exists(path) ? OpenExclusiveRead(path) : null;

    private static void CopyStream(FileStream source, string destinationPath)
    {
        using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.SequentialScan);

        source.CopyTo(destination);
        destination.Flush(flushToDisk: true);
    }

    private static FileStream OpenExclusiveRead(string path)
    {
        try
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.SequentialScan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "No se pudo bloquear exclusivamente un archivo del proyecto de origen. "
                + "Otro proceso puede tener el proyecto abierto; ciérrelo e intente duplicarlo de nuevo.",
                ex);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    internal static void CheckpointAndClose(string databasePath)
    {
        ClearPool(databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        var result = command.ExecuteScalar();
        var busy = result is null or DBNull
            ? 0
            : Convert.ToInt32(result, CultureInfo.InvariantCulture);

        if (busy != 0)
        {
            throw new InvalidOperationException(
                "No se pudo cerrar el WAL del borrador del proyecto: hay lectores activos.");
        }
    }

    internal static void ClearPool(string databasePath)
        => SqliteConnectionPool.Clear(databasePath);

    internal static void DeleteSidecars(string databasePath)
        => DeleteSidecars(databasePath, ProjectDraftFileOperations.Default);

    /// <summary>
    /// Elimina los sidecars SQLite del temporal con reintentos acotados. Si
    /// alguno sigue existiendo, lanza <see cref="ProjectDraftCleanupException"/>
    /// antes de que la publicación pueda mover el archivo.
    /// </summary>
    internal static void DeleteSidecars(string databasePath, IProjectDraftFileOperations operations)
    {
        var paths = SidecarSuffixes
            .Select(suffix => databasePath + suffix)
            .ToArray();

        var residual = DeletePathsWithRetry(databasePath, paths, operations);
        if (residual.Count > 0)
            throw new ProjectDraftCleanupException(residual);
    }

    internal static void DeleteDraftFiles(string databasePath)
        => DeleteDraftFiles(databasePath, ProjectDraftFileOperations.Default);

    internal static void DeleteDraftFiles(string databasePath, IProjectDraftFileOperations operations)
    {
        var residual = TryDeleteDraftFiles(databasePath, operations);
        if (residual.Count > 0)
            throw new ProjectDraftCleanupException(residual);
    }

    /// <summary>
    /// Intenta borrar sidecars y archivo principal con reintentos acotados. No
    /// olvida las rutas: devuelve las que sigan existiendo para que el llamador
    /// las exponga o reintente.
    /// </summary>
    internal static IReadOnlyList<string> TryDeleteDraftFiles(
        string databasePath,
        IProjectDraftFileOperations operations)
    {
        return DeletePathsWithRetry(databasePath, GetDraftFileCandidates(databasePath), operations);
    }

    /// <summary>
    /// Rutas exactas que puede ocupar un borrador (principal + sidecars). Se usan
    /// para exponer residuos incluso si la propia limpieza falla de forma inesperada.
    /// </summary>
    internal static IReadOnlyList<string> GetDraftFileCandidates(string databasePath)
        => SidecarSuffixes
            .Select(suffix => databasePath + suffix)
            .Append(databasePath)
            .ToArray();

    private static IReadOnlyList<string> DeletePathsWithRetry(
        string databasePath,
        IReadOnlyList<string> paths,
        IProjectDraftFileOperations operations)
    {
        IReadOnlyList<string> residual = Array.Empty<string>();

        for (var attempt = 1; attempt <= ProjectDraftFileOperations.MaxDeleteAttempts; attempt++)
        {
            operations.ClearSqlitePool(databasePath);

            var attemptResidual = new List<string>();
            foreach (var path in paths)
            {
                if (!operations.FileExists(path))
                    continue;

                try
                {
                    operations.DeleteFile(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                if (operations.FileExists(path))
                    attemptResidual.Add(path);
            }

            if (attemptResidual.Count == 0)
                return Array.Empty<string>();

            residual = attemptResidual;
            if (attempt < ProjectDraftFileOperations.MaxDeleteAttempts)
                operations.Pause(ProjectDraftFileOperations.DeleteRetryDelayMilliseconds);
        }

        return residual;
    }

    private static int? CompareVersions(string left, string right)
    {
        var leftParts = ParseVersion(left);
        var rightParts = ParseVersion(right);
        if (leftParts is null || rightParts is null)
            return null;

        for (var i = 0; i < leftParts.Length; i++)
        {
            var comparison = leftParts[i].CompareTo(rightParts[i]);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }

    private static int[]? ParseVersion(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 3
            || parts[0].Length != 4
            || parts[1].Length != 2
            || parts[2].Length == 0)
            return null;

        var parsed = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                return null;

            foreach (var caracter in parts[i])
            {
                if (caracter is < '0' or > '9')
                    return null;
            }

            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out parsed[i]))
                return null;
        }

        return parsed;
    }
}

/// <summary>
/// Operaciones de archivo usadas por la limpieza y la verificación de sidecars.
/// Es un seam interno y estrecho para poder probar fallos de borrado de forma
/// determinista sin depender de las reglas de sharing del sistema operativo.
/// </summary>
internal interface IProjectDraftFileOperations
{
    bool FileExists(string path);

    void DeleteFile(string path);

    void ClearSqlitePool(string databasePath);

    void Pause(int milliseconds);
}

internal sealed class ProjectDraftFileOperations : IProjectDraftFileOperations
{
    internal const int MaxDeleteAttempts = 5;
    internal const int DeleteRetryDelayMilliseconds = 100;

    public static ProjectDraftFileOperations Default { get; } = new();

    public bool FileExists(string path) => File.Exists(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void ClearSqlitePool(string databasePath)
        => ProjectDuplicationService.ClearPool(databasePath);

    public void Pause(int milliseconds) => Thread.Sleep(milliseconds);
}

/// <summary>
/// El temporal de duplicación o sus sidecars no pudieron eliminarse. Las rutas
/// residuales se exponen para que la UI pueda reintentar o informar con precisión
/// sin olvidar el borrador.
/// </summary>
internal sealed class ProjectDraftCleanupException : IOException
{
    public ProjectDraftCleanupException(IReadOnlyList<string> residualPaths)
        : this(residualPaths, innerException: null, lockException: null)
    {
    }

    public ProjectDraftCleanupException(
        IReadOnlyList<string> residualPaths,
        Exception? innerException)
        : this(residualPaths, innerException, lockException: null)
    {
    }

    public ProjectDraftCleanupException(
        IReadOnlyList<string> residualPaths,
        Exception? innerException,
        Exception? lockException)
        : base(BuildMessage(residualPaths, lockException), innerException)
    {
        ResidualPaths = residualPaths;
        LockException = lockException;
    }

    public IReadOnlyList<string> ResidualPaths { get; }

    /// <summary>
    /// Error al liberar el candado durante la limpieza, si ocurrió. Se conserva
    /// aparte para no perder ni el error del candado ni las rutas residuales.
    /// </summary>
    public Exception? LockException { get; }

    private static string BuildMessage(
        IReadOnlyList<string> residualPaths,
        Exception? lockException)
    {
        var message = "No se pudieron eliminar todos los archivos temporales de la duplicación. "
            + "Rutas residuales: " + string.Join("; ", residualPaths);

        if (lockException is not null)
        {
            message += " Error al liberar el candado del borrador: "
                + lockException.Message;
        }

        return message;
    }
}
