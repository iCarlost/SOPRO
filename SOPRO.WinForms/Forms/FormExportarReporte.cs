using SOPRO.Data.Context;
using SOPRO.Core.Entities;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormExportarReporte : Form
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly bool _exportarPdf;

        public FormExportarReporte(SOPROContext context, Proyecto proyecto, bool exportarPdf = false)
        {
            _context  = context;
            _proyecto = proyecto;
            _exportarPdf = exportarPdf;
            InitializeComponent();
            Text = exportarPdf ? "Exportar a PDF" : "Exportar a Excel";
            btnExportar.Text = exportarPdf ? "📄 Exportar" : "📊 Exportar";
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            // Localizar el FormPresupuesto padre para llamar sus métodos
            FormPresupuesto frmPres = null;
            foreach (Form f in System.Windows.Forms.Application.OpenForms)
                if (f is FormPresupuesto fp) { frmPres = fp; break; }

            Close();

            if (rdoPresupuesto.Checked)
            {
                if (_exportarPdf)
                    frmPres?.GenerarPdfPresupuesto();
                else
                    frmPres?.GenerarExcelPresupuesto();
            }
            else if (rdoPU.Checked)
            {
                if (_exportarPdf)
                    frmPres?.GenerarPdfAPU();
                else
                    frmPres?.GenerarExcelAPU();
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e) => Close();
    }
}
