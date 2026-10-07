using SOPRO.Application.Contracts;

namespace SOPRO.Application.Services;

/// <summary>
/// Extrae la ruta de un proyecto SOPRO a partir de los argumentos de línea de
/// comandos (por ejemplo al abrir un archivo desde el Explorador o por doble clic).
/// Toma el primer argumento que sea un archivo de proyecto admitido e ignora los
/// flags/switches (p. ej. <c>-x</c>, <c>--x</c>).
/// </summary>
public static class CommandLineProjectPathParser
{
    /// <summary>
    /// Intenta obtener la ruta absoluta normalizada del primer argumento que sea
    /// un archivo de proyecto admitido. Devuelve <c>false</c> y <c>path = null</c>
    /// si no hay argumento de archivo, está vacío o su extensión no se admite.
    /// </summary>
    public static bool TryParse(string[]? args, out string? path)
    {
        path = null;

        if (args is null)
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || IsSwitch(arg))
            {
                continue;
            }

            if (!ProjectFileExtensions.IsSupportedForOpen(arg))
            {
                continue;
            }

            path = Path.GetFullPath(arg);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Variante de conveniencia de <see cref="TryParse"/>; devuelve la ruta
    /// normalizada o <c>null</c> si no hay un argumento de proyecto válido.
    /// </summary>
    public static string? Parse(string[]? args)
        => TryParse(args, out var path) ? path : null;

    private static bool IsSwitch(string arg)
        => arg.Length > 1 && arg[0] == '-';
}
