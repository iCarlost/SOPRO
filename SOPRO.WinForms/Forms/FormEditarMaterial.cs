using SOPRO.WinForms.Helpers;
using System;
using System.Windows.Forms;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarMaterial : Form
    {
        private readonly ProjectSessionInfo _sessionInfo;
        private readonly int? _proyectoId;
        private readonly MaterialListItem _material;
        private readonly bool _esNuevo;

        /// <summary>Id del material persistido por el último guardado exitoso (para el selector de insumos).</summary>
        public int? UltimoMaterialIdGuardado { get; private set; }

        /// <summary>Si el último guardado fue en el catálogo maestro (su Id no existe en el proyecto).</summary>
        public bool UltimoGuardadoEnMaestro { get; private set; }

        public FormEditarMaterial(ProjectSessionInfo sessionInfo, MaterialListItem material = null)
        {
            InitializeComponent();

            _sessionInfo = sessionInfo ?? throw new ArgumentNullException(nameof(sessionInfo));
            _proyectoId = sessionInfo.Project.ProjectId;
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

        private async void txtClave_Leave(object sender, EventArgs e)
        {
            // Auto-completar desde material existente con la misma clave
            // (versión headless del flujo legacy; la consulta vive en el caso de uso).
            if (!_proyectoId.HasValue) return;

            string clave = txtClave.Text.Trim();
            if (string.IsNullOrWhiteSpace(clave)) return;

            var result = await new FindMaterialByKey().Execute(
                _sessionInfo, new FindMaterialByKeyRequest(clave));

            if (!result.IsSuccess || result.Value == null) return;

            var materialExistente = result.Value;
            if (!_esNuevo && materialExistente.Id == _material.Id) return;

            // Si es NUEVO → auto-completar silenciosamente
            if (_esNuevo)
            {
                txtDescripcion.Text = materialExistente.Descripcion;
                txtUnidad.Text = materialExistente.Unidad;
                nudPrecio.Value = materialExistente.PrecioUnitario;
                txtNotas.Text = materialExistente.Notas;
                return;
            }

            // Si es EDICIÓN → confirmar reemplazo
            var resultMensaje = MessageBox.Show(
                $"La clave '{clave}' ya está asignada a:\n\n" +
                $"  → {materialExistente.Descripcion}\n" +
                $"  → Precio: {materialExistente.PrecioUnitario:C2}\n\n" +
                $"¿Desea REEMPLAZAR los datos actuales con los de este material?",
                "Clave Duplicada - Confirmar Reemplazo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (resultMensaje == DialogResult.Yes)
            {
                txtDescripcion.Text = materialExistente.Descripcion;
                txtUnidad.Text = materialExistente.Unidad;
                nudPrecio.Value = materialExistente.PrecioUnitario;
                txtNotas.Text = materialExistente.Notas;
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
                    MessageBox.Show(result.Error!.Message,
                        result.Error!.Code == AppErrorCode.Conflict ? "Clave Duplicada" : "Error al guardar el material",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                UltimoMaterialIdGuardado = result.Value!.MaterialId;
                UltimoGuardadoEnMaestro = rbMaestro.Checked;

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
            catch (OperationCanceledException)
            {
                // Carga cancelada o guardado cancelado: no mostrar error.
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