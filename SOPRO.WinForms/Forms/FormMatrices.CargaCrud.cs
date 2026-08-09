using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
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

        private async Task EliminarMatricesSeleccionadasAsync()
        {
            var seleccionadas = dgvMatrices.SelectedRows.Cast<DataGridViewRow>()
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

            string mensaje = seleccionadas.Count == 1
                ? _matrixDeleteFlowService.BuildPreview(seleccionadas[0]).ConfirmationMessage
                : $"¿Desea eliminar las {seleccionadas.Count} matrices seleccionadas?\n\nEsta acción no se puede deshacer.";

            if (MessageBox.Show(mensaje, "Confirmar Eliminación",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                foreach (var matriz in seleccionadas)
                    await _repository.DeleteAsync(matriz);

                await _repository.SaveChangesAsync();
                MessageBox.Show(seleccionadas.Count == 1 ? "Matriz eliminada exitosamente." : "Matrices eliminadas exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMatrices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar la(s) matriz(ces):\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
