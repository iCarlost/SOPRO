using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal sealed class SoproIconButtonOptions
    {
        public SoproIconType IconType { get; init; }
        public int IconSize { get; init; } = SoproUiMetrics.RibbonTextButtonIconSize;
        public bool ShowText { get; init; } = true;
        public bool AutoSizeWidthToContent { get; init; } = true;
        public int MinButtonWidth { get; init; } = SoproUiMetrics.RibbonTextButtonMinWidth;
        public int FixedHeight { get; init; } = SoproUiMetrics.RibbonTextButtonHeight;
        public int IconTextSpacing { get; init; } = SoproUiMetrics.RibbonIconTextSpacing;
        public Padding ContentPadding { get; init; } = default;
        public TextImageRelation TextRelation { get; init; } = TextImageRelation.ImageBeforeText;
        public ContentAlignment ImageAlign { get; init; } = ContentAlignment.MiddleLeft;
        public ContentAlignment TextAlign { get; init; } = ContentAlignment.MiddleCenter;
        public int ExtraWidthBuffer { get; init; } = SoproUiMetrics.RibbonTextButtonExtraWidthBuffer;

        public static SoproIconButtonOptions ForRibbonText(SoproIconType iconType, int? minWidth = null, int? iconSize = null)
            => new SoproIconButtonOptions
            {
                IconType = iconType,
                IconSize = iconSize ?? SoproUiMetrics.RibbonTextButtonIconSize,
                ShowText = true,
                AutoSizeWidthToContent = true,
                MinButtonWidth = minWidth ?? SoproUiMetrics.RibbonTextButtonMinWidth,
                FixedHeight = SoproUiMetrics.RibbonTextButtonHeight,
                IconTextSpacing = SoproUiMetrics.RibbonIconTextSpacing,
                ContentPadding = SoproUiMetrics.RibbonTextButtonPadding,
                TextRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                ExtraWidthBuffer = SoproUiMetrics.RibbonTextButtonExtraWidthBuffer,
            };

        public static SoproIconButtonOptions ForRibbonGlyph(SoproIconType iconType, int? iconSize = null)
            => new SoproIconButtonOptions
            {
                IconType = iconType,
                IconSize = iconSize ?? SoproUiMetrics.RibbonGlyphButtonIconSize,
                ShowText = false,
                AutoSizeWidthToContent = false,
                MinButtonWidth = 28,
                FixedHeight = 28,
                IconTextSpacing = 0,
                ContentPadding = SoproUiMetrics.RibbonGlyphButtonPadding,
                TextRelation = TextImageRelation.Overlay,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.MiddleCenter,
            };
    }
}
