using System.Drawing;
using SOPRO.WinForms.Models;

namespace SOPRO.WinForms.Forms
{
    internal sealed class FormConfiguracionVisualGantt : Form
    {
        private readonly Label _lblFuenteValor;
        private readonly Button _btnFuente;
        private readonly Button _btnTexto;
        private readonly Button _btnContorno;
        private readonly Button _btnBarraNormal;
        private readonly Button _btnBarraCritica;
        private readonly Button _btnBarraResumen;
        private readonly Panel _previewPanel;
        private readonly Button _btnAceptar;
        private readonly Button _btnCancelar;
        private readonly Button _btnRestablecer;
        private readonly FontDialog _fontDialog = new();
        private readonly ColorDialog _colorDialog = new() { FullOpen = true };
        private GanttVisualSettings _settings;

        public GanttVisualSettings Settings => _settings.Clone();

        public FormConfiguracionVisualGantt(GanttVisualSettings currentSettings)
        {
            _settings = currentSettings.Clone();

            Text = "Configurar apariencia del Gantt";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 355);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(12),
                AutoSize = false
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 7; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(layout);

            _lblFuenteValor = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3, 0, 3, 0)
            };
            _btnFuente = CrearBoton("Cambiar tipo de fuente...", (_, __) => SeleccionarFuente());
            _btnTexto = CrearBoton("Color texto importes...", (_, __) => SeleccionarColor(c => _settings.TextColor = c, _settings.TextColor));
            _btnContorno = CrearBoton("Color contorno texto...", (_, __) => SeleccionarColor(c => _settings.OutlineColor = c, _settings.OutlineColor));
            _btnBarraNormal = CrearBoton("Color barra normal...", (_, __) => SeleccionarColor(c => _settings.NormalBarColor = c, _settings.NormalBarColor));
            _btnBarraCritica = CrearBoton("Color barra crítica...", (_, __) => SeleccionarColor(c => _settings.CriticalBarColor = c, _settings.CriticalBarColor));
            _btnBarraResumen = CrearBoton("Color barra resumen...", (_, __) => SeleccionarColor(c => _settings.SummaryBarColor = c, _settings.SummaryBarColor));

            layout.Controls.Add(CrearLabel("Fuente importes:"), 0, 0);
            layout.Controls.Add(_lblFuenteValor, 1, 0);
            layout.Controls.Add(_btnFuente, 0, 1);
            layout.SetColumnSpan(_btnFuente, 2);
            layout.Controls.Add(_btnTexto, 0, 2);
            layout.SetColumnSpan(_btnTexto, 2);
            layout.Controls.Add(_btnContorno, 0, 3);
            layout.SetColumnSpan(_btnContorno, 2);
            layout.Controls.Add(_btnBarraNormal, 0, 4);
            layout.SetColumnSpan(_btnBarraNormal, 2);
            layout.Controls.Add(_btnBarraCritica, 0, 5);
            layout.SetColumnSpan(_btnBarraCritica, 2);
            layout.Controls.Add(_btnBarraResumen, 0, 6);
            layout.SetColumnSpan(_btnBarraResumen, 2);

            _previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 8),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _previewPanel.Paint += PreviewPanel_Paint;
            layout.Controls.Add(_previewPanel, 0, 7);
            layout.SetColumnSpan(_previewPanel, 2);

            var panelBotones = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12, 0, 12, 12),
                Height = 46
            };
            Controls.Add(panelBotones);

            _btnAceptar = CrearBotonSimple("Aceptar", (_, __) => { DialogResult = DialogResult.OK; Close(); });
            _btnCancelar = CrearBotonSimple("Cancelar", (_, __) => { DialogResult = DialogResult.Cancel; Close(); });
            _btnRestablecer = CrearBotonSimple("Restablecer", (_, __) => { _settings = GanttVisualSettings.CreateDefault(); ActualizarVista(); });
            panelBotones.Controls.Add(_btnAceptar);
            panelBotones.Controls.Add(_btnCancelar);
            panelBotones.Controls.Add(_btnRestablecer);

            AcceptButton = _btnAceptar;
            CancelButton = _btnCancelar;
            ActualizarVista();
        }

        private static Label CrearLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Margin = new Padding(3, 0, 3, 0)
            };
        }

        private static Button CrearBoton(string text, EventHandler handler)
        {
            var button = CrearBotonSimple(text, handler);
            button.Dock = DockStyle.Fill;
            return button;
        }

        private static Button CrearBotonSimple(string text, EventHandler handler)
        {
            var button = new Button
            {
                AutoSize = true,
                Text = text,
                Margin = new Padding(3)
            };
            button.Click += handler;
            return button;
        }

        private void SeleccionarFuente()
        {
            using var currentFont = _settings.CreateFinancialTextFont(8f);
            _fontDialog.Font = currentFont;
            _fontDialog.ShowColor = false;
            _fontDialog.AllowVectorFonts = false;
            _fontDialog.AllowVerticalFonts = false;
            if (_fontDialog.ShowDialog(this) != DialogResult.OK)
                return;

            _settings.FontFamilyName = _fontDialog.Font.FontFamily.Name;
            _settings.FontStyle = _fontDialog.Font.Style;
            ActualizarVista();
        }

        private void SeleccionarColor(Action<Color> setter, Color currentColor)
        {
            _colorDialog.Color = currentColor;
            if (_colorDialog.ShowDialog(this) != DialogResult.OK)
                return;

            setter(_colorDialog.Color);
            ActualizarVista();
        }

        private void ActualizarVista()
        {
            _lblFuenteValor.Text = $"{_settings.FontFamilyName}, {_settings.FontStyle}";
            _btnTexto.ForeColor = _settings.TextColor;
            _btnContorno.ForeColor = _settings.OutlineColor;
            _btnBarraNormal.ForeColor = _settings.NormalBarColor;
            _btnBarraCritica.ForeColor = _settings.CriticalBarColor;
            _btnBarraResumen.ForeColor = _settings.SummaryBarColor;
            _previewPanel.Invalidate();
        }

        private void PreviewPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);

            var rectNormal = new Rectangle(16, 16, 120, 18);
            var rectCritica = new Rectangle(16, 48, 120, 18);
            var rectResumen = new Rectangle(16, 80, 120, 18);

            using var brushNormal = new SolidBrush(_settings.NormalBarColor);
            using var brushCritica = new SolidBrush(_settings.CriticalBarColor);
            using var penResumen = new Pen(_settings.SummaryBarColor, 2f);
            using var font = _settings.CreateFinancialTextFont(8f);
            using var outlinePen = new Pen(_settings.OutlineColor, 2f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            using var fillBrush = new SolidBrush(_settings.TextColor);

            g.FillRectangle(brushNormal, rectNormal);
            g.FillRectangle(brushCritica, rectCritica);
            g.DrawLine(penResumen, rectResumen.Left, rectResumen.Top + rectResumen.Height / 2, rectResumen.Right, rectResumen.Top + rectResumen.Height / 2);

            DrawPreviewText(g, "$1,800,526.36", font, fillBrush, outlinePen, new RectangleF(160, 12, 220, 24));
            DrawPreviewText(g, "$845,220.10", font, fillBrush, outlinePen, new RectangleF(160, 44, 220, 24));
            DrawPreviewText(g, "$2,645,746.46", font, fillBrush, outlinePen, new RectangleF(160, 76, 220, 24));
        }

        private static void DrawPreviewText(Graphics g, string text, Font font, Brush fillBrush, Pen outlinePen, RectangleF rect)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var emSize = g.DpiY * font.SizeInPoints / 72f;
            path.AddString(text, font.FontFamily, (int)font.Style, emSize, new PointF(rect.Left, rect.Top), StringFormat.GenericTypographic);
            var previous = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawPath(outlinePen, path);
            g.FillPath(fillBrush, path);
            g.SmoothingMode = previous;
        }
    }
}
