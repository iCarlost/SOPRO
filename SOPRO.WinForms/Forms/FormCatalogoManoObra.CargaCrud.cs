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

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_manoDeObraSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _manoDeObraSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show("No puede eliminar registros del catálogo maestro desde un proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"¿Está seguro de eliminar el registro?\n\nClave: {_manoDeObraSeleccionada.Clave}\n" +
                $"Descripción: {_manoDeObraSeleccionada.Descripcion}\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    var compsMO = _context.ComponentesMatriz
                        .Where(c => c.ManoDeObraId == _manoDeObraSeleccionada.Id).ToList();
                    if (compsMO.Any())
                    {
                        int nm = compsMO.Select(c => c.MatrizId).Distinct().Count();
                        if (MessageBox.Show(
                            $"Este insumo está usado en {compsMO.Count} componente(s) de {nm} matriz/matrices.\n\n"
                            + "Al eliminarlo, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                            "Insumo en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                        _context.ComponentesMatriz.RemoveRange(compsMO);
                    }

                    // Primero borrar el insumo y guardar
                    var enBDMO = _context.ManoDeObra.Find(_manoDeObraSeleccionada.Id);
                    if (enBDMO != null) _context.ManoDeObra.Remove(enBDMO);
                    _context.SaveChanges();

                    // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                    if (compsMO.Any())
                    {
                        var matrizIds = compsMO.Select(c => c.MatrizId).Distinct().ToList();
                        RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                        OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                    }

                    InsumosModificados?.Invoke(null, EventArgs.Empty);
                    MessageBox.Show("Registro eliminado exitosamente.", "Eliminado",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    CargarManoDeObra();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar el registro:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarManoDeObra();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void dgvManoObra_SelectionChanged(object sender, EventArgs e)
        {
            _manoDeObraSeleccionada = dgvManoObra.SelectedRows.Count > 0
                ? dgvManoObra.SelectedRows[0].DataBoundItem as ManoDeObra
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
            var haySeleccion = _manoDeObraSeleccionada != null;
            var esMaestro = haySeleccion && _manoDeObraSeleccionada.Origen == OrigenInsumo.Maestro;
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
