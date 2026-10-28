using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Reajuste de costos de UNA matriz desde un concepto hoja (ADR-0001): la base
    /// esperada (proyecto, matriz, fingerprint de componentes, fuentes, rowset y
    /// hoja) se captura del ESTADO CARGADO POR LA VISTA antes del diálogo y viaja
    /// como comando inmutable a
    /// <see cref="BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand"/>
    /// sobre un contexto NUEVO. La ruta confirmada aplica la instantánea sin un
    /// segundo <c>SaveChanges</c> compartido ni <see cref="RefreshBudgetPrices"/>.
    /// El reajuste de agrupadores permanece por la ruta heredada, explícitamente
    /// diferida.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Tras un <see cref="BudgetOperationStatus.OutcomeUnknown"/> no se permite
        /// otro reajuste hasta que la vista se recargue de forma segura: reintentar
        /// con una base refrescada aplicaría el ajuste dos veces.
        /// </summary>
        private bool _reajusteCostoBloqueadoPorResultadoIncierto;

        /// <summary>Clasificación del último reajuste ejecutado como comando, o null si no llegó a enviarse.</summary>
        internal BudgetOperationStatus? UltimoEstadoReajusteCosto { get; private set; }

        /// <summary>Detalle técnico del último reajuste no confirmado; nunca se muestra.</summary>
        internal string? UltimoDiagnosticoReajusteCosto { get; private set; }

        internal bool ReajusteCostoBloqueadoPorResultadoIncierto => _reajusteCostoBloqueadoPorResultadoIncierto;

        private void InicializarBotonReajustarCosto()
        {
            _btnReajustarCosto = new ToolStripButton
            {
                BackColor = SystemColors.Control,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "btnReajustarCosto",
                Size = new Size(111, 19),
                Text = "🎯 Reajustar costo"
            };
            _btnReajustarCosto.Click += btnReajustarCosto_Click;

            var indexImportar = panelToolbar.Items.IndexOf(btnImportarExcel);
            if (indexImportar >= 0)
                panelToolbar.Items.Insert(indexImportar + 1, _btnReajustarCosto);
            else
                panelToolbar.Items.Add(_btnReajustarCosto);

            // El formulario ya recarga la vista desde la base al activarse. Este
            // handler se programa con BeginInvoke para ejecutarse DESPUÉS de esa
            // recarga y sólo entonces liberar el bloqueo por resultado incierto.
            this.Activated += (_, __) => ProgramarDesbloqueoReajusteCostoTrasRecargaSegura();
        }

        private void btnReajustarCosto_Click(object? sender, EventArgs e)
        {
            try
            {
                if (dgvPresupuesto.CurrentRow == null)
                {
                    MessageBox.Show("Seleccione un concepto del presupuesto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var rowIndex = dgvPresupuesto.CurrentRow.Index;
                var concepto = dgvPresupuesto.CurrentRow.Tag as ConceptoPresupuesto;
                var tipo = ObtenerCeldaTexto(rowIndex, "Tipo", "Concepto");
                var currentInternalName = (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag as ColumnaPersonalizada)?.NombreInterno ?? string.Empty;
                bool ajustarPorImporte = string.Equals(currentInternalName, "Importe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentInternalName, "ImporteTotal", StringComparison.OrdinalIgnoreCase);

                if (concepto == null)
                {
                    MessageBox.Show("Seleccione un concepto o agrupador del presupuesto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Ruta heredada del agrupador: diferida de forma explícita. No se
                // convierte en un bucle de comandos ni se cambia su semántica.
                if (concepto.EsAgrupador || !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ajustarPorImporte)
                    {
                        MessageBox.Show("Para agrupadores, el reajuste solo está disponible seleccionando la celda Importe.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    AjustarAgrupadorSeleccionado(rowIndex, concepto);
                    return;
                }

                if (!concepto.MatrizId.HasValue)
                {
                    MessageBox.Show("Por ahora el reajuste solo está disponible para conceptos con matriz asignada.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!PuedeReajustarCosto(out string? motivo))
                {
                    MostrarMensajeReajusteCosto(motivo, MessageBoxIcon.Warning);
                    return;
                }

                // Captura del estado cargado por la vista ANTES del diálogo: el
                // objetivo capturado se interpreta contra la base que el usuario
                // tenía a la vista, no contra una lectura fresca de SQLite.
                var baseCapturada = CapturarBaseReajusteCosto(concepto.Id);
                if (baseCapturada == null)
                {
                    MessageBox.Show("No se pudo cargar la base actual de la matriz asignada al concepto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                decimal actualMostrado = CalcularMontoMostradoReajusteCosto(baseCapturada, ajustarPorImporte);
                string contexto = ajustarPorImporte
                    ? $"Concepto: {concepto.Descripcion} \n"+
"Se reajustará la matriz por rendimiento/cantidad para acercar el IMPORTE del concepto al monto objetivo. La cantidad del concepto en presupuesto no se modificará."
                    : $"Concepto: {concepto.Descripcion} \n"+
"Se reajustará la matriz por rendimiento/cantidad para acercar el P.U. del concepto al monto objetivo. La cantidad del concepto en presupuesto no se modificará.";

                using var frm = new FormReajustarCosto("Reajustar costo", actualMostrado, contexto);
                if (frm.ShowDialog(this) != DialogResult.OK)
                    return;

                var targetKind = ajustarPorImporte
                    ? BudgetMatrixCostAdjustmentTargetKind.Importe
                    : BudgetMatrixCostAdjustmentTargetKind.PrecioUnitario;

                var resultado = EjecutarReajusteCostoCapturado(
                    baseCapturada, targetKind, frm.TargetAmount, frm.SelectedScopes, out string? mensaje);

                if (resultado is null)
                {
                    MostrarMensajeReajusteCosto(mensaje, MessageBoxIcon.Warning);
                    return;
                }

                if (resultado.IsCommitted && resultado.Confirmed is not null)
                {
                    var snapshot = resultado.Confirmed;
                    MessageBox.Show(
                        $"Monto actual: {snapshot.CurrentTargetValue:C2} \n" +
                        $"Objetivo: {snapshot.RequestedTargetValue:C2} \n" +
                        $"Resultado: {snapshot.AchievedTargetValue:C2} \n" +
                        $"Factor aplicado sobre rendimientos/cantidades de la matriz: {snapshot.FactorApplied:N4}" +
                        (mensaje is null ? string.Empty : "\n" + mensaje),
                        "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                MostrarMensajeReajusteCosto(mensaje, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reajustar costo:{ex.Message}", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void MostrarMensajeReajusteCosto(string? mensaje, MessageBoxIcon icono)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
                return;

            MessageBox.Show(mensaje, "Reajustar costo", MessageBoxButtons.OK, icono);
        }

        /// <summary>
        /// Base inmutable de la vista para el reajuste, capturada antes del diálogo
        /// desde el estado que el formulario/panel ya tienen cargado. No contiene
        /// costo directo objetivo ni factor: Application los deriva de estas bases y
        /// del monto capturado por el usuario.
        /// </summary>
        private sealed class BaseReajusteCosto
        {
            public BaseReajusteCosto(
                Proyecto proyecto,
                Matriz matriz,
                BudgetMatrixCostAdjustmentClosureBase closure,
                BudgetPriceRefreshRowset rowset,
                BudgetMatrixCostAdjustmentLeaf leaf)
            {
                Proyecto = proyecto;
                Matriz = matriz;
                Closure = closure;
                Rowset = rowset;
                Leaf = leaf;
            }

            public Proyecto Proyecto { get; }
            public Matriz Matriz { get; }
            public BudgetMatrixCostAdjustmentClosureBase Closure { get; }
            public BudgetPriceRefreshRowset Rowset { get; }
            public BudgetMatrixCostAdjustmentLeaf Leaf { get; }
        }

        /// <summary>
        /// Captura la base que la VISTA tiene cargada, sin releerla al pulsar: el
        /// proyecto rastreado por el formulario, el cierre ascendente de la matriz
        /// (capturado por Application con el contexto cargado), la entrada del origen
        /// sustituida por la matriz visible en el panel y el rowset COMPLETO formado
        /// con las filas/Tags actuales del grid. Así un cambio externo ocurrido desde
        /// la carga no queda absorbido en silencio: el comando lo detecta como
        /// conflicto. Devuelve null si la vista no puede formar un rowset/cierre
        /// coherente; el llamador aborta sin diálogo.
        /// </summary>
        private BaseReajusteCosto? CapturarBaseReajusteCosto(int conceptoId)
        {
            if (conceptoId <= 0 || _proyecto is null)
                return null;

            var filas = ObtenerFilasVistaReajusteCosto();
            if (filas is null)
                return null;

            var hoja = filas.FirstOrDefault(c => c.Id == conceptoId);
            if (hoja == null || hoja.EsAgrupador || !hoja.MatrizId.HasValue)
                return null;

            var matriz = ObtenerMatrizVistaReajusteCosto(hoja.MatrizId.Value);
            if (matriz == null || matriz.ProyectoId != _proyecto.Id)
                return null;

            BudgetMatrixCostAdjustmentClosureBase closure;
            try
            {
                closure = BudgetPersistenceService.CaptureMatrixCostAdjustmentClosure(
                    _context, _proyecto.Id, matriz.Id);

                // El origen manda sobre lo leído: la entrada esperada de la matriz
                // seleccionada es la que la vista/panel tiene cargada.
                closure = closure.WithEntry(
                    BudgetMatrixCostAdjustmentClosureEntry.FromMatriz(matriz));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Captura del cierre de reajuste: {ex.Message}");
                return null;
            }

            return new BaseReajusteCosto(
                _proyecto,
                matriz,
                closure,
                BudgetPriceRefreshRowset.FromConceptos(filas),
                BudgetMatrixCostAdjustmentLeaf.FromConcepto(hoja));
        }

        /// <summary>
        /// Filas que la vista muestra: los Tags persistidos del grid, sin releer la
        /// base. Las filas vacías de captura no representan datos y se omiten; una
        /// fila con contenido pero sin entidad persistida, fuera del proyecto,
        /// repetida o con Id inválido invalida la captura (no se puede formar el
        /// rowset completo que el comando revalidará).
        /// </summary>
        private List<ConceptoPresupuesto>? ObtenerFilasVistaReajusteCosto()
        {
            var filas = new List<ConceptoPresupuesto>();
            var ids = new HashSet<int>();

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                if (row.Tag is not ConceptoPresupuesto concepto)
                {
                    string tipo = ObtenerCeldaTexto(i, "Tipo");
                    string descripcion = ObtenerCeldaTexto(i, "Descripcion");
                    string clave = ObtenerCeldaTexto(i, "Clave");

                    if (string.IsNullOrWhiteSpace(tipo)
                        && string.IsNullOrWhiteSpace(descripcion)
                        && string.IsNullOrWhiteSpace(clave))
                    {
                        continue;
                    }

                    return null;
                }

                if (concepto.Id <= 0 || concepto.ProyectoId != _proyecto.Id || !ids.Add(concepto.Id))
                    return null;

                filas.Add(concepto);
            }

            return filas.Count > 0 ? filas : null;
        }

        /// <summary>
        /// Matriz que la vista cargó para el concepto: la visible en el panel si
        /// coincide con la matriz del concepto o, en su defecto, la instancia
        /// rastreada por el contexto compartido. Nunca se lee fresca de la base.
        /// </summary>
        private Matriz? ObtenerMatrizVistaReajusteCosto(int matrizId)
        {
            var visible = _panelMatricesEmbebido?.MatrizVisibleEnPanel;
            if (visible != null
                && visible.Id == matrizId
                && visible.ProyectoId == _proyecto.Id
                && visible.Componentes.Count > 0)
            {
                return visible;
            }

            var rastreada = _context.ChangeTracker.Entries<Matriz>()
                .Select(e => e.Entity)
                .FirstOrDefault(m => m.Id == matrizId && m.ProyectoId == _proyecto.Id);

            return rastreada?.Componentes.Count > 0 ? rastreada : null;
        }

        private static decimal CalcularMontoMostradoReajusteCosto(BaseReajusteCosto baseCapturada, bool ajustarPorImporte)
        {
            decimal precioUnitario = BudgetPricingService.CalculateUnitPrice(
                baseCapturada.Proyecto, baseCapturada.Matriz.CostoDirecto);

            return ajustarPorImporte
                ? BudgetPricingService.MultiplyUsingDisplayPrecision(
                    baseCapturada.Proyecto, baseCapturada.Leaf.Cantidad, precioUnitario)
                : precioUnitario;
        }

        /// <summary>
        /// Seam interno para las pruebas del ensamblado: captura el estado cargado
        /// por la vista ANTES de cualquier diálogo, permite simular un cambio externo
        /// con <paramref name="entreCapturaYComando"/> y ejecuta la intención con el
        /// monto y los rubros indicados. No muestra mensajes de UI.
        /// </summary>
        internal BudgetMatrixCostAdjustmentResult? EjecutarReajusteCostoDesdeFila(
            int rowIndex,
            BudgetMatrixCostAdjustmentTargetKind targetKind,
            decimal targetValue,
            MatrixAdjustmentScopes scopes,
            Action? entreCapturaYComando = null)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return null;

            if (dgvPresupuesto.Rows[rowIndex].Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                return null;

            if (!PuedeReajustarCosto(out _))
                return null;

            var baseCapturada = CapturarBaseReajusteCosto(concepto.Id);
            if (baseCapturada == null)
                return null;

            entreCapturaYComando?.Invoke();

            return EjecutarReajusteCostoCapturado(
                baseCapturada, targetKind, targetValue, scopes, out _);
        }

        /// <summary>
        /// Ejecuta el comando único sobre un contexto nuevo con la base capturada y,
        /// si se confirma, aplica su instantánea. No reintenta automáticamente:
        /// conflicto y fallo dejan la vista intacta; un resultado incierto bloquea el
        /// siguiente reajuste hasta una recarga segura.
        /// </summary>
        private BudgetMatrixCostAdjustmentResult? EjecutarReajusteCostoCapturado(
            BaseReajusteCosto baseCapturada,
            BudgetMatrixCostAdjustmentTargetKind targetKind,
            decimal targetValue,
            MatrixAdjustmentScopes scopes,
            out string? mensaje)
        {
            mensaje = null;

            if (!PuedeReajustarCosto(out mensaje))
                return null;

            UltimoEstadoReajusteCosto = null;
            UltimoDiagnosticoReajusteCosto = null;

            BudgetMatrixCostAdjustmentResult resultado;
            try
            {
                var comando = new BudgetMatrixCostAdjustmentCommand(
                    _proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(baseCapturada.Proyecto),
                    baseCapturada.Closure,
                    baseCapturada.Rowset,
                    baseCapturada.Leaf,
                    scopes,
                    targetKind,
                    targetValue);

                resultado = BudgetPersistenceService.ExecuteMatrixCostAdjustmentCommand(
                    _factory, _context.DatabasePath, comando);
            }
            catch (Exception ex)
            {
                // Un fallo al preparar la intención no confirma ninguna escritura.
                UltimoDiagnosticoReajusteCosto = ex.ToString();
                mensaje = $"Error al reajustar costo:{ex.Message}";
                return null;
            }

            UltimoEstadoReajusteCosto = resultado.Status;
            UltimoDiagnosticoReajusteCosto = resultado.Diagnostic;

            if (resultado.IsCommitted && resultado.Confirmed is not null)
            {
                if (!AplicarReajusteCostoConfirmado(resultado.Confirmed))
                {
                    mensaje =
                        "El reajuste se guardó, pero la vista conservó cambios pendientes y no se " +
                        "sincronizó. Recargue el presupuesto antes de continuar.";
                }

                return resultado;
            }

            if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
            {
                _reajusteCostoBloqueadoPorResultadoIncierto = true;
                mensaje =
                    "El resultado del reajuste es incierto: no se puede afirmar si la base quedó " +
                    "modificada. Recargue el presupuesto antes de volver a reajustar costos.";
                return resultado;
            }

            mensaje = resultado.Message
                ?? "No se pudo reajustar la matriz; no se guardó nada.";
            return resultado;
        }

        /// <summary>
        /// Vuelca la instantánea confirmada sobre el proyecto, las filas (hojas y
        /// agrupadores) y las matrices/componentes rastreados por el contexto
        /// compartido, dejando todo <c>Unchanged</c> para que ningún guardado heredado
        /// posterior lo reescriba. Si el tracker tiene trabajo ajeno pendiente no se
        /// pisa nada y se informa al llamador.
        /// </summary>
        private bool AplicarReajusteCostoConfirmado(BudgetMatrixCostAdjustmentSnapshot snapshot)
        {
            if (snapshot.ProjectId != _proyecto.Id)
                return false;

            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return false;

            _actualizandoDesdeComando = true;
            try
            {
                SincronizarProyectoCompartido(snapshot.Project);
                AplicarFilasConfirmadas(snapshot.Leaves.Concat(snapshot.Aggregators).ToArray());
                SincronizarMatricesYComponentesConfirmados(
                    snapshot.AffectedMatrices, snapshot.AffectedComponents);
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }

            ActualizarTitulo();

            // No reemplazar la matriz visible si el panel tiene un borrador de
            // cabecera: la sincronización de filas ya ocurrió y el usuario conserva
            // su trabajo. La recarga se hará cuando lo resuelva.
            if (_panelMatricesEmbebido?.TieneCambiosPendientesCabeceraEnPanel != true)
                _panelMatricesEmbebido?.RecargarMatrizVisibleDesdeBd();

            dgvPresupuesto.Refresh();
            return true;
        }

        private void SincronizarMatricesYComponentesConfirmados(
            IReadOnlyList<BudgetConfirmedMatrixRow> matrices,
            IReadOnlyList<BudgetConfirmedMatrixComponentRow> componentes)
        {
            var matricesPorId = matrices.ToDictionary(m => m.MatrixId);
            foreach (var entry in _context.ChangeTracker.Entries<Matriz>().ToList())
            {
                if (!matricesPorId.TryGetValue(entry.Entity.Id, out var confirmada))
                    continue;

                entry.Entity.CostoDirecto = confirmada.CostoDirecto;
                entry.Entity.FechaModificacion = confirmada.FechaModificacion;
                entry.OriginalValues.SetValues(entry.CurrentValues);
                entry.State = EntityState.Unchanged;
            }

            var componentesPorId = componentes.ToDictionary(c => c.ComponentId);
            foreach (var entry in _context.ChangeTracker.Entries<ComponenteMatriz>().ToList())
            {
                if (!componentesPorId.TryGetValue(entry.Entity.Id, out var confirmado))
                    continue;

                entry.Entity.TipoComponente = confirmado.TipoComponente;
                entry.Entity.MaterialId = confirmado.MaterialId;
                entry.Entity.ManoDeObraId = confirmado.ManoDeObraId;
                entry.Entity.MaquinariaId = confirmado.MaquinariaId;
                entry.Entity.AuxiliarId = confirmado.AuxiliarId;
                entry.Entity.HerramientaId = confirmado.HerramientaId;
                entry.Entity.Cantidad = confirmado.Cantidad;
                entry.Entity.Rendimiento = confirmado.Rendimiento;
                entry.Entity.Importe = confirmado.Importe;
                entry.Entity.Orden = confirmado.Orden;
                entry.OriginalValues.SetValues(entry.CurrentValues);
                entry.State = EntityState.Unchanged;
            }
        }

        /// <summary>
        /// Guards previos al reajuste: sin edición activa, sin trabajo pendiente en el
        /// contexto compartido y sin un resultado incierto por resolver. No descarta
        /// nada del usuario: sólo rechaza el envío y explica el motivo.
        /// </summary>
        private bool PuedeReajustarCosto(out string? motivo)
        {
            motivo = null;

            if (_cargando || IsDisposed)
                return false;

            if (_reajusteCostoBloqueadoPorResultadoIncierto)
            {
                motivo =
                    "El reajuste anterior terminó con un estado incierto. Recargue el " +
                    "presupuesto (o vuelva a abrirlo) antes de reajustar costos de nuevo.";
                return false;
            }

            if (dgvPresupuesto.IsCurrentCellInEditMode)
            {
                motivo = "Finalice la edición de la celda activa antes de reajustar costos.";
                return false;
            }

            if (_panelMatricesEmbebido?.TieneCambiosPendientesCabeceraEnPanel == true)
            {
                motivo =
                    "Hay cambios pendientes en la cabecera de la matriz del panel. " +
                    "Guárdelos o descártelos antes de reajustar costos.";
                return false;
            }

            // El panel no reemplaza su matriz visible mientras una celda de
            // componentes está en edición: la instantánea posterior no podría
            // sincronizarse y el usuario perdería (o vería desfasado) su borrador.
            if (_panelMatricesEmbebido?.TieneCeldaComponenteEnEdicionEnPanel == true)
            {
                motivo =
                    "Finalice la edición de la celda de componentes de la matriz antes de " +
                    "reajustar costos.";
                return false;
            }

            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
            {
                motivo =
                    "Hay cambios pendientes sin guardar en el presupuesto. Guárdelos o " +
                    "descártelos antes de reajustar costos.";
                return false;
            }

            return true;
        }

        private void ProgramarDesbloqueoReajusteCostoTrasRecargaSegura()
        {
            if (!_reajusteCostoBloqueadoPorResultadoIncierto || IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() => NotificarRecargaSeguraReajusteCosto()));
            }
            catch (InvalidOperationException)
            {
                // El formulario se está cerrando: el bloqueo se resuelve al reabrir.
            }
        }

        /// <summary>
        /// Reconciliación explícita tras un resultado incierto. Sólo libera el
        /// bloqueo cuando <see cref="RecargarPreciosDesdeBase"/> terminó la recarga
        /// de sólo lectura y aplicó su instantánea. Un error, trabajo pendiente, una
        /// edición activa (en el presupuesto o en el panel), un borrador de cabecera
        /// o un proyecto no encontrado devuelven <c>false</c> y conservan el bloqueo.
        /// Que el tracker esté limpio no basta por sí solo.
        /// Expuesto a las pruebas del ensamblado; devuelve <c>true</c> sólo si el
        /// bloqueo quedó liberado.
        /// </summary>
        internal bool NotificarRecargaSeguraReajusteCosto()
        {
            if (!_reajusteCostoBloqueadoPorResultadoIncierto || IsDisposed)
                return false;

            if (_cargando || dgvPresupuesto.IsCurrentCellInEditMode)
                return false;

            if (_panelMatricesEmbebido?.TieneCambiosPendientesCabeceraEnPanel == true)
                return false;

            if (_panelMatricesEmbebido?.TieneCeldaComponenteEnEdicionEnPanel == true)
                return false;

            if (!IntentarRecargarPreciosDesdeBase())
                return false;

            if (IsDisposed || dgvPresupuesto.IsCurrentCellInEditMode)
                return false;

            if (_panelMatricesEmbebido?.TieneCambiosPendientesCabeceraEnPanel == true)
                return false;

            if (_panelMatricesEmbebido?.TieneCeldaComponenteEnEdicionEnPanel == true)
                return false;

            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return false;

            _reajusteCostoBloqueadoPorResultadoIncierto = false;
            return true;
        }

        private void AjustarAgrupadorSeleccionado(int rowIndex, ConceptoPresupuesto agrupador)
        {
            decimal currentImporte = ObtenerCeldaDecimal(rowIndex, "Importe");
            string contexto = $"Agrupador: {agrupador.Descripcion} \n"+
"Se reajustarán recursivamente los conceptos descendientes por rendimiento/cantidad dentro de sus matrices. La cantidad de los conceptos en presupuesto no se modificará.";
            using var frm = new FormReajustarCosto("Reajustar costo de agrupador", currentImporte, contexto);
            if (frm.ShowDialog(this) != DialogResult.OK)
                return;

            decimal factorPu = CalcularFactorPU();
            if (factorPu <= 0m)
            {
                MessageBox.Show("No se pudo calcular el factor del precio unitario del proyecto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var descendientes = ObtenerConceptosDescendientesHoja(rowIndex, agrupador);
            if (descendientes.Count == 0)
            {
                MessageBox.Show("El agrupador seleccionado no contiene conceptos hoja con matriz asignada.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var matrixIds = descendientes.Where(c => c.MatrizId.HasValue).Select(c => c.MatrizId!.Value).Distinct().ToList();
            var matrices = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrixIds.Contains(m.Id))
                .ToDictionary(m => m.Id);

            var candidatos = new List<(ConceptoPresupuesto Concepto, MatrixAdjustmentBasis Basis)>();
            int omitidos = 0;
            foreach (var conceptoHijo in descendientes)
            {
                if (!conceptoHijo.MatrizId.HasValue || !matrices.TryGetValue(conceptoHijo.MatrizId.Value, out var matriz))
                {
                    omitidos++;
                    continue;
                }

                var basis = MatrixCostAdjustmentService.GetAdjustmentBasis(matriz, _proyecto, frm.SelectedScopes);
                if (basis == null || !basis.HasAdjustableScope)
                {
                    omitidos++;
                    continue;
                }

                candidatos.Add((conceptoHijo, basis));
            }

            if (candidatos.Count == 0)
            {
                MessageBox.Show("No se encontraron conceptos ajustables con los rubros seleccionados dentro del agrupador.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal targetCdTotal = frm.TargetAmount / factorPu;
            decimal fixedTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.FixedCost);
            decimal adjustableTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.AdjustableCost);
            decimal minTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.MinCost);
            decimal currentTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.CurrentCost);

            if (adjustableTotal <= 0m)
            {
                MessageBox.Show("Los rubros seleccionados no tienen importe ajustable dentro del agrupador.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (targetCdTotal < minTotal - 0.01m)
            {
                MessageBox.Show($"No es posible bajar el agrupador al monto solicitado con los rubros seleccionados. El mínimo alcanzable es {(minTotal * factorPu):C2}.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal factor = (targetCdTotal - fixedTotal) / adjustableTotal;
            if (factor < 0m)
            {
                MessageBox.Show("Con los rubros seleccionados no es posible alcanzar el monto solicitado.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var matricesAjustables = candidatos
                .Select(x => x.Concepto.MatrizId!.Value)
                .Distinct()
                .Select(id => matrices[id])
                .ToList();

            foreach (var matriz in matricesAjustables)
            {
                var result = MatrixCostAdjustmentService.AdjustMatrixByFactor(matriz, _proyecto, frm.SelectedScopes, factor);
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _context.SaveChanges();
            RefreshBudgetPrices();

            if (UltimoEstadoRefrescoPrecios != BudgetOperationStatus.Committed)
            {
                MessageBox.Show(
                    "El ajuste de las matrices se guardó, pero los precios del presupuesto no pudieron " +
                    "refrescarse. Recargue el presupuesto antes de continuar.",
                    "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal logrado = ObtenerCeldaDecimal(rowIndex, "Importe");
            var conceptosIds = descendientes.Select(x => x.Id).ToHashSet();
            int compartidasFuera = _context.ConceptosPresupuesto.Count(c => c.ProyectoId == _proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue && matrixIds.Contains(c.MatrizId.Value) && !conceptosIds.Contains(c.Id));
            string notaCompartidas = compartidasFuera > 0
                ? $"Nota: {compartidasFuera} concepto(s) fuera del agrupador comparten alguna matriz reajustada."
                : string.Empty;

            MessageBox.Show(
                $"Monto actual: {currentImporte:C2}\n" +
                $"Objetivo: {frm.TargetAmount:C2}\n" +
                $"Resultado: {logrado:C2}\n" +
                $"Conceptos hoja encontrados: {descendientes.Count}\n" +
                $"Conceptos ajustados: {candidatos.Count}\n" +
                $"Conceptos omitidos: {omitidos}\n" +
                $"Factor global aplicado sobre rendimientos/cantidades: {factor:N4}" +
                notaCompartidas,
                "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private List<ConceptoPresupuesto> ObtenerConceptosDescendientesHoja(int rowIndex, ConceptoPresupuesto agrupador)
        {
            var result = new List<ConceptoPresupuesto>();
            int nivelPadre = agrupador.Nivel;
            for (int i = rowIndex + 1; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                var concepto = row.Tag as ConceptoPresupuesto;
                string tipo = ObtenerCeldaTexto(i, "Tipo");
                string descripcion = ObtenerCeldaTexto(i, "Descripcion");
                string clave = ObtenerCeldaTexto(i, "Clave");
                bool hasContent = concepto != null || !string.IsNullOrWhiteSpace(tipo) || !string.IsNullOrWhiteSpace(descripcion) || !string.IsNullOrWhiteSpace(clave);
                if (!hasContent)
                    continue;

                int nivel = concepto?.Nivel ?? ObtenerNivelDesdeTipoPresupuesto(tipo);
                if (nivel <= nivelPadre)
                    break;

                if (concepto != null && !concepto.EsAgrupador && concepto.MatrizId.HasValue)
                    result.Add(concepto);
            }

            return result;
        }

        private int ObtenerNivelDesdeTipoPresupuesto(string? tipo)
        {
            return tipo switch
            {
                "Capitulo" => 0,
                "Subcapitulo" => 1,
                "Nivel 1" => 2,
                "Nivel 2" => 3,
                "Nivel 3" => 4,
                _ => 5
            };
        }
    }
}
