using System.Drawing;
using System.Text.Json;
using SOPRO.WinForms.Models;

namespace SOPRO.WinForms.Helpers
{
    internal sealed class FormProgramaInsumosLayoutState
    {
        public int GanttPanelWidth { get; set; }
        public int? GanttCellWidth { get; set; }
        public int? TipoInsumo { get; set; }
        public string? Vista { get; set; }
        public string? GanttFontFamily { get; set; }
        public int? GanttFontStyle { get; set; }
        public int? GanttTextColorArgb { get; set; }
        public int? GanttOutlineColorArgb { get; set; }
        public int? GanttNormalBarColorArgb { get; set; }
        public int? GanttCriticalBarColorArgb { get; set; }
        public int? GanttSummaryBarColorArgb { get; set; }
    }

    internal static class FormProgramaInsumosLayoutStateStore
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        private static string GetFilePath()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOPRO");
            Directory.CreateDirectory(root);
            return Path.Combine(root, "form-programa-insumos-layout.json");
        }

        public static FormProgramaInsumosLayoutState? Load(int proyectoId)
        {
            try
            {
                var path = GetFilePath();
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                var all = JsonSerializer.Deserialize<Dictionary<int, FormProgramaInsumosLayoutState>>(json, _jsonOptions);
                return all != null && all.TryGetValue(proyectoId, out var state) ? state : null;
            }
            catch { return null; }
        }

        public static void Save(int proyectoId, FormProgramaInsumosLayoutState state)
        {
            try
            {
                var path = GetFilePath();
                Dictionary<int, FormProgramaInsumosLayoutState> all;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    all = JsonSerializer.Deserialize<Dictionary<int, FormProgramaInsumosLayoutState>>(json, _jsonOptions) ?? new();
                }
                else all = new();
                all[proyectoId] = state;
                File.WriteAllText(path, JsonSerializer.Serialize(all, _jsonOptions));
            }
            catch { }
        }

        public static GanttVisualSettings ToVisualSettings(this FormProgramaInsumosLayoutState? state)
        {
            var settings = GanttVisualSettings.CreateDefault();
            if (state == null) return settings;
            if (!string.IsNullOrWhiteSpace(state.GanttFontFamily)) settings.FontFamilyName = state.GanttFontFamily;
            if (state.GanttFontStyle.HasValue) settings.FontStyle = (FontStyle)state.GanttFontStyle.Value;
            if (state.GanttTextColorArgb.HasValue) settings.TextColor = Color.FromArgb(state.GanttTextColorArgb.Value);
            if (state.GanttOutlineColorArgb.HasValue) settings.OutlineColor = Color.FromArgb(state.GanttOutlineColorArgb.Value);
            if (state.GanttNormalBarColorArgb.HasValue) settings.NormalBarColor = Color.FromArgb(state.GanttNormalBarColorArgb.Value);
            if (state.GanttCriticalBarColorArgb.HasValue) settings.CriticalBarColor = Color.FromArgb(state.GanttCriticalBarColorArgb.Value);
            if (state.GanttSummaryBarColorArgb.HasValue) settings.SummaryBarColor = Color.FromArgb(state.GanttSummaryBarColorArgb.Value);
            return settings;
        }
    }
}
