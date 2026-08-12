using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        public void RecargarCatalogo() => CargarMateriales();

        private async void CargarMateriales()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando materiales...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMateriales);

                var result = await _listMaterials.Execute(_sessionInfo, new ListMaterialsRequest
                {
                    SearchText = txtBuscar.Text,
                    OnlyProjectItems = chkSoloProyecto.Checked,
                    OnlyMasterItems = chkSoloMaestros.Checked,
                });

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Error.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error al cargar materiales";
                    return;
                }

                var materiales = result.Value;

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMateriales))
                {
                    dgvMateriales.DataSource = null;
                    dgvMateriales.DataSource = materiales;
                    DataGridViewStateHelper.Restore(dgvMateriales, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = $"{materiales.Count} material(es) encontrado(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar materiales:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar materiales";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMaterial(_context, _proyectoId);
            if (form.ShowDialog() == DialogResult.OK)
                CargarMateriales();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_materialSeleccionado == null)
            {
                MessageBox.Show("Seleccione un material para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_proyectoId.HasValue && _materialSeleccionado.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede editar materiales del catálogo maestro desde un proyecto.\n\n" +
                    "Para modificar este material, ábralo desde Catálogos Maestros o cree una copia para este proyecto.",
                    "Material del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormEditarMaterial(_context, _proyectoId, _materialSeleccionado);
            if (form.ShowDialog() == DialogResult.OK)
                CargarMateriales();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_materialSeleccionado == null)
            {
                MessageBox.Show("Seleccione un material para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_proyectoId.HasValue && _materialSeleccionado.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show("No puede eliminar materiales del catálogo maestro desde un proyecto.",
                    "Material del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el material?\n\n" +
                $"Clave: {_materialSeleccionado.Clave}\n" +
                $"Descripción: {_materialSeleccionado.Descripcion}\n\n" +
                "Esta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            try
            {
                // Previsualización del impacto: la UI solo decide la confirmación.
                var previewResult = await _previewMaterialDeletion.Execute(
                    _sessionInfo, new PreviewMaterialDeletionRequest(_materialSeleccionado.Id));

                if (!previewResult.IsSuccess)
                {
                    MessageBox.Show(previewResult.Error.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var preview = previewResult.Value;
                if (preview.ComponentCount > 0)
                {
                    var confirmar = MessageBox.Show(
                        $"Este insumo está usado en {preview.ComponentCount} componente(s) de {preview.AffectedMatrixCount} matriz/matrices.\n\n" +
                        "Al eliminarlo, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                        "Insumo en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirmar != DialogResult.Yes) return;
                }

                var deleteResult = await _deleteMaterial.Execute(
                    _sessionInfo, new DeleteMaterialRequest(_materialSeleccionado.Id));

                if (!deleteResult.IsSuccess)
                {
                    MessageBox.Show(deleteResult.Error.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (deleteResult.Value.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                InsumosModificados?.Invoke(null, EventArgs.Empty);

                MessageBox.Show("Material eliminado exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMateriales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar el material:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.Materiales);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarMateriales();
            }
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarMateriales();

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void dgvMateriales_SelectionChanged(object sender, EventArgs e)
        {
            _materialSeleccionado = dgvMateriales.SelectedRows.Count > 0
                ? dgvMateriales.SelectedRows[0].DataBoundItem as MaterialListItem
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMateriales_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarMateriales();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarMateriales();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarMateriales();
        }

        private void ActualizarEstadoBotones()
        {
            var haySeleccion = _materialSeleccionado != null;
            var esMaestro = haySeleccion && _materialSeleccionado.Origen == OrigenInsumo.Maestro;
            var enProyecto = _proyectoId.HasValue;

            btnEditar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            btnEliminar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
        }

        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCatalogoExcel();

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
