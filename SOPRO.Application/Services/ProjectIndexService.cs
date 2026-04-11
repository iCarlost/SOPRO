using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SOPRO.Application.Models;
using SOPRO.Application.Models.Selector;

namespace SOPRO.Application.Services
{
    public sealed class ProjectIndexService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ProjectWorkspaceService _workspaceService;
        private DateTime _lastDiscoveryUtc = DateTime.MinValue;
        private string IndexFilePath => Path.Combine(_workspaceService.LocalDataFolder, "selector-project-index.json");

        private string CatalogIndexFolder => Path.Combine(_workspaceService.LocalDataFolder, "selector-catalog-index");

        public ProjectIndexService(ProjectWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        }


        public void RefreshKnownProjects(string? currentProjectPath = null, bool force = false)
        {
            if (!force && DateTime.UtcNow - _lastDiscoveryUtc < TimeSpan.FromSeconds(15))
                return;

            var entries = LoadEntries();
            bool changed = false;

            void UpsertDiscoveredProject(string? projectPath, string? projectName = null)
            {
                if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                    return;

                string normalizedPath;
                try
                {
                    normalizedPath = NormalizePath(projectPath);
                }
                catch
                {
                    return;
                }

                var existing = entries.FirstOrDefault(x => string.Equals(x.ProjectPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    entries.Add(new ProjectIndexEntry
                    {
                        ProjectPath = normalizedPath,
                        ProjectName = ResolveProjectName(normalizedPath, projectName),
                        LastOpenedUtc = SafeGetLastWriteUtc(normalizedPath)
                    });
                    changed = true;
                    return;
                }

                var resolvedName = ResolveProjectName(normalizedPath, projectName);
                if (!string.Equals(existing.ProjectName, resolvedName, StringComparison.Ordinal))
                {
                    existing.ProjectName = resolvedName;
                    changed = true;
                }

                if (existing.LastOpenedUtc == default)
                {
                    existing.LastOpenedUtc = SafeGetLastWriteUtc(normalizedPath);
                    changed = true;
                }
            }

            foreach (var recent in _workspaceService.GetRecentProjects(200))
                UpsertDiscoveredProject(recent.FilePath, recent.Name);

            if (!string.IsNullOrWhiteSpace(currentProjectPath))
            {
                UpsertDiscoveredProject(currentProjectPath, Path.GetFileNameWithoutExtension(currentProjectPath));

                try
                {
                    var currentDirectory = Path.GetDirectoryName(NormalizePath(currentProjectPath));
                    if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
                    {
                        foreach (var siblingProject in Directory.GetFiles(currentDirectory, "*.db", SearchOption.TopDirectoryOnly))
                            UpsertDiscoveredProject(siblingProject, Path.GetFileNameWithoutExtension(siblingProject));
                    }
                }
                catch
                {
                    // Ignorar directorios inaccesibles o rutas inválidas.
                }
            }

            foreach (var indexed in EnumerateCatalogIndexedProjects())
                UpsertDiscoveredProject(indexed.ProjectPath, indexed.ProjectName);

            if (changed)
                SaveEntries(entries);

            _lastDiscoveryUtc = DateTime.UtcNow;
        }

        public void RegisterProjectOpened(string projectPath, string? projectName = null)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            var normalizedPath = NormalizePath(projectPath);
            var entries = LoadEntries();
            var existing = entries.FirstOrDefault(x => string.Equals(x.ProjectPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new ProjectIndexEntry
                {
                    ProjectPath = normalizedPath,
                    ProjectName = ResolveProjectName(normalizedPath, projectName),
                    LastOpenedUtc = DateTime.UtcNow
                };
                entries.Add(existing);
            }
            else
            {
                existing.ProjectName = ResolveProjectName(normalizedPath, projectName);
                existing.LastOpenedUtc = DateTime.UtcNow;
            }

            SaveEntries(entries);
        }

        public IReadOnlyList<RecentProjectInfo> GetRecentProjects(int take = 10)
        {
            return LoadEntries()
                .OrderByDescending(x => x.LastOpenedUtc)
                .Take(take)
                .Select(ToRecentProjectInfo)
                .ToList();
        }

        public IReadOnlyList<RecentProjectInfo> GetFavoriteProjects()
        {
            return LoadEntries()
                .Where(x => x.IsFavorite)
                .OrderByDescending(x => x.LastOpenedUtc)
                .Select(ToRecentProjectInfo)
                .ToList();
        }

        public bool IsFavorite(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return false;

            var normalizedPath = NormalizePath(projectPath);
            return LoadEntries().Any(x => string.Equals(x.ProjectPath, normalizedPath, StringComparison.OrdinalIgnoreCase) && x.IsFavorite);
        }

        public void SetFavorite(string projectPath, bool isFavorite, string? projectName = null)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            var normalizedPath = NormalizePath(projectPath);
            var entries = LoadEntries();
            var existing = entries.FirstOrDefault(x => string.Equals(x.ProjectPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new ProjectIndexEntry
                {
                    ProjectPath = normalizedPath,
                    ProjectName = ResolveProjectName(normalizedPath, projectName),
                    LastOpenedUtc = DateTime.UtcNow,
                    IsFavorite = isFavorite
                };
                entries.Add(existing);
            }
            else
            {
                existing.ProjectName = ResolveProjectName(normalizedPath, projectName);
                existing.IsFavorite = isFavorite;
                if (existing.LastOpenedUtc == default)
                    existing.LastOpenedUtc = DateTime.UtcNow;
            }

            SaveEntries(entries);
        }

        public RecentProjectInfo? GetLastOpenedProject(string? excludeProjectPath = null)
        {
            string? normalizedExclude = string.IsNullOrWhiteSpace(excludeProjectPath)
                ? null
                : NormalizePath(excludeProjectPath);

            var entry = LoadEntries()
                .Where(x => normalizedExclude == null || !string.Equals(x.ProjectPath, normalizedExclude, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.LastOpenedUtc)
                .FirstOrDefault();

            return entry == null ? null : ToRecentProjectInfo(entry);
        }

        public IReadOnlyList<ProjectCandidateInfo> GetCandidateProjects(string? currentProjectPath, bool includeRecentProjects, bool includeFavoriteProjects, int takeRecent = 12)
        {
            var normalizedCurrent = string.IsNullOrWhiteSpace(currentProjectPath) ? null : NormalizePath(currentProjectPath);
            var entries = LoadEntries()
                .Where(x => string.IsNullOrWhiteSpace(normalizedCurrent) || !string.Equals(x.ProjectPath, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                .Where(x => File.Exists(x.ProjectPath))
                .ToList();

            var results = new List<ProjectCandidateInfo>();
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (includeFavoriteProjects)
            {
                foreach (var entry in entries.Where(x => x.IsFavorite).OrderByDescending(x => x.LastOpenedUtc))
                {
                    if (added.Add(entry.ProjectPath))
                        results.Add(new ProjectCandidateInfo(entry.ProjectPath, entry.ProjectName, true, true));
                }
            }

            if (includeRecentProjects)
            {
                foreach (var entry in entries.OrderByDescending(x => x.LastOpenedUtc).Take(Math.Max(1, takeRecent)))
                {
                    if (added.Add(entry.ProjectPath))
                        results.Add(new ProjectCandidateInfo(entry.ProjectPath, entry.ProjectName, entry.IsFavorite, true));
                }
            }

            return results;
        }

        public string GetCatalogIndexFilePath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("La ruta del proyecto es requerida.", nameof(projectPath));

            _workspaceService.EnsureWorkspaceExists();
            Directory.CreateDirectory(CatalogIndexFolder);

            var normalizedPath = NormalizePath(projectPath);
            var baseName = Path.GetFileNameWithoutExtension(normalizedPath);
            var safeName = SanitizeFileName(baseName);
            var hash = ComputeShortHash(normalizedPath);
            return Path.Combine(CatalogIndexFolder, $"{safeName}_{hash}.json");
        }

        public ProjectCatalogIndexFile? LoadCatalogIndex(string projectPath)
        {
            try
            {
                var filePath = GetCatalogIndexFilePath(projectPath);
                if (!File.Exists(filePath))
                    return null;

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<ProjectCatalogIndexFile>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public void SaveCatalogIndex(ProjectCatalogIndexFile indexFile)
        {
            if (indexFile == null)
                throw new ArgumentNullException(nameof(indexFile));
            if (string.IsNullOrWhiteSpace(indexFile.ProjectPath))
                throw new ArgumentException("La ruta del proyecto indexado es requerida.", nameof(indexFile));

            var filePath = GetCatalogIndexFilePath(indexFile.ProjectPath);
            var json = JsonSerializer.Serialize(indexFile, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public bool IsCatalogIndexFresh(string projectPath, ProjectCatalogIndexFile? indexFile = null)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return false;

            indexFile ??= LoadCatalogIndex(projectPath);
            if (indexFile == null)
                return false;

            var lastWrite = File.GetLastWriteTimeUtc(NormalizePath(projectPath));
            return indexFile.ProjectLastWriteUtc >= lastWrite;
        }

        private List<ProjectIndexEntry> LoadEntries()
        {
            try
            {
                _workspaceService.EnsureWorkspaceExists();
                if (!File.Exists(IndexFilePath))
                    return new List<ProjectIndexEntry>();

                var json = File.ReadAllText(IndexFilePath);
                var entries = JsonSerializer.Deserialize<List<ProjectIndexEntry>>(json, JsonOptions) ?? new List<ProjectIndexEntry>();
                return entries
                    .Where(x => !string.IsNullOrWhiteSpace(x.ProjectPath))
                    .GroupBy(x => NormalizePath(x.ProjectPath), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.LastOpenedUtc).First())
                    .ToList();
            }
            catch
            {
                return new List<ProjectIndexEntry>();
            }
        }

        private void SaveEntries(List<ProjectIndexEntry> entries)
        {
            _workspaceService.EnsureWorkspaceExists();
            var normalized = entries
                .Where(x => !string.IsNullOrWhiteSpace(x.ProjectPath))
                .Select(x =>
                {
                    x.ProjectPath = NormalizePath(x.ProjectPath);
                    x.ProjectName = ResolveProjectName(x.ProjectPath, x.ProjectName);
                    return x;
                })
                .GroupBy(x => x.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.LastOpenedUtc).First())
                .OrderByDescending(x => x.IsFavorite)
                .ThenByDescending(x => x.LastOpenedUtc)
                .ToList();

            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(IndexFilePath, json);
        }

        private IEnumerable<(string ProjectPath, string? ProjectName)> EnumerateCatalogIndexedProjects()
        {
            var folder = Path.Combine(_workspaceService.LocalDataFolder, "selector-catalog-index");
            if (!Directory.Exists(folder))
                yield break;

            foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                ProjectCatalogIndexFile? index = null;
                try
                {
                    var json = File.ReadAllText(file);
                    index = JsonSerializer.Deserialize<ProjectCatalogIndexFile>(json, JsonOptions);
                }
                catch
                {
                    index = null;
                }

                if (index == null || string.IsNullOrWhiteSpace(index.ProjectPath))
                    continue;

                yield return (index.ProjectPath, index.ProjectName);
            }
        }

        private static DateTime SafeGetLastWriteUtc(string projectPath)
        {
            try
            {
                return File.Exists(projectPath) ? File.GetLastWriteTimeUtc(projectPath) : DateTime.UtcNow;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static RecentProjectInfo ToRecentProjectInfo(ProjectIndexEntry entry)
        {
            return new RecentProjectInfo
            {
                Name = entry.ProjectName,
                FilePath = entry.ProjectPath,
                LastModified = entry.LastOpenedUtc == default ? DateTime.MinValue : entry.LastOpenedUtc.ToLocalTime()
            };
        }

        private static string NormalizePath(string projectPath)
        {
            return Path.GetFullPath(projectPath.Trim());
        }

        private static string ResolveProjectName(string projectPath, string? projectName)
        {
            if (!string.IsNullOrWhiteSpace(projectName))
                return projectName.Trim();

            return Path.GetFileNameWithoutExtension(projectPath);
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "project" : sanitized;
        }

        private static string ComputeShortHash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes, 0, 6);
        }
    }

    public sealed record ProjectCandidateInfo(string FilePath, string ProjectName, bool IsFavorite, bool IsRecent);
}
