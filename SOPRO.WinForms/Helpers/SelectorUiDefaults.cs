using System;
using System.IO;

namespace SOPRO.WinForms.Helpers
{
    internal static class SelectorUiDefaults
    {
        public const string ExaminarTag = "__EXAMINAR__";
        public const string ScopeAllTag = "__SCOPE_ALL__";
        public const string ScopeCurrentTag = "__SCOPE_CURRENT__";
        public const string ScopeRecentTag = "__SCOPE_RECENT__";
        public const string ScopeFavoritesTag = "__SCOPE_FAVORITES__";

        public const int MaxProjectsInCombo = 12;
        public const int MaxRowsInGrid = 120;
        public const int SearchDebounceMs = 350;

        public static string NormalizePath(string path) => Path.GetFullPath((path ?? string.Empty).Trim());

        public static DateTime? GetProjectReferenceDate(string? path)
            => !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.GetLastWriteTime(path) : null;

        public static string BuildBaseLoadStatus(int total, int shown, string label, string source)
            => total > shown
                ? $"Mostrando {shown} de {total} {label} de {source}. Refine la búsqueda para acotar más."
                : $"Mostrando {shown} {label} de {source}.";

        public static string BuildAllScopePrompt(string nounPlural)
            => $"Ámbito: todos. Escriba para buscar en todos los proyectos de {nounPlural}.";

        public static string BuildRecentScopePrompt()
            => "Ámbito: recientes. Escriba para buscar en proyectos recientes.";

        public static string BuildFavoritesScopePrompt()
            => "Ámbito: favoritos. Escriba para buscar en proyectos favoritos.";
    }
}
