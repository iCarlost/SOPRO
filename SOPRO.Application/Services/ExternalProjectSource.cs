using Microsoft.Data.Sqlite;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace SOPRO.Application.Services;

/// <summary>
/// Arrendamiento de una base externa (otro proyecto) para operaciones de solo
/// lectura: búsqueda de insumos, matrices y catálogos ajenos.
///
/// Contrato de acceso:
///   - Esquema registrado == <see cref="SchemaManager.VersionActual"/>: se abre un
///     contexto en solo lectura sobre el archivo ORIGINAL. Tratar la versión exacta
///     como contrato evita migrar (y por tanto escribir) una fuente ajena.
///   - Esquema más reciente que el soportado, o versión registrada no reconocible:
///     se rechaza (fallo cerrado); SOPRO no puede garantizar una lectura consistente
///     ni que migrar una copia sea seguro.
///   - Esquema anterior o sin versión: se copia con <c>BackupDatabase</c> de SQLite
///     a una base temporal única, se migra SOLO la copia y el arrendamiento expone
///     esa copia. El origen nunca se escribe; aquí no se usa <c>File.Copy</c>.
///
/// <see cref="OriginalPath"/> conserva la ruta lógica del proyecto externo para
/// que el consumidor pueda mostrarla o volver a referenciarla; <see cref="Context"/>
/// es el contexto utilizable. Al liberar se cierran las conexiones y, si hubo copia,
/// se elimina su directorio incluso si el cierre del contexto falla.
/// </summary>
public sealed class ExternalProjectSource : IDisposable
{
    private const string SnapshotRootFolder = "external-snapshots";

    private readonly bool _isSnapshot;
    private readonly string? _snapshotConnectionString;
    private string? _snapshotDirectory;
    private int _disposed;

    private ExternalProjectSource(
        SOPROContext context,
        string originalPath,
        string? snapshotDirectory,
        string? snapshotConnectionString = null)
    {
        Context = context;
        OriginalPath = originalPath;
        _isSnapshot = snapshotDirectory is not null;
        _snapshotDirectory = snapshotDirectory;
        _snapshotConnectionString = snapshotConnectionString;
    }

    /// <summary>Contexto utilizable durante la vida del arrendamiento.</summary>
    public SOPROContext Context { get; }

    /// <summary>Ruta lógica original del proyecto externo (puede no ser la conectada).</summary>
    public string OriginalPath { get; }

    /// <summary>Ruta del archivo realmente conectado (el original o la copia temporal).</summary>
    public string DatabasePath => Context.DatabasePath;

    /// <summary>Indica que el contexto trabaja sobre una copia temporal migrada.</summary>
    public bool IsSnapshot => _isSnapshot;

    /// <summary>
    /// Abre la fuente externa aplicando la política de esquema descrita en el tipo.
    /// </summary>
    /// <exception cref="ArgumentNullException">Falta la fábrica.</exception>
    /// <exception cref="ArgumentException">La ruta está vacía.</exception>
    /// <exception cref="FileNotFoundException">La ruta no existe.</exception>
    /// <exception cref="InvalidOperationException">
    /// El esquema de la fuente es más reciente que el actual o su versión registrada
    /// no es reconocible (se falla cerrado, sin migrar).
    /// </exception>
    public static ExternalProjectSource Open(IProjectDbContextFactory factory, string originalPath)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (string.IsNullOrWhiteSpace(originalPath))
            throw new ArgumentException("Debe indicar la ruta del proyecto externo.", nameof(originalPath));

        var fullPath = Path.GetFullPath(originalPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No se encontró el proyecto externo.", fullPath);

        var registeredVersion = ReadRegisteredVersion(factory, fullPath);
        if (registeredVersion is not null)
        {
            var comparison = CompareVersions(registeredVersion, SchemaManager.VersionActual);

            if (comparison is null)
                throw new InvalidOperationException(
                    "El proyecto externo registra una versión de esquema no reconocida "
                    + $"({registeredVersion}). Por seguridad no se migra: ábralo con una "
                    + "versión de SOPRO que reconozca ese esquema.");

            if (comparison > 0)
                throw new InvalidOperationException(
                    "El proyecto externo usa una versión de esquema más reciente "
                    + $"({registeredVersion}) que la soportada por esta instalación "
                    + $"({SchemaManager.VersionActual}). Actualice SOPRO para abrirlo.");

            if (comparison == 0)
                return new ExternalProjectSource(factory.CreateReadOnly(fullPath), fullPath, snapshotDirectory: null);
        }

        return OpenSnapshot(factory, fullPath);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            Context.Dispose();
        }
        catch (Exception)
        {
            // La limpieza de la copia no puede depender del cierre del contexto.
        }

        if (_snapshotDirectory is not null)
        {
            // El contexto temporal se construyó con la cadena estándar (pooling
            // habilitado): se limpia SOLO el pool de esa cadena, sin tocar los pools
            // de otros contextos del proceso.
            if (_snapshotConnectionString is not null)
                TryClearPool(_snapshotConnectionString);

            TryDeleteDirectory(_snapshotDirectory);
            _snapshotDirectory = null;
        }
    }

    private static void TryClearPool(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(connection);
        }
        catch (Exception)
        {
            // Best-effort: el borrado reintenta por si el handle sigue vivo.
        }
    }

    private static string? ReadRegisteredVersion(IProjectDbContextFactory factory, string path)
    {
        using var context = factory.CreateReadOnly(path);
        return SchemaManager.GetVersionRegistrada(context);
    }

    private static ExternalProjectSource OpenSnapshot(IProjectDbContextFactory factory, string fullPath)
    {
        var snapshotDirectory = Path.Combine(
            Path.GetTempPath(),
            "SOPRO",
            SnapshotRootFolder,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(snapshotDirectory);

        var snapshotPath = Path.Combine(snapshotDirectory, Path.GetFileName(fullPath));

        SOPROContext? context = null;
        try
        {
            CopyWithBackup(fullPath, snapshotPath);

            context = factory.Create(snapshotPath);
            SchemaManager.EnsureCurrentSchema(context);

            return new ExternalProjectSource(
                context,
                fullPath,
                snapshotDirectory,
                BuildConnectionString(snapshotPath));
        }
        catch
        {
            context?.Dispose();
            // Contexto creado por la fábrica (pooling estándar): limpiar solo su pool
            // antes de borrar para no dejar handles en Windows.
            TryClearPool(BuildConnectionString(snapshotPath));
            TryDeleteDirectory(snapshotDirectory);
            throw;
        }
    }

    private static string BuildConnectionString(string databasePath)
        => new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

    /// <summary>
    /// Copia consistente del archivo SQLite (incluye WAL pendiente) sin abrir el
    /// origen en escritura. Nunca usa <see cref="File.Copy(string, string)"/>.
    /// </summary>
    private static void CopyWithBackup(string sourcePath, string destinationPath)
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        using var source = new SqliteConnection(sourceConnectionString);
        source.Open();

        using var destination = new SqliteConnection(destinationConnectionString);
        destination.Open();

        source.BackupDatabase(destination);
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
        // Contrato documentado: AAAA.MM.revision con año de 4 dígitos, mes de 2 y
        // revisión de 1+ dígitos. Nada de componentes extra (2026.04.4.0 no equivale
        // a 2026.04.4), longitudes laxas (2026.4.4 / 26.04.4), signos ni espacios.
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

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort: el archivo temporal se libera; un residuo no debe romper
            // la operación del usuario.
            RetryDelete(directory);
        }
        catch (UnauthorizedAccessException)
        {
            RetryDelete(directory);
        }
    }

    private static void RetryDelete(string directory)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Thread.Sleep(50);
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
