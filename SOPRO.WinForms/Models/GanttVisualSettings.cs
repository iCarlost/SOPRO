using System.Drawing;

namespace SOPRO.WinForms.Models
{
    public sealed class GanttVisualSettings
    {
        public string FontFamilyName { get; set; } = "Segoe UI";
        public FontStyle FontStyle { get; set; } = FontStyle.Regular;
        public Color TextColor { get; set; } = Color.FromArgb(64, 64, 64);
        public Color OutlineColor { get; set; } = Color.FromArgb(240, 255, 255, 255);
        public Color NormalBarColor { get; set; } = Color.FromArgb(52, 152, 219);
        public Color CriticalBarColor { get; set; } = Color.FromArgb(231, 76, 60);
        public Color SummaryBarColor { get; set; } = Color.FromArgb(74, 96, 173);

        public Font CreateFinancialTextFont(float size = 8f)
        {
            return new Font(FontFamilyName, size, FontStyle);
        }

        public GanttVisualSettings Clone()
        {
            return new GanttVisualSettings
            {
                FontFamilyName = FontFamilyName,
                FontStyle = FontStyle,
                TextColor = TextColor,
                OutlineColor = OutlineColor,
                NormalBarColor = NormalBarColor,
                CriticalBarColor = CriticalBarColor,
                SummaryBarColor = SummaryBarColor
            };
        }

        public static GanttVisualSettings CreateDefault()
        {
            return new GanttVisualSettings();
        }
    }
}
