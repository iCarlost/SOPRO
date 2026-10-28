using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Eliminación de filas del presupuesto (ADR-0001). La eliminación jerárquica y la
    /// múltiple comparten un único comando inmutable que se aplica sobre un contexto
    /// NUEVO: revalida todas las filas, las elimina en una sola operación y recalcula
    /// los agrupadores que permanecen. La vista solo quita sus filas y sincroniza los
    /// Tags con la instantánea confirmada; un fallo deja grid y base intactos.
    /// Las filas aún no persistidas (Id=0) no viajan al comando: se quitan solo de la
    /// vista, conservando la experiencia existente.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Clasificación de la última eliminación ejecutada como comando, o <c>null</c>
        /// si no llegó a enviarse (solo filas Id=0, trabajo pendiente en el contexto
        /// compartido o comando no construible). Observabilidad interna para las pruebas
        /// del ensamblado; no es API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoEliminacion { get; private set; }

        /// <summary>
        /// Ejecuta la eliminación de las filas indicadas por el plan jerárquico o por la
        /// multiselección. El diálogo de confirmación se resuelve ANTES de llamar aquí.
        /// Devuelve <c>true</c> cuando la intención quedó atendida (confirmada o no).
        /// </summary>
        internal bool EjecutarEliminacionDeFilas(IReadOnlyList<int> rowIndexes)
        {
            if (rowIndexes == null || rowIndexes.Count == 0)
                return false;

            var indices = rowIndexes
                .Where(i => i >= 0 && i < dgvPresupuesto.Rows.Count)
                .Where(i => !dgvPresupuesto.Rows[i].IsNewRow)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            if (indices.Count == 0)
                return false;

            UltimoEstadoEliminacion = null;

            var objetivos = new List<BudgetDeletionTarget>();
            foreach (int index in indices)
            {
                if (dgvPresupuesto.Rows[index].Tag is ConceptoPresupuesto concepto && concepto.Id > 0)
                    objetivos.Add(BudgetDeletionTarget.FromConcepto(concepto));
            }

            // Solo borradores Id=0: no hay nada persistido que confirmar.
            if (objetivos.Count == 0)
            {
                EliminarFilasSoloEnVista(indices);
                return true;
            }

            // Conservador: si el contexto compartido arrastra trabajo pendiente de otras
            // rutas, no se envía el comando ni se guarda nada.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return true;

            BudgetDeletionCommand command;
            try
            {
                command = new BudgetDeletionCommand(_proyecto.Id, objetivos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eliminación de filas: {ex.Message}");
                return true;
            }

            BudgetDeletionResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteDeletionCommand(
                    _factory,
                    _context.DatabasePath,
                    command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eliminación de filas: {ex.Message}");
                return true;
            }

            UltimoEstadoEliminacion = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                // Conflicto, fallo o resultado incierto: las filas siguen en el grid y el
                // tracker compartido queda intacto. No se afirma ningún borrado.
                System.Diagnostics.Debug.WriteLine(
                    $"Eliminación no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return true;
            }

            AplicarEliminacionConfirmada(indices, resultado.Confirmed);
            return true;
        }

        /// <summary>
        /// Quita las filas después de confirmar el comando, desasocia las entidades
        /// eliminadas del contexto compartido y vuelca los agrupadores confirmados en
        /// celdas y Tags, dejándolos <c>Unchanged</c>. No se guarda nada aquí.
        /// </summary>
        private void AplicarEliminacionConfirmada(
            IReadOnlyList<int> indices,
            BudgetDeletionSnapshot confirmado)
        {
            var entidadesEliminadas = new List<ConceptoPresupuesto>();

            foreach (int index in indices.OrderByDescending(i => i))
            {
                if (dgvPresupuesto.Rows[index].Tag is ConceptoPresupuesto concepto)
                    entidadesEliminadas.Add(concepto);

                dgvPresupuesto.Rows.RemoveAt(index);
            }

            foreach (var entidad in entidadesEliminadas)
                DesasociarConceptoEliminado(entidad);

            _actualizandoDesdeComando = true;
            try
            {
                var filasPorConcepto = ConstruirIndiceFilasPorConcepto();

                foreach (var agrupador in confirmado.Aggregators)
                {
                    var entidad = ResolverEntidadCompartida(filasPorConcepto, agrupador.ConceptId);
                    if (entidad is not null)
                        SincronizarEntidadCompartida(entidad, agrupador);

                    if (!filasPorConcepto.TryGetValue(agrupador.ConceptId, out int filaAgrupador))
                        continue;

                    var importeCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
                    if (importeCell != null)
                        importeCell.Value = agrupador.ImporteTotal.ToStringImporte();
                }
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }

            ReasignarNumerosConceptos();
            ActualizarEstadisticas();
            dgvPresupuesto.Refresh();
        }

        /// <summary>
        /// Borradores Id=0: se quitan de la vista sin comando ni persistencia. Los totales
        /// visibles se recalculan sobre las filas restantes sin marcar entidades como
        /// modificadas.
        /// </summary>
        private void EliminarFilasSoloEnVista(IReadOnlyList<int> indices)
        {
            _actualizandoDesdeComando = true;
            try
            {
                foreach (int index in indices.OrderByDescending(i => i))
                {
                    if (dgvPresupuesto.Rows[index].Tag is ConceptoPresupuesto concepto)
                        DesasociarConceptoEliminado(concepto);

                    dgvPresupuesto.Rows.RemoveAt(index);
                }

                ActualizarTotalesAgrupadoresSoloEnVista();
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }

            ReasignarNumerosConceptos();
            ActualizarEstadisticas();
            dgvPresupuesto.Refresh();
        }

        private void DesasociarConceptoEliminado(ConceptoPresupuesto entidad)
        {
            var entry = _context.Entry(entidad);
            if (entry.State != EntityState.Detached)
                entry.State = EntityState.Detached;
        }

        private void ActualizarTotalesAgrupadoresSoloEnVista()
        {
            var snapshot = BuildHierarchyRowsSnapshot();
            var totales = BudgetHierarchyService.CalculateAggregatorTotals(snapshot);

            foreach (var par in totales)
            {
                var importeCell = ObtenerCeldaPorNombreInterno(par.Key, "Importe");
                if (importeCell != null)
                    importeCell.Value = par.Value.ToStringImporte();
            }
        }
    }
}
