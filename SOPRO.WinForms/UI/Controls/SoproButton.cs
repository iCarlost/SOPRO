using System.ComponentModel;
using System.Drawing.Drawing2D;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.UI.Controls
{
    /// <summary>
    /// Presentaciones disponibles del boton SOPRO.
    /// </summary>
    public enum SoproButtonLayout
    {
        /// <summary>Icono y texto en linea (presentacion clasica del ribbon).</summary>
        Inline,

        /// <summary>
        /// Icono y rotulo EN LINEA, centrados como un bloque (acciones
        /// compactas). A diferencia de <see cref="Inline"/>, el icono y el
        /// texto no se reparten por separado: se dibujan juntos.
        /// </summary>
        Small,

        /// <summary>
        /// Icono grande sobre el texto (acciones principales del ribbon).
        /// El texto se dibuja SIEMPRE debajo del icono, aunque
        /// <see cref="SoproButton.SoproShowText"/> este desactivado.
        /// </summary>
        Large,

        /// <summary>
        /// Glifo de letra (color de texto): la letra se dibuja legible y una
        /// barra inferior muestra el color de muestra.
        /// </summary>
        SampleLetter,

        /// <summary>Muestra de color (color de fondo de la celda).</summary>
        SampleSwatch
    }

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
        private SoproButtonLayout _layout = SoproButtonLayout.Inline;
        private string _caption = string.Empty;
        private Font? _glyphFont;
        private Font? _captionFont;
        private Color? _sampleGlyphColor;
        private bool _dualFocusCue;
        private bool _updatingRegion;
        // Estado de puntero seguido POR EVENTOS (issue #186): OnPaint NUNCA
        // consulta Cursor.Position ni Control.MouseButtons. Leer el estado
        // global del ratón durante el pintado hace que un repintado PARCIAL
        // (franja expuesta por el sistema) pueda usar un estado distinto al de
        // la cara ya pintada y dejar una línea de 1 px "fantasma" — el
        // artefacto de Recalcular/Ajustar. Con estado seguido, todo repintado
        // (completo o parcial) produce píxeles idénticos para el mismo estado.
        private bool _pointerOver;
        private bool _pressed;

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

        /// <summary>
        /// Tamaño LÓGICO del icono en px (a 96 DPI). El bitmap se rasteriza a
        /// <c>SoproIconSize * (DeviceDpi / 96)</c> para que la proporción
        /// icono/botón se mantenga en 125 %, 150 % o 200 % de escala.
        /// </summary>
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
        [DefaultValue(typeof(Color), "120, 120, 120")]
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
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
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

        /// <summary>
        /// Presentacion del boton. <see cref="SoproButtonLayout.Inline"/> es la
        /// clasica (icono + texto en linea); las demas se pintan a mano.
        /// </summary>
        [Category("SOPRO")]
        [DefaultValue(SoproButtonLayout.Inline)]
        public SoproButtonLayout SoproLayout
        {
            get => _layout;
            set
            {
                if (_layout == value) return;
                _layout = value;
                UpdateVisuals();
            }
        }

        /// <summary>
        /// Rotulo adicional de la presentacion de muestra (color de texto /
        /// color de fondo). Se dibuja a la derecha del glifo y no depende de
        /// <see cref="Button.Text"/>, que el tema reescribe en tiempo de
        /// ejecucion con el glifo de muestra.
        /// </summary>
        [Category("SOPRO")]
        [DefaultValue("")]
        public string SoproCaption
        {
            get => _caption;
            set
            {
                var normalized = value ?? string.Empty;
                if (_caption == normalized) return;
                _caption = normalized;
                Invalidate();
            }
        }

        /// <summary>
        /// Estilo de foco OPT-IN: doble filete anidado tinta/blanco en lugar del
        /// aro simple <see cref="SoproTheme.Colors.BrandLt"/>. Pensado para el
        /// ribbon de FormProyecto, donde el comando activo usa fondo azul de
        /// marca (el aro azul simple cae a ~1.8:1 sobre el propio azul). El
        /// filete de tinta contrasta sobre superficies claras y el blanco sobre
        /// el azul activo, de modo que el foco es visible en ambos cromatismas.
        /// Por defecto (false) el pintado historico no cambia para los
        /// consumidores existentes. Solo aplica con foco de teclado
        /// (<see cref="Control.ShowFocusCues"/>).
        /// </summary>
        [Category("SOPRO")]
        [DefaultValue(false)]
        public bool SoproDualFocusCue
        {
            get => _dualFocusCue;
            set
            {
                if (_dualFocusCue == value) return;
                _dualFocusCue = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Color del glifo y del rotulo de las presentaciones de muestra
        /// (<see cref="SoproButtonLayout.SampleLetter"/> y
        /// <see cref="SoproButtonLayout.SampleSwatch"/>) cuando el boton esta
        /// habilitado. Es un override OPT-IN pensado para superficies claras:
        /// por defecto (null) se conserva el tono claro historico
        /// (<see cref="SoproTheme.Colors.RibbonText"/>) del chrome oscuro, asi
        /// que los consumidores existentes no cambian. El estado deshabilitado
        /// sigue usando <see cref="SoproDisabledIconColor"/>. No afecta a las
        /// presentaciones Inline/Large/Small ni a la barra/cuadro de muestra
        /// (que siempre pinta el color elegido).
        /// </summary>
        [Category("SOPRO")]
        [DefaultValue(null)]
        public Color? SoproSampleGlyphColor
        {
            get => _sampleGlyphColor;
            set
            {
                if (_sampleGlyphColor == value) return;
                _sampleGlyphColor = value;
                Invalidate();
            }
        }

        public SoproButton()
        {
            // Permite BackColor = Transparent (p. ej. fundir el boton con el
            // gradiente del ribbon). No altera el pintado cuando el BackColor es
            // opaco: los usos existentes renderizan exactamente igual.
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TextImageRelation = TextImageRelation.ImageBeforeText;
            ImageAlign = ContentAlignment.MiddleLeft;
            TextAlign = ContentAlignment.MiddleCenter;
            UseVisualStyleBackColor = false;

            // Las presentaciones con pintado propio necesitan redibujarse al
            // cambiar el estado del puntero (el botón nativo ya lo hace por su
            // cuenta). El estado se SIGUE aquí y se sincroniza en eventos
            // discretos; nunca se lee el cursor global durante OnPaint.
            MouseEnter += (_, __) => SetPointerOver(true);
            MouseLeave += (_, __) => SetPointerOver(false);
            // Red de seguridad y semántica de arrastre: con captura (botón
            // presionado fuera del área) el movimiento sigue llegando y el
            // estado "encima" sigue la contención real, como hacía la lectura
            // directa del cursor. Sin lecturas globales: e.Location es cliente.
            MouseMove += (_, e) => SetPointerOver(ClientRectangle.Contains(e.Location));
            MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) SetPressed(true); };
            MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) SetPressed(false); };
            // Un menú/diálogo que roba la captura deja el botón en "presionado"
            // para siempre si no se libera aquí.
            MouseCaptureChanged += (_, __) => SetPressed(false);

            SizeChanged += (_, __) => ApplyRoundedRegion();
            HandleCreated += (_, __) => { ApplyRoundedRegion(); UpdateIcon(); SyncPointerOverFromCursor(); };
            // Al cambiar el DPI hay que re-rasterizar el icono: su tamano logico
            // se mantiene, pero el bitmap debe crecer con la escala.
            DpiChangedAfterParent += (_, __) => { ApplyRoundedRegion(); UpdateIcon(); };
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
            _glyphFont?.Dispose();
            _glyphFont = null;
            _captionFont?.Dispose();
            _captionFont = null;
            UpdateLayoutMetrics();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
                _pressed = false;
            // Deshabilitado el control deja de recibir Leave: el estado seguido
            // puede quedar obsoleto al re-habilitar. Sincronía discreta.
            SyncPointerOverFromCursor();
            UpdateIcon();
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // Al (re)aparecer bajo el cursor el sistema no siempre entrega
            // MouseEnter: se re-sincroniza el estado seguido. Es una lectura
            // del cursor en un evento DISCRETO, nunca durante OnPaint.
            if (Visible)
                SyncPointerOverFromCursor();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            UpdateIcon();
            Invalidate();
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            if (!_internalSync)
                _soproContentPadding = base.Padding;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_layout == SoproButtonLayout.Inline)
            {
                base.OnPaint(e);
                return;
            }

            var graphics = e.Graphics;
            if (graphics == null) return;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // Estado seguido por eventos (issue #186): nunca se lee el cursor
            // ni los botones del ratón durante el pintado, de modo que un
            // repintado parcial produce exactamente los mismos píxeles que el
            // repintado completo para el mismo estado visible.
            bool over = _pointerOver && Enabled;
            bool pressed = over && _pressed;

            using (var background = new SolidBrush(ResolveBackground(over, pressed)))
                graphics.FillRectangle(background, bounds);

            if (FlatAppearance.BorderSize > 0)
            {
                using var border = new Pen(FlatAppearance.BorderColor);
                graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }

            if (Focused && ShowFocusCues)
            {
                if (_dualFocusCue)
                {
                    // Doble filete anidado tinta/blanco: sobre la superficie
                    // clara el filete oscuro aporta el contraste y sobre el
                    // comando activo (azul de marca) lo aporta el blanco, de
                    // modo que el foco de teclado es visible en ambos
                    // cromatismas (el aro azul simple cae a ~1.8:1 sobre azul).
                    using var oscuro = new Pen(SoproTheme.Colors.Ink2);
                    graphics.DrawRectangle(oscuro, 1, 1, bounds.Width - 3, bounds.Height - 3);
                    using var claro = new Pen(SoproTheme.Colors.Surface);
                    graphics.DrawRectangle(claro, 2, 2, bounds.Width - 5, bounds.Height - 5);
                }
                else
                {
                    using var focus = new Pen(SoproTheme.Colors.BrandLt);
                    graphics.DrawRectangle(focus, 1, 1, bounds.Width - 3, bounds.Height - 3);
                }
            }

            if (_layout == SoproButtonLayout.Large)
                PaintLarge(graphics, bounds);
            else if (_layout == SoproButtonLayout.Small)
                PaintSmall(graphics, bounds);
            else
                PaintSample(graphics, bounds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _glyphFont?.Dispose();
                _glyphFont = null;
                _captionFont?.Dispose();
                _captionFont = null;
            }

            base.Dispose(disposing);
        }

        private void InvalidateIfCustomLayout()
        {
            if (_layout != SoproButtonLayout.Inline)
                Invalidate();
        }

        private void SetPointerOver(bool valor)
        {
            if (_pointerOver == valor) return;
            _pointerOver = valor;
            InvalidateIfCustomLayout();
        }

        private void SetPressed(bool valor)
        {
            if (_pressed == valor) return;
            _pressed = valor;
            InvalidateIfCustomLayout();
        }

        /// <summary>
        /// Re-sincroniza el estado "encima" con la posición real del cursor.
        /// Solo se invoca desde eventos discretos (handle creado, visible,
        /// habilitado); el pintado usa exclusivamente el estado seguido.
        /// </summary>
        private void SyncPointerOverFromCursor()
        {
            if (!IsHandleCreated) return;
            SetPointerOver(
                Enabled && ClientRectangle.Contains(PointToClient(MousePosition)));
        }

        private void UpdateVisuals()
        {
            UpdateIcon();
            UpdateLayoutMetrics();
            ApplyRoundedRegion();
            Invalidate();
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
            var newImage = SoproIconProvider.GetIcon(_iconType.Value, color, TamanoIconoPx);
            var oldImage = Image;
            Image = newImage;
            oldImage?.Dispose();
        }

        /// <summary>
        /// Escala del DPI del control (1.0 a 96 DPI). Los tamaños de SoproButton
        /// son LOGICOS: se multiplican por esta escala al pintar.
        /// </summary>
        private float EscalaDpi => DeviceDpi > 0 ? DeviceDpi / 96f : 1f;

        /// <summary>
        /// Tamaño en px REALES del icono (logico * escala de DPI). A 96 DPI
        /// devuelve exactamente <see cref="SoproIconSize"/>, de modo que el
        /// render a 100 % no cambia en un solo pixel.
        /// </summary>
        private int TamanoIconoPx
        {
            get
            {
                int px = (int)Math.Round(_iconSize * EscalaDpi);
                return Math.Max(12, px);
            }
        }

        private void UpdateLayoutMetrics()
        {
            _internalSync = true;
            try
            {
                base.Padding = _soproContentPadding;

                if (_layout != SoproButtonLayout.Inline)
                {
                    // Las presentaciones vertical (icono sobre texto) y de muestra
                    // se pintan a mano en OnPaint: el metodo estandar de Button
                    // (icono en linea) no aplica y la etiqueta siempre se dibuja.
                    return;
                }

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

                var iconWidth = _iconType.HasValue ? TamanoIconoPx : 0;
                var spacing = iconWidth > 0 && textWidth > 0
                    ? (int)Math.Round(_iconTextSpacing * EscalaDpi)
                    : 0;
                var computedWidth = _soproContentPadding.Left + iconWidth + spacing + textWidth + _soproContentPadding.Right;

                Width = Math.Max(_minimumAutoWidth, computedWidth);
                Height = _fixedButtonHeight;
            }
            finally
            {
                _internalSync = false;
            }
        }

        // =====================================================================
        // Pintado propio (presentaciones Large / Sample)
        // =====================================================================

        private Color ResolveBackground(bool over, bool pressed)
        {
            Color baseColor = BackColor;
            if (baseColor.A == 0)
                baseColor = ResolveInheritedBackground();

            if (!Enabled) return baseColor;

            if (pressed)
            {
                Color down = FlatAppearance.MouseDownBackColor;
                if (!down.IsEmpty && down.A != 0) return down;
                return baseColor;
            }

            if (over)
            {
                Color hover = FlatAppearance.MouseOverBackColor;
                if (!hover.IsEmpty && hover.A != 0) return hover;
                return baseColor;
            }

            return baseColor;
        }

        private Color ResolveInheritedBackground()
        {
            Control? parent = Parent;
            while (parent != null)
            {
                if (parent.BackColor.A != 0) return parent.BackColor;
                parent = parent.Parent;
            }

            return SoproTheme.Colors.HeaderDark;
        }

        /// <summary>
        /// Icono grande (30 px) centrado y etiqueta debajo, centrada.
        /// </summary>
        private void PaintLarge(Graphics graphics, Rectangle bounds)
        {
            // El icono se rasteriza ya escalado por DPI: se dibuja a su tamano
            // real para no reescalar el bitmap (y mantenerlo nitido).
            int iconSize = TamanoIconoPx;
            string text = Text ?? string.Empty;

            int textHeight = 0;
            if (text.Length > 0)
            {
                textHeight = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Height;
            }

            int gap = Math.Max(2, (int)Math.Round(4 * EscalaDpi));
            int contentHeight = iconSize + (textHeight > 0 ? gap + textHeight : 0);
            int top = bounds.Top + Math.Max(0, (bounds.Height - contentHeight) / 2);

            if (Image != null)
            {
                int iconX = bounds.Left + ((bounds.Width - iconSize) / 2);
                graphics.DrawImage(Image, new Rectangle(iconX, top, iconSize, iconSize));
            }

            if (textHeight <= 0) return;

            Color color = Enabled ? ForeColor : _disabledIconColor;
            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                new Rectangle(bounds.Left, top + iconSize + gap, bounds.Width, textHeight),
                color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        /// <summary>
        /// Icono y rotulo en linea, centrados como un bloque. Necesario porque
        /// el <see cref="Button"/> nativo reparte icono y texto por separado
        /// (ImageAlign/TextAlign) y en botones estrechos ambos se pisan.
        /// </summary>
        private void PaintSmall(Graphics graphics, Rectangle bounds)
        {
            // Icono ya rasterizado al tamano real de pantalla (ver TamanoIconoPx):
            // se dibuja 1:1 para no reescalar el bitmap.
            int iconSize = TamanoIconoPx;
            string text = Text ?? string.Empty;

            int textWidth = 0;
            int textHeight = 0;
            if (text.Length > 0)
            {
                Size size = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                textWidth = size.Width;
                textHeight = size.Height;
            }

            bool hayIcono = Image != null;
            int gap = hayIcono && textWidth > 0
                ? (int)Math.Round(_iconTextSpacing * EscalaDpi)
                : 0;
            int totalWidth = (hayIcono ? iconSize : 0) + gap + textWidth;

            int disponible = Math.Max(0, bounds.Width - _soproContentPadding.Left - _soproContentPadding.Right);
            int x = bounds.Left + _soproContentPadding.Left + Math.Max(0, (disponible - totalWidth) / 2);

            if (hayIcono)
            {
                int iconY = bounds.Top + Math.Max(0, (bounds.Height - iconSize) / 2);
                graphics.DrawImage(Image!, new Rectangle(x, iconY, iconSize, iconSize));
            }

            if (textWidth <= 0) return;

            Color color = Enabled ? ForeColor : _disabledIconColor;
            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                new Rectangle(x + (hayIcono ? iconSize + gap : 0), bounds.Top + ((bounds.Height - textHeight) / 2), textWidth + 2, textHeight),
                color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        /// <summary>
        /// Glifo de muestra (letra con barra de color, o muestra de color)
        /// seguido del rotulo <see cref="SoproCaption"/>.
        /// </summary>
        private void PaintSample(Graphics graphics, Rectangle bounds)
        {
            EnsureSampleFonts();

            string caption = _caption ?? string.Empty;
            string glyph = Text ?? string.Empty;
            bool letter = _layout == SoproButtonLayout.SampleLetter;

            const int gap = 6;       // separacion horizontal glifo-rotulo
            const int barHeight = 4; // barra de muestra del color de texto
            const int barGap = 1;    // separacion vertical glifo-barra
            int glyphWidth;
            int glyphHeight;

            if (letter)
            {
                Size size = TextRenderer.MeasureText(glyph, _glyphFont!, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                glyphWidth = Math.Max(size.Width, 10);
                glyphHeight = size.Height;
            }
            else
            {
                glyphWidth = 12;
                glyphHeight = 12;
            }

            int captionWidth = 0;
            int captionHeight = 0;
            if (caption.Length > 0)
            {
                Size size = TextRenderer.MeasureText(caption, _captionFont!, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                captionWidth = size.Width;
                captionHeight = size.Height;
            }

            int totalWidth = glyphWidth + (captionWidth > 0 ? gap + captionWidth : 0);
            int x = bounds.Left + Math.Max(0, (bounds.Width - totalWidth) / 2);

            // El glifo y su barra de muestra se pintan como un bloque UNICO
            // centrado en el control. Antes la barra se colocaba en
            // "y + altoGlifo - 1", con lo que en un boton de 26 px quedaba
            // pegada al borde inferior (y su marco, medio fuera) y el color de
            // texto se leia peor que el de fondo.
            int blockHeight = letter ? glyphHeight + barGap + barHeight : glyphHeight;
            if (blockHeight > bounds.Height) blockHeight = bounds.Height;
            int blockTop = bounds.Top + Math.Max(0, (bounds.Height - blockHeight) / 2);

            int glyphTop = blockTop;
            int glyphDrawHeight = letter
                ? Math.Max(1, Math.Min(glyphHeight, blockHeight - barGap - barHeight))
                : glyphHeight;

            Color sampleColor = Enabled ? ForeColor : _disabledIconColor;

            // Texto fijo de la presentacion (glifo y rotulo): usa el override
            // opt-in si el consumidor define uno (superficies claras); si no,
            // el tono claro historico del chrome oscuro. Deshabilitado usa
            // siempre el token de deshabilitado.
            Color textoMuestra = Enabled
                ? (_sampleGlyphColor ?? SoproTheme.Colors.RibbonText)
                : _disabledIconColor;

            if (letter)
            {
                // La letra se dibuja siempre legible (independiente del color
                // de texto elegido) y la barra inferior muestra ese color.
                TextRenderer.DrawText(
                    graphics,
                    glyph,
                    _glyphFont!,
                    new Rectangle(x, glyphTop, glyphWidth, glyphDrawHeight),
                    textoMuestra,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                // La barra nunca puede rebasar el borde inferior del control
                // (y su marco tampoco: por eso se reserva un px arriba).
                int barY = Math.Min(glyphTop + glyphDrawHeight + barGap, bounds.Bottom - barHeight);
                if (barY < 1) barY = 1;

                if (IsDark(sampleColor))
                {
                    // Un color muy oscuro sobre el ribbon oscuro no se ve: se
                    // encuadra la barra con un filete claro (sin taparla).
                    using var frame = new Pen(SoproTheme.Colors.RibbonDisabledIcon) { Alignment = PenAlignment.Inset };
                    graphics.DrawRectangle(frame, x - 1, barY - 1, glyphWidth + 1, barHeight + 1);
                }

                using (var bar = new SolidBrush(sampleColor))
                    graphics.FillRectangle(bar, x, barY, glyphWidth, barHeight);
            }
            else
            {
                using (var swatch = new SolidBrush(sampleColor))
                    graphics.FillRectangle(swatch, x, glyphTop, glyphWidth, glyphHeight);

                using var border = new Pen(SoproTheme.Colors.RibbonDisabledIcon);
                graphics.DrawRectangle(border, x, glyphTop, glyphWidth - 1, glyphHeight - 1);
            }

            if (captionWidth <= 0) return;

            TextRenderer.DrawText(
                graphics,
                caption,
                _captionFont!,
                new Rectangle(x + glyphWidth + gap, bounds.Top + ((bounds.Height - captionHeight) / 2), captionWidth + 2, captionHeight),
                textoMuestra,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        private void EnsureSampleFonts()
        {
            if (_captionFont == null
                || _captionFont.FontFamily != Font.FontFamily
                || Math.Abs(_captionFont.Size - Font.Size) > 0.01f
                || _captionFont.Unit != Font.Unit)
            {
                _captionFont?.Dispose();
                _captionFont = new Font(Font.FontFamily, Font.Size, FontStyle.Regular, Font.Unit);
            }

            if (_glyphFont == null
                || _glyphFont.FontFamily != Font.FontFamily
                || Math.Abs(_glyphFont.Size - Font.Size) > 0.01f
                || _glyphFont.Unit != Font.Unit)
            {
                _glyphFont?.Dispose();
                _glyphFont = new Font(Font.FontFamily, Font.Size, FontStyle.Bold, Font.Unit);
            }
        }

        private static bool IsDark(Color color)
        {
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return luminance < 0.40;
        }

        /// <summary>
        /// Recorte redondeado (radio 4 px logicos) para las acciones grandes,
        /// recalculado ante cambios de tamano o de DPI.
        /// </summary>
        private void ApplyRoundedRegion()
        {
            if (_updatingRegion) return;

            _updatingRegion = true;
            try
            {
                if (_layout != SoproButtonLayout.Large || Width <= 0 || Height <= 0)
                {
                    var previous = Region;
                    if (previous != null)
                    {
                        Region = null;
                        previous.Dispose();
                    }
                    return;
                }

                int radius = Math.Max(2, (int)Math.Round(4 * EscalaDpi));
                using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), radius);
                var previousRegion = Region;
                Region = new Region(path);
                previousRegion?.Dispose();
            }
            finally
            {
                _updatingRegion = false;
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = Math.Max(1, radius * 2);
            diameter = Math.Min(diameter, Math.Min(bounds.Width, bounds.Height));

            var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
