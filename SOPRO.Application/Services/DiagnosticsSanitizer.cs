using System.Text;
using System.Text.RegularExpressions;

namespace SOPRO.Application.Services;

/// <summary>
/// Sanitización aplicada en la frontera de persistencia del diagnóstico.
/// Sustituye el perfil del usuario por <c>%USERPROFILE%</c>, elimina caracteres de
/// control, redacta patrones obvios de secretos y acota la longitud. No pretende
/// ser una garantía criptográfica: el payload de origen ya excluye texto libre de
/// excepciones y datos de negocio.
/// </summary>
internal static partial class DiagnosticsSanitizer
{
    internal const int MaxValueLength = 1024;
    private const int MaxInputLength = 8192;
    private const string Redacted = "[REDACTED]";
    private const string ProfileToken = "%USERPROFILE%";

    private static readonly string[] ProfileRoots = BuildProfileRoots();

    internal static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var candidate = value.Length > MaxInputLength
            ? value[..MaxInputLength]
            : value;

        candidate = ReplaceProfileRoots(candidate);
        candidate = BearerTokenRegex().Replace(candidate, $"$1 {Redacted}");
        candidate = SecretAssignmentRegex().Replace(
            candidate,
            static match => string.Concat(match.Groups[1].Value, match.Groups[2].Value, Redacted));
        candidate = KnownTokenRegex().Replace(candidate, Redacted);
        candidate = StripControlCharacters(candidate);

        return candidate.Length > MaxValueLength
            ? candidate[..MaxValueLength] + "..."
            : candidate;
    }

    private static string[] BuildProfileRoots()
    {
        var roots = new List<string>(4);

        void Add(string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var trimmed = root.TrimEnd('\\', '/');
            if (trimmed.Length == 0)
            {
                return;
            }

            if (!roots.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(trimmed);
            }
        }

        Add(SafeGetFolder(Environment.SpecialFolder.UserProfile));
        Add(Environment.GetEnvironmentVariable("USERPROFILE"));
        Add(Environment.GetEnvironmentVariable("HOME"));

        // Los más largos primero para no dejar prefijos a medio sustituir.
        roots.Sort(static (left, right) => right.Length.CompareTo(left.Length));
        return roots.ToArray();
    }

    private static string? SafeGetFolder(Environment.SpecialFolder folder)
    {
        try
        {
            return Environment.GetFolderPath(folder);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ReplaceProfileRoots(string value)
    {
        var result = value;
        foreach (var root in ProfileRoots)
        {
            result = ReplaceProfileRoot(result, root);

            var alternative = root.Replace('\\', '/');
            if (!string.Equals(alternative, root, StringComparison.Ordinal))
            {
                result = ReplaceProfileRoot(result, alternative);
            }

            alternative = root.Replace('/', '\\');
            if (!string.Equals(alternative, root, StringComparison.Ordinal))
            {
                result = ReplaceProfileRoot(result, alternative);
            }
        }

        return result;
    }

    /// <summary>
    /// Sustituye el perfil solo cuando termina en fin de cadena o en un carácter que
    /// no continúe el nombre de la ruta (separador, espacio, comilla, etc.). Así
    /// <c>.../usuario</c> no transforma <c>.../usuariocopia</c>.
    /// </summary>
    private static string ReplaceProfileRoot(string value, string root)
    {
        var index = IndexOfProfileRoot(value, root, 0);
        if (index < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var start = 0;
        while (index >= 0)
        {
            builder.Append(value, start, index - start);
            builder.Append(ProfileToken);
            start = index + root.Length;
            index = IndexOfProfileRoot(value, root, start);
        }

        builder.Append(value, start, value.Length - start);
        return builder.ToString();
    }

    private static int IndexOfProfileRoot(string value, string root, int startIndex)
    {
        var searchFrom = startIndex;
        while (searchFrom <= value.Length - root.Length)
        {
            var index = value.IndexOf(root, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return -1;
            }

            var end = index + root.Length;
            if (end == value.Length || !IsPathNameCharacter(value[end]))
            {
                return index;
            }

            searchFrom = index + 1;
        }

        return -1;
    }

    private static bool IsPathNameCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '_' or '-' or '.';

    private static string StripControlCharacters(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                if (builder.Length == 0 || builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"\b(Bearer)\s+[A-Za-z0-9\-._~+/]{6,}=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        @"\b(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?token|refresh[_-]?token|authorization)\b(\s*[:=]\s*)(""[^""]*""|'[^']*'|[^\s,;&]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(
        @"\b(?:gh[pousr]_[A-Za-z0-9]{8,}|github_pat_[A-Za-z0-9_]{8,}|sk-[A-Za-z0-9]{8,}|AKIA[0-9A-Z]{12,})",
        RegexOptions.CultureInvariant)]
    private static partial Regex KnownTokenRegex();
}
