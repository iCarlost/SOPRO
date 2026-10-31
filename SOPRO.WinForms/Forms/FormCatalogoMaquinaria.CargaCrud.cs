using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        public void RecargarCatalogo() => CargarMaquinaria();

        private async void CargarMaquinaria()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando maquinaria...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMaquinaria);

                _listaActual = await _catalogLoadService.LoadMaquinariaAsync(_context, new CatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SoloProyecto = chkSoloProyecto.Checked,
                    SoloMaestros = chkSoloMaestros.Checked,
                    SearchText = txtBuscar.Text
                });

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMaquinaria))
                {
                    dgvMaquinaria.DataSource = null;
                    dgvMaquinaria.DataSource = _listaActual;
                    DataGridViewStateHelper.Restore(dgvMaquinaria, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = $"{_listaActual.Count} registro(s) encontrado(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar maquinaria:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar maquinaria";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMaquinaria(_context, _proyectoId);
            if (form.ShowDialog() == DialogResult.OK) CargarMaquinaria();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_maquinariaSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede editar registros del catálogo maestro desde un proyecto.\n\n" +
                    "Para modificar este registro, ábralo desde Catálogos Maestros o cree una copia.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormEditarMaquinaria(_context, _proyectoId, _maquinariaSeleccionada);
            if (form.ShowDialog() == DialogResult.OK) CargarMaquinaria();
        }

        private void btnCalcularCosto_Click(object sender, EventArgs e)
        {
            if (_maquinariaSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para calcular su costo horario.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede calcular costos de registros del catálogo maestro desde un proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormCalculoCostoHorario(_context, _maquinariaSeleccionada);
            if (form.ShowDialog() == DialogResult.OK) CargarMaquinaria();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            var ids = ObtenerIdsSeleccionados();

            if (ids.Count == 0)
            {
                MessageBox.Show("Seleccione al menos un registro para eliminar.", "Selección Requerida",
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
                var previewResult = await _previewCatalogBatchDeletion.Execute(
                    _sessionInfo, new PreviewCatalogBatchDeletionRequest(CatalogItemKind.Maquinaria, ids));

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
                    new DeleteCatalogBatchRequest(CatalogItemKind.Maquinaria, ids, preview.Fingerprint));

                if (!deleteResult.IsSuccess)
                {
                    MessageBox.Show(deleteResult.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Refresco SOLO después del commit.
                if (deleteResult.Value!.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                InsumosModificados?.Invoke(null, EventArgs.Empty);
                MessageBox.Show("Registro(s) eliminado(s) exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMaquinaria();
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada: no mostrar error.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar los registros:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal IReadOnlyList<Maquinaria> ObtenerMaquinariaSeleccionada()
        {
            var seleccionados = new List<Maquinaria>();

            foreach (var fila in SeleccionGridHelper.FilasSeleccionadas(dgvMaquinaria))
            {
                if (fila is null || fila.IsNewRow) continue;
                if (fila.DataBoundItem is Maquinaria maquinaria)
                    seleccionados.Add(maquinaria);
            }

            return seleccionados;
        }

        internal IReadOnlyList<int> ObtenerIdsSeleccionados()
            => CatalogoBatchDeletionHelper.IdsSeleccionados<Maquinaria>(dgvMaquinaria, m => m.Id);


        private void btnRefrescar_Click(object sender, EventArgs e) => CargarMaquinaria();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();
        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCostoHorario();
        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void dgvMaquinaria_SelectionChanged(object sender, EventArgs e)
        {
            var filas = SeleccionGridHelper.FilasSeleccionadas(dgvMaquinaria);
            _maquinariaSeleccionada = filas.Count > 0
                ? filas[0].DataBoundItem as Maquinaria
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMaquinaria_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarMaquinaria();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarMaquinaria();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarMaquinaria();
        }

        private void ActualizarEstadoBotones()
        {
            var seleccionados = ObtenerMaquinariaSeleccionada();
            var haySeleccion = seleccionados.Count > 0;
            var enProyecto = _proyectoId.HasValue;
            var todosMaestros = haySeleccion && seleccionados.All(m => m.Origen == OrigenInsumo.Maestro);

            btnEditar.Enabled = haySeleccion && !(enProyecto && todosMaestros);
            // La elegibilidad para eliminar no depende de Origen: el comando resuelve
            // el alcance por ProyectoId y la previsualización rechaza lo no permitido.
            btnEliminar.Enabled = haySeleccion;
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
            btnCalcularCosto.Enabled = haySeleccion && !(enProyecto && todosMaestros);
        }
    }
}
