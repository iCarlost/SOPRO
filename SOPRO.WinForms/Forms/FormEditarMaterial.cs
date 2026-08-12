using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarMaterial : Form
    {
        private readonly SOPROContext _context;
        private readonly ProjectSessionInfo _sessionInfo;
        private readonly int? _proyectoId;
        private readonly MaterialListItem _material;
        private readonly bool _esNuevo;
        
        public FormEditarMaterial(SOPROContext context, int? proyectoId, MaterialListItem material = null)
        {
            InitializeComponent();
            
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sessionInfo = ProjectSessionInfo.FromLegacy(_context, proyectoId);
            _proyectoId = proyectoId;
            _material = material;
            _esNuevo = material == null;
            
            ConfigurarFormulario();
            
            if (_esNuevo)
                txtClave.Text = KeySuggestionService.Generate();
            else
                CargarDatos();
        }
        
        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nuevo Material" : "Editar Material";
            
            // Si estamos en proyecto, ofrecer las dos opciones
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
                // Si no hay proyecto, solo catálogo maestro
                rbMaestro.Checked = true;
                rbProyecto.Enabled = false;
            }
        }
        
        private void CargarDatos()
        {
            txtClave.Text = _material.Clave;
            txtDescripcion.Text = _material.Descripcion;
            txtUnidad.Text = _material.Unidad;
            nudPrecio.Value = _material.PrecioUnitario;
            txtNotas.Text = _material.Notas;
            
            if (_material.Origen == OrigenInsumo.Maestro)
                rbMaestro.Checked = true;
            else
                rbProyecto.Checked = true;
        }
        
        private void txtClave_Leave(object sender, EventArgs e)
        {
            // Auto-completar desde material existente con la misma clave
            if (!_proyectoId.HasValue) return;
            
            string clave = txtClave.Text.Trim();
            if (string.IsNullOrWhiteSpace(clave)) return;
            
            // Buscar material con esta clave
            var materialExistente = _context.Materiales
                .Where(m => m.ProyectoId == _proyectoId.Value)
                .Where(m => m.Clave.ToUpper() == clave.ToUpper())
                .Where(m => _esNuevo || m.Id != _material.Id) // Excluir el mismo si se edita
                .FirstOrDefault();
            
            if (materialExistente != null)
            {
                // Si es NUEVO → auto-completar silenciosamente
                if (_esNuevo)
                {
                    txtDescripcion.Text = materialExistente.Descripcion;
                    txtUnidad.Text = materialExistente.Unidad;
                    nudPrecio.Value = materialExistente.PrecioUnitario;
                    txtNotas.Text = materialExistente.Notas ?? "";
                    return;
                }
                
                // Si es EDICIÓN → confirmar reemplazo
                var result = MessageBox.Show(
                    $"La clave '{clave}' ya está asignada a:\n\n" +
                    $"  → {materialExistente.Descripcion}\n" +
                    $"  → Precio: {materialExistente.PrecioUnitario:C2}\n\n" +
                    $"¿Desea REEMPLAZAR los datos actuales con los de este material?",
                    "Clave Duplicada - Confirmar Reemplazo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    txtDescripcion.Text = materialExistente.Descripcion;
                    txtUnidad.Text = materialExistente.Unidad;
                    nudPrecio.Value = materialExistente.PrecioUnitario;
                    txtNotas.Text = materialExistente.Notas ?? "";
                }
                else
                {
                    // Restaurar clave anterior
                    if (_material != null)
                        txtClave.Text = _material.Clave;
                    else
                        txtClave.Text = "";
                    txtClave.Focus();
                }
            }
        }
        
        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                // Validaciones y persistencia viven en el caso de uso SaveMaterial
                // (probables sin formularios, PLAN-01 §12).
                var result = await new SaveMaterial().Execute(_sessionInfo, new SaveMaterialRequest(
                    MaterialId: _esNuevo ? null : _material.Id,
                    Clave: txtClave.Text,
                    Descripcion: txtDescripcion.Text,
                    Unidad: txtUnidad.Text,
                    PrecioUnitario: nudPrecio.Value,
                    Notas: txtNotas.Text,
                    SaveToMaster: rbMaestro.Checked,
                    ProjectId: _proyectoId));

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Error.Message,
                        result.Error.Code == AppErrorCode.Conflict ? "Clave Duplicada" : "Error al guardar el material",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (result.Value.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                OpenFormsRefreshHelper.RefrescarCatalogosMaterialesAbiertos();

                MessageBox.Show(
                    result.Value.IsNew ? "Material creado exitosamente." : "Material actualizado exitosamente.",
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
                    $"Error al guardar el material:\n{ex.Message}",
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