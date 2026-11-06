using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormDatosProyecto : Form
    {
        public Proyecto Proyecto { get; private set; }

        private readonly bool _suppressGlobalFormatUpdates;
        private ToolTip? _toolTipFormatoBloqueado;

        /// <summary>
        /// Texto de ayuda para los controles de cálculo bloqueados en captura aislada.
        /// </summary>
        private const string TooltipFormatoBloqueado =
            "Heredado del proyecto original. Los cálculos copiados no se recalculan: " +
            "podrás cambiar el IVA y los decimales después de crear la copia.";

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
            : this(proyecto, suppressGlobalFormatUpdates: false)
        {
        }

        /// <summary>
        /// Variante para captura aislada (p. ej. duplicación de proyecto): al
        /// aceptar no actualiza <see cref="FormatoHelper"/> ni dispara
        /// <see cref="DecimalesActualizados"/>, porque el proyecto todavía no es
        /// el proyecto activo de la sesión.
        /// </summary>
        public FormDatosProyecto(Proyecto proyecto, bool suppressGlobalFormatUpdates)
        {
            InitializeComponent();
            Proyecto = proyecto;
            _suppressGlobalFormatUpdates = suppressGlobalFormatUpdates;
            LoadData();
            if (_suppressGlobalFormatUpdates) ConfigurarCalculoSoloLectura();
        }

        /// <summary>
        /// En captura aislada (duplicación) los cálculos copiados no se recalculan:
        /// se bloquea el IVA y los decimales mostrando los valores heredados y se
        /// deja nota visible con tu-tooltip explicativo. Metadatos siguen editables.
        /// </summary>
        private void ConfigurarCalculoSoloLectura()
        {
            _toolTipFormatoBloqueado = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 200,
                ReshowDelay  = 100
            };

            Control[] controlesCalculo =
            {
                nudIVA, nudDecimalesCantidad, nudDecimalesImporte, nudDecimalesPorcentaje
            };

            foreach (Control control in controlesCalculo)
            {
                control.Enabled = false;
                _toolTipFormatoBloqueado.SetToolTip(control, TooltipFormatoBloqueado);
            }

            var aviso = new Label
            {
                AutoSize  = true,
                Location  = new System.Drawing.Point(20, 330),
                ForeColor = System.Drawing.Color.FromArgb(110, 110, 110),
                Font      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic),
                Text      = "ℹ Duplicación: IVA y decimales se heredan del proyecto original, ya que " +
                            "los cálculos copiados no se recalculan.\nPuedes ajustarlos en la copia " +
                            "después de guardarla."
            };
            tabCalculo.Controls.Add(aviso);
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

            // Tab Cálculo: en captura aislada se conservan los valores heredados
            // del original; los cálculos copiados no se recalculan.
            if (!_suppressGlobalFormatUpdates)
            {
                Proyecto.PorcentajeIVA       = nudIVA.Value;
                Proyecto.DecimalesCantidad   = (int)nudDecimalesCantidad.Value;
                Proyecto.DecimalesImporte    = (int)nudDecimalesImporte.Value;
                Proyecto.DecimalesPorcentaje = (int)nudDecimalesPorcentaje.Value;

                // Actualizar FormatoHelper para que todos los forms usen la nueva configuración
                FormatoHelper.EstablecerProyecto(Proyecto);

                // Disparar evento para refrescar grids abiertos
                DecimalesActualizados?.Invoke(this, EventArgs.Empty);
            }

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
