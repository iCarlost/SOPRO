using System.Text.Json;

namespace SOPRO.WinForms.Helpers
{
    internal sealed class FormProgramaObraLayoutState
    {
        public int GanttPanelWidth { get; set; }
        public int BottomPanelHeight { get; set; }
        public int BottomPanelExpandedHeight { get; set; }
        public bool BottomPanelCollapsed { get; set; }
        public int? VistaCurvaMode { get; set; }
        public int? PieGanttMode { get; set; }
        public int? SegmentLabelPosition { get; set; }
        public int? GanttCellWidth { get; set; }
        public string? GanttFontFamily { get; set; }
        public float? GanttFontSize { get; set; }
        public int? GanttFontStyle { get; set; }
        public int? GanttTextColorArgb { get; set; }
        public int? GanttOutlineColorArgb { get; set; }
        public int? GanttNormalBarColorArgb { get; set; }
        public int? GanttCriticalBarColorArgb { get; set; }
        public int? GanttSummaryBarColorArgb { get; set; }
    }

    internal static class FormProgramaObraLayoutStateStore
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        private static string GetFilePath()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOPRO");
            Directory.CreateDirectory(root);
            return Path.Combine(root, "form-programa-obra-layout.json");
        }

        public static FormProgramaObraLayoutState? Load(int proyectoId)
        {
            try
            {
                var path = GetFilePath();
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                var all = JsonSerializer.Deserialize<Dictionary<int, FormProgramaObraLayoutState>>(json, _jsonOptions);
                if (all == null)
                    return null;

                return all.TryGetValue(proyectoId, out var state) ? state : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(int proyectoId, FormProgramaObraLayoutState state)
        {
            try
            {
                var path = GetFilePath();
                Dictionary<int, FormProgramaObraLayoutState> all;

                if (File.Exists(path))
                {
                    var existingJson = File.ReadAllText(path);
                    all = JsonSerializer.Deserialize<Dictionary<int, FormProgramaObraLayoutState>>(existingJson, _jsonOptions)
                        ?? new Dictionary<int, FormProgramaObraLayoutState>();
                }
                else
                {
                    all = new Dictionary<int, FormProgramaObraLayoutState>();
                }

                all[proyectoId] = state;
                File.WriteAllText(path, JsonSerializer.Serialize(all, _jsonOptions));
            }
            catch
            {
            }
        }
    }
}
