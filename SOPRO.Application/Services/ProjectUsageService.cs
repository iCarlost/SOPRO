using System.Text.Json;
using SOPRO.Application.Models.Selector;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public sealed class ProjectUsageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ProjectWorkspaceService _workspaceService;
        private string MatrixUsageFilePath => Path.Combine(_workspaceService.LocalDataFolder, "matrix-usage.json");
        private string InsumoUsageFilePath => Path.Combine(_workspaceService.LocalDataFolder, "insumo-usage.json");

        public ProjectUsageService(ProjectWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        }

        public void RegisterMatrixSelection(string projectPath, int matrixId)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || matrixId <= 0)
                return;

            var normalizedPath = NormalizePath(projectPath);
            var entries = LoadMatrixEntries();
            var existing = entries.FirstOrDefault(x =>
                string.Equals(x.ProjectPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
                && x.MatrixId == matrixId);

            if (existing == null)
            {
                existing = new MatrixUsageEntry
                {
                    ProjectPath = normalizedPath,
                    MatrixId = matrixId,
                    UsageCount = 1,
                    LastUsedUtc = DateTime.UtcNow
                };
                entries.Add(existing);
            }
            else
            {
                existing.UsageCount++;
                existing.LastUsedUtc = DateTime.UtcNow;
            }

            SaveMatrixEntries(entries);
        }

        public IReadOnlyDictionary<string, MatrixUsageEntry> GetUsageLookup()
        {
            return LoadMatrixEntries()
                .Where(x => !string.IsNullOrWhiteSpace(x.ProjectPath) && x.MatrixId > 0)
                .GroupBy(x => BuildKey(x.ProjectPath, x.MatrixId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.LastUsedUtc).First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public int GetUsageCount(string projectPath, int matrixId)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || matrixId <= 0)
                return 0;

            var lookup = GetUsageLookup();
            return lookup.TryGetValue(BuildKey(projectPath, matrixId), out var entry)
                ? entry.UsageCount
                : 0;
        }

        public void RegisterInsumoSelection(string projectPath, TipoComponenteMatriz tipoComponente, int itemId, string? tag)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || itemId <= 0)
                return;

            var normalizedPath = NormalizePath(projectPath);
            var entries = LoadInsumoEntries();
            var existing = entries.FirstOrDefault(x =>
                string.Equals(x.ProjectPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
                && x.ItemId == itemId
                && x.TipoComponente == tipoComponente
                && string.Equals(x.Tag ?? string.Empty, tag ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new InsumoUsageEntry
                {
                    ProjectPath = normalizedPath,
                    ItemId = itemId,
                    TipoComponente = tipoComponente,
                    Tag = tag,
                    UsageCount = 1,
                    LastUsedUtc = DateTime.UtcNow
                };
                entries.Add(existing);
            }
            else
            {
                existing.UsageCount++;
                existing.LastUsedUtc = DateTime.UtcNow;
            }

            SaveInsumoEntries(entries);
        }

        public IReadOnlyDictionary<string, InsumoUsageEntry> GetInsumoUsageLookup()
        {
            return LoadInsumoEntries()
                .Where(x => !string.IsNullOrWhiteSpace(x.ProjectPath) && x.ItemId > 0)
                .GroupBy(x => BuildInsumoKey(x.ProjectPath, x.TipoComponente, x.ItemId, x.Tag), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.LastUsedUtc).First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public int GetInsumoUsageCount(string projectPath, TipoComponenteMatriz tipoComponente, int itemId, string? tag)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || itemId <= 0)
                return 0;

            var lookup = GetInsumoUsageLookup();
            return lookup.TryGetValue(BuildInsumoKey(projectPath, tipoComponente, itemId, tag), out var entry)
                ? entry.UsageCount
                : 0;
        }

        public static string BuildKey(string projectPath, int matrixId)
        {
            return $"{NormalizePath(projectPath)}|{matrixId}";
        }

        public static string BuildInsumoKey(string projectPath, TipoComponenteMatriz tipoComponente, int itemId, string? tag)
        {
            return $"{NormalizePath(projectPath)}|{tipoComponente}|{itemId}|{(tag ?? string.Empty).Trim().ToUpperInvariant()}";
        }

        private List<MatrixUsageEntry> LoadMatrixEntries()
        {
            try
            {
                _workspaceService.EnsureWorkspaceExists();
                if (!File.Exists(MatrixUsageFilePath))
                    return new List<MatrixUsageEntry>();

                var json = File.ReadAllText(MatrixUsageFilePath);
                return JsonSerializer.Deserialize<List<MatrixUsageEntry>>(json, JsonOptions) ?? new List<MatrixUsageEntry>();
            }
            catch
            {
                return new List<MatrixUsageEntry>();
            }
        }

        private void SaveMatrixEntries(List<MatrixUsageEntry> entries)
        {
            _workspaceService.EnsureWorkspaceExists();
            var normalized = entries
                .Where(x => !string.IsNullOrWhiteSpace(x.ProjectPath) && x.MatrixId > 0)
                .Select(x =>
                {
                    x.ProjectPath = NormalizePath(x.ProjectPath);
                    return x;
                })
                .GroupBy(x => BuildKey(x.ProjectPath, x.MatrixId), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.LastUsedUtc).First())
                .OrderByDescending(x => x.LastUsedUtc)
                .ToList();

            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(MatrixUsageFilePath, json);
        }

        private List<InsumoUsageEntry> LoadInsumoEntries()
        {
            try
            {
                _workspaceService.EnsureWorkspaceExists();
                if (!File.Exists(InsumoUsageFilePath))
                    return new List<InsumoUsageEntry>();

                var json = File.ReadAllText(InsumoUsageFilePath);
                return JsonSerializer.Deserialize<List<InsumoUsageEntry>>(json, JsonOptions) ?? new List<InsumoUsageEntry>();
            }
            catch
            {
                return new List<InsumoUsageEntry>();
            }
        }

        private void SaveInsumoEntries(List<InsumoUsageEntry> entries)
        {
            _workspaceService.EnsureWorkspaceExists();
            var normalized = entries
                .Where(x => !string.IsNullOrWhiteSpace(x.ProjectPath) && x.ItemId > 0)
                .Select(x =>
                {
                    x.ProjectPath = NormalizePath(x.ProjectPath);
                    return x;
                })
                .GroupBy(x => BuildInsumoKey(x.ProjectPath, x.TipoComponente, x.ItemId, x.Tag), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.LastUsedUtc).First())
                .OrderByDescending(x => x.LastUsedUtc)
                .ToList();

            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(InsumoUsageFilePath, json);
        }

        private static string NormalizePath(string path) => Path.GetFullPath(path.Trim());
    }
}
