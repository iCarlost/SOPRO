using SOPRO.Application.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    internal sealed class FormReajustarCosto : Form
    {
        private readonly Label _lblActual = new();
        private readonly NumericUpDown _nudObjetivo = new();
        private readonly CheckBox _chkMo = new();
        private readonly CheckBox _chkMat = new();
        private readonly CheckBox _chkMaq = new();
        private readonly CheckBox _chkHer = new();
        private readonly Button _btnAceptar = new();
        private readonly Button _btnCancelar = new();

        public decimal TargetAmount => _nudObjetivo.Value;
        public MatrixAdjustmentScopes SelectedScopes
        {
            get
            {
                var scopes = MatrixAdjustmentScopes.None;
                if (_chkMat.Checked) scopes |= MatrixAdjustmentScopes.Materiales;
                if (_chkMo.Checked) scopes |= MatrixAdjustmentScopes.ManoDeObra;
                if (_chkMaq.Checked) scopes |= MatrixAdjustmentScopes.Maquinaria;
                if (_chkHer.Checked) scopes |= MatrixAdjustmentScopes.Herramienta;
                return scopes;
            }
        }

        public FormReajustarCosto(string titulo, decimal actual, string contexto)
        {
            Text = titulo;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(470, 260);

            var lblContexto = new Label
            {
                AutoSize = false,
                Left = 16, Top = 16, Width = 438, Height = 38,
                Text = contexto,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _lblActual.AutoSize = false;
            _lblActual.Left = 16; _lblActual.Top = 62; _lblActual.Width = 438; _lblActual.Height = 20;
            _lblActual.Text = $"Monto actual: {actual:C2}";

            var lblObjetivo = new Label { Left = 16, Top = 95, Width = 160, Height = 22, Text = "Monto objetivo:" };
            _nudObjetivo.Left = 180; _nudObjetivo.Top = 92; _nudObjetivo.Width = 160;
            _nudObjetivo.DecimalPlaces = 2; _nudObjetivo.Maximum = 1000000000m; _nudObjetivo.Value = actual > 0 ? actual : 1m;
            _nudObjetivo.ThousandsSeparator = true;

            var grp = new GroupBox { Left = 16, Top = 126, Width = 438, Height = 82, Text = "Rubros a reajustar (por rendimiento/cantidad)" };
            _chkMo.Text = "Mano de obra"; _chkMo.Left = 16; _chkMo.Top = 28; _chkMo.Width = 120; _chkMo.Checked = true;
            _chkMat.Text = "Materiales"; _chkMat.Left = 152; _chkMat.Top = 28; _chkMat.Width = 100;
            _chkMaq.Text = "Maquinaria"; _chkMaq.Left = 268; _chkMaq.Top = 28; _chkMaq.Width = 100;
            _chkHer.Text = "Herramienta"; _chkHer.Left = 16; _chkHer.Top = 52; _chkHer.Width = 120;
            grp.Controls.AddRange(new Control[] { _chkMo, _chkMat, _chkMaq, _chkHer });

            _btnAceptar.Text = "Aplicar"; _btnAceptar.Left = 274; _btnAceptar.Top = 222; _btnAceptar.Width = 86;
            _btnCancelar.Text = "Cancelar"; _btnCancelar.Left = 368; _btnCancelar.Top = 222; _btnCancelar.Width = 86;

            _btnAceptar.Click += (_, __) =>
            {
                if (SelectedScopes == MatrixAdjustmentScopes.None)
                {
                    MessageBox.Show("Seleccione al menos un rubro para reajustar.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            _btnCancelar.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { lblContexto, _lblActual, lblObjetivo, _nudObjetivo, grp, _btnAceptar, _btnCancelar });
            AcceptButton = _btnAceptar;
            CancelButton = _btnCancelar;
        }
    }
}
