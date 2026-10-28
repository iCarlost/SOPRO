using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Reordenamiento de filas persistidas del presupuesto (ADR-0001). El arrastre ya
    /// ocurrió en la vista; la intención viaja como comando inmutable a
    /// <see cref="BudgetPersistenceService.ExecuteReorderCommand"/>, que la aplica
    /// sobre un contexto NUEVO. Los Tags compartidos no se tocan antes de conocer el
    /// resultado; si se confirma, se sincronizan Orden y agrupadores y se dejan
    /// <c>Unchanged</c>. Un conflicto o fallo conserva el borrador visual y no guarda
    /// nada por la ruta heredada.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Clasificación del último reordenamiento ejecutado como comando, o <c>null</c>
        /// si no llegó a enviarse (solo borradores Id=0 o trabajo pendiente en el
        /// contexto compartido). Observabilidad interna para las pruebas del ensamblado;
        /// no es API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoReorden { get; private set; }

        /// <summary>
        /// <c>true</c> cuando el arrastre quedó solo en la vista porque el comando no se
        /// envió o no se confirmó. El grid conserva el borrador; nada se afirma guardado.
        /// </summary>
        internal bool HayReordenSinConfirmar { get; private set; }

        /// <summary>
        /// Persiste el orden visual actual como comando. Devuelve <c>false</c> solo
        /// cuando no hay ninguna fila persistida que reordenar (todo son borradores
        /// Id=0); en cualquier otro caso el reordenamiento queda atendido aquí y el
        /// llamador NO debe invocar <see cref="GuardarCambios"/>.
        /// </summary>
        internal bool EjecutarReordenPersistido()
        {
            UltimoEstadoReorden = null;
            HayReordenSinConfirmar = false;

            // Se construye ANTES de tocar ninguna entidad compartida: la base esperada
            // es el Orden que aún conservan los Tags.
            var objetivos = ConstruirObjetivosReorden();
            if (objetivos.Count == 0)
            {
                // Solo borradores Id=0: nada persistido que reordenar; la numeración
                // visible sigue al borrador.
                ReasignarNumerosConceptosConGuard();
                return false;
            }

            // Conservador: si el contexto compartido arrastra trabajo pendiente de otras
            // rutas, no se envía el comando ni se guarda nada.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
            {
                MarcarReordenSinConfirmar();
                return true;
            }

            BudgetReorderCommand command;
            try
            {
                command = new BudgetReorderCommand(_proyecto.Id, objetivos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reordenamiento de filas: {ex.Message}");
                MarcarReordenSinConfirmar();
                return true;
            }

            BudgetReorderResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteReorderCommand(
                    _factory,
                    _context.DatabasePath,
                    command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reordenamiento de filas: {ex.Message}");
                MarcarReordenSinConfirmar();
                return true;
            }

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                UltimoEstadoReorden = resultado.Status;
                System.Diagnostics.Debug.WriteLine(
                    $"Reordenamiento no confirmado ({resultado.Status}): {resultado.Diagnostic}");
                MarcarReordenSinConfirmar();
                return true;
            }

            UltimoEstadoReorden = resultado.Status;
            AplicarReordenConfirmado(resultado.Confirmed);
            return true;
        }

        private List<BudgetReorderTarget> ConstruirObjetivosReorden()
        {
            var objetivos = new List<BudgetReorderTarget>();
            int posicion = 0;

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                if (dgvPresupuesto.Rows[i].Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                    continue;

                objetivos.Add(BudgetReorderTarget.FromConcepto(concepto, posicion));
                posicion++;
            }

            return objetivos;
        }

        private void MarcarReordenSinConfirmar()
        {
            HayReordenSinConfirmar = true;
            ReasignarNumerosConceptosConGuard();
        }

        /// <summary>
        /// Vuelca lo CONFIRMADO: Orden de cada fila y agrupadores recalculados sobre las
        /// entidades compartidas, dejándolas <c>Unchanged</c>, y renumera el grid. No se
        /// guarda nada aquí; el comando ya confirmó la escritura.
        /// </summary>
        private void AplicarReordenConfirmado(BudgetReorderSnapshot confirmado)
        {
            _actualizandoDesdeComando = true;
            try
            {
                var filasPorConcepto = ConstruirIndiceFilasPorConcepto();

                foreach (var filaConfirmada in confirmado.Order)
                {
                    var entidad = ResolverEntidadCompartida(filasPorConcepto, filaConfirmada.ConceptId);
                    if (entidad is not null)
                        SincronizarOrdenEntidadCompartida(entidad, filaConfirmada.Order);
                }

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

                ReasignarNumerosConceptos();
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }
        }

        private void SincronizarOrdenEntidadCompartida(ConceptoPresupuesto entidad, int orden)
        {
            entidad.Orden = orden;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }

        private void ReasignarNumerosConceptosConGuard()
        {
            bool previo = _actualizandoDesdeComando;
            _actualizandoDesdeComando = true;
            try
            {
                ReasignarNumerosConceptos();
            }
            finally
            {
                _actualizandoDesdeComando = previo;
            }
        }
    }
}
