using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Sincronización de presupuesto con las rutas de comando de cantidad y de
    /// eliminación de componente de matriz (ADR-0001): el panel confirma con su
    /// comando único sobre un contexto nuevo y notifica aquí la instantánea; el
    /// formulario aplica las hojas y los agrupadores por Id con
    /// <see cref="AplicarFilasConfirmadas"/> y marca las entidades compartidas
    /// afectadas como Unchanged. Nunca dispara
    /// <see cref="RefreshBudgetPrices"/> desde estas rutas (la escritura ya la
    /// hizo el comando); <see cref="PanelMatricesEmbebido.MatrizActualizada"/> sigue
    /// reservada a las rutas heredadas.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>Instancia del panel embebido para las pruebas del ensamblado.</summary>
        internal Controls.PanelMatricesEmbebido? PanelMatricesInterno => _panelMatricesEmbebido;

        /// <summary>Expande el área de trabajo (Panel2) para las pruebas del ensamblado.</summary>
        internal void ExpandirWorkspaceParaPrueba() => EnsureWorkspaceExpanded();

        /// <summary>Suscribe las rutas de comando del panel a la sincronización por Id.</summary>
        private void ConectarComandoCantidadComponente()
        {
            if (_panelMatricesEmbebido == null)
                return;

            _panelMatricesEmbebido.CantidadComponenteConfirmada += (sender, snapshot) =>
            {
                if (IsDisposed || _cargando)
                    return;
                if (snapshot is null || snapshot.ProjectId != _proyecto.Id)
                    return;

                AplicarInstantaneaComponente(snapshot.Project, snapshot.Leaves, snapshot.Aggregators);
            };

            _panelMatricesEmbebido.ComponenteMatrizEliminadoConfirmado += (sender, snapshot) =>
            {
                if (IsDisposed || _cargando)
                    return;
                if (snapshot is null || snapshot.ProjectId != _proyecto.Id)
                    return;

                AplicarInstantaneaComponente(snapshot.Project, snapshot.Leaves, snapshot.Aggregators);
            };

            _panelMatricesEmbebido.AltasComponentesConfirmadas += (sender, snapshot) =>
            {
                if (IsDisposed || _cargando)
                    return;
                if (snapshot is null || snapshot.ProjectId != _proyecto.Id)
                    return;

                AplicarInstantaneaComponente(snapshot.Project, snapshot.Leaves, snapshot.Aggregators);
            };

            _panelMatricesEmbebido.NuevaMatrizConComponentesConfirmada += (sender, snapshot) =>
            {
                if (IsDisposed || _cargando)
                    return;
                if (snapshot is null || snapshot.ProjectId != _proyecto.Id)
                    return;

                // El selector/panel pudo ocultarse al iniciar la creación: tras el
                // commit se repone la superficie del panel embebido.
                MostrarPanelMatrizEmbebido(true);
                AplicarNuevaMatrizConfirmada(snapshot);
            };
        }

        /// <summary>
        /// Aplica la primera alta confirmada por el panel: identifica la fila por
        /// <c>ConceptoId</c> (no por índice) y vuelca la instantánea de asignación
        /// canónica. No hay callback heredado ni segunda escritura.
        /// </summary>
        private void AplicarNuevaMatrizConfirmada(BudgetNewMatrixWithComponentsSnapshot snapshot)
        {
            var filasPorConcepto = ConstruirIndiceFilasPorConcepto();
            if (!filasPorConcepto.TryGetValue(snapshot.ConceptId, out int rowIndex))
            {
                AdvertirRecargaPorFilaNoVisible();
                return;
            }

            var concepto = dgvPresupuesto.Rows[rowIndex].Tag as ConceptoPresupuesto;
            if (concepto is null)
            {
                AdvertirRecargaPorFilaNoVisible();
                return;
            }

            AplicarAsignacionMatrizConfirmada(rowIndex, concepto, snapshot.Assignment);
        }

        private void AdvertirRecargaPorFilaNoVisible()
        {
            MostrarAvisoAsignacionMatriz(
                "Nueva matriz",
                "La matriz se creó y vinculó, pero la fila del concepto ya no está visible en la vista. " +
                "Se recargará el presupuesto para reconciliar la pantalla.",
                MessageBoxIcon.Warning);
            RecargarPreciosDesdeBase();
        }

        /// <summary>
        /// Aplica la instantánea de una ruta de comando confirmada: sincroniza el
        /// proyecto compartido y vuelca hojas y agrupadores por Id. Si el tracker
        /// compartido tiene trabajo ajeno pendiente, NO se aplica (se preserva el
        /// trabajo pendiente sin pisarlo); la reconciliación queda para la
        /// recarga segura de la vista.
        /// </summary>
        private void AplicarInstantaneaComponente(
            BudgetProjectCalculationBase proyectoConfirmado,
            IReadOnlyList<BudgetConfirmedConceptRow> hojas,
            IReadOnlyList<BudgetConfirmedConceptRow> agrupadores)
        {
            try
            {
                _context.ChangeTracker.DetectChanges();
                if (_context.ChangeTracker.HasChanges())
                    return;

                SincronizarProyectoCompartido(proyectoConfirmado);
                AplicarFilasConfirmadas(hojas.Concat(agrupadores).ToArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Sincronización de componente de matriz: {ex.Message}");
            }
        }
    }
}
