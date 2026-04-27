using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormRendimiento : Form
    {
        public decimal Cantidad { get; private set; }

        public FormRendimiento(string nombreInsumo, decimal cantidadActual)
        {
            InitializeComponent();

            lblNombre.Text = nombreInsumo;

            txt1RMedio.Text = cantidadActual.ToString("0.######");
            ActualizarRendimiento();
        }

        private void ActualizarRendimiento()
        {
            if (decimal.TryParse(txt1RMedio.Text, out decimal valor) && valor != 0)
            {
                txtRendMedio.Text = (1 / valor).ToString("0.######");
            }
            else
            {
                txtRendMedio.Text = "";
            }
        }

        private void ActualizarInverso()
        {
            if (decimal.TryParse(txtRendMedio.Text, out decimal valor) && valor != 0)
            {
                txt1RMedio.Text = (1 / valor).ToString("0.######");
            }
            else
            {
                txt1RMedio.Text = "";
            }
        }

        private void txtRendMedio_TextChanged(object sender, EventArgs e)
        {
            ActualizarInverso();
        }

        private void txt1RMedio_TextChanged(object sender, EventArgs e)
        {
            ActualizarRendimiento();
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            if (decimal.TryParse(txt1RMedio.Text, out decimal valor) && valor > 0)
            {
                Cantidad = valor;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("Valor inválido.");
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}