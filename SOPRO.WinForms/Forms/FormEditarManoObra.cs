using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Services;
using SOPRO.Application.DTOs.Catalog;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarManoObra : Form
    {
        private readonly SOPROContext _context;
        private readonly int? _proyectoId;
        private readonly ManoDeObra _manoDeObra;
        private readonly bool _esNuevo;
        private readonly Proyecto? _proyecto; // para calcular FSR automáticamente
        
        public FormEditarManoObra(SOPROContext context, int? proyectoId, ManoDeObra manoDeObra = null, Proyecto proyecto = null)
        {
            InitializeComponent();
            
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _manoDeObra = manoDeObra;
            _esNuevo = manoDeObra == null;
            _proyecto = proyecto;
            
            ConfigurarFormulario();
            
            if (_esNuevo)
                txtClave.Text = KeySuggestionService.Generate();
            else
            {
                CargarDatos();
                CalcularSalarioReal();
            }
        }
        
        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nueva Mano de Obra" : "Editar Mano de Obra";
            
            if (_proyectoId.HasValue)
            {
                rbProyecto.Enabled = true;
                rbMaestro.Enabled = true;
                
                if (_esNuevo)
                {
                    rbProyecto.Checked = true;
                }
            }
            else
            {
                rbMaestro.Checked = true;
                rbProyecto.Enabled = false;
            }
        }
        
        private void CargarDatos()
        {
            txtClave.Text = _manoDeObra.Clave;
            txtDescripcion.Text = _manoDeObra.Descripcion;
            cboUnidad.Text = _manoDeObra.Unidad;
            nudSalarioBase.Value = _manoDeObra.SalarioBase;
            nudFSR.Value = _manoDeObra.FactorSalarioReal;
            nudSalarioReal.Value = _manoDeObra.SalarioReal;
            txtNotas.Text = _manoDeObra.Notas;
            
            if (_manoDeObra.Origen == OrigenInsumo.Maestro)
                rbMaestro.Checked = true;
            else
                rbProyecto.Checked = true;
            
            ConfigurarCamposSegunUnidad(); // Configurar visibilidad según unidad
        }
        
        private void nudSalarioBase_ValueChanged(object sender, EventArgs e)
        {
            AplicarFSRAutomatico();
            CalcularSalarioReal();
        }

        /// <summary>
        /// Si el proyecto tiene parámetros FSR configurados, calcula y aplica
        /// el FSR automáticamente según el SalarioBase actual.
        /// El usuario puede sobreescribir nudFSR manualmente después.
        /// </summary>
        private void AplicarFSRAutomatico()
        {
            if (_proyecto == null) return;
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR)) return;

            var fsr = FsrCalculationService.Calcular(_proyecto.ParametrosFSR, nudSalarioBase.Value);
            if (fsr.HasValue)
            {
                var fsrRedondeado = Math.Round(fsr.Value, 6);
                var fsrSeguro = Math.Max(nudFSR.Minimum, Math.Min(nudFSR.Maximum, fsrRedondeado));

                nudFSR.ValueChanged -= nudFSR_ValueChanged;
                try
                {
                    nudFSR.Value = fsrSeguro;
                }
                finally
                {
                    nudFSR.ValueChanged += nudFSR_ValueChanged;
                }
            }
        }

        private void nudFSR_ValueChanged(object sender, EventArgs e)
        {
            CalcularSalarioReal();
        }
        
        private void cboUnidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigurarCamposSegunUnidad();
        }
        
        private void ConfigurarCamposSegunUnidad()
        {
            bool esPercentMO = cboUnidad.Text?.Trim().ToUpper() == "%MO";
            
            // Ocultar/mostrar campos de salario cuando es %MO
            lblSalarioBase.Visible = !esPercentMO;
            nudSalarioBase.Visible = !esPercentMO;
            lblFSR.Visible = !esPercentMO;
            nudFSR.Visible = !esPercentMO;
            lblSalarioReal.Visible = !esPercentMO;
            nudSalarioReal.Visible = !esPercentMO;
            
            if (esPercentMO)
            {
                // Para %MO, limpiar valores de salario
                nudSalarioBase.Value = 0;
                nudFSR.Value = 1;
                nudSalarioReal.Value = 0;
            }
        }
        
        private void CalcularSalarioReal()
        {
            var salarioReal = nudSalarioBase.Value * nudFSR.Value;
            if (salarioReal < nudSalarioReal.Minimum)
                salarioReal = nudSalarioReal.Minimum;
            else if (salarioReal > nudSalarioReal.Maximum)
                salarioReal = nudSalarioReal.Maximum;

            nudSalarioReal.Value = salarioReal;
        }
        
        private void txtClave_Leave(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue) return;
            
            string clave = txtClave.Text.Trim();
            if (string.IsNullOrWhiteSpace(clave)) return;
            
            var moExistente = _context.ManoDeObra
                .Where(m => m.ProyectoId == _proyectoId.Value)
                .Where(m => m.Clave.ToUpper() == clave.ToUpper())
                .Where(m => _esNuevo || m.Id != _manoDeObra.Id)
                .FirstOrDefault();
            
            if (moExistente != null)
            {
                if (_esNuevo)
                {
                    txtDescripcion.Text = moExistente.Descripcion;
                    nudSalarioBase.Value = moExistente.SalarioBase;
                    nudFSR.Value = moExistente.FactorSalarioReal;
                    txtNotas.Text = moExistente.Notas ?? "";
                    return;
                }
                
                var result = MessageBox.Show(
                    $"La clave '{clave}' ya está asignada a:\n\n" +
                    $"  → {moExistente.Descripcion}\n" +
                    $"  → Salario Real: {moExistente.SalarioReal:C2}\n\n" +
                    $"¿Desea REEMPLAZAR los datos actuales?",
                    "Clave Duplicada",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    txtDescripcion.Text = moExistente.Descripcion;
                    nudSalarioBase.Value = moExistente.SalarioBase;
                    nudFSR.Value = moExistente.FactorSalarioReal;
                    txtNotas.Text = moExistente.Notas ?? "";
                }
                else
                {
                    if (_manoDeObra != null)
                        txtClave.Text = _manoDeObra.Clave;
                    else
                        txtClave.Text = "";
                    txtClave.Focus();
                }
            }
        }
        
        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(txtClave.Text))
            {
                MessageBox.Show(
                    "La clave es requerida.",
                    "Campo Requerido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                txtClave.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show(
                    "La descripción es requerida.",
                    "Campo Requerido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                txtDescripcion.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(cboUnidad.Text))
            {
                MessageBox.Show(
                    "La unidad es requerida.",
                    "Campo Requerido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                cboUnidad.Focus();
                return;
            }
            
            if (_proyectoId.HasValue)
            {
                var errorClave = KeyValidationService.ValidateUniqueKey(
                    _context,
                    txtClave.Text.Trim(),
                    _proyectoId.Value,
                    _esNuevo ? null : _manoDeObra.Id,
                    CatalogItemType.ManoDeObra
                );

                if (errorClave != null)
                {
                    MessageBox.Show(
                        errorClave,
                        "Clave Duplicada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    txtClave.Focus();
                    return;
                }
            }
            
            try
            {
                var dto = new ManoDeObraEditDto
                {
                    Clave = txtClave.Text,
                    Descripcion = txtDescripcion.Text,
                    Unidad = cboUnidad.Text,
                    SalarioBase = nudSalarioBase.Value,
                    FactorSalarioReal = nudFSR.Value,
                    SalarioReal = nudSalarioReal.Value,
                    Notas = txtNotas.Text,
                    GuardarEnMaestro = rbMaestro.Checked,
                    ProyectoId = _proyectoId
                };

                var result = await CatalogItemService.SaveManoDeObraAsync(_context, dto, _esNuevo ? null : _manoDeObra);

                if (result.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                OpenFormsRefreshHelper.RefrescarCatalogosManoDeObraAbiertos();

                MessageBox.Show(
                    result.IsNew ? "Registro creado exitosamente." : "Registro actualizado exitosamente.",
                    "Guardado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}