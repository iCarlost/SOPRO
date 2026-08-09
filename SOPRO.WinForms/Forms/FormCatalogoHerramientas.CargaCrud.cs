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

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_herramientaSeleccionada == null)
            {
                MessageBox.Show("Seleccione una herramienta para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"¿Está seguro de eliminar la herramienta '{_herramientaSeleccionada.Clave}'?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            try
            {
                var compsH = _context.ComponentesMatriz
                    .Where(c => c.HerramientaId == _herramientaSeleccionada.Id).ToList();
                if (compsH.Any())
                {
                    int nm = compsH.Select(c => c.MatrizId).Distinct().Count();
                    if (MessageBox.Show(
                        $"Esta herramienta está usada en {compsH.Count} componente(s) de {nm} matriz/matrices.\n\n"
                        + "Al eliminarla, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                        "Herramienta en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    _context.ComponentesMatriz.RemoveRange(compsH);
                }

                // Primero borrar el insumo y guardar
                var enBDH = _context.Herramientas.Find(_herramientaSeleccionada.Id);
                if (enBDH != null) _context.Herramientas.Remove(enBDH);
                _context.SaveChanges();

                // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                if (compsH.Any())
                {
                    var matrizIds = compsH.Select(c => c.MatrizId).Distinct().ToList();
                    RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                }

                InsumosModificados?.Invoke(null, EventArgs.Empty);
                MessageBox.Show("Herramienta eliminada exitosamente.", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarHerramientas();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar herramienta:\n{ex.Message}", "Error",
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
            _herramientaSeleccionada = dgvHerramientas.SelectedRows.Count > 0
                ? dgvHerramientas.SelectedRows[0].DataBoundItem as Herramienta
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
            bool haySeleccion = _herramientaSeleccionada != null;
            btnEditar.Enabled = haySeleccion;
            btnEliminar.Enabled = haySeleccion;
        }
    }
}
