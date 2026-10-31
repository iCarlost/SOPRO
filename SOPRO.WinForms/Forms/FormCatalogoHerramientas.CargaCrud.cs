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

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoHerramientas
    {

        public void RecargarCatalogo() => CargarHerramientas();

        private void CargarHerramientas()
        {
            try
            {
                var _gridState = DataGridViewStateHelper.Capture(dgvHerramientas);
                IQueryable<Herramienta> query = _context.Herramientas;

                if (_proyectoId.HasValue)
                    query = query.Where(h => h.ProyectoId == _proyectoId.Value);

                if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
                {
                    var term = txtBuscar.Text.Trim().ToLower();
                    query = query.Where(h =>
                        h.Clave.ToLower().Contains(term) ||
                        h.Descripcion.ToLower().Contains(term));
                }

                var lista = query.OrderBy(h => h.Clave).ToList();

                if (chkSoloProyecto.Checked)
                    lista = lista.Where(h => string.IsNullOrWhiteSpace(ImportOriginStampService.ExtractProjectName(h.Notas))).ToList();
                else if (chkSoloMaestros.Checked)
                    lista = lista.Where(h => !string.IsNullOrWhiteSpace(ImportOriginStampService.ExtractProjectName(h.Notas))).ToList();

                using (GridRedrawHelper.Suspend(dgvHerramientas))
                {
                    dgvHerramientas.DataSource = null;
                    dgvHerramientas.DataSource = lista;
                    DataGridViewStateHelper.Restore(dgvHerramientas, _gridState);
                }

                string colPrecioName = dgvHerramientas.Columns.Contains("col_PrecioUnitario") ? "col_PrecioUnitario" : "colPrecio";
                if (dgvHerramientas.Columns.Contains(colPrecioName))
                {
                    foreach (DataGridViewRow row in dgvHerramientas.Rows)
                        if (row.DataBoundItem is Herramienta h)
                            row.Cells[colPrecioName].Value = h.PrecioUnitario;
                }

                lblStatus.Text = $"{lista.Count} herramienta(s) encontrada(s)";
                ActualizarEstadoBotones();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar herramientas:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Debe seleccionar un proyecto primero.", "Proyecto Requerido",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dialog = new FormEditarHerramienta(_context, _proyectoId.Value);
            if (dialog.ShowDialog(this) == DialogResult.OK) CargarHerramientas();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_herramientaSeleccionada == null)
            {
                MessageBox.Show("Seleccione una herramienta para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dialog = new FormEditarHerramienta(_context, _proyectoId.Value, _herramientaSeleccionada);
            if (dialog.ShowDialog(this) == DialogResult.OK) CargarHerramientas();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            var ids = ObtenerIdsSeleccionados();

            if (ids.Count == 0)
            {
                MessageBox.Show("Seleccione al menos una herramienta para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Las herramientas pertenecen a un proyecto y el alcance lo decide la
            // previsualización por ProyectoId, no Origen: una copia importada del
            // maestro es eliminable; el lote fuera de alcance se rechaza con Conflict.

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
                    _sessionInfo, new PreviewCatalogBatchDeletionRequest(CatalogItemKind.Herramienta, ids));

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
                    new DeleteCatalogBatchRequest(CatalogItemKind.Herramienta, ids, preview.Fingerprint));

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
                MessageBox.Show("Herramienta(s) eliminada(s) exitosamente.", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarHerramientas();
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada: no mostrar error.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar herramienta:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal IReadOnlyList<Herramienta> ObtenerHerramientasSeleccionadas()
        {
            var seleccionados = new List<Herramienta>();

            foreach (var fila in SeleccionGridHelper.FilasSeleccionadas(dgvHerramientas))
            {
                if (fila is null || fila.IsNewRow) continue;
                if (fila.DataBoundItem is Herramienta herramienta)
                    seleccionados.Add(herramienta);
            }

            return seleccionados;
        }

        internal IReadOnlyList<int> ObtenerIdsSeleccionados()
            => CatalogoBatchDeletionHelper.IdsSeleccionados<Herramienta>(dgvHerramientas, h => h.Id);


        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.Herramientas);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasHerramientaHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarHerramientas();
            }
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarHerramientas();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarHerramientas();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarHerramientas();
        }

        private void dgvHerramientas_SelectionChanged(object sender, EventArgs e)
        {
            var filas = SeleccionGridHelper.FilasSeleccionadas(dgvHerramientas);
            _herramientaSeleccionada = filas.Count > 0
                ? filas[0].DataBoundItem as Herramienta
                : null;
            ActualizarEstadoBotones();
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
        }

        private void dgvHerramientas_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void ActualizarEstadoBotones()
        {
            var seleccionados = ObtenerHerramientasSeleccionadas();
            var haySeleccion = seleccionados.Count > 0;
            var enProyecto = _proyectoId.HasValue;
            var todosMaestros = haySeleccion && seleccionados.All(h => h.Origen == OrigenInsumo.Maestro);

            btnEditar.Enabled = haySeleccion && !(enProyecto && todosMaestros);
            // La elegibilidad para eliminar no depende de Origen: el comando resuelve
            // el alcance por ProyectoId y la previsualización rechaza lo no permitido.
            btnEliminar.Enabled = haySeleccion;
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
        }
    }
}
