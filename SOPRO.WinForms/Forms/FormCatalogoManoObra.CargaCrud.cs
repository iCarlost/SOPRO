using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        public void RecargarCatalogo() => CargarManoDeObra();

        private async void CargarManoDeObra()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando mano de obra...";
                var _gridState = DataGridViewStateHelper.Capture(dgvManoObra);

                var lista = await _catalogLoadService.LoadManoDeObraAsync(_context, new CatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SoloProyecto = chkSoloProyecto.Checked,
                    SoloMaestros = chkSoloMaestros.Checked,
                    SearchText = txtBuscar.Text
                });

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvManoObra))
                {
                    dgvManoObra.DataSource = null;
                    dgvManoObra.DataSource = lista;
                    DataGridViewStateHelper.Restore(dgvManoObra, _gridState);
                }
                ResumeLayout();
                lblStatus.Text = $"{lista.Count} registro(s) encontrado(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar mano de obra:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar mano de obra";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarManoObra(_context, _proyectoId, proyecto: ProyectoActual);
            if (form.ShowDialog() == DialogResult.OK) CargarManoDeObra();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_manoDeObraSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _manoDeObraSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede editar registros del catálogo maestro desde un proyecto.\n\n" +
                    "Para modificar este registro, ábralo desde Catálogos Maestros o cree una copia para este proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormEditarManoObra(_context, _proyectoId, _manoDeObraSeleccionada, ProyectoActual);
            if (form.ShowDialog() == DialogResult.OK) CargarManoDeObra();
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
                    _sessionInfo, new PreviewCatalogBatchDeletionRequest(CatalogItemKind.ManoDeObra, ids));

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
                    new DeleteCatalogBatchRequest(CatalogItemKind.ManoDeObra, ids, preview.Fingerprint));

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
                CargarManoDeObra();
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

        internal IReadOnlyList<ManoDeObra> ObtenerManoDeObraSeleccionadas()
        {
            var seleccionados = new List<ManoDeObra>();

            foreach (var fila in SeleccionGridHelper.FilasSeleccionadas(dgvManoObra))
            {
                if (fila is null || fila.IsNewRow) continue;
                if (fila.DataBoundItem is ManoDeObra manoDeObra)
                    seleccionados.Add(manoDeObra);
            }

            return seleccionados;
        }

        internal IReadOnlyList<int> ObtenerIdsSeleccionados()
            => CatalogoBatchDeletionHelper.IdsSeleccionados<ManoDeObra>(dgvManoObra, m => m.Id);


        private void btnRefrescar_Click(object sender, EventArgs e) => CargarManoDeObra();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void dgvManoObra_SelectionChanged(object sender, EventArgs e)
        {
            var filas = SeleccionGridHelper.FilasSeleccionadas(dgvManoObra);
            _manoDeObraSeleccionada = filas.Count > 0
                ? filas[0].DataBoundItem as ManoDeObra
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvManoObra_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarManoDeObra();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarManoDeObra();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarManoDeObra();
        }

        private void ActualizarEstadoBotones()
        {
            var seleccionados = ObtenerManoDeObraSeleccionadas();
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
