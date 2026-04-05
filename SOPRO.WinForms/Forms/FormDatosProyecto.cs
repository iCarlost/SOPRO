using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormDatosProyecto : Form
    {
        public Proyecto Proyecto { get; private set; }

        /// <summary>
        /// Evento que se dispara cuando se actualizan los decimales del proyecto.
        /// </summary>
        public static event EventHandler DecimalesActualizados;

        public FormDatosProyecto()
        {
            InitializeComponent();
            Proyecto = new Proyecto();
        }

        public FormDatosProyecto(Proyecto proyecto)
        {
            InitializeComponent();
            Proyecto = proyecto;
            LoadData();
        }

        private void LoadData()
        {
            if (Proyecto == null) return;

            // Tab Obra
            txtNombre.Text      = Proyecto.Nombre;
            txtDescripcion.Text = Proyecto.Descripcion;
            txtUbicacion.Text   = Proyecto.Ubicacion;
            txtConvocante.Text  = Proyecto.Convocante;
            txtContratista.Text = Proyecto.Contratista;
            txtApoderado.Text   = Proyecto.ApoderadoLegal;
            dtpInicio.Value     = Proyecto.FechaInicio;
            dtpTermino.Value    = Proyecto.FechaTermino;
            nudPlazo.Value      = Proyecto.PlazoEjecucion;

            // Tab Cálculo
            nudIVA.Value                 = Proyecto.PorcentajeIVA;
            nudDecimalesCantidad.Value   = Proyecto.DecimalesCantidad;
            nudDecimalesImporte.Value    = Proyecto.DecimalesImporte;
            nudDecimalesPorcentaje.Value = Proyecto.DecimalesPorcentaje;
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MessageBox.Show("El nombre de la obra es obligatorio.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabControl.SelectedIndex = 0;
                txtNombre.Focus();
                return;
            }

            // Tab Obra
            Proyecto.Nombre        = txtNombre.Text.Trim();
            Proyecto.Descripcion   = txtDescripcion.Text.Trim();
            Proyecto.Ubicacion     = txtUbicacion.Text.Trim();
            Proyecto.Convocante    = txtConvocante.Text.Trim();
            Proyecto.Contratista   = txtContratista.Text.Trim();
            Proyecto.ApoderadoLegal= txtApoderado.Text.Trim();
            Proyecto.FechaInicio   = dtpInicio.Value;
            Proyecto.FechaTermino  = dtpTermino.Value;
            Proyecto.PlazoEjecucion= (int)nudPlazo.Value;

            // Tab Cálculo
            Proyecto.PorcentajeIVA        = nudIVA.Value;
            Proyecto.DecimalesCantidad    = (int)nudDecimalesCantidad.Value;
            Proyecto.DecimalesImporte     = (int)nudDecimalesImporte.Value;
            Proyecto.DecimalesPorcentaje  = (int)nudDecimalesPorcentaje.Value;

            // Actualizar FormatoHelper para que todos los forms usen la nueva configuración
            FormatoHelper.EstablecerProyecto(Proyecto);

            // Disparar evento para refrescar grids abiertos
            DecimalesActualizados?.Invoke(this, EventArgs.Empty);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
