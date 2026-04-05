using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Insumos;
using SOPRO.Application.Models;
using SOPRO.Application.Models.Selector;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class InsumoSearchService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly ProjectIndexService _projectIndexService;
        private readonly ProjectUsageService _projectUsageService;
        private readonly ProjectWorkspaceService _workspaceService;

        private string InsumoIndexFolder => Path.Combine(_workspaceService.LocalDataFolder, "selector-insumo-index");

        public InsumoSearchService(ProjectIndexService projectIndexService, ProjectUsageService projectUsageService, ProjectWorkspaceService workspaceService)
        {
            _projectIndexService = projectIndexService ?? throw new ArgumentNullException(nameof(projectIndexService));
            _projectUsageService = projectUsageService ?? throw new ArgumentNullException(nameof(projectUsageService));
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        }

        public IReadOnlyList<InsumoSearchResultDto> SearchInsumos(
            SOPROContext currentContext,
            int proyectoId,
            TipoComponenteMatriz tipoComponente,
            string query,
            bool includeCurrentProject,
            bool includeRecentProjects,
            bool includeFavoriteProjects,
            int maxResults = 120,
            IEnumerable<string>? specificProjectPaths = null,
            bool incluirManoDeObraIndividual = true,
            bool incluirCuadrillas = true)
        {
            if (currentContext == null) throw new ArgumentNullException(nameof(currentContext));
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<InsumoSearchResultDto>();

            var normalizedQuery = NormalizeQuery(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery)) return Array.Empty<InsumoSearchResultDto>();

            var results = new List<InsumoSearchResultDto>();
            var usageLookup = _projectUsageService.GetInsumoUsageLookup();
            var currentPath = NormalizePath(currentContext.DatabasePath);

            if (includeCurrentProject)
            {
                var currentProjectName = currentContext.Proyectos.AsNoTracking()
                    .Where(p => p.Id == proyectoId)
                    .Select(p => p.Nombre)
                    .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(currentPath);
                var fechaRef = GetProjectReferenceDate(currentPath);
                var currentItems = InsumoSelectionService.GetSelectableInsumos(currentContext, proyectoId, tipoComponente, normalizedQuery, incluirManoDeObraIndividual, incluirCuadrillas);
                results.AddRange(currentItems.Select(item => MapCurrentResult(item, currentPath, currentProjectName, fechaRef, normalizedQuery, usageLookup)).Where(r => r.Score > 0));
            }

            IReadOnlyList<ProjectCandidateInfo> candidates;
            if (specificProjectPaths != null)
            {
                candidates = specificProjectPaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => NormalizePath(p!))
                    .Where(File.Exists)
                    .Where(p => !string.Equals(p, currentPath, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(p => new ProjectCandidateInfo(p, Path.GetFileNameWithoutExtension(p), _projectIndexService.IsFavorite(p), true))
                    .ToList();
            }
            else
            {
                candidates = _projectIndexService.GetCandidateProjects(currentPath, includeRecentProjects, includeFavoriteProjects);
            }

            foreach (var candidate in candidates)
            {
                ProjectInsumoIndexFile index;
                try
                {
                    index = EnsureProjectInsumoIndex(candidate.FilePath);
                }
                catch
                {
                    continue;
                }

                foreach (var entry in index.Insumos)
                {
                    if (entry.TipoComponente != tipoComponente && !(tipoComponente == TipoComponenteMatriz.ManoDeObra && entry.TipoComponente == TipoComponenteMatriz.Auxiliar && string.Equals(entry.Tag, "Cuadrilla", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (tipoComponente != TipoComponenteMatriz.ManoDeObra && entry.TipoComponente != tipoComponente)
                        continue;
                    if (tipoComponente == TipoComponenteMatriz.ManoDeObra)
                    {
                        if (!incluirManoDeObraIndividual && string.Equals(entry.Tag, "Individual", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!incluirCuadrillas && string.Equals(entry.Tag, "Cuadrilla", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    var mapped = MapExternalResult(entry, index, candidate.IsFavorite, candidate.IsRecent, normalizedQuery, usageLookup);
                    if (mapped.Score > 0)
                        results.Add(mapped);
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.FrecuenciaUso)
                .ThenByDescending(r => r.EsActual)
                .ThenByDescending(r => r.EsFavorito)
                .ThenBy(r => r.Clave)
                .Take(Math.Max(1, maxResults))
                .ToList();
        }

        public void RebuildProjectInsumoIndex(string projectPath)
        {
            BuildProjectInsumoIndex(projectPath);
        }

        private ProjectInsumoIndexFile EnsureProjectInsumoIndex(string projectPath)
        {
            var existing = LoadProjectInsumoIndex(projectPath);
            if (existing != null && IsProjectInsumoIndexFresh(projectPath, existing))
                return existing;

            return BuildProjectInsumoIndex(projectPath);
        }

        private ProjectInsumoIndexFile? LoadProjectInsumoIndex(string projectPath)
        {
            try
            {
                var filePath = GetProjectInsumoIndexFilePath(projectPath);
                if (!File.Exists(filePath)) return null;
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<ProjectInsumoIndexFile>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private bool IsProjectInsumoIndexFresh(string projectPath, ProjectInsumoIndexFile? index)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath) || index == null)
                return false;
            return index.ProjectLastWriteUtc >= File.GetLastWriteTimeUtc(NormalizePath(projectPath));
        }

        private ProjectInsumoIndexFile BuildProjectInsumoIndex(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("La ruta del proyecto es requerida.", nameof(projectPath));
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("No se encontró el proyecto a indexar.", projectPath);

            using var context = new SOPROContext(projectPath);
            SchemaManager.EnsureCurrentSchema(context);
            var normalizedPath = NormalizePath(projectPath);
            var projectName = context.Proyectos.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Nombre).FirstOrDefault() ?? Path.GetFileNameWithoutExtension(normalizedPath);

            var index = new ProjectInsumoIndexFile
            {
                ProjectPath = normalizedPath,
                ProjectName = projectName,
                IndexedAtUtc = DateTime.UtcNow,
                ProjectLastWriteUtc = File.GetLastWriteTimeUtc(normalizedPath)
            };

            index.Insumos.AddRange(context.Materiales.AsNoTracking().OrderBy(x => x.Clave).Select(x => new ProjectInsumoIndexEntry
            {
                ItemId = x.Id,
                TipoComponente = TipoComponenteMatriz.Material,
                Clave = x.Clave,
                Descripcion = x.Descripcion,
                Unidad = x.Unidad,
                PrecioUnitario = x.PrecioUnitario,
                PrecioMostrado = x.PrecioUnitario.ToString("C2")
            }).ToList());

            index.Insumos.AddRange(context.ManoDeObra.AsNoTracking().OrderBy(x => x.Clave).Select(x => new ProjectInsumoIndexEntry
            {
                ItemId = x.Id,
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                Tag = "Individual",
                Clave = x.Clave,
                Descripcion = x.Descripcion,
                Unidad = x.Unidad,
                PrecioUnitario = x.SalarioReal,
                PrecioMostrado = x.SalarioReal.ToString("C2")
            }).ToList());

            index.Insumos.AddRange(context.Matrices.AsNoTracking().Where(m => m.Tipo == TipoMatriz.Cuadrilla).OrderBy(x => x.Clave).Select(x => new ProjectInsumoIndexEntry
            {
                ItemId = x.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                Tag = "Cuadrilla",
                Clave = x.Clave,
                Descripcion = "👷 " + x.Descripcion,
                Unidad = x.Unidad,
                PrecioUnitario = x.CostoDirecto,
                PrecioMostrado = x.CostoDirecto.ToString("C2")
            }).ToList());

            index.Insumos.AddRange(context.Maquinaria.AsNoTracking().OrderBy(x => x.Clave).Select(x => new ProjectInsumoIndexEntry
            {
                ItemId = x.Id,
                TipoComponente = TipoComponenteMatriz.Maquinaria,
                Clave = x.Clave,
                Descripcion = x.Descripcion,
                Unidad = "hora",
                PrecioUnitario = x.CostoHorario,
                PrecioMostrado = x.CostoHorario.ToString("C2")
            }).ToList());

            index.Insumos.AddRange(context.Matrices.AsNoTracking().Where(m => m.Tipo == TipoMatriz.Basico).OrderBy(x => x.Clave).Select(x => new ProjectInsumoIndexEntry
            {
                ItemId = x.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                Clave = x.Clave,
                Descripcion = x.Descripcion,
                Unidad = x.Unidad,
                PrecioUnitario = x.CostoDirecto,
                PrecioMostrado = x.CostoDirecto.ToString("C4")
            }).ToList());

            index.Insumos.AddRange(context.Herramientas.AsNoTracking().OrderBy(x => x.Clave).Select(x => new ProjectInsumoIndexEntry
            {
                ItemId = x.Id,
                TipoComponente = TipoComponenteMatriz.Herramienta,
                Clave = x.Clave,
                Descripcion = x.Descripcion,
                Unidad = x.Unidad,
                PrecioUnitario = x.PrecioUnitario,
                PrecioMostrado = x.EsPorcentajeMO ? $"{x.PrecioUnitario:N2}%" : x.PrecioUnitario.ToString("C2")
            }).ToList());

            SaveProjectInsumoIndex(index);
            _projectIndexService.RegisterProjectOpened(normalizedPath, projectName);
            return index;
        }

        private void SaveProjectInsumoIndex(ProjectInsumoIndexFile index)
        {
            _workspaceService.EnsureWorkspaceExists();
            Directory.CreateDirectory(InsumoIndexFolder);
            var path = GetProjectInsumoIndexFilePath(index.ProjectPath);
            var json = JsonSerializer.Serialize(index, JsonOptions);
            File.WriteAllText(path, json);
        }

        private string GetProjectInsumoIndexFilePath(string projectPath)
        {
            _workspaceService.EnsureWorkspaceExists();
            Directory.CreateDirectory(InsumoIndexFolder);
            var normalizedPath = NormalizePath(projectPath);
            var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(normalizedPath));
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedPath))).Substring(0, 10);
            return Path.Combine(InsumoIndexFolder, $"{safeName}_{hash}.json");
        }

        private static InsumoSearchResultDto MapCurrentResult(SelectableInsumoDto item, string projectPath, string projectName, DateTime? fechaRef, string query, IReadOnlyDictionary<string, InsumoUsageEntry> usageLookup)
        {
            var frecuencia = ResolveUsageCount(usageLookup, projectPath, item.TipoComponente, item.Id, item.Tag);
            return new InsumoSearchResultDto
            {
                RutaProyecto = projectPath,
                NombreProyecto = projectName,
                ElementoId = item.Id,
                TipoComponente = item.TipoComponente,
                Tag = item.Tag,
                Clave = item.Clave,
                Descripcion = item.Descripcion,
                Unidad = item.Unidad,
                PrecioUnitario = item.PrecioUnitario,
                PrecioMostrado = item.PrecioMostrado,
                FechaReferencia = fechaRef,
                EsActual = true,
                EsFavorito = false,
                EsReciente = true,
                FrecuenciaUso = frecuencia,
                Score = CalculateScore(item.Clave, item.Descripcion, query, true, false, true, frecuencia)
            };
        }

        private static InsumoSearchResultDto MapExternalResult(ProjectInsumoIndexEntry entry, ProjectInsumoIndexFile index, bool isFavorite, bool isRecent, string query, IReadOnlyDictionary<string, InsumoUsageEntry> usageLookup)
        {
            var frecuencia = ResolveUsageCount(usageLookup, index.ProjectPath, entry.TipoComponente, entry.ItemId, entry.Tag);
            return new InsumoSearchResultDto
            {
                RutaProyecto = index.ProjectPath,
                NombreProyecto = index.ProjectName,
                ElementoId = entry.ItemId,
                TipoComponente = entry.TipoComponente,
                Tag = entry.Tag,
                Clave = entry.Clave,
                Descripcion = entry.Descripcion,
                Unidad = entry.Unidad,
                PrecioUnitario = entry.PrecioUnitario,
                PrecioMostrado = entry.PrecioMostrado,
                FechaReferencia = index.ProjectLastWriteUtc == default ? null : index.ProjectLastWriteUtc.ToLocalTime(),
                EsActual = false,
                EsFavorito = isFavorite,
                EsReciente = isRecent,
                FrecuenciaUso = frecuencia,
                Score = CalculateScore(entry.Clave, entry.Descripcion, query, false, isFavorite, isRecent, frecuencia)
            };
        }

        private static int ResolveUsageCount(IReadOnlyDictionary<string, InsumoUsageEntry> usageLookup, string projectPath, TipoComponenteMatriz tipoComponente, int itemId, string? tag)
        {
            if (usageLookup == null || usageLookup.Count == 0 || string.IsNullOrWhiteSpace(projectPath) || itemId <= 0)
                return 0;

            return usageLookup.TryGetValue(ProjectUsageService.BuildInsumoKey(projectPath, tipoComponente, itemId, tag), out var entry)
                ? entry.UsageCount
                : 0;
        }

        private static decimal CalculateScore(string? clave, string? descripcion, string query, bool isCurrent, bool isFavorite, bool isRecent, int frecuenciaUso)
        {
            var claveNorm = NormalizeQuery(clave);
            var descNorm = NormalizeQuery(descripcion);
            var score = 0m;
            if (string.IsNullOrWhiteSpace(query)) return score;

            if (!string.IsNullOrWhiteSpace(claveNorm))
            {
                if (claveNorm == query) score += 140m;
                else if (claveNorm.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 110m;
                else if (claveNorm.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 70m;
            }
            if (!string.IsNullOrWhiteSpace(descNorm))
            {
                if (descNorm == query) score += 150m;
                else if (descNorm.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 120m;
                else if (descNorm.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 95m;
            }
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var term in terms)
            {
                if (!string.IsNullOrWhiteSpace(claveNorm) && claveNorm.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 8m;
                if (!string.IsNullOrWhiteSpace(descNorm) && descNorm.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 14m;
            }
            if (isCurrent) score += 35m;
            if (isFavorite) score += 18m;
            if (isRecent) score += 9m;
            if (frecuenciaUso > 0) score += Math.Min(frecuenciaUso, 20) * 6m;
            return score;
        }

        private static DateTime? GetProjectReferenceDate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            return File.GetLastWriteTime(path);
        }

        private static string NormalizeQuery(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static string NormalizePath(string path) => Path.GetFullPath(path.Trim());

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", value.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
