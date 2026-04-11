using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Models.Selector;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class CatalogSearchService
    {
        private readonly ProjectIndexService _projectIndexService;
        private readonly ProjectUsageService _projectUsageService;

        public CatalogSearchService(ProjectIndexService projectIndexService, ProjectUsageService projectUsageService)
        {
            _projectIndexService = projectIndexService ?? throw new ArgumentNullException(nameof(projectIndexService));
            _projectUsageService = projectUsageService ?? throw new ArgumentNullException(nameof(projectUsageService));
        }

        public IReadOnlyList<CatalogSearchResultDto> SearchMatrices(
            SOPROContext currentContext,
            int proyectoId,
            string query,
            bool includeCurrentProject,
            bool includeRecentProjects,
            bool includeFavoriteProjects,
            bool includeAuxiliaries,
            TipoMatriz? tipoFiltro = null,
            int maxResults = 100,
            IEnumerable<string>? specificProjectPaths = null)
        {
            if (currentContext == null) throw new ArgumentNullException(nameof(currentContext));
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<CatalogSearchResultDto>();

            var normalizedQuery = NormalizeQuery(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery)) return Array.Empty<CatalogSearchResultDto>();

            var results = new List<CatalogSearchResultDto>();
            var usageLookup = _projectUsageService.GetUsageLookup();
            var currentPath = NormalizePath(currentContext.DatabasePath);
            _projectIndexService.RefreshKnownProjects(currentPath);

            if (includeCurrentProject)
            {
                var currentProjectName = currentContext.Proyectos
                    .AsNoTracking()
                    .Where(p => p.Id == proyectoId)
                    .Select(p => p.Nombre)
                    .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(currentPath);

                var currentMatrices = includeAuxiliaries
                    ? MatrixApplicationService.GetProjectMatrices(currentContext, proyectoId, tipoFiltro)
                    : MatrixApplicationService.GetProjectApus(currentContext, proyectoId);

                var fechaRef = GetProjectReferenceDate(currentPath);
                results.AddRange(currentMatrices
                    .Select(m => MapCurrentResult(m, currentPath, currentProjectName, fechaRef, normalizedQuery, usageLookup))
                    .Where(r => r.Score > 0));
            }

            IReadOnlyList<ProjectCandidateInfo> candidates;
            if (specificProjectPaths != null)
            {
                var specific = specificProjectPaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => NormalizePath(p!))
                    .Where(File.Exists)
                    .Where(p => !string.Equals(p, currentPath, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(p => new ProjectCandidateInfo(
                        p,
                        Path.GetFileNameWithoutExtension(p),
                        _projectIndexService.IsFavorite(p),
                        true))
                    .ToList();
                candidates = specific;
            }
            else
            {
                candidates = _projectIndexService.GetCandidateProjects(currentPath, includeRecentProjects, includeFavoriteProjects);
            }

            foreach (var candidate in candidates)
            {
                ProjectCatalogIndexFile index;
                try
                {
                    index = EnsureProjectCatalogIndex(candidate.FilePath);
                }
                catch
                {
                    continue;
                }

                foreach (var entry in index.Matrices)
                {
                    if (!includeAuxiliaries && entry.Tipo != TipoMatriz.APU)
                        continue;
                    if (includeAuxiliaries && tipoFiltro.HasValue && entry.Tipo != tipoFiltro.Value)
                        continue;

                    var mapped = MapExternalResult(entry, index, candidate.IsFavorite, candidate.IsRecent, normalizedQuery, usageLookup);
                    if (mapped.Score > 0)
                        results.Add(mapped);
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.EsActual)
                .ThenByDescending(r => r.EsFavorito)
                .ThenByDescending(r => r.EsReciente)
                .ThenByDescending(r => r.FrecuenciaUso)
                .ThenBy(r => r.Clave)
                .ThenBy(r => r.Descripcion)
                .Take(Math.Max(1, maxResults))
                .ToList();
        }

        public void RebuildProjectCatalogIndex(string projectPath)
        {
            BuildProjectCatalogIndex(projectPath);
        }

        public void RebuildIndexesForRecentProjects(int maxProjects = 20)
        {
            foreach (var candidate in _projectIndexService.GetCandidateProjects(null, true, true).Take(maxProjects))
            {
                try
                {
                    BuildProjectCatalogIndex(candidate.FilePath);
                }
                catch
                {
                    // Ignorar proyectos con errores de lectura para no bloquear el resto.
                }
            }
        }

        private ProjectCatalogIndexFile EnsureProjectCatalogIndex(string projectPath)
        {
            var existing = _projectIndexService.LoadCatalogIndex(projectPath);
            if (existing != null && _projectIndexService.IsCatalogIndexFresh(projectPath, existing))
                return existing;

            return BuildProjectCatalogIndex(projectPath);
        }

        private ProjectCatalogIndexFile BuildProjectCatalogIndex(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("La ruta del proyecto es requerida.", nameof(projectPath));
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("No se encontró el proyecto a indexar.", projectPath);

            using var context = new SOPROContext(projectPath);
            SchemaManager.EnsureCurrentSchema(context);
            var normalizedPath = NormalizePath(projectPath);
            var projectName = context.Proyectos
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => p.Nombre)
                .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(normalizedPath);

            var index = new ProjectCatalogIndexFile
            {
                ProjectPath = normalizedPath,
                ProjectName = projectName,
                IndexedAtUtc = DateTime.UtcNow,
                ProjectLastWriteUtc = File.GetLastWriteTimeUtc(normalizedPath),
                Matrices = context.Matrices
                    .AsNoTracking()
                    .OrderBy(m => m.Tipo)
                    .ThenBy(m => m.Clave)
                    .Select(m => new ProjectCatalogIndexEntry
                    {
                        MatrixId = m.Id,
                        Clave = m.Clave,
                        Descripcion = m.Descripcion,
                        Unidad = m.Unidad,
                        CostoDirecto = m.CostoDirecto,
                        Tipo = m.Tipo
                    })
                    .ToList()
            };

            _projectIndexService.SaveCatalogIndex(index);
            _projectIndexService.RegisterProjectOpened(normalizedPath, projectName);
            return index;
        }

        private static CatalogSearchResultDto MapCurrentResult(
            MatrixListItemDto item,
            string projectPath,
            string projectName,
            DateTime? fechaRef,
            string query,
            IReadOnlyDictionary<string, MatrixUsageEntry> usageLookup)
        {
            var frecuenciaUso = ResolveUsageCount(usageLookup, projectPath, item.Id);
            return new CatalogSearchResultDto
            {
                TipoElemento = "Matriz",
                RutaProyecto = projectPath,
                NombreProyecto = projectName,
                ElementoId = item.Id,
                Clave = item.Clave,
                Descripcion = item.Descripcion,
                Unidad = item.Unidad,
                PrecioOCosto = item.CostoDirecto,
                FechaReferencia = fechaRef,
                EsActual = true,
                EsFavorito = false,
                EsReciente = true,
                FrecuenciaUso = frecuenciaUso,
                TipoMatriz = item.Tipo,
                Score = CalculateScore(item.Clave, item.Descripcion, query, true, false, true, frecuenciaUso)
            };
        }

        private static CatalogSearchResultDto MapExternalResult(
            ProjectCatalogIndexEntry entry,
            ProjectCatalogIndexFile index,
            bool isFavorite,
            bool isRecent,
            string query,
            IReadOnlyDictionary<string, MatrixUsageEntry> usageLookup)
        {
            var frecuenciaUso = ResolveUsageCount(usageLookup, index.ProjectPath, entry.MatrixId);
            return new CatalogSearchResultDto
            {
                TipoElemento = "Matriz",
                RutaProyecto = index.ProjectPath,
                NombreProyecto = index.ProjectName,
                ElementoId = entry.MatrixId,
                Clave = entry.Clave,
                Descripcion = entry.Descripcion,
                Unidad = entry.Unidad,
                PrecioOCosto = entry.CostoDirecto,
                FechaReferencia = index.ProjectLastWriteUtc == default ? null : index.ProjectLastWriteUtc.ToLocalTime(),
                EsActual = false,
                EsFavorito = isFavorite,
                EsReciente = isRecent,
                FrecuenciaUso = frecuenciaUso,
                TipoMatriz = entry.Tipo,
                Score = CalculateScore(entry.Clave, entry.Descripcion, query, false, isFavorite, isRecent, frecuenciaUso)
            };
        }

        private static decimal CalculateScore(string? clave, string? descripcion, string query, bool isCurrent, bool isFavorite, bool isRecent, int frecuenciaUso)
        {
            var claveNorm = NormalizeQuery(clave);
            var descNorm = NormalizeQuery(descripcion);
            var score = 0m;

            if (string.IsNullOrWhiteSpace(query))
                return score;

            if (!string.IsNullOrWhiteSpace(claveNorm))
            {
                if (claveNorm == query) score += 160m;
                else if (claveNorm.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 130m;
                else if (claveNorm.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 78m;
            }

            if (!string.IsNullOrWhiteSpace(descNorm))
            {
                if (descNorm == query) score += 175m;
                else if (descNorm.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 145m;
                else if (descNorm.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 105m;
            }

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length > 0)
            {
                bool allTermsInClave = !string.IsNullOrWhiteSpace(claveNorm);
                bool allTermsInDesc = !string.IsNullOrWhiteSpace(descNorm);
                bool orderedInClave = !string.IsNullOrWhiteSpace(claveNorm);
                bool orderedInDesc = !string.IsNullOrWhiteSpace(descNorm);
                int lastClaveIndex = -1;
                int lastDescIndex = -1;

                foreach (var term in terms)
                {
                    if (!string.IsNullOrWhiteSpace(claveNorm))
                    {
                        int claveIndex = claveNorm.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                        if (claveIndex >= 0)
                        {
                            score += 10m;
                            if (claveNorm.Contains($" {term}", StringComparison.OrdinalIgnoreCase))
                                score += 4m;
                            if (claveIndex == 0 || (claveIndex > 0 && claveNorm[claveIndex - 1] == ' '))
                                score += 7m;
                            if (orderedInClave)
                            {
                                if (claveIndex < lastClaveIndex) orderedInClave = false;
                                else lastClaveIndex = claveIndex;
                            }
                        }
                        else
                        {
                            allTermsInClave = false;
                            orderedInClave = false;
                        }
                    }
                    else
                    {
                        allTermsInClave = false;
                        orderedInClave = false;
                    }

                    if (!string.IsNullOrWhiteSpace(descNorm))
                    {
                        int descIndex = descNorm.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                        if (descIndex >= 0)
                        {
                            score += 18m;
                            if (descNorm.Contains($" {term}", StringComparison.OrdinalIgnoreCase))
                                score += 6m;
                            if (descIndex == 0 || (descIndex > 0 && descNorm[descIndex - 1] == ' '))
                                score += 10m;
                            if (orderedInDesc)
                            {
                                if (descIndex < lastDescIndex) orderedInDesc = false;
                                else lastDescIndex = descIndex;
                            }
                        }
                        else
                        {
                            allTermsInDesc = false;
                            orderedInDesc = false;
                        }
                    }
                    else
                    {
                        allTermsInDesc = false;
                        orderedInDesc = false;
                    }
                }

                if (allTermsInDesc) score += 36m;
                if (allTermsInClave) score += 20m;
                if (orderedInDesc && terms.Length > 1) score += 24m;
                if (orderedInClave && terms.Length > 1) score += 12m;
            }

            if (!string.IsNullOrWhiteSpace(descNorm) && descNorm.Length > query.Length)
                score += Math.Max(0m, 10m - Math.Min(10m, descNorm.Length - query.Length));

            if (isCurrent) score += 55m;
            if (isFavorite) score += 26m;
            if (isRecent) score += 14m;
            if (frecuenciaUso > 0) score += Math.Min(frecuenciaUso, 20) * 7m;

            return score;
        }

        private static int ResolveUsageCount(IReadOnlyDictionary<string, MatrixUsageEntry> usageLookup, string projectPath, int matrixId)
        {
            if (usageLookup == null || usageLookup.Count == 0 || string.IsNullOrWhiteSpace(projectPath) || matrixId <= 0)
                return 0;

            return usageLookup.TryGetValue(ProjectUsageService.BuildKey(projectPath, matrixId), out var entry)
                ? entry.UsageCount
                : 0;
        }

        private static string NormalizeQuery(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
            return string.Join(' ', new string(chars).Normalize(System.Text.NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static DateTime? GetProjectReferenceDate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            return File.GetLastWriteTime(path);
        }

        private static string NormalizePath(string path) => Path.GetFullPath(path.Trim());
    }
}
