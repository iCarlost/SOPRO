using System.Text.Json;
using SOPRO.Application.Models.Selector;

namespace SOPRO.Application.Services
{
    public sealed class SelectorContextService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ProjectWorkspaceService _workspaceService;
        private string ContextFilePath => Path.Combine(_workspaceService.LocalDataFolder, "selector-context.json");

        public SelectorContextService(ProjectWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        }

        public SelectorContextState Get(string selectorKey)
        {
            if (string.IsNullOrWhiteSpace(selectorKey))
                throw new ArgumentException("La clave del selector es requerida.", nameof(selectorKey));

            var normalizedKey = selectorKey.Trim();
            var states = LoadStates();
            return states.FirstOrDefault(x => string.Equals(x.SelectorKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
                ?? new SelectorContextState { SelectorKey = normalizedKey };
        }

        public void Save(string selectorKey, string? lastExternalProjectPath, string? lastFilterText, string? lastSearchMode)
        {
            if (string.IsNullOrWhiteSpace(selectorKey))
                throw new ArgumentException("La clave del selector es requerida.", nameof(selectorKey));

            var normalizedKey = selectorKey.Trim();
            var states = LoadStates();
            var state = states.FirstOrDefault(x => string.Equals(x.SelectorKey, normalizedKey, StringComparison.OrdinalIgnoreCase));
            if (state == null)
            {
                state = new SelectorContextState { SelectorKey = normalizedKey };
                states.Add(state);
            }

            state.LastExternalProjectPath = string.IsNullOrWhiteSpace(lastExternalProjectPath)
                ? null
                : Path.GetFullPath(lastExternalProjectPath.Trim());
            state.LastFilterText = string.IsNullOrWhiteSpace(lastFilterText) ? null : lastFilterText.Trim();
            state.LastSearchMode = string.IsNullOrWhiteSpace(lastSearchMode) ? null : lastSearchMode.Trim();
            state.LastUsedUtc = DateTime.UtcNow;

            SaveStates(states);
        }

        public void Clear(string selectorKey)
        {
            if (string.IsNullOrWhiteSpace(selectorKey))
                return;

            var states = LoadStates();
            states.RemoveAll(x => string.Equals(x.SelectorKey, selectorKey.Trim(), StringComparison.OrdinalIgnoreCase));
            SaveStates(states);
        }

        private List<SelectorContextState> LoadStates()
        {
            try
            {
                _workspaceService.EnsureWorkspaceExists();
                if (!File.Exists(ContextFilePath))
                    return new List<SelectorContextState>();

                var json = File.ReadAllText(ContextFilePath);
                return JsonSerializer.Deserialize<List<SelectorContextState>>(json, JsonOptions) ?? new List<SelectorContextState>();
            }
            catch
            {
                return new List<SelectorContextState>();
            }
        }

        private void SaveStates(List<SelectorContextState> states)
        {
            _workspaceService.EnsureWorkspaceExists();
            var json = JsonSerializer.Serialize(states, JsonOptions);
            File.WriteAllText(ContextFilePath, json);
        }
    }
}
