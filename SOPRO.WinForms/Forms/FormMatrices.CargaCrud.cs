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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            => IntentarRecargarCatalogoTrasEscrituraExterna(
                "Hay cambios sin guardar; no se recargó el catálogo de matrices.");

        /// <summary>
        /// Recarga el catálogo después de que el editor guardó una matriz (#121: la
        /// escritura ocurre en un contexto por operación y el tracker de la sesión
        /// queda intacto). Con el tracker limpio descarta lo rastreado para que la
        /// recarga materialice los valores confirmados. Con trabajo pendiente NO
        /// recarga y deja el aviso visible: la recarga inmediata que el llamador
        /// hace al cerrar el editor no puede enmascarar el conflicto con los datos
        /// stale del identity map. Nunca guarda ni limpia el tracker ajeno.
        /// </summary>
        /// <returns>true sólo si la recarga se ejecutó.</returns>
        internal bool RecargarTrasGuardadoDeMatriz()
            => IntentarRecargarCatalogoTrasEscrituraExterna(
                MatrixCatalogRefreshHelper.HayEscriturasMatricesPendientes(_context)
                    ? "La matriz se guardó, pero hay cambios sin guardar en matrices del proyecto; confirmarlos después puede pisar los valores recién guardados. El catálogo no se recargó."
                    : "La matriz se guardó, pero hay cambios sin guardar; el catálogo no se recargó.");

        private bool IntentarRecargarCatalogoTrasEscrituraExterna(string mensajeCambiosPendientes)
        {
            if (!MatrixCatalogRefreshHelper.DescartarRastreados(_context))
            {
                lblStatus.Text = mensajeCambiosPendientes;
                return false;
            }

            SolicitarCargaMatrices();
            return true;
        }

        // Serializa las cargas sobre el contexto de ESTA instancia (no un lock
        // global): la consulta usa el DbContext de la sesión y no es seguro
        // solaparla con otra carga del mismo formulario. La generación descarta
        // resultados obsoletos; el token solo cancela la espera, no la consulta EF.
        private readonly SemaphoreSlim _cargaLock = new(1, 1);
        private CancellationTokenSource? _cargaCts;
        private int _cargaGeneracion;

        /// <summary>
        /// Seam interno de pruebas: sustituye la consulta del catálogo para
        /// controlar el orden de finalización sin abrir otra capa. En producción
        /// la carga usa <see cref="MatrixCatalogViewService"/>.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Func<MatrixCatalogFilterInput, Task<MatrixCatalogLoadResult>>? CargaMatricesParaPrueba { get; set; }

        /// <summary>
        /// Seam interno de pruebas: simula un fallo al resolver la selección del
        /// grid (primera operación riesgosa del borrado).
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Func<IReadOnlyList<Matriz>>? SeleccionMatricesParaPrueba { get; set; }

        /// <summary>
        /// Seam interno de pruebas: evita abrir un MessageBox real al verificar el
        /// mensaje recuperable del borrado.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Action<string>? MostrarMensajeEliminacionParaPrueba { get; set; }

        /// <summary>
        /// Límite seguro para los llamadores síncronos (eventos estáticos, API
        /// embebida y recalculables): la carga captura sus propias excepciones y
        /// nunca publica en un formulario cerrado.
        /// </summary>
        private void SolicitarCargaMatrices()
        {
            if (!EsUiViva()) return;
            ObservarCarga(CargarMatricesAsync());
        }

        private static void ObservarCarga(Task carga)
        {
            if (carga.IsCompleted)
            {
                _ = carga.Exception;
                return;
            }

            _ = carga.ContinueWith(
                static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private bool EsUiViva() => !IsDisposed && !Disposing;

        private bool EsCargaVigente(int generacion) => generacion == _cargaGeneracion && EsUiViva();

        private void CancelarCargaPendiente()
        {
            try
            {
                _cargaCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // La carga anterior ya terminó y liberó su token.
            }
        }

        private MatrixCatalogFilterInput CapturarFiltroCarga()
        {
            TipoMatriz? tipo = null;
            if (rbSoloAPU.Checked) tipo = TipoMatriz.APU;
            else if (rbSoloBasicos.Checked) tipo = TipoMatriz.Basico;
            else if (rbSoloCuadrillas.Checked) tipo = TipoMatriz.Cuadrilla;

            return new MatrixCatalogFilterInput
            {
                ProyectoId = _proyectoId,
                SearchText = txtBuscar.Text,
                Tipo = tipo
            };
        }

        internal async Task CargarMatricesAsync()
        {
            if (!EsUiViva()) return;

            int generacion = ++_cargaGeneracion;
            var cts = new CancellationTokenSource();
            var anterior = Interlocked.Exchange(ref _cargaCts, cts);
            try
            {
                anterior?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // La carga anterior ya terminó y liberó su token.
            }

            bool lockTomado = false;

            try
            {
                // La petición describe el estado que el usuario tenía al dispararla,
                // aunque otra carga esté en curso.
                var gridState = DataGridViewStateHelper.Capture(dgvMatrices);
                var filtro = CapturarFiltroCarga();

                // Espera la carga en curso; si una petición más nueva la supera,
                // el token libera esta espera sin tocar la consulta de la otra.
                await _cargaLock.WaitAsync(cts.Token);
                lockTomado = true;

                if (!EsCargaVigente(generacion)) return;

                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando matrices...";

                var result = CargaMatricesParaPrueba is null
                    ? await _matrixCatalogViewService.LoadAsync(_context, filtro)
                    : await CargaMatricesParaPrueba(filtro);

                // Cancelar no implica que EF haya terminado: sólo la generación
                // vigente publica, y sólo si la UI sigue viva.
                if (!EsCargaVigente(generacion)) return;

                _rowsActuales = result.Rows;
                _listaActual = _rowsActuales.Select(r => r.Source).ToList();

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMatrices))
                {
                    dgvMatrices.DataSource = null;
                    dgvMatrices.DataSource = _rowsActuales;
                    DataGridViewStateHelper.Restore(dgvMatrices, gridState);
                }
                ResumeLayout();

                lblStatus.Text = result.StatusText;
                ActualizarEstadoBotones();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Carga superada por una más reciente o cierre del formulario.
            }
            catch (Exception ex)
            {
                if (!EsCargaVigente(generacion)) return;
                MessageBox.Show($"Error al cargar matrices:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar matrices";
            }
            finally
            {
                if (lockTomado) _cargaLock.Release();
                if (ReferenceEquals(_cargaCts, cts)) _cargaCts = null;
                if (EsCargaVigente(generacion)) btnRefrescar.Enabled = true;
                cts.Dispose();
            }
        }

        private async void btnConfigReporte_Click(object sender, EventArgs e)
        {
            using var form = new FormColumnasAPU(_context, _proyectoId, FormColumnasAPU.ModoColumnas.Matrices);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMatrizHelper.ObtenerColumnas(_context, _proyectoId);
                ConfigurarGrid();
                await CargarMatricesAsync();
            }
        }

        /// <summary>
        /// Aviso de la guarda de contexto compartido: la eliminación escribe por un
        /// contexto nuevo y no debe convivir con ediciones pendientes de la UI.
        /// </summary>
        private const string MensajeCambiosSinGuardarMatrices =
            "Hay cambios sin guardar en el proyecto. Guarde o descarte esos cambios antes de eliminar matrices.";

        internal const string MensajeEliminacionRecuperable =
            "No se pudo completar la eliminación de matrices. Vuelva a intentarlo.";

        internal async Task EliminarMatricesSeleccionadasAsync()
        {
            if (!EsUiViva()) return;

            // El try cubre desde la primera sentencia: resolver la selección, leer
            // el grid, comprobar cambios pendientes y cada MessageBox son
            // operaciones riesgosas que antes quedaban fuera del límite.
            try
            {
                var seleccionadas = SeleccionMatricesParaPrueba is not null
                    ? SeleccionMatricesParaPrueba().ToList()
                    : SeleccionGridHelper.FilasSeleccionadas(dgvMatrices)
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

                // La previsualización describe el mismo lote que ejecutará el comando:
                // referencias auxiliares externas que se quitarán, recálculo y filas
                // del presupuesto que bloquean. La confirmación nunca promete un
                // borrado que luego vaya a rechazarse por una dependencia esperada.
                var previewResult = await _previewMatricesBatchDeletion.Execute(
                    _sessionInfo, new PreviewMatricesBatchDeletionRequest(ids));

                if (!EsUiViva()) return;

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

                if (!EsUiViva()) return;

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
            catch (Exception)
            {
                // Mensaje recuperable y sin detalles técnicos: el fallo de una
                // operación de borrado no debe exponer la excepción al usuario.
                MostrarErrorEliminacionRecuperable();
            }
        }

        private void MostrarErrorEliminacionRecuperable()
        {
            if (!EsUiViva()) return;

            if (MostrarMensajeEliminacionParaPrueba is not null)
            {
                MostrarMensajeEliminacionParaPrueba(MensajeEliminacionRecuperable);
                return;
            }

            MessageBox.Show(MensajeEliminacionRecuperable, "Eliminación no completada",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
