using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Reajuste del IMPORTE TOTAL de un agrupador (ADR-0001). Las bases se capturan del
    /// ESTADO CARGADO POR LA VISTA antes del diálogo —proyecto, rowset completo,
    /// agrupador, hojas descendientes y cierre auxiliar de las matrices visibles— y
    /// viajan como comando inmutable a
    /// <see cref="BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand"/> sobre un
    /// contexto NUEVO. El formulario no calcula factores ni costos, no guarda matrices y
    /// no refresca precios desde el contexto compartido: sólo aplica la instantánea
    /// confirmada.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Seam de prueba: se ejecuta después de confirmar el comando y antes de volcar
        /// la instantánea, para simular trabajo ajeno que aparece postcommit.
        /// </summary>
        internal Action? AntesDeAplicarReajusteCostoGrupalConfirmadoParaPrueba { get; set; }

        /// <summary>
        /// Seam interno para las pruebas del ensamblado: valida el guard ANTES de
        /// capturar, captura las bases del agrupador tal como la vista las ve, permite
        /// simular un cambio externo con <paramref name="entreCapturaYComando"/> y
        /// despacha la intención. No muestra mensajes de UI.
        /// </summary>
        internal BudgetGroupCostAdjustmentResult? EjecutarReajusteCostoGrupalDesdeFila(
            int rowIndex,
            decimal targetAmount,
            MatrixAdjustmentScopes scopes,
            Action? entreCapturaYComando = null)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return null;

            if (dgvPresupuesto.Rows[rowIndex].Tag is not ConceptoPresupuesto agrupador
                || agrupador.Id <= 0
                || !agrupador.EsAgrupador)
            {
                return null;
            }

            if (!PuedeReajustarCosto(out _))
                return null;

            var bases = CapturarBasesReajusteCostoGrupal(agrupador.Id);
            if (bases == null)
                return null;

            entreCapturaYComando?.Invoke();

            return EjecutarReajusteCostoGrupalCapturado(
                bases, scopes, targetAmount, out _);
        }

        /// <summary>
        /// Captura PRE-DIÁLOGO las bases que la VISTA tiene cargadas. La unión de
        /// cierres la calcula Application sobre el contexto cargado; las entradas de las
        /// matrices visibles se sustituyen por la instancia del panel para que el
        /// comando revalide exactamente lo que el usuario vio. Filas, agrupador y hojas
        /// descendientes también salen de los Tags del grid, nunca de una lectura
        /// fresca. Devuelve <c>null</c> si la vista no puede formar una base coherente.
        /// </summary>
        private BudgetGroupCostAdjustmentBases? CapturarBasesReajusteCostoGrupal(int agrupadorId)
        {
            if (agrupadorId <= 0)
                return null;

            var filasVista = ObtenerFilasVistaReajusteCosto();
            if (filasVista is null)
                return null;

            var agrupador = filasVista.FirstOrDefault(c => c.Id == agrupadorId && c.EsAgrupador);
            if (agrupador == null)
                return null;

            var hojasVista = ObtenerHojasVisiblesReajusteCostoGrupal(filasVista, agrupador);
            if (hojasVista.Count == 0)
                return null;

            try
            {
                var capturadas = BudgetPersistenceService.CaptureGroupCostAdjustmentBases(
                    _context, _proyecto.Id, agrupadorId);

                var cierre = capturadas.Closure;
                foreach (int matrizId in capturadas.SelectedMatrixIds)
                {
                    var matrizVista = ObtenerMatrizVistaReajusteCosto(matrizId);
                    if (matrizVista != null)
                    {
                        cierre = cierre.WithEntry(
                            BudgetMatrixCostAdjustmentClosureEntry.FromMatriz(matrizVista));
                    }
                }

                return new BudgetGroupCostAdjustmentBases(
                    _proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(_proyecto),
                    BudgetPriceRefreshRowset.FromConceptos(filasVista),
                    BudgetGroupCostAdjustmentGroupBase.FromConcepto(agrupador),
                    hojasVista.Select(BudgetGroupCostAdjustmentLeafBase.FromConcepto),
                    cierre);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Captura de bases del reajuste de agrupador: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Hojas descendientes VISIBLES del agrupador, con la misma política jerárquica
        /// del backend: toda fila no agrupadora cuenta como nivel 5 y el recorrido
        /// termina en el siguiente agrupador de nivel igual o superior. Incluye hojas
        /// sin matriz (costo fijo), que no se reescriben.
        /// </summary>
        private static List<ConceptoPresupuesto> ObtenerHojasVisiblesReajusteCostoGrupal(
            IReadOnlyList<ConceptoPresupuesto> filasVista,
            ConceptoPresupuesto agrupador)
        {
            var hojas = new List<ConceptoPresupuesto>();
            int indice = -1;

            for (int i = 0; i < filasVista.Count; i++)
            {
                if (filasVista[i].Id == agrupador.Id)
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
                return hojas;

            int nivelAgrupador = agrupador.Nivel;
            for (int i = indice + 1; i < filasVista.Count; i++)
            {
                var fila = filasVista[i];
                int nivel = fila.EsAgrupador ? fila.Nivel : 5;
                if (nivel <= nivelAgrupador)
                    break;

                if (!fila.EsAgrupador)
                    hojas.Add(fila);
            }

            return hojas;
        }

        /// <summary>
        /// Ejecuta el comando grupal sobre un contexto nuevo con las bases capturadas y,
        /// si se confirma, aplica su instantánea. No reintenta automáticamente:
        /// conflicto y fallo dejan la vista intacta; un resultado incierto o una
        /// sincronización postcommit fallida bloquean el siguiente reajuste hasta una
        /// recarga segura.
        /// </summary>
        private BudgetGroupCostAdjustmentResult? EjecutarReajusteCostoGrupalCapturado(
            BudgetGroupCostAdjustmentBases bases,
            MatrixAdjustmentScopes scopes,
            decimal targetAmount,
            out string? mensaje)
        {
            mensaje = null;

            if (!PuedeReajustarCosto(out mensaje))
                return null;

            UltimoEstadoReajusteCosto = null;
            UltimoDiagnosticoReajusteCosto = null;
            UltimoMensajeReajusteCosto = null;

            BudgetGroupCostAdjustmentResult resultado;
            try
            {
                var comando = new BudgetGroupCostAdjustmentCommand(
                    bases, scopes, targetAmount);

                resultado = BudgetPersistenceService.ExecuteGroupCostAdjustmentCommand(
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
                if (!IntentarAplicarReajusteCostoGrupalConfirmado(resultado.Confirmed))
                {
                    _reajusteCostoBloqueadoPorSincronizacionPendiente = true;
                    mensaje =
                        "El reajuste se guardó, pero la vista no se sincronizó. " +
                        "Recargue el presupuesto antes de continuar.";
                }

                UltimoMensajeReajusteCosto = mensaje;
                return resultado;
            }

            if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
            {
                _reajusteCostoBloqueadoPorResultadoIncierto = true;
                mensaje =
                    "El resultado del reajuste es incierto: no se puede afirmar si la base quedó " +
                    "modificada. Recargue el presupuesto antes de volver a reajustar costos.";
                UltimoMensajeReajusteCosto = mensaje;
                return resultado;
            }

            mensaje = resultado.Message
                ?? "No se pudo reajustar el agrupador; no se guardó nada.";
            UltimoMensajeReajusteCosto = mensaje;
            return resultado;
        }

        /// <summary>
        /// Aplica la instantánea grupal confirmada sin dejar que una excepción posterior
        /// al commit escape al llamador: la base ya quedó escrita, así que se clasifica
        /// como sincronización pendiente (no como fallo) y el bloqueo impide reenviar.
        /// </summary>
        private bool IntentarAplicarReajusteCostoGrupalConfirmado(
            BudgetGroupCostAdjustmentSnapshot snapshot)
        {
            try
            {
                return AplicarReajusteCostoGrupalConfirmado(snapshot);
            }
            catch (Exception ex)
            {
                UltimoDiagnosticoReajusteCosto = ex.ToString();
                return false;
            }
        }

        /// <summary>
        /// Vuelca la instantánea grupal confirmada sobre el proyecto, las filas (el
        /// agrupador, las hojas afectadas dentro y fuera del grupo y todos los
        /// agrupadores recalculados) y las matrices/componentes rastreados, dejando todo
        /// <c>Unchanged</c>. Si el tracker tiene trabajo ajeno pendiente no se pisa nada
        /// y el llamador conserva el bloqueo.
        /// </summary>
        private bool AplicarReajusteCostoGrupalConfirmado(
            BudgetGroupCostAdjustmentSnapshot snapshot)
        {
            if (snapshot.ProjectId != _proyecto.Id)
                return false;

            AntesDeAplicarReajusteCostoGrupalConfirmadoParaPrueba?.Invoke();

            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return false;

            // El agrupador puede venir también en Aggregators: se consolida por Id para
            // no aplicar dos veces la misma fila.
            var filas = snapshot.AffectedLeaves
                .Concat(snapshot.Aggregators)
                .Append(snapshot.Group)
                .GroupBy(f => f.ConceptId)
                .Select(g => g.Last())
                .ToArray();

            _actualizandoDesdeComando = true;
            try
            {
                SincronizarProyectoCompartido(snapshot.Project);
                AplicarFilasConfirmadas(filas);
                SincronizarMatricesYComponentesConfirmados(
                    snapshot.AffectedMatrices, snapshot.AffectedComponents);
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }

            ActualizarTitulo();

            // Si el panel muestra una matriz del cierre afectado, la recarga debe
            // confirmarse: una matriz visible desfasada es vista no sincronizada.
            if (!IntentarRecargarMatrizAfectadaDesdeBd(
                    snapshot.AffectedMatrices.Select(m => m.MatrixId)))
                return false;

            dgvPresupuesto.Refresh();
            return true;
        }
    }
}
