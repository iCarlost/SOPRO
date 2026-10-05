using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal static class SoproUiMetrics
    {
        public const int RibbonTextButtonIconSize = 16;
        public const int RibbonTextButtonExtraWidthBuffer = 14;
        public const int RibbonGlyphButtonIconSize = 16;
        public const int RibbonTextButtonHeight = 28;
        public const int RibbonTextButtonMinWidth = 82;
        public const int RibbonButtonHorizontalGap = 6;
        public const int RibbonSeparatorGap = 8;
        public const int RibbonIconTextSpacing = 6;
        public static readonly Padding RibbonTextButtonPadding = new Padding(10, 0, 12, 0);
        public static readonly Padding RibbonGlyphButtonPadding = new Padding(0);

        // --- Métricas generales de contenido y paneles (reutilizables fuera del ribbon) ---

        // Alturas lógicas de controles de contenido.
        public const int ContentButtonHeight = 30;
        public const int CompactButtonHeight = 28;
        public const int GridHeaderHeight = 34;

        // Ancho mínimo lógico de un botón de contenido.
        public const int ContentButtonMinWidth = 90;

        // Separaciones generales (fluida entre controles contiguos).
        public const int HorizontalGap = 6;
        public const int VerticalGap = 4;

        // Margen interno estándar de contenedores/secciones.
        public const int StandardMargin = 12;

        // Relleno interno estándar de paneles de contenido.
        public static readonly Padding PanelPadding = new Padding(10);

        // Relleno interno de botones de contenido (normal y compacto).
        public static readonly Padding ContentButtonPadding = new Padding(14, 4, 14, 4);
        public static readonly Padding CompactButtonPadding = new Padding(8, 3, 8, 3);
    }
}
