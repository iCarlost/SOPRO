using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarHerramienta : Form
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly Herramienta _herramienta;
        private readonly bool _esNuevo;
        
        public FormEditarHerramienta(SOPROContext context, int proyectoId, Herramienta herramienta = null)
        {
            InitializeComponent();
            
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _herramienta = herramienta;
            _esNuevo = herramienta == null;
            
            ConfigurarFormulario();
            CargarUnidades();
            
            if (!_esNuevo)
                CargarDatos();
            else
                txtClave.Text = KeySuggestionService.Generate();
        }
        
        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nueva Herramienta" : "Editar Herramienta";
            lblTitulo.Text = _esNuevo ? "NUEVA HERRAMIENTA" : "EDITAR HERRAMIENTA";
        }
        
        private void CargarUnidades()
        {
            cboUnidad.Items.Clear();
            cboUnidad.Items.Add("pza");
            cboUnidad.Items.Add("hr");
            cboUnidad.Items.Add("%MO");
            cboUnidad.SelectedIndex = 0;
        }
        
        private void CargarDatos()
        {
            txtClave.Text = _herramienta.Clave;
            txtDescripcion.Text = _herramienta.Descripcion;
            cboUnidad.Text = _herramienta.Unidad;
            nudPrecio.Value = _herramienta.PrecioUnitario;
            
            ActualizarEtiquetaPrecio();
        }
        
        private void cboUnidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActualizarEtiquetaPrecio();
        }
        
        private void ActualizarEtiquetaPrecio()
        {
            if (cboUnidad.Text == "%MO")
            {
                lblPrecio.Text = "Porcentaje (%):";
                lblHint.Text = "💡 Ejemplo: 0.03 para 3% de la mano de obra total";
                lblHint.Visible = true;
                nudPrecio.DecimalPlaces = 4;
                nudPrecio.Maximum = 1;  // Máximo 100% = 1.00
                nudPrecio.Minimum = 0;
                nudPrecio.Increment = 0.01m;
            }
            else
            {
                lblPrecio.Text = "Precio Unitario:";
                lblHint.Text = "";
                lblHint.Visible = false;
                nudPrecio.DecimalPlaces = 2;
                nudPrecio.Maximum = 999999;
                nudPrecio.Minimum = 0;
            }
        }
        
        private void btnGuardar_Click(object sender, EventArgs e)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(txtClave.Text))
            {
                MessageBox.Show(
                    "La clave es obligatoria.",
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtClave.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show(
                    "La descripción es obligatoria.",
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtDescripcion.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(cboUnidad.Text))
            {
                MessageBox.Show(
                    "La unidad es obligatoria.",
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                cboUnidad.Focus();
                return;
            }
            
            // Validar clave única
            var claveExistente = _context.Herramientas
                .Any(h => h.ProyectoId == _proyectoId && 
                         h.Clave == txtClave.Text.Trim() &&
                         (_esNuevo || h.Id != _herramienta.Id));
            
            if (claveExistente)
            {
                MessageBox.Show(
                    $"Ya existe una herramienta con la clave '{txtClave.Text}'.",
                    "Clave Duplicada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtClave.Focus();
                return;
            }
            
            try
            {
                if (_esNuevo)
                {
                    var nueva = new Herramienta
                    {
                        ProyectoId = _proyectoId,
                        Clave = txtClave.Text.Trim(),
                        Descripcion = txtDescripcion.Text.Trim(),
                        Unidad = cboUnidad.Text.Trim(),
                        PrecioUnitario = nudPrecio.Value,
                        FechaCreacion = DateTime.Now
                    };
                    
                    _context.Herramientas.Add(nueva);
                }
                else
                {
                    _herramienta.Clave = txtClave.Text.Trim();
                    _herramienta.Descripcion = txtDescripcion.Text.Trim();
                    _herramienta.Unidad = cboUnidad.Text.Trim();
                    _herramienta.PrecioUnitario = nudPrecio.Value;
                    _herramienta.FechaModificacion = DateTime.Now;
                }
                
                _context.SaveChanges();

                OpenFormsRefreshHelper.RefrescarCatalogosHerramientasAbiertos();
                OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
