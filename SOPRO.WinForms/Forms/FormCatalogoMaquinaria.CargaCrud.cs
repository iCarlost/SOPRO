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

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_maquinariaSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show("No puede eliminar registros del catálogo maestro desde un proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"¿Está seguro de eliminar el registro?\n\nClave: {_maquinariaSeleccionada.Clave}\n" +
                $"Descripción: {_maquinariaSeleccionada.Descripcion}\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                var compsMaq = _context.ComponentesMatriz
                    .Where(c => c.MaquinariaId == _maquinariaSeleccionada.Id).ToList();
                if (compsMaq.Any())
                {
                    int nm = compsMaq.Select(c => c.MatrizId).Distinct().Count();
                    if (MessageBox.Show(
                        $"Este insumo está usado en {compsMaq.Count} componente(s) de {nm} matriz/matrices.\n\n"
                        + "Al eliminarlo, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                        "Insumo en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    _context.ComponentesMatriz.RemoveRange(compsMaq);
                }

                // Primero borrar el insumo y guardar
                var enBDMaq = _context.Maquinaria.Find(_maquinariaSeleccionada.Id);
                if (enBDMaq != null) _context.Maquinaria.Remove(enBDMaq);
                _context.SaveChanges();

                // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                if (compsMaq.Any())
                {
                    var matrizIds = compsMaq.Select(c => c.MatrizId).Distinct().ToList();
                    RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                }

                InsumosModificados?.Invoke(null, EventArgs.Empty);
                MessageBox.Show("Registro eliminado exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMaquinaria();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar el registro:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
            _maquinariaSeleccionada = dgvMaquinaria.SelectedRows.Count > 0
                ? dgvMaquinaria.SelectedRows[0].DataBoundItem as Maquinaria
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
            var haySeleccion = _maquinariaSeleccionada != null;
            var esMaestro = haySeleccion && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro;
            var enProyecto = _proyectoId.HasValue;

            btnEditar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            btnEliminar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
            btnCalcularCosto.Enabled = haySeleccion && !(enProyecto && esMaestro);
        }
    }
}
