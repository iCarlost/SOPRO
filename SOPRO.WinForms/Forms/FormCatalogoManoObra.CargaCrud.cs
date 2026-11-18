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
using System.Threading;
using System.Threading.Tasks;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del catálogo y operaciones CRUD con eventos del formulario.
    /// </summary>
    public partial class FormCatalogoManoObra
    {
        // Carga del catálogo: una generación por solicitud (la última manda), la
        // petición anterior se cancela y la exclusión LOCAL a esta instancia
        // serializa las consultas completas sobre el _context de sesión. La
        // cancelación no garantiza que EF haya terminado de leer, así que una
        // carga nueva no inicia su consulta hasta que la anterior libera turno.
        // No pretende coordinar a otros consumidores del mismo contexto.
        private readonly SemaphoreSlim _cargaLock = new(1, 1);
        private CancellationTokenSource? _cargaCts;
        private int _cargaGeneracion;
        private volatile bool _cargaCerrada;

        /// <summary>
        /// Seam interno de prueba: sustituye la consulta por una carga diferida
        /// controlada. En producción queda en null y se usa el servicio real.
        /// </summary>
        internal Func<CatalogFilterInput, Task<List<ManoDeObra>>>? CargadorManoDeObraOverride { get; set; }

        /// <summary>
        /// Seam interno de prueba para observar el aviso de error sin abrir un
        /// modal real. El comportamiento productivo no cambia.
        /// </summary>
        internal Action<string, string> NotificarErrorCarga { get; set; } = (mensaje, titulo) =>
            MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public void RecargarCatalogo() => SolicitarCargaManoDeObra();

        /// <summary>
        /// Frontera de UI: arranca la carga y OBSERVA su tarea. Un formulario ya
        /// cerrado no inicia nada, y cualquier fallo (incluido uno completado de
        /// forma síncrona) queda absorbido y registrado como observado; nunca se
        /// descarta una tarea sin observación.
        /// </summary>
        private void SolicitarCargaManoDeObra()
        {
            if (EstaCerrada)
                return;

            ObservarCarga(CargarManoDeObraAsync());
        }

        internal static void ObservarCarga(Task carga)
        {
            if (carga.IsCompleted)
            {
                // Rama síncrona: leer Exception marca la falla como observada.
                _ = carga.Exception;
                return;
            }

            _ = carga.ContinueWith(
                static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Carga el catálogo con generación por instancia: descarta resultados
        /// obsoletos, no publica en un formulario cerrado y nunca inicia una
        /// segunda consulta sobre el mismo contexto antes de liberar el turno.
        /// Devuelve <c>true</c> solo cuando el resultado se publicó. Las fallas
        /// esperadas se informan aquí; una falla inesperada la observa la
        /// frontera <see cref="SolicitarCargaManoDeObra"/>.
        /// </summary>
        internal async Task<bool> CargarManoDeObraAsync()
        {
            // Un formulario ya cerrado no captura controles ni arranca consultas.
            if (EstaCerrada)
                return false;

            var generacion = Interlocked.Increment(ref _cargaGeneracion);
            var cts = new CancellationTokenSource();
            bool turno = false;
            try
            {
                CancelarSinFalla(Interlocked.Exchange(ref _cargaCts, cts));

                // Los filtros se capturan antes de ceder el hilo: la UI puede
                // cambiar mientras esta carga espera turno o espera la consulta.
                CatalogFilterInput filtro;
                try
                {
                    filtro = new CatalogFilterInput
                    {
                        ProyectoId = _proyectoId,
                        SoloProyecto = chkSoloProyecto.Checked,
                        SoloMaestros = chkSoloMaestros.Checked,
                        SearchText = txtBuscar.Text
                    };
                }
                catch (ObjectDisposedException)
                {
                    // El formulario se descartó entre la guarda y la captura.
                    return false;
                }
                try
                {
                    await _cargaLock.WaitAsync(cts.Token).ConfigureAwait(true);
                    turno = true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                if (EsCargaObsoleta(generacion, cts))
                    return false;

                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando mano de obra...";
                var estadoGrid = DataGridViewStateHelper.Capture(dgvManoObra);

                List<ManoDeObra> lista;
                try
                {
                    var cargador = CargadorManoDeObraOverride;
                    lista = cargador is null
                        ? await _catalogLoadService.LoadManoDeObraAsync(_context, filtro).ConfigureAwait(true)
                        : await cargador(filtro).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    // Ni un resultado obsoleto ni un formulario cerrado muestran
                    // modal: la carga ya no le pertenece a la UI.
                    if (EsCargaObsoleta(generacion, cts))
                        return false;

                    NotificarErrorCarga($"Error al cargar mano de obra:\n{ex.Message}", "Error");
                    lblStatus.Text = "Error al cargar mano de obra";
                    return false;
                }

                if (EsCargaObsoleta(generacion, cts))
                    return false;

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvManoObra))
                {
                    dgvManoObra.DataSource = null;
                    dgvManoObra.DataSource = lista;
                    DataGridViewStateHelper.Restore(dgvManoObra, estadoGrid);
                }
                ResumeLayout();
                lblStatus.Text = $"{lista.Count} registro(s) encontrado(s)";
                return true;
            }
            finally
            {
                if (turno)
                {
                    // Solo la carga vigente devuelve el botón a la UI; una carga
                    // superada deja el estado al cargo de su sucesora y un form
                    // cerrado no se toca.
                    if (!EsCargaObsoleta(generacion, cts))
                        btnRefrescar.Enabled = true;

                    _cargaLock.Release();
                }

                Interlocked.CompareExchange(ref _cargaCts, null, cts);
                cts.Dispose();
            }
        }

        private bool EstaCerrada => _cargaCerrada || IsDisposed || Disposing;

        private bool EsCargaObsoleta(int generacion, CancellationTokenSource cts)
            => EstaCerrada
               || cts.IsCancellationRequested
               || generacion != Volatile.Read(ref _cargaGeneracion);

        private static void CancelarSinFalla(CancellationTokenSource? cts)
        {
            if (cts is null)
                return;

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // La carga dueña ya liberó su CTS.
            }
            catch (AggregateException)
            {
                // Un callback de cancelación falló; la carga ya está invalidada.
            }
        }

        /// <summary>
        /// Invalida las cargas pendientes al cerrar/descartar el formulario. El
        /// CTS queda a cargo de su carga (que lo libera al terminar).
        /// </summary>
        private void CerrarCargaCatalogo()
        {
            _cargaCerrada = true;
            Interlocked.Increment(ref _cargaGeneracion);
            CancelarSinFalla(Interlocked.Exchange(ref _cargaCts, null));
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = CrearEditorManoObra(manoDeObra: null);
            if (form.ShowDialog() == DialogResult.OK) SolicitarCargaManoDeObra();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_manoDeObraSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // La propiedad se decide por ProyectoId, no por Origen: una copia
            // importada conserva Origen = Maestro y es editable dentro de su
            // proyecto; solo se bloquea una fila fuera del alcance del proyecto.
            if (_proyectoId.HasValue && _manoDeObraSeleccionada.ProyectoId != _proyectoId)
            {
                MessageBox.Show(
                    "Este registro no pertenece al proyecto actual y no puede editarse desde aquí.",
                    "Registro Fuera de Alcance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = CrearEditorManoObra(_manoDeObraSeleccionada);
            if (form.ShowDialog() == DialogResult.OK) SolicitarCargaManoDeObra();
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
                SolicitarCargaManoDeObra();
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


        private void btnRefrescar_Click(object sender, EventArgs e) => SolicitarCargaManoDeObra();
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
                    _context, _proyectoId.Value, TipoComponenteMatriz.ManoDeObra, modoCatalogo: true,
                    diagnosticsSink: _diagnosticsSink, productVersion: _productVersion);
                if (selector.ShowDialog(this) != DialogResult.OK) return;

                OpenFormsRefreshHelper.RefrescarCatalogoPorTipo(TipoComponenteMatriz.ManoDeObra);
                SolicitarCargaManoDeObra();
                MessageBox.Show($"{selector.CantidadImportada} registro(s) de mano de obra importado(s) al catálogo.",
                    "Importar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar mano de obra:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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

        private void txtBuscar_TextChanged(object sender, EventArgs e) => SolicitarCargaManoDeObra();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            SolicitarCargaManoDeObra();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            SolicitarCargaManoDeObra();
        }

        private void ActualizarEstadoBotones()
        {
            var seleccionados = ObtenerManoDeObraSeleccionadas();
            var haySeleccion = seleccionados.Count > 0;

            // Elegibilidad de edición por propiedad (ProyectoId), no por Origen:
            // una copia importada conserva Origen = Maestro y es editable dentro
            // de su proyecto; filas del maestro puro solo aparecen en sesión
            // maestra, donde editar es la operación correcta.
            btnEditar.Enabled = haySeleccion;
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
