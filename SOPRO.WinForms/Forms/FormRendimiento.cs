using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormRendimiento : Form
    {
        public decimal Cantidad { get; private set; }

        private bool _actualizando = false;

        public FormRendimiento(string nombreInsumo, decimal cantidadActual)
        {
            InitializeComponent();
            this.Text = $"Rendimiento - {nombreInsumo}";
            Cantidad  = cantidadActual;
            CargarValorInicial(cantidadActual);
        }

        private void CargarValorInicial(decimal cantidad)
        {
            decimal rend = cantidad > 0 ? 1m / cantidad : 10m;
            decimal inv  = cantidad > 0 ? cantidad      : 0.1m;

            txtRendMedio.Text  = rend.ToString("F5");
            txt1RMedio.Text    = inv.ToString("F5");
            txtRendMinimo.Text = rend.ToString("F5");
            txt1RMinimo.Text   = inv.ToString("F5");
            txtRendOptimo.Text = rend.ToString("F5");
            txt1ROptimo.Text   = inv.ToString("F5");

            rbMedio.Checked = true;
        }

        // ── Eventos TextChanged ───────────────────────────────────────────────
        private void txtRendMinimo_TextChanged(object sender, EventArgs e)
            => ActualizarInverso(txtRendMinimo, txt1RMinimo);

        private void txt1RMinimo_TextChanged(object sender, EventArgs e)
            => ActualizarRendimiento(txtRendMinimo, txt1RMinimo);

        private void txtRendMedio_TextChanged(object sender, EventArgs e)
            => ActualizarInverso(txtRendMedio, txt1RMedio);

        private void txt1RMedio_TextChanged(object sender, EventArgs e)
            => ActualizarRendimiento(txtRendMedio, txt1RMedio);

        private void txtRendOptimo_TextChanged(object sender, EventArgs e)
            => ActualizarInverso(txtRendOptimo, txt1ROptimo);

        private void txt1ROptimo_TextChanged(object sender, EventArgs e)
            => ActualizarRendimiento(txtRendOptimo, txt1ROptimo);

        // ── Lógica de cálculo ─────────────────────────────────────────────────
        private void ActualizarInverso(TextBox txtRend, TextBox txt1R)
        {
            if (_actualizando) return;
            _actualizando = true;
            if (decimal.TryParse(txtRend.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal r) && r != 0)
            {
                txt1R.Text = (1m / r).ToString("F5");
            }
            _actualizando = false;
        }

        private void ActualizarRendimiento(TextBox txtRend, TextBox txt1R)
        {
            if (_actualizando) return;
            _actualizando = true;
            if (decimal.TryParse(txt1R.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal inv) && inv != 0)
            {
                txtRend.Text = (1m / inv).ToString("F5");
            }
            _actualizando = false;
        }

        // ── Botones ───────────────────────────────────────────────────────────
        private void btnAceptar_Click(object sender, EventArgs e)
        {
            TextBox txt1R = rbMinimo.Checked ? txt1RMinimo :
                            rbOptimo.Checked ? txt1ROptimo : txt1RMedio;

            if (decimal.TryParse(txt1R.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal val) && val > 0)
            {
                Cantidad     = val;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("Ingresa un valor numerico valido mayor a cero.",
                    "Validacion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
