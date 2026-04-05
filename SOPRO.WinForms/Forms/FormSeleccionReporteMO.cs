using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormSeleccionReporteMO : Form
    {
        public enum TipoReporte { Catalogo, TabuladorFSR }
        public TipoReporte Seleccion { get; private set; } = TipoReporte.Catalogo;

        public FormSeleccionReporteMO()
        {
            InitializeComponent();
        }

        private void panelOpcion1_Click(object sender, System.EventArgs e)
        {
            rbCatalogo.Checked = true;
        }

        private void panelOpcion2_Click(object sender, System.EventArgs e)
        {
            rbTabulador.Checked = true;
        }

        private void rbCatalogo_CheckedChanged(object sender, System.EventArgs e)
        {
            panelOpcion1.BackColor = rbCatalogo.Checked
                ? Color.FromArgb(232, 234, 246)
                : Color.FromArgb(245, 245, 255);
        }

        private void rbTabulador_CheckedChanged(object sender, System.EventArgs e)
        {
            panelOpcion2.BackColor = rbTabulador.Checked
                ? Color.FromArgb(232, 234, 246)
                : Color.FromArgb(245, 245, 255);
        }

        private void btnGenerar_Click(object sender, System.EventArgs e)
        {
            Seleccion = rbTabulador.Checked ? TipoReporte.TabuladorFSR : TipoReporte.Catalogo;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancelar_Click(object sender, System.EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
