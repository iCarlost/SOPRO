using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static partial class BudgetPersistenceService
    {

        /// <summary>
        /// Elimina una o varias filas YA persistidas del presupuesto (ADR-0001) sobre un
        /// contexto NUEVO por operación. Revalida proyecto, existencia y base esperada de
        /// TODAS las filas, las elimina en una sola operación, recalcula los agrupadores
        /// que permanecen con <see cref="PricePropagationService.ActualizarAgrupadores"/>
        /// (sin guardar) y confirma una sola vez. Un conflicto o fallo deja la base sin
        /// cambios; la vista conserva sus filas hasta conocer el resultado.
        /// </summary>
        public static BudgetDeletionResult ExecuteDeletionCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetDeletionCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageDeletion(context, command));

            return new BudgetDeletionResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetDeletionSnapshot> StageDeletion(
            SOPROContext context,
            BudgetDeletionCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            var ids = command.Targets.Select(t => t.ConceptId).ToList();

            var conceptos = context.ConceptosPresupuesto
                .Where(c => ids.Contains(c.Id))
                .ToList();

            if (conceptos.Count != ids.Count)
                return BudgetWriteStep<BudgetDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas a eliminar ya no existen en la base; la vista debe recargarse."));

            if (conceptos.Any(c => c.ProyectoId != command.ProjectId))
                return BudgetWriteStep<BudgetDeletionSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas a eliminar no pertenecen al proyecto; la vista debe recargarse."));

            var porId = conceptos.ToDictionary(c => c.Id);

            foreach (var target in command.Targets)
            {
                var concepto = porId[target.ConceptId];
                if (concepto.Orden != target.ExpectedOrder
                    || concepto.Nivel != target.ExpectedLevel
                    || concepto.EsAgrupador != target.ExpectedIsAggregator)
                {
                    return BudgetWriteStep<BudgetDeletionSnapshot>.Abort(
                        BudgetOperationResult.Conflict(
                            $"La fila {target.ConceptId} cambió en la base desde que se cargó; " +
                            "la vista debe recargarse."));
                }
            }

            // Dependencia explícita fuera del conjunto: eliminar solo el padre dejaría
            // hijos huérfanos o haría fallar la FK. Se rechaza la operación completa.
            bool haySubFilasFuera = context.ConceptosPresupuesto.Any(c =>
                c.ProyectoId == command.ProjectId
                && c.PadreId.HasValue
                && ids.Contains(c.PadreId.Value)
                && !ids.Contains(c.Id));

            if (haySubFilasFuera)
                return BudgetWriteStep<BudgetDeletionSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "La selección deja sub-filas vinculadas fuera de la eliminación; " +
                        "no se guardó nada."));

            context.ConceptosPresupuesto.RemoveRange(conceptos);

            // El recálculo debe ignorar las filas marcadas para eliminar: el SaveChanges
            // aún no ocurrió y una consulta normal todavía las devolvería.
            PricePropagationService.ActualizarAgrupadores(
                context, new List<Proyecto> { proyecto }, ids);

            var agrupadores = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId
                    && c.EsAgrupador
                    && !ids.Contains(c.Id))
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var snapshot = new BudgetDeletionSnapshot(
                command.ProjectId,
                ids,
                agrupadores.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());

            return BudgetWriteStep<BudgetDeletionSnapshot>.Commit(snapshot, ids);
        }

        /// <summary>
        /// Reordena las filas YA persistidas del presupuesto (ADR-0001) sobre un contexto
        /// NUEVO por operación. Revalida que el comando cubra todas las filas del
        /// proyecto y que la base esperada de cada una siga vigente, aplica todos los
        /// cambios de Orden, recalcula los agrupadores con la jerarquía resultante
        /// (sin guardado intermedio) y confirma una sola vez. Un conflicto o fallo deja
        /// la base sin cambios; la vista conserva su borrador visual.
        /// </summary>
        public static BudgetReorderResult ExecuteReorderCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetReorderCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageReorder(context, command));

            return new BudgetReorderResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetReorderSnapshot> StageReorder(
            SOPROContext context,
            BudgetReorderCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetReorderSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            var ids = command.Targets.Select(t => t.ConceptId).ToList();

            var conceptos = context.ConceptosPresupuesto
                .Where(c => ids.Contains(c.Id))
                .ToList();

            if (conceptos.Count != ids.Count)
                return BudgetWriteStep<BudgetReorderSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas reordenadas ya no existen en la base; la vista debe recargarse."));

            if (conceptos.Any(c => c.ProyectoId != command.ProjectId))
                return BudgetWriteStep<BudgetReorderSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas reordenadas no pertenecen al proyecto; la vista debe recargarse."));

            // El Orden persistido es contiguo sobre las filas persistidas: si la vista
            // no cubre todas las filas del proyecto, aplicarlo dejaría posiciones
            // ambiguas. Se rechaza la operación completa.
            int filasEnProyecto = context.ConceptosPresupuesto
                .Count(c => c.ProyectoId == command.ProjectId);

            if (filasEnProyecto != command.Targets.Count)
                return BudgetWriteStep<BudgetReorderSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "La vista no contiene todas las filas del proyecto; la vista debe recargarse."));

            var porId = conceptos.ToDictionary(c => c.Id);

            foreach (var target in command.Targets)
            {
                var concepto = porId[target.ConceptId];
                if (concepto.Orden != target.ExpectedOrder
                    || concepto.Nivel != target.ExpectedLevel
                    || concepto.EsAgrupador != target.ExpectedIsAggregator)
                {
                    return BudgetWriteStep<BudgetReorderSnapshot>.Abort(
                        BudgetOperationResult.Conflict(
                            $"La fila {target.ConceptId} cambió en la base desde que se cargó; " +
                            "la vista debe recargarse."));
                }
            }

            foreach (var target in command.Targets)
                porId[target.ConceptId].Orden = target.ProposedOrder;

            // La jerarquía se infiere por Orden/Nivel: recalcular sobre la lista ya
            // reordenada evita el orden obsoleto que devolvería una consulta a la base.
            var ordenados = conceptos
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            PricePropagationService.ActualizarAgrupadores(proyecto, ordenados);

            var snapshot = new BudgetReorderSnapshot(
                command.ProjectId,
                command.Targets
                    .OrderBy(t => t.ProposedOrder)
                    .Select(t => new BudgetConfirmedConceptOrder(t.ConceptId, t.ProposedOrder))
                    .ToArray(),
                ordenados
                    .Where(c => c.EsAgrupador)
                    .Select(BudgetConfirmedConceptRow.FromConcepto)
                    .ToArray());

            return BudgetWriteStep<BudgetReorderSnapshot>.Commit(snapshot, ids);
        }
    }
}
