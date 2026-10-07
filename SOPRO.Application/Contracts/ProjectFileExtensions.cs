namespace SOPRO.Application.Contracts;

/// <summary>
/// Fuente única de verdad de las extensiones de archivo de proyecto SOPRO.
/// Un proyecto es una base SQLite; la extensión canónica es <c>.soproproj</c>,
/// mientras que <c>.db</c> (legacy) y <c>.sopro</c> (alias transicional) se
/// siguen admitiendo al abrir. Todas las comparaciones son case-insensitive.
/// </summary>
public static class ProjectFileExtensions
{
    /// <summary>Extensión canónica de proyecto. Es la que se crea y asocia.</summary>
    public const string Canonical = ".soproproj";

    /// <summary>Extensión legacy de base de proyecto; siempre admitida al abrir.</summary>
    public const string LegacyDb = ".db";

    /// <summary>Alias transicional admitido al abrir (no se crea ni se asocia).</summary>
    public const string TransitionalSopro = ".sopro";

    /// <summary>
    /// Extensiones admitidas al abrir un proyecto, en orden de preferencia
    /// (canónica primero). Las comparaciones deben hacerse con
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static IReadOnlyList<string> SupportedForOpen { get; } =
        new[] { Canonical, LegacyDb, TransitionalSopro };

    /// <summary>
    /// Indica si <paramref name="pathOrExtension"/> corresponde a una extensión
    /// de proyecto admitida (ruta completa o extensión suelta, p. ej. <c>.db</c>).
    /// </summary>
    public static bool IsSupportedForOpen(string? pathOrExtension)
        => GetProjectExtension(pathOrExtension) is not null;

    /// <summary>
    /// Devuelve la extensión admitida normalizada (en minúsculas) presente en
    /// <paramref name="pathOrExtension"/>, o <c>null</c> si no es de proyecto.
    /// </summary>
    public static string? GetProjectExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return null;
        }

        var candidate = pathOrExtension.Trim();

        // Acepta tanto una ruta como una extensión suelta (".db").
        var extension = LooksLikeBareExtension(candidate)
            ? candidate
            : Path.GetExtension(candidate);

        return SupportedForOpen.FirstOrDefault(
            admitted => string.Equals(admitted, extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Deriva el patrón de búsqueda (glob) de una extensión admitida,
    /// por ejemplo <c>.soproproj</c> → <c>*.soproproj</c>.
    /// </summary>
    /// <exception cref="ArgumentException">Si la extensión no está admitida.</exception>
    public static string GetSearchPattern(string extension)
    {
        var normalized = NormalizeAdmittedExtension(extension);
        return "*" + normalized;
    }

    /// <summary>
    /// Patrones de búsqueda de todas las extensiones admitidas, en orden de
    /// preferencia (canónica primero).
    /// </summary>
    public static IReadOnlyList<string> GetOpenSearchPatterns()
        => SupportedForOpen.Select(admitted => "*" + admitted).ToArray();

    /// <summary>
    /// Construye el filtro de <c>OpenFileDialog</c>/<c>SaveFileDialog</c>:
    /// formato actual primero, luego los anteriores y por último todos los archivos.
    /// </summary>
    public static string BuildFileDialogFilter()
        => string.Join(
            "|",
            $"Proyecto SOPRO (*{Canonical})",
            "*" + Canonical,
            $"Proyectos anteriores (*{LegacyDb};*{TransitionalSopro})",
            $"*{LegacyDb};*{TransitionalSopro}",
            "Todos los archivos (*.*)",
            "*.*");

    private static bool LooksLikeBareExtension(string candidate)
        => candidate.Length > 1
           && candidate[0] == '.'
           && candidate.IndexOfAny(new[] { '\\', '/', ':' }) < 0;

    private static string NormalizeAdmittedExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("La extensión no puede estar vacía.", nameof(extension));
        }

        var trimmed = extension.Trim();
        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }

        if (!IsSupportedForOpen(trimmed))
        {
            throw new ArgumentException(
                $"Extensión de proyecto no admitida: '{extension}'.", nameof(extension));
        }

        return trimmed.ToLowerInvariant();
    }
}
