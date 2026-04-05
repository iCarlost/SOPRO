using SOPRO.Core.Entities;
using System.Drawing.Text;

namespace SOPRO.WinForms.Forms
{
    public class FormEditarTituloReporte : Form
    {
        private readonly TextBox txtTitulo = new();
        private readonly ComboBox cboFuente = new();
        private readonly NumericUpDown nudTamano = new();
        private readonly CheckBox chkNegrita = new();
        private readonly CheckBox chkCursiva = new();
        private readonly Button btnColor = new();
        private readonly Label lblPreview = new();
        private string _colorTexto = "#FFFFFF";

        public string TextoTitulo => txtTitulo.Text.Trim();
        public string NombreFuente => cboFuente.Text;
        public float TamanoFuente => (float)nudTamano.Value;
        public bool Negrita => chkNegrita.Checked;
        public bool Cursiva => chkCursiva.Checked;
        public string ColorTexto => _colorTexto;

        public FormEditarTituloReporte(ConfiguracionTituloReporte cfg)
        {
            Text = "Editar título del reporte";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 250);

            var lblTitulo = new Label { Left = 16, Top = 18, Width = 120, Text = "Título:" };
            txtTitulo.Left = 140; txtTitulo.Top = 14; txtTitulo.Width = 360;

            var lblFuente = new Label { Left = 16, Top = 56, Width = 120, Text = "Fuente:" };
            cboFuente.Left = 140; cboFuente.Top = 52; cboFuente.Width = 220; cboFuente.DropDownStyle = ComboBoxStyle.DropDownList;

            var lblTamano = new Label { Left = 372, Top = 56, Width = 56, Text = "Tamaño:" };
            nudTamano.Left = 430; nudTamano.Top = 52; nudTamano.Width = 70; nudTamano.Minimum = 8; nudTamano.Maximum = 48; nudTamano.DecimalPlaces = 1; nudTamano.Increment = 0.5M;

            chkNegrita.Left = 140; chkNegrita.Top = 92; chkNegrita.Width = 90; chkNegrita.Text = "Negrita";
            chkCursiva.Left = 238; chkCursiva.Top = 92; chkCursiva.Width = 90; chkCursiva.Text = "Cursiva";
            btnColor.Left = 336; btnColor.Top = 88; btnColor.Width = 164; btnColor.Height = 28; btnColor.Text = "Color del texto";

            var grpPreview = new GroupBox { Left = 16, Top = 128, Width = 484, Height = 72, Text = "Vista previa" };
            lblPreview.Left = 12; lblPreview.Top = 28; lblPreview.Width = 456; lblPreview.Height = 28; lblPreview.TextAlign = ContentAlignment.MiddleCenter;
            grpPreview.Controls.Add(lblPreview);

            var btnAceptar = new Button { Text = "Guardar", DialogResult = DialogResult.OK, Left = 316, Width = 88, Height = 32, Top = 210 };
            var btnCancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 412, Width = 88, Height = 32, Top = 210 };
            AcceptButton = btnAceptar;
            CancelButton = btnCancelar;

            Controls.AddRange(new Control[] { lblTitulo, txtTitulo, lblFuente, cboFuente, lblTamano, nudTamano, chkNegrita, chkCursiva, btnColor, grpPreview, btnAceptar, btnCancelar });

            foreach (FontFamily family in new InstalledFontCollection().Families.OrderBy(f => f.Name))
                cboFuente.Items.Add(family.Name);

            txtTitulo.Text = cfg.TextoTitulo;
            cboFuente.SelectedItem = cfg.NombreFuente;
            if (cboFuente.SelectedIndex < 0 && cboFuente.Items.Count > 0)
                cboFuente.SelectedItem = cboFuente.Items.Contains("Segoe UI") ? "Segoe UI" : cboFuente.Items[0];
            nudTamano.Value = (decimal)(cfg.TamanoFuente > 0 ? cfg.TamanoFuente : 13f);
            chkNegrita.Checked = cfg.Negrita;
            chkCursiva.Checked = cfg.Cursiva;
            _colorTexto = string.IsNullOrWhiteSpace(cfg.ColorTexto) ? "#FFFFFF" : cfg.ColorTexto;
            btnColor.BackColor = ColorTranslator.FromHtml(_colorTexto);
            btnColor.ForeColor = GetContrastingColor(btnColor.BackColor);

            txtTitulo.TextChanged += (_, __) => RefreshPreview();
            cboFuente.SelectedIndexChanged += (_, __) => RefreshPreview();
            nudTamano.ValueChanged += (_, __) => RefreshPreview();
            chkNegrita.CheckedChanged += (_, __) => RefreshPreview();
            chkCursiva.CheckedChanged += (_, __) => RefreshPreview();
            btnColor.Click += BtnColor_Click;

            RefreshPreview();
        }

        private void BtnColor_Click(object? sender, EventArgs e)
        {
            using var dlg = new ColorDialog();
            dlg.Color = ColorTranslator.FromHtml(_colorTexto);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _colorTexto = ColorTranslator.ToHtml(dlg.Color);
            btnColor.BackColor = dlg.Color;
            btnColor.ForeColor = GetContrastingColor(dlg.Color);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var style = FontStyle.Regular;
            if (chkNegrita.Checked) style |= FontStyle.Bold;
            if (chkCursiva.Checked) style |= FontStyle.Italic;
            lblPreview.Text = string.IsNullOrWhiteSpace(txtTitulo.Text) ? "Vista previa del título" : txtTitulo.Text;
            lblPreview.Font = new Font(string.IsNullOrWhiteSpace(cboFuente.Text) ? "Segoe UI" : cboFuente.Text, (float)nudTamano.Value, style);
            lblPreview.ForeColor = ColorTranslator.FromHtml(_colorTexto);
        }

        private static Color GetContrastingColor(Color color)
            => color.GetBrightness() < 0.5f ? Color.White : Color.Black;
    }
}
