using System;
using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal static class SoproRibbonButtonStyler
    {
        public static void Apply(Button button, SoproIconButtonOptions options, Color iconColor)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (options == null) throw new ArgumentNullException(nameof(options));

            button.AutoSize = false;
            button.AutoEllipsis = false;
            button.TextImageRelation = options.TextRelation;
            button.ImageAlign = options.ImageAlign;
            button.TextAlign = options.TextAlign;
            button.Padding = options.ContentPadding;
            button.Text = options.ShowText ? button.Text : string.Empty;

            button.Image?.Dispose();
            button.Image = SoproIconProvider.GetIcon(options.IconType, iconColor, options.IconSize);

            ApplySize(button, options);
        }

        public static void ApplySize(Button button, SoproIconButtonOptions options)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var width = options.AutoSizeWidthToContent
                ? MeasurePreferredWidth(button, options)
                : Math.Max(button.Width, options.MinButtonWidth);

            button.Size = new Size(width, options.FixedHeight);
        }

        public static int MeasurePreferredWidth(Button button, SoproIconButtonOptions options)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (!options.ShowText)
                return Math.Max(options.MinButtonWidth, button.Width);

            var currentAutoSize = button.AutoSize;
            var preferred = button.GetPreferredSize(Size.Empty);
            button.AutoSize = currentAutoSize;

            var textWidth = 0;
            if (!string.IsNullOrWhiteSpace(button.Text))
            {
                textWidth = TextRenderer.MeasureText(
                    button.Text,
                    button.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            }

            var iconWidth = options.IconSize > 0 ? options.IconSize : 0;
            var gap = iconWidth > 0 && textWidth > 0 ? options.IconTextSpacing : 0;
            var fallbackWidth = options.ContentPadding.Left
                              + iconWidth
                              + gap
                              + textWidth
                              + options.ContentPadding.Right
                              + options.ExtraWidthBuffer;

            var width = Math.Max(preferred.Width + options.ExtraWidthBuffer, fallbackWidth);
            return Math.Max(width, options.MinButtonWidth);
        }
    }
}
