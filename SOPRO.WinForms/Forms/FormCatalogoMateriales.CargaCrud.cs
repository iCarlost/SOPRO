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
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;
using System.Threading;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        public void RecargarCatalogo() => CargarMateriales();

        // Serializa las cargas sobre el contexto de sesión y descarta resultados
        // de cargas anteriores (el texto puede cambiar mientras una carga corre).
        private static readonly SemaphoreSlim _cargaLock = new(1, 1);
        private CancellationTokenSource? _cargaCts;

        private async void CargarMateriales()
        {
            _cargaCts?.Cancel();
            var cts = new CancellationTokenSource();
            _cargaCts = cts;

            try
            {
                await _cargaLock.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

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
                }, cts.Token);

                if (cts.IsCancellationRequested) return;

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
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Carga superada por una más reciente: no tocar la UI.
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
                _cargaLock.Release();
            }
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMaterial(_sessionInfo, factory: _factory);
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

            using var form = new FormEditarMaterial(_sessionInfo, _materialSeleccionado, _factory);
            if (form.ShowDialog() == DialogResult.OK)
                CargarMateriales();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            var ids = ObtenerIdsSeleccionados();

            if (ids.Count == 0)
            {
                MessageBox.Show("Seleccione al menos un material para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // El alcance lo decide la previsualización por el ProyectoId real de cada
            // fila, no Origen: una copia importada del maestro vive en el proyecto y
            // es eliminable; un lote maestro puro o mixto se rechaza con Conflict.

            // El comando escribe por un contexto nuevo: si la UI tiene ediciones
            // pendientes en el contexto compartido, se aborta sin guardar ni limpiar.
            if (CatalogoBatchDeletionHelper.HayCambiosSinGuardar(_context))
            {
                MessageBox.Show(CatalogoBatchDeletionHelper.MensajeCambiosSinGuardar,
                    "Cambios sin guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // La previsualización solo lee y describe el mismo lote que ejecutará
                // el comando; la UI únicamente decide la confirmación.
                var previewResult = await _previewCatalogBatchDeletion.Execute(
                    _sessionInfo, new PreviewCatalogBatchDeletionRequest(CatalogItemKind.Material, ids));

                if (!previewResult.IsSuccess)
                {
                    MessageBox.Show(previewResult.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var preview = previewResult.Value!;

                var confirmar = MessageBox.Show(
                    CatalogoBatchDeletionHelper.ConstruirConfirmacion(preview),
                    "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (confirmar != DialogResult.Yes) return;

                if (CatalogoBatchDeletionHelper.HayCambiosSinGuardar(_context))
                {
                    MessageBox.Show(CatalogoBatchDeletionHelper.MensajeCambiosSinGuardar,
                        "Cambios sin guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var deleteResult = await _deleteCatalogBatch.Execute(
                    _sessionInfo,
                    new DeleteCatalogBatchRequest(CatalogItemKind.Material, ids, preview.Fingerprint));

                if (!deleteResult.IsSuccess)
                {
                    MessageBox.Show(deleteResult.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // El refresco ocurre SOLO después del commit: una selección rechazada
                // o revertida no debe recargar ni notificar a otros formularios.
                if (deleteResult.Value!.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                InsumosModificados?.Invoke(null, EventArgs.Empty);

                MessageBox.Show("Material(es) eliminado(s) exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMateriales();
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada: no mostrar error.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar los materiales:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal IReadOnlyList<MaterialListItem> ObtenerMaterialesSeleccionados()
        {
            var seleccionados = new List<MaterialListItem>();

            foreach (var fila in SeleccionGridHelper.FilasSeleccionadas(dgvMateriales))
            {
                if (fila is null || fila.IsNewRow) continue;
                if (fila.DataBoundItem is MaterialListItem material)
                    seleccionados.Add(material);
            }

            return seleccionados;
        }

        internal IReadOnlyList<int> ObtenerIdsSeleccionados()
            => CatalogoBatchDeletionHelper.IdsSeleccionados<MaterialListItem>(dgvMateriales, m => m.Id);


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
            var filas = SeleccionGridHelper.FilasSeleccionadas(dgvMateriales);
            _materialSeleccionado = filas.Count > 0
                ? filas[0].DataBoundItem as MaterialListItem
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
            var seleccionados = ObtenerMaterialesSeleccionados();
            var haySeleccion = seleccionados.Count > 0;
            var enProyecto = _proyectoId.HasValue;
            var todosMaestros = haySeleccion && seleccionados.All(m => m.Origen == OrigenInsumo.Maestro);

            btnEditar.Enabled = haySeleccion && !(enProyecto && todosMaestros);
            // La elegibilidad para eliminar no depende de Origen: el comando resuelve
            // el alcance por ProyectoId y la previsualización rechaza lo no permitido.
            btnEliminar.Enabled = haySeleccion;
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
