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

        /// <summary>
        /// Recarga el catálogo después de un aviso del bus propio (el guardado de
        /// la calculadora de costo horario, la consolidación u otra instancia del
        /// catálogo escribió por el MISMO contexto compartido). Solo rebindea el
        /// grid: la consulta con seguimiento devuelve las mismas instancias
        /// rastreadas ya actualizadas por <c>CatalogItemService
        /// .SaveMaquinariaAsync</c>, sin crear segundas instancias ni romper la
        /// identidad que un editor abierto conserva en su referencia.
        /// Con trabajo pendiente no se recarga: nunca se guarda ni se limpia el
        /// tracker ajeno.
        /// </summary>
        internal void RecargarTrasCambioDeInsumos()
        {
            if (_context.ChangeTracker.HasChanges())
            {
                lblStatus.Text = "Hay cambios sin guardar; no se recargó el catálogo de maquinaria.";
                return;
            }

            CargarMaquinaria();
        }

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
            // La propiedad se decide por ProyectoId, no por Origen: una copia
            // importada conserva Origen = Maestro y es editable dentro de su
            // proyecto; solo se bloquea una fila fuera del alcance del proyecto.
            if (_proyectoId.HasValue && _maquinariaSeleccionada.ProyectoId != _proyectoId)
            {
                MessageBox.Show(
                    "Este registro no pertenece al proyecto actual y no puede editarse desde aquí.",
                    "Registro Fuera de Alcance", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            // La propiedad se decide por ProyectoId, no por Origen: una copia
            // importada conserva Origen = Maestro y pertenece al proyecto.
            if (_proyectoId.HasValue && _maquinariaSeleccionada.ProyectoId != _proyectoId)
            {
                MessageBox.Show(
                    "Este registro no pertenece al proyecto actual y no puede calcularse desde aquí.",
                    "Registro Fuera de Alcance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormCalculoCostoHorario(_context, _maquinariaSeleccionada);
            form.ShowDialog();
            // Si la calculadora guardó, avisó por el bus InsumosModificados y esta
            // instancia ya se recargó por su suscripción; recargar aquí duplicaría
            // el llenado. Cancelar no muta nada y no necesita recarga.
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
                // No se recarga aquí: el aviso ya llena esta propia instancia por
                // su suscripción al bus y así se evita el llenado doble.
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

        private void btnImportar_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para importar insumos primero debe existir un proyecto activo.", "Importar",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (CatalogoBatchDeletionHelper.HayCambiosSinGuardar(_context))
            {
                MessageBox.Show(CatalogoBatchDeletionHelper.MensajeCambiosSinGuardar,
                    "Cambios sin guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var selector = new FormSeleccionarInsumo(
                    _context, _proyectoId.Value, TipoComponenteMatriz.Maquinaria, modoCatalogo: true);
                if (selector.ShowDialog(this) != DialogResult.OK) return;

                OpenFormsRefreshHelper.RefrescarCatalogoPorTipo(TipoComponenteMatriz.Maquinaria);
                CargarMaquinaria();
                MessageBox.Show($"{selector.CantidadImportada} maquinaria(s) importada(s) al catálogo.",
                    "Importar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar maquinaria:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
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

            // Elegibilidad por propiedad (ProyectoId), no por Origen: una copia
            // importada conserva Origen = Maestro y es operable dentro de su
            // proyecto; filas del maestro puro solo aparecen en sesión maestra,
            // donde operar es la operación correcta.
            btnEditar.Enabled = haySeleccion;
            // La elegibilidad para eliminar no depende de Origen: el comando resuelve
            // el alcance por ProyectoId y la previsualización rechaza lo no permitido.
            btnEliminar.Enabled = haySeleccion;
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
            btnCalcularCosto.Enabled = haySeleccion;
        }
    }
}
