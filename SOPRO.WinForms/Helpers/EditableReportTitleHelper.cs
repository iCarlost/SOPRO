using SOPRO.Application.Services;
using SOPRO.Data.Context;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Helpers
{
    internal sealed class EditableReportTitleHelper
    {
        private readonly SOPROContext _context;
        private readonly Panel _panelTop;
        private readonly Label _label;
        private readonly Func<int> _projectIdAccessor;
        private readonly string _moduleKey;
        private Button? _button;

        public EditableReportTitleHelper(SOPROContext context, Panel panelTop, Label label, Func<int> projectIdAccessor, string moduleKey)
        {
            _context = context;
            _panelTop = panelTop;
            _label = label;
            _projectIdAccessor = projectIdAccessor;
            _moduleKey = moduleKey;
        }

        public void Attach()
        {
            int projectId = _projectIdAccessor();
            if (projectId <= 0) return;

            var service = new ConfiguracionTituloReporteService(_context);
            var cfg = service.ObtenerOCrear(projectId, _moduleKey, _label.Text);
            ApplyToLabel(cfg);

            _button = _panelTop.Controls.OfType<Button>().FirstOrDefault(x => x.Name == "btnEditarTituloReporte_" + _moduleKey);
            if (_button == null)
            {
                _button = new Button
                {
                    Name = "btnEditarTituloReporte_" + _moduleKey,
                    Text = "✎",
                    Width = 28,
                    Height = 28,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                    TabStop = false,
                    Cursor = Cursors.Hand
                };
                _button.FlatAppearance.BorderSize = 0;
                _button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 255, 255, 255);
                _button.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 255, 255, 255);
                _button.Click += (_, __) => EditTitle();
                _panelTop.Controls.Add(_button);
            }

            RepositionButton();
            _label.SizeChanged += (_, __) => RepositionButton();
            _label.TextChanged += (_, __) => RepositionButton();
            _panelTop.SizeChanged += (_, __) => RepositionButton();
            _button.BringToFront();
        }

        private void EditTitle()
        {
            int projectId = _projectIdAccessor();
            if (projectId <= 0) return;

            var service = new ConfiguracionTituloReporteService(_context);
            var cfg = service.ObtenerOCrear(projectId, _moduleKey, _label.Text);
            using var dlg = new FormEditarTituloReporte(cfg);
            if (dlg.ShowDialog(_panelTop.FindForm()) != DialogResult.OK) return;

            cfg = service.Guardar(projectId, _moduleKey, dlg.TextoTitulo, dlg.NombreFuente, dlg.TamanoFuente, dlg.Negrita, dlg.Cursiva, dlg.ColorTexto);
            ApplyToLabel(cfg);
        }

        private void ApplyToLabel(ConfiguracionTituloReporte cfg)
        {
            _label.Text = string.IsNullOrWhiteSpace(cfg.TextoTitulo) ? _label.Text : cfg.TextoTitulo;
            var style = FontStyle.Regular;
            if (cfg.Negrita) style |= FontStyle.Bold;
            if (cfg.Cursiva) style |= FontStyle.Italic;
            _label.Font = new Font(string.IsNullOrWhiteSpace(cfg.NombreFuente) ? _label.Font.Name : cfg.NombreFuente, cfg.TamanoFuente > 0 ? cfg.TamanoFuente : _label.Font.Size, style);
            try { _label.ForeColor = ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(cfg.ColorTexto) ? "#FFFFFF" : cfg.ColorTexto); } catch { }
            RepositionButton();
        }

        private void RepositionButton()
        {
            if (_button == null) return;
            _button.Location = new Point(_label.Left + _label.Width + 8, _label.Top + Math.Max(0, (_label.Height - _button.Height) / 2));
        }
    }
}
