using System;
using System.Text.RegularExpressions;

namespace SOPRO.Application.Services
{
    public static class ImportOriginStampService
    {
        /// <summary>Etiqueta de procedencia de una fila del catálogo maestro.</summary>
        public const string EtiquetaMaestro = "Maestro";

        /// <summary>Etiqueta de procedencia de una fila propiedad de un proyecto.</summary>
        public const string EtiquetaLocal = "Local";

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

        /// <summary>
        /// Texto visible de procedencia legacy. Sin marca devuelve
        /// <paramref name="fallback"/>; con marca conserva la redacción histórica
        /// <c>Importado: {proyecto}</c> para no romper a los consumidores existentes
        /// (p. ej. los goldens de paridad legacy del PDF de matrices). Para la
        /// redacción canónica y el respaldo por propiedad use
        /// <see cref="BuildOriginDisplayForOwner"/>.
        /// </summary>
        /// <param name="notes">Notas de la fila, donde se busca la marca de importación.</param>
        /// <param name="fallback">Etiqueta cuando la fila no está marcada como importada.</param>
        public static string BuildOriginDisplay(string? notes, string fallback = EtiquetaLocal)
        {
            var project = ExtractProjectName(notes);
            return string.IsNullOrWhiteSpace(project) ? fallback : $"Importado: {project}";
        }

        /// <summary>
        /// Formateador único de procedencia con respaldo basado en la PROPIEDAD real
        /// de la fila (<paramref name="ownerProjectId"/>), no en <c>Origen</c>: una
        /// copia importada conserva <c>Origen = Maestro</c> y reclamaría falsamente
        /// el catálogo maestro. Con marca de importación manda la marca y se muestra
        /// exactamente como <c>Importado de: {proyecto}</c>; sin marca, fila sin
        /// proyecto ⇒ <see cref="EtiquetaMaestro"/> y fila con proyecto ⇒
        /// <see cref="EtiquetaLocal"/>.
        /// </summary>
        /// <param name="notes">Notas de la fila, donde se busca la marca de importación.</param>
        /// <param name="ownerProjectId">ProyectoId real de la fila (null = catálogo maestro).</param>
        public static string BuildOriginDisplayForOwner(string? notes, int? ownerProjectId)
        {
            var project = ExtractProjectName(notes);
            if (!string.IsNullOrWhiteSpace(project))
                return $"Importado de: {project}";

            return ownerProjectId.HasValue ? EtiquetaLocal : EtiquetaMaestro;
        }
    }
}
