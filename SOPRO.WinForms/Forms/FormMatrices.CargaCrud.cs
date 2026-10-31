using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Matrices;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormMatrices
    {

        /// <summary>
        /// Recarga el catálogo después de una escritura externa (eliminación en lote
        /// de insumos o consolidación). La consulta del catálogo es con seguimiento,
        /// así que con el tracker limpio se descartan matrices/componentes rastreados
        /// para materializar el precio recalculado desde la base. Con trabajo
        /// pendiente no se recarga: nunca se guarda ni se limpia el tracker ajeno.
        /// </summary>
        internal void RecargarTrasCambioDeInsumos()
        {
            if (!MatrixCatalogRefreshHelper.DescartarRastreados(_context))
            {
                lblStatus.Text = "Hay cambios sin guardar; no se recargó el catálogo de matrices.";
                return;
            }

            CargarMatrices();
        }

        private async void CargarMatrices()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando matrices...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMatrices);

                TipoMatriz? tipo = null;
                if (rbSoloAPU.Checked) tipo = TipoMatriz.APU;
                else if (rbSoloBasicos.Checked) tipo = TipoMatriz.Basico;
                else if (rbSoloCuadrillas.Checked) tipo = TipoMatriz.Cuadrilla;

                var result = await _matrixCatalogViewService.LoadAsync(_context, new MatrixCatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SearchText = txtBuscar.Text,
                    Tipo = tipo
                });

                _rowsActuales = result.Rows;
                _listaActual = _rowsActuales.Select(r => r.Source).ToList();

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMatrices))
                {
                    dgvMatrices.DataSource = null;
                    dgvMatrices.DataSource = _rowsActuales;
                    DataGridViewStateHelper.Restore(dgvMatrices, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = result.StatusText;
                ActualizarEstadoBotones();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar matrices:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar matrices";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        private void btnConfigReporte_Click(object sender, EventArgs e)
        {
            using var form = new FormColumnasAPU(_context, _proyectoId, FormColumnasAPU.ModoColumnas.Matrices);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMatrizHelper.ObtenerColumnas(_context, _proyectoId);
                ConfigurarGrid();
                CargarMatrices();
            }
        }

        /// <summary>
        /// Aviso de la guarda de contexto compartido: la eliminación escribe por un
        /// contexto nuevo y no debe convivir con ediciones pendientes de la UI.
        /// </summary>
        private const string MensajeCambiosSinGuardarMatrices =
            "Hay cambios sin guardar en el proyecto. Guarde o descarte esos cambios antes de eliminar matrices.";

        private async Task EliminarMatricesSeleccionadasAsync()
        {
            var seleccionadas = SeleccionGridHelper.FilasSeleccionadas(dgvMatrices)
                .Where(r => !r.IsNewRow)
                .Select(r => (r.DataBoundItem as MatrixGridRowDisplay)?.Source)
                .Where(x => x != null)
                .DistinctBy(x => x!.Id)
                .Cast<Matriz>()
                .ToList();

            if (!seleccionadas.Any())
            {
                if (_matrizSeleccionada == null)
                {
                    MessageBox.Show("Seleccione una o más matrices para eliminar.", "Selección Requerida",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                seleccionadas.Add(_matrizSeleccionada);
            }

            // El comando escribe por un contexto nuevo: si la UI tiene ediciones
            // pendientes en el contexto compartido, se aborta sin guardar ni limpiar.
            if (CatalogoBatchDeletionHelper.HayCambiosSinGuardar(_context))
            {
                MessageBox.Show(MensajeCambiosSinGuardarMatrices,
                    "Cambios sin guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var ids = seleccionadas
                .Select(m => m.Id)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            try
            {
                // La previsualización describe el mismo lote que ejecutará el comando:
                // referencias auxiliares externas que se quitarán, recálculo y filas
                // del presupuesto que bloquean. La confirmación nunca promete un
                // borrado que luego vaya a rechazarse por una dependencia esperada.
                var previewResult = await _previewMatricesBatchDeletion.Execute(
                    _sessionInfo, new PreviewMatricesBatchDeletionRequest(ids));

                if (!previewResult.IsSuccess)
                {
                    MessageBox.Show(previewResult.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var preview = previewResult.Value!;

                if (preview.IsBlocked)
                {
                    MessageBox.Show(preview.BuildBlockingMessage(), "Eliminación bloqueada",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show(preview.BuildConfirmationMessage(), "Confirmar Eliminación",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;

                var result = await _deleteMatricesBatch.Execute(
                    _sessionInfo, new DeleteMatricesBatchRequest(ids));

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(seleccionadas.Count == 1 ? "Matriz eliminada exitosamente." : "Matrices eliminadas exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // El refresco ocurre SOLO después del commit: una selección rechazada
                // o revertida no debe recargar el catálogo.
                RecargarTrasCambioDeInsumos();
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada: no mostrar error.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar la(s) matriz(ces):\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
