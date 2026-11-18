using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;
using System.Threading;
using System.Threading.Tasks;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        public void RecargarCatalogo() => CargarMaterialesObservada();

        // Serializa las cargas de ESTE formulario (no de todos): dos catálogos
        // abiertos pueden cargar en paralelo y cada uno descarta sus resultados
        // obsoletos con su propio token.
        private readonly SemaphoreSlim _cargaLock = new(1, 1);
        private CancellationTokenSource? _cargaCts;
        private bool _cerrando;

        /// <summary>
        /// Cancela la carga en curso al cerrar/liberar el formulario. No libera el
        /// CTS ni el semáforo: la carga que los usa los libera en su propio finally,
        /// después de que la operación pendiente termine.
        /// </summary>
        private void CancelarCargaPendiente()
        {
            _cerrando = true;
            var cts = _cargaCts;
            _cargaCts = null;

            try
            {
                cts?.Cancel();
            }
            catch (Exception ex)
            {
                // Cancelar al cierre es best-effort: un callback que falle no debe
                // impedir el cierre del formulario.
                System.Diagnostics.Debug.WriteLine($"CancelarCargaPendiente: {ex.Message}");
            }
        }

        private bool VidaUtil => !_cerrando && !IsDisposed && !Disposing;

        /// <summary>
        /// Boundary observada para callers void legados que no pueden hacer
        /// <c>await</c> (RecalcularTodo/IRecalculable y RecargarCatalogo invocado
        /// desde otros formularios): arranca el núcleo y observa cualquier fallo
        /// residual sin <c>_ =</c> explícito ni un async void auxiliar. El núcleo
        /// ya captura todo desde la primera operación riesgosa.
        /// </summary>
        private void CargarMaterialesObservada()
        {
            CargarMaterialesAsync().ContinueWith(
                fallo => System.Diagnostics.Debug.WriteLine($"Carga de materiales falló: {fallo.Exception}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        internal async Task CargarMaterialesAsync()
        {
            if (!VidaUtil) return;

            CancellationTokenSource? cts = null;
            bool lockAdquirido = false;
            try
            {
                var anterior = _cargaCts;
                cts = new CancellationTokenSource();
                _cargaCts = cts;
                anterior?.Cancel();

                try
                {
                    await _cargaLock.WaitAsync(cts.Token);
                    lockAdquirido = true;
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!VidaUtil || cts.IsCancellationRequested) return;

                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando materiales...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMateriales);

                var result = await _listMaterials.Execute(_sessionInfo, new ListMaterialsRequest
                {
                    SearchText = txtBuscar.Text,
                    OnlyProjectItems = chkSoloProyecto.Checked,
                    OnlyMasterItems = chkSoloMaestros.Checked,
                }, cts.Token);

                if (!VidaUtil || cts.IsCancellationRequested) return;

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Error.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error al cargar materiales";
                    return;
                }

                var materiales = result.Value;

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMateriales))
                {
                    dgvMateriales.DataSource = null;
                    dgvMateriales.DataSource = materiales;
                    DataGridViewStateHelper.Restore(dgvMateriales, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = $"{materiales.Count} material(es) encontrado(s)";
            }
            catch (OperationCanceledException) when (cts?.IsCancellationRequested == true)
            {
                // Carga superada por una más reciente o cierre del formulario: no tocar la UI.
            }
            catch (Exception ex)
            {
                if (!VidaUtil) return;

                MessageBox.Show($"Error al cargar materiales:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar materiales";
            }
            finally
            {
                if (lockAdquirido && cts is not null)
                {
                    _cargaLock.Release();

                    // Solo la carga vigente rehabilita el botón: una carga cancelada
                    // no debe pisar el estado que dejó la que la reemplazó.
                    if (VidaUtil && ReferenceEquals(_cargaCts, cts))
                        btnRefrescar.Enabled = true;
                }

                if (cts is not null && ReferenceEquals(_cargaCts, cts))
                    _cargaCts = null;

                cts?.Dispose();
            }
        }

        private async void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMaterial(_sessionInfo, factory: _factory);
            if (form.ShowDialog() == DialogResult.OK)
                await CargarMaterialesAsync();
        }

        private async void btnEditar_Click(object sender, EventArgs e)
        {
            // La propiedad se decide por alcance, no por Origen: ListMaterials solo
            // devuelve filas del proyecto de la sesión (ProyectoId = proyecto), así
            // que cualquier fila visible aquí -incluida una copia importada que
            // conserva Origen = Maestro- es de este proyecto y es editable.
            if (_materialSeleccionado == null)
            {
                MessageBox.Show("Seleccione un material para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormEditarMaterial(_sessionInfo, _materialSeleccionado, _factory);
            if (form.ShowDialog() == DialogResult.OK)
                await CargarMaterialesAsync();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            var ids = ObtenerIdsSeleccionados();

            if (ids.Count == 0)
            {
                MessageBox.Show("Seleccione al menos un material para eliminar.", "Selección Requerida",
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
                // La previsualización solo lee y describe el mismo lote que ejecutará
                // el comando; la UI únicamente decide la confirmación.
                var previewResult = await _previewCatalogBatchDeletion.Execute(
                    _sessionInfo, new PreviewCatalogBatchDeletionRequest(CatalogItemKind.Material, ids));

                if (!previewResult.IsSuccess)
                {
                    MessageBox.Show(previewResult.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var preview = previewResult.Value!;

                if (CatalogoBatchDeletionHelper.BloqueadaPorMaquinaria(preview))
                {
                    MessageBox.Show(
                        CatalogoBatchDeletionHelper.ConstruirAvisoMaquinariaVinculada(preview.LinkedMachinery),
                        "Insumo vinculado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

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
                    new DeleteCatalogBatchRequest(CatalogItemKind.Material, ids, preview.Fingerprint));

                if (!deleteResult.IsSuccess)
                {
                    MessageBox.Show(deleteResult.Error!.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // El refresco ocurre SOLO después del commit: una selección rechazada
                // o revertida no debe recargar ni notificar a otros formularios.
                if (deleteResult.Value!.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                InsumosModificados?.Invoke(null, EventArgs.Empty);

                MessageBox.Show("Material(es) eliminado(s) exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                await CargarMaterialesAsync();
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada: no mostrar error.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar los materiales:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal IReadOnlyList<MaterialListItem> ObtenerMaterialesSeleccionados()
        {
            var seleccionados = new List<MaterialListItem>();

            foreach (var fila in SeleccionGridHelper.FilasSeleccionadas(dgvMateriales))
            {
                if (fila is null || fila.IsNewRow) continue;
                if (fila.DataBoundItem is MaterialListItem material)
                    seleccionados.Add(material);
            }

            return seleccionados;
        }

        internal IReadOnlyList<int> ObtenerIdsSeleccionados()
            => CatalogoBatchDeletionHelper.IdsSeleccionados<MaterialListItem>(dgvMateriales, m => m.Id);


        private async void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.Materiales);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                await CargarMaterialesAsync();
            }
        }

        private async void btnRefrescar_Click(object sender, EventArgs e) => await CargarMaterialesAsync();

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void dgvMateriales_SelectionChanged(object sender, EventArgs e)
        {
            var filas = SeleccionGridHelper.FilasSeleccionadas(dgvMateriales);
            _materialSeleccionado = filas.Count > 0
                ? filas[0].DataBoundItem as MaterialListItem
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMateriales_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private async void txtBuscar_TextChanged(object sender, EventArgs e) => await CargarMaterialesAsync();

        private async void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            await CargarMaterialesAsync();
        }

        private async void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            await CargarMaterialesAsync();
        }

        private void ActualizarEstadoBotones()
        {
            var seleccionados = ObtenerMaterialesSeleccionados();
            var haySeleccion = seleccionados.Count > 0;
            // La propiedad se decide por alcance/ProyectoId, no por Origen: una
            // copia importada conserva Origen = Maestro y es editable dentro de
            // su proyecto; filas del maestro puro solo aparecen en sesión maestra,
            // donde editar es la operación correcta.
            btnEditar.Enabled = haySeleccion;
            // La elegibilidad para eliminar no depende de Origen: el comando resuelve
            // el alcance por ProyectoId y la previsualización rechaza lo no permitido.
            btnEliminar.Enabled = haySeleccion;
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
        }

        private async void btnImportar_Click(object sender, EventArgs e)
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
                    _context, _proyectoId.Value, TipoComponenteMatriz.Material, modoCatalogo: true);
                if (selector.ShowDialog(this) != DialogResult.OK) return;

                OpenFormsRefreshHelper.RefrescarCatalogoPorTipo(TipoComponenteMatriz.Material);
                await CargarMaterialesAsync();
                MessageBox.Show($"{selector.CantidadImportada} material(es) importado(s) al catálogo.",
                    "Importar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar materiales:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCatalogoExcel();

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
