using System;
using System.Text.RegularExpressions;

namespace SOPRO.Application.Services
{
    public static class ImportOriginStampService
    {
        private static readonly Regex StampRegex = new(@"\[IMPORTADO DE:\s*(.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string AppendStamp(string? notes, string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return notes ?? string.Empty;

            var stamp = $"[IMPORTADO DE: {projectName.Trim()}]";
            var baseNotes = string.IsNullOrWhiteSpace(notes) ? string.Empty : notes!.Trim();
            if (StampRegex.IsMatch(baseNotes))
                return StampRegex.Replace(baseNotes, stamp, 1);
            return string.IsNullOrWhiteSpace(baseNotes) ? stamp : stamp + " " + baseNotes;
        }

        public static string? ExtractProjectName(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            var match = StampRegex.Match(notes);
            if (!match.Success) return null;
            var value = match.Groups[1].Value?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public static string BuildOriginDisplay(string? notes, string fallback = "Local")
        {
            var project = ExtractProjectName(notes);
            return string.IsNullOrWhiteSpace(project) ? fallback : $"Importado: {project}";
        }
    }
}
