using System.ComponentModel;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.UI.Controls
{
    public class SoproButton : Button
    {
        private SoproIconType? _iconType;
        private int _iconSize = 16;
        private Color _disabledIconColor = Color.FromArgb(120, 120, 120);
        private int _iconTextSpacing = 6;
        private bool _autoSizeToContent;
        private int _minimumAutoWidth = 28;
        private int _fixedButtonHeight = 28;
        private bool _showSoproText = true;
        private Padding _soproContentPadding = new(8, 0, 8, 0);
        private bool _internalSync;

        [Category("SOPRO")]
        [DefaultValue(null)]
        public SoproIconType? SoproIcon
        {
            get => _iconType;
            set
            {
                if (_iconType == value) return;
                _iconType = value;
                UpdateVisuals();
            }
        }

        [Category("SOPRO")]
        [DefaultValue(16)]
        public int SoproIconSize
        {
            get => _iconSize;
            set
            {
                var normalized = Math.Max(12, value);
                if (_iconSize == normalized) return;
                _iconSize = normalized;
                UpdateVisuals();
            }
        }

        [Category("SOPRO")]
        public Color SoproDisabledIconColor
        {
            get => _disabledIconColor;
            set
            {
                if (_disabledIconColor == value) return;
                _disabledIconColor = value;
                UpdateVisuals();
            }
        }

        [Category("SOPRO")]
        [DefaultValue(6)]
        public int SoproIconTextSpacing
        {
            get => _iconTextSpacing;
            set
            {
                var normalized = Math.Max(0, value);
                if (_iconTextSpacing == normalized) return;
                _iconTextSpacing = normalized;
                UpdateLayoutMetrics();
            }
        }

        [Category("SOPRO")]
        [DefaultValue(false)]
        public bool SoproAutoSizeToContent
        {
            get => _autoSizeToContent;
            set
            {
                if (_autoSizeToContent == value) return;
                _autoSizeToContent = value;
                UpdateLayoutMetrics();
            }
        }

        [Category("SOPRO")]
        [DefaultValue(28)]
        public int SoproMinimumAutoWidth
        {
            get => _minimumAutoWidth;
            set
            {
                var normalized = Math.Max(24, value);
                if (_minimumAutoWidth == normalized) return;
                _minimumAutoWidth = normalized;
                UpdateLayoutMetrics();
            }
        }

        [Category("SOPRO")]
        [DefaultValue(28)]
        public int SoproFixedHeight
        {
            get => _fixedButtonHeight;
            set
            {
                var normalized = Math.Max(24, value);
                if (_fixedButtonHeight == normalized) return;
                _fixedButtonHeight = normalized;
                UpdateLayoutMetrics();
            }
        }

        [Category("SOPRO")]
        [DefaultValue(true)]
        public bool SoproShowText
        {
            get => _showSoproText;
            set
            {
                if (_showSoproText == value) return;
                _showSoproText = value;
                UpdateLayoutMetrics();
            }
        }

        [Category("SOPRO")]
        public Padding SoproContentPadding
        {
            get => _soproContentPadding;
            set
            {
                if (_soproContentPadding == value) return;
                _soproContentPadding = value;
                UpdateLayoutMetrics();
            }
        }

        public SoproButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TextImageRelation = TextImageRelation.ImageBeforeText;
            ImageAlign = ContentAlignment.MiddleLeft;
            TextAlign = ContentAlignment.MiddleCenter;
            UseVisualStyleBackColor = false;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            UpdateVisuals();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (!_internalSync)
                UpdateLayoutMetrics();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateLayoutMetrics();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            UpdateIcon();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            UpdateIcon();
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            if (!_internalSync)
                _soproContentPadding = base.Padding;
        }

        private void UpdateVisuals()
        {
            UpdateIcon();
            UpdateLayoutMetrics();
        }

        private void UpdateIcon()
        {
            if (_iconType == null)
            {
                if (Image != null)
                {
                    var old = Image;
                    Image = null;
                    old.Dispose();
                }
                return;
            }

            var color = Enabled ? ForeColor : _disabledIconColor;
            var newImage = SoproIconProvider.GetIcon(_iconType.Value, color, _iconSize);
            var oldImage = Image;
            Image = newImage;
            oldImage?.Dispose();
        }

        private void UpdateLayoutMetrics()
        {
            _internalSync = true;
            try
            {
                base.Padding = _soproContentPadding;

                if (!_showSoproText)
                {
                    TextImageRelation = TextImageRelation.Overlay;
                    ImageAlign = ContentAlignment.MiddleCenter;
                    TextAlign = ContentAlignment.MiddleCenter;
                }
                else
                {
                    TextImageRelation = TextImageRelation.ImageBeforeText;
                    ImageAlign = ContentAlignment.MiddleLeft;
                    TextAlign = ContentAlignment.MiddleCenter;
                }

                if (!_autoSizeToContent)
                {
                    return;
                }

                var text = _showSoproText ? (Text ?? string.Empty) : string.Empty;
                var textWidth = string.IsNullOrWhiteSpace(text)
                    ? 0
                    : TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;

                var iconWidth = _iconType.HasValue ? _iconSize : 0;
                var spacing = iconWidth > 0 && textWidth > 0 ? _iconTextSpacing : 0;
                var computedWidth = _soproContentPadding.Left + iconWidth + spacing + textWidth + _soproContentPadding.Right;

                Width = Math.Max(_minimumAutoWidth, computedWidth);
                Height = _fixedButtonHeight;
            }
            finally
            {
                _internalSync = false;
            }
        }
    }
}
