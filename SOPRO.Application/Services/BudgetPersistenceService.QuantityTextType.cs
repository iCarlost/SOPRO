using System.Globalization;
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
        /// Edita la cantidad de un concepto existente (ADR-0001) sobre un contexto NUEVO
        /// por operación: valida proyecto y bases esperadas, recalcula con
        /// <see cref="BudgetRowEditFlowService.HandleQuantityCellChange"/> y los agrupadores
        /// con <see cref="PricePropagationService.ActualizarAgrupadores"/> (ambos sin guardar)
        /// y confirma una sola vez. Los importes no vienen del llamador: se derivan aquí.
        /// </summary>
        public static BudgetQuantityEditResult ExecuteQuantityEditCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetQuantityEditCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageQuantityEdit(context, command));

            return new BudgetQuantityEditResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        /// <summary>
        /// Edita la Descripción, la Unidad o las Observaciones de un concepto existente
        /// (ADR-0001) sobre un contexto NUEVO por operación: valida pertenencia al
        /// proyecto y el valor esperado del campo, aplica solo ese campo y confirma una
        /// sola vez. No transporta ni recalcula importes. Las Observaciones se escriben
        /// en <see cref="ConceptoPresupuesto.Notas"/> sin normalizar.
        /// </summary>
        public static BudgetTextEditResult ExecuteTextEditCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetTextEditCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageTextEdit(context, command));

            return new BudgetTextEditResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        /// <summary>
        /// Cambia el tipo de una fila del presupuesto (ADR-0001): actualiza el estado
        /// jerárquico de un concepto existente o crea el concepto de una fila nueva,
        /// sobre un contexto NUEVO por operación. Usa la normalización única de
        /// <see cref="BudgetHierarchyService"/>, limpia los valores que la UI deja
        /// vacíos cuando la fila pasa a agrupador, recalcula los agrupadores con
        /// <see cref="PricePropagationService.ActualizarAgrupadores"/> (sin guardar) y
        /// confirma una sola vez. El Id de una fila nueva se asigna en este contexto,
        /// nunca en la vista.
        /// </summary>
        public static BudgetTypeChangeResult ExecuteTypeChangeCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetTypeChangeCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageTypeChange(context, command));

            return new BudgetTypeChangeResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetTypeChangeSnapshot> StageTypeChange(
            SOPROContext context,
            BudgetTypeChangeCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetTypeChangeSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            bool isAggregator = !string.Equals(command.Tipo, "Concepto", StringComparison.OrdinalIgnoreCase);
            int level = BudgetHierarchyService.GetLevelFromType(command.Tipo);

            ConceptoPresupuesto concepto;
            bool stateAlreadyCorrect = false;

            if (command.ConceptId is int conceptId)
            {
                var encontrado = context.ConceptosPresupuesto
                    .FirstOrDefault(c => c.Id == conceptId && c.ProyectoId == command.ProjectId);

                if (encontrado == null)
                    return BudgetWriteStep<BudgetTypeChangeSnapshot>.Abort(
                        BudgetOperationResult.Conflict(
                            $"El concepto {conceptId} no pertenece al proyecto {command.ProjectId} " +
                            "en la base; la vista debe recargarse."));

                concepto = encontrado;
                var expected = command.ExpectedConcept!;
                if (concepto.Nivel != expected.Nivel || concepto.EsAgrupador != expected.EsAgrupador)
                    return BudgetWriteStep<BudgetTypeChangeSnapshot>.Abort(
                        BudgetOperationResult.Conflict(
                            $"El estado del concepto {conceptId} cambió en la base desde que se cargó; " +
                            "la vista debe recargarse."));

                stateAlreadyCorrect = concepto.Nivel == level && concepto.EsAgrupador == isAggregator;
                if (!stateAlreadyCorrect)
                    BudgetConceptCreationService.ApplyExistingTypeState(concepto, isAggregator, level);
            }
            else
            {
                concepto = BudgetConceptCreationService.PrepareNewForTypeChange(
                    context, command.ProjectId, command, isAggregator, level);
                context.ConceptosPresupuesto.Add(concepto);
            }

            // Un no-op confirmado no debe tocar ningún agrupador (ni su marca de
            // modificación): el resultado se clasifica como confirmado sin escritura.
            if (!stateAlreadyCorrect)
            {
                PricePropagationService.ActualizarAgrupadores(
                    context, new List<Proyecto> { proyecto });

                // El Id de una fila nueva solo existe después del SaveChanges: la
                // instantánea se materializa entonces, sin una segunda escritura.
                return BudgetWriteStep<BudgetTypeChangeSnapshot>.CommitAfterSave(
                    () => BuildTypeChangeSnapshot(context, command.ProjectId, concepto, stateAlreadyCorrect: false),
                    () => new[] { concepto.Id });
            }

            return BudgetWriteStep<BudgetTypeChangeSnapshot>.Commit(
                BuildTypeChangeSnapshot(context, command.ProjectId, concepto, stateAlreadyCorrect: true),
                new[] { concepto.Id });
        }

        private static BudgetTypeChangeSnapshot BuildTypeChangeSnapshot(
            SOPROContext context,
            int projectId,
            ConceptoPresupuesto concepto,
            bool stateAlreadyCorrect)
        {
            var agrupadores = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == projectId && c.EsAgrupador)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            return new BudgetTypeChangeSnapshot(
                projectId,
                BudgetConfirmedConceptDetail.FromConcepto(concepto),
                agrupadores.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray(),
                stateAlreadyCorrect);
        }

        private static BudgetWriteStep<BudgetTextEditSnapshot> StageTextEdit(
            SOPROContext context,
            BudgetTextEditCommand command)
        {
            var concepto = context.ConceptosPresupuesto
                .FirstOrDefault(c => c.Id == command.ConceptId && c.ProyectoId == command.ProjectId);

            if (concepto == null)
                return BudgetWriteStep<BudgetTextEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            string valorActual = command.Field switch
            {
                BudgetTextEditField.Descripcion => concepto.Descripcion,
                BudgetTextEditField.Unidad => concepto.Unidad,
                BudgetTextEditField.Observaciones => concepto.Notas ?? string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(command), command.Field, null)
            };

            if (!string.Equals(valorActual, command.ExpectedValue, StringComparison.Ordinal))
                return BudgetWriteStep<BudgetTextEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El campo {command.Field} del concepto {command.ConceptId} cambió en la base " +
                        "desde que se cargó; la vista debe recargarse."));

            var fechaModificacion = DateTime.Now;

            switch (command.Field)
            {
                case BudgetTextEditField.Descripcion:
                    concepto.Descripcion = command.ProposedValue;
                    break;
                case BudgetTextEditField.Unidad:
                    concepto.Unidad = command.ProposedValue;
                    break;
                case BudgetTextEditField.Observaciones:
                    concepto.Notas = command.ProposedValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command.Field, null);
            }

            concepto.FechaModificacion = fechaModificacion;

            var snapshot = new BudgetTextEditSnapshot(
                command.ProjectId,
                concepto.Id,
                command.Field,
                command.ProposedValue,
                fechaModificacion);

            return BudgetWriteStep<BudgetTextEditSnapshot>.Commit(
                snapshot, new[] { concepto.Id });
        }

        private static BudgetWriteStep<BudgetQuantityEditSnapshot> StageQuantityEdit(
            SOPROContext context,
            BudgetQuantityEditCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetQuantityEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetQuantityEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var concepto = context.ConceptosPresupuesto
                .FirstOrDefault(c => c.Id == command.ConceptId && c.ProyectoId == command.ProjectId);

            if (concepto == null)
                return BudgetWriteStep<BudgetQuantityEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (concepto.EsAgrupador)
                return BudgetWriteStep<BudgetQuantityEditSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Un agrupador no admite edición directa de cantidad; no se guardó nada."));

            if (!MatchesConceptBase(concepto, command.ExpectedConcept))
                return BudgetWriteStep<BudgetQuantityEditSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // Única fuente de aritmética de la edición: la misma que usa la pantalla.
            // Sin matriz, el servicio canónico no produce cambios (la UI no recalcula).
            var quantity = BudgetRowEditFlowService.HandleQuantityCellChange(
                proyecto,
                concepto,
                command.ProposedQuantity.ToString(CultureInfo.InvariantCulture));

            if (!quantity.HasChanges)
                return BudgetWriteStep<BudgetQuantityEditSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "El concepto no tiene matriz asignada; la cantidad no se puede recalcular " +
                        "y no se guardó nada."));

            concepto.Cantidad = quantity.Cantidad;
            concepto.PrecioUnitario = quantity.PrecioUnitario;
            concepto.ImporteTotal = quantity.Importe;
            concepto.CostoDirectoTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(
                proyecto, quantity.Cantidad, concepto.CostoDirectoUnitario);
            concepto.FechaModificacion = DateTime.Now;

            PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });

            var agrupadores = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId && c.EsAgrupador)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var snapshot = new BudgetQuantityEditSnapshot(
                command.ProjectId,
                BudgetConfirmedConceptRow.FromConcepto(concepto),
                agrupadores.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());

            return BudgetWriteStep<BudgetQuantityEditSnapshot>.Commit(
                snapshot, new[] { concepto.Id });
        }
    }
}
