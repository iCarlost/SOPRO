using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
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
    /// Acciones de botones: eliminar, copiar, importar y reportes.
    /// </summary>
    public partial class FormMatrices
    {

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_matrizSeleccionada == null)
            {
                MessageBox.Show("Seleccione una matriz para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var preview = _matrixDeleteFlowService.BuildPreview(_matrizSeleccionada);
            if (MessageBox.Show(preview.ConfirmationMessage, "Confirmar Eliminación",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                await _repository.DeleteAsync(_matrizSeleccionada);
                await _repository.SaveChangesAsync();
                MessageBox.Show("Matriz eliminada exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMatrices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar la matriz:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCopiar_Click(object sender, EventArgs e)
        {
            if (_matrizSeleccionada == null)
            {
                MessageBox.Show("Seleccione una matriz para copiar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show("Función de copiar matriz próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarMatrices();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();
        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCatalogoMatrices();

        public void GenerarPdfCatalogoMatrices()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay matrices en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string filtroTitulo = "Todos";
            if (rbSoloAPU.Checked) filtroTitulo = "APU";
            else if (rbSoloBasicos.Checked) filtroTitulo = "Básicos";
            else if (rbSoloCuadrillas.Checked) filtroTitulo = "Cuadrillas";

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar catálogo de matrices en PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"CatalogoMatrices_{filtroTitulo}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var svcRep = new ReporteService(_context);
                // Side effect intencional: persiste la plantilla por defecto (paridad del primer reporte).
                svcRep.ObtenerOCrearPlantilla(_proyectoId);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.CatalogoMatrices, lblTitulo.Text);
                var request = new BuildMatrixCatalogReportRequest(
                    _listaActual.Select(m => m.Id).ToList(),
                    filtroTitulo,
                    MatrixCatalogTitleOptionsMapper.FromLegacy(tituloCfg));

                _exportService.ExportarPdf(_sessionInfo, request, dlg.FileName);

                if (MessageBox.Show(
                    $"Catálogo PDF generado con {_listaActual.Count} matrices.\n\n¿Desea abrir el archivo?",
                    "PDF generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF del catálogo:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void dgvMatrices_SelectionChanged(object sender, EventArgs e)
        {
            _matrizSeleccionada = dgvMatrices.SelectedRows.Count > 0
                ? (dgvMatrices.SelectedRows[0].DataBoundItem as MatrixGridRowDisplay)?.Source
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMatrices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarMatrices();
        private void rbTodos_CheckedChanged(object sender, EventArgs e) { if (rbTodos.Checked) CargarMatrices(); }
        private void rbSoloAPU_CheckedChanged(object sender, EventArgs e) { if (rbSoloAPU.Checked) CargarMatrices(); }
        private void rbSoloBasicos_CheckedChanged(object sender, EventArgs e) { if (rbSoloBasicos.Checked) CargarMatrices(); }
        private void rbSoloCuadrillas_CheckedChanged(object sender, EventArgs e) { if (rbSoloCuadrillas.Checked) CargarMatrices(); }

        private void ActualizarEstadoBotones()
        {
            bool hay = _matrizSeleccionada != null;
            btnEditar.Enabled = hay;
            btnEliminar.Enabled = hay;
            btnCopiar.Enabled = hay;
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
        }
    }
}
