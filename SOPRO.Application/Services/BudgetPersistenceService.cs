using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static class BudgetPersistenceService
    {
        /// <summary>
        /// RUTA HEREDADA (temporal): escribe en el contexto COMPARTIDO y no controla
        /// conflictos ni confirma resultados. Se conserva solo porque WinForms aún la
        /// consume; la ruta vigente es <see cref="ExecuteQuantityEditCommand"/>. Se
        /// eliminará al completar la migración (ADR-0001).
        /// </summary>
        public static void ApplyAutoSaveChanges(SOPROContext context, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows.Where(r => r.ExistingConceptId.HasValue))
            {
                var enBd = context.ConceptosPresupuesto.Find(row.ExistingConceptId!.Value);
                if (enBd == null) continue;

                enBd.Descripcion = row.Descripcion;
                enBd.Clave = row.Clave;
                enBd.Unidad = row.Unidad;
                enBd.Cantidad = row.Cantidad;
                enBd.EsAgrupador = row.EsAgrupador;
                enBd.CostoDirectoUnitario = row.CostoDirectoUnitario;
                enBd.CostoDirectoTotal = row.CostoDirectoTotal;
                enBd.PrecioUnitario = row.PrecioUnitario;
                enBd.ImporteTotal = row.ImporteTotal;
                enBd.MatrizId = row.MatrizId;
                enBd.FechaModificacion = DateTime.Now;
            }

            context.SaveChanges();
        }

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
        /// Edita la Descripción o la Unidad de un concepto existente (ADR-0001) sobre un
        /// contexto NUEVO por operación: valida pertenencia al proyecto y el valor
        /// esperado del campo, aplica solo ese campo y confirma una sola vez. No
        /// transporta ni recalcula importes.
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
        /// Asigna una matriz APU a un concepto YA existente (ADR-0001) sobre un contexto
        /// NUEVO por operación: recarga y revalida proyecto, concepto y matriz, construye
        /// el borrador con la aritmética canónica de
        /// <see cref="BudgetConceptAssignmentService"/>, recalcula los agrupadores con
        /// <see cref="PricePropagationService.ActualizarAgrupadores"/> (sin guardar) y
        /// confirma una sola vez. El llamador no transporta importes calculados.
        /// </summary>
        public static BudgetMatrixAssignmentResult ExecuteMatrixAssignmentCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetMatrixAssignmentCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageMatrixAssignment(context, command));

            return new BudgetMatrixAssignmentResult(execution.Result, execution.Confirmed, execution.Diagnostic);
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

        private static BudgetWriteStep<BudgetMatrixAssignmentSnapshot> StageMatrixAssignment(
            SOPROContext context,
            BudgetMatrixAssignmentCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var concepto = context.ConceptosPresupuesto
                .FirstOrDefault(c => c.Id == command.ConceptId && c.ProyectoId == command.ProjectId);

            if (concepto == null)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (concepto.EsAgrupador || command.ExpectedConcept.EsAgrupador)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Un agrupador no admite asignación directa de matriz; no se guardó nada."));

            if (!MatchesConceptAssignmentBase(concepto, command.ExpectedConcept))
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var matriz = context.Matrices
                .FirstOrDefault(m => m.Id == command.MatrixId && m.ProyectoId == command.ProjectId);

            if (matriz == null)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (matriz.Tipo != TipoMatriz.APU)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. " +
                        "Seleccione una matriz de tipo APU."));

            // Única fuente de aritmética de la asignación: el servicio canónico.
            var draft = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                proyecto,
                matriz,
                command.ProposedQuantity,
                command.ProposedKey,
                command.DescriptionOverride,
                command.UnitOverride);

            BudgetConceptAssignmentService.ApplyDraftValues(concepto, draft);

            PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });

            var agrupadores = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId && c.EsAgrupador)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var snapshot = new BudgetMatrixAssignmentSnapshot(
                command.ProjectId,
                concepto.Id,
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.MatrizId,
                concepto.FechaModificacion,
                agrupadores.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());

            return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Commit(
                snapshot, new[] { concepto.Id });
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

        private static bool MatchesProjectBase(Proyecto proyecto, BudgetProjectCalculationBase expected)
        {
            return proyecto.PorcentajeIndirectosCentral == expected.PorcentajeIndirectosCentral
                && proyecto.PorcentajeIndirectosCampo == expected.PorcentajeIndirectosCampo
                && proyecto.PorcentajeFinanciamiento == expected.PorcentajeFinanciamiento
                && proyecto.PorcentajeUtilidad == expected.PorcentajeUtilidad
                && proyecto.PorcentajeCargosAdicionales == expected.PorcentajeCargosAdicionales
                && proyecto.PorcentajeIVA == expected.PorcentajeIVA
                && string.Equals(proyecto.ModoCalculoPorcentajes, expected.ModoCalculoPorcentajes,
                    StringComparison.OrdinalIgnoreCase)
                && proyecto.DecimalesCantidad == expected.DecimalesCantidad
                && proyecto.DecimalesImporte == expected.DecimalesImporte
                && proyecto.DecimalesPorcentaje == expected.DecimalesPorcentaje;
        }

        private static bool MatchesConceptBase(ConceptoPresupuesto concepto, BudgetConceptQuantityBase expected)
        {
            return concepto.Cantidad == expected.Cantidad
                && concepto.CostoDirectoUnitario == expected.CostoDirectoUnitario
                && concepto.MatrizId == expected.MatrizId
                && concepto.EsAgrupador == expected.EsAgrupador;
        }

        private static bool MatchesConceptAssignmentBase(
            ConceptoPresupuesto concepto,
            BudgetConceptAssignmentBase expected)
        {
            return concepto.EsAgrupador == expected.EsAgrupador
                && string.Equals(concepto.Clave ?? string.Empty, expected.Clave, StringComparison.Ordinal)
                && string.Equals(concepto.Descripcion ?? string.Empty, expected.Descripcion, StringComparison.Ordinal)
                && string.Equals(concepto.Unidad ?? string.Empty, expected.Unidad, StringComparison.Ordinal)
                && concepto.Cantidad == expected.Cantidad
                && concepto.MatrizId == expected.MatrizId
                && concepto.CostoDirectoUnitario == expected.CostoDirectoUnitario
                && concepto.CostoDirectoTotal == expected.CostoDirectoTotal
                && concepto.PrecioUnitario == expected.PrecioUnitario
                && concepto.ImporteTotal == expected.ImporteTotal;
        }

        public static BudgetSaveResult ReplaceProjectConcepts(SOPROContext context, int projectId, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var concepts = BuildConcepts(projectId, rows).ToList();
            var conceptosExistentes = context.ConceptosPresupuesto.Where(c => c.ProyectoId == projectId);
            context.ConceptosPresupuesto.RemoveRange(conceptosExistentes);
            context.ConceptosPresupuesto.AddRange(concepts);
            context.SaveChanges();

            return new BudgetSaveResult { SavedConceptCount = concepts.Count };
        }

        public static IEnumerable<ConceptoPresupuesto> BuildConcepts(int projectId, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows.Where(r => r.HasContent).OrderBy(r => r.Orden))
            {
                yield return new ConceptoPresupuesto
                {
                    ProyectoId = projectId,
                    Clave = row.Clave,
                    Descripcion = row.Descripcion,
                    EsAgrupador = row.EsAgrupador,
                    Nivel = ResolveLevelFromType(row.Tipo),
                    Orden = row.Orden,
                    Unidad = row.Unidad,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty,
                    Cantidad = row.Cantidad,
                    MatrizId = row.EsAgrupador ? null : row.MatrizId,
                    CostoDirectoUnitario = row.EsAgrupador ? 0 : row.CostoDirectoUnitario,
                    CostoDirectoTotal = row.CostoDirectoTotal,  // agrupadores guardan su total calculado
                    PrecioUnitario = row.EsAgrupador ? 0 : row.PrecioUnitario,
                    ImporteTotal = row.ImporteTotal,            // agrupadores guardan su total calculado
                };
            }
        }

        public static int ResolveLevelFromType(string? tipo)
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

    /// <summary>Resultado de un ejecutor de escritura: clasificación, instantánea y diagnóstico técnico.</summary>
    internal sealed class BudgetWriteExecution<TConfirmed> where TConfirmed : class
    {
        private BudgetWriteExecution(BudgetOperationResult result, TConfirmed? confirmed, string? diagnostic)
        {
            Result = result;
            Confirmed = confirmed;
            Diagnostic = diagnostic;
        }

        public BudgetOperationResult Result { get; }

        public TConfirmed? Confirmed { get; }

        public string? Diagnostic { get; }

        internal static BudgetWriteExecution<TConfirmed> Committed(
            TConfirmed confirmed,
            IReadOnlyList<int> confirmedConceptIds,
            string? diagnostic)
            => new(BudgetOperationResult.Committed(confirmedConceptIds), confirmed, diagnostic);

        internal static BudgetWriteExecution<TConfirmed> Aborted(BudgetOperationResult result, string? diagnostic)
            => new(result, null, diagnostic);

        internal static BudgetWriteExecution<TConfirmed> Failed(string message, string? diagnostic)
            => new(BudgetOperationResult.Failed(message), null, diagnostic);

        internal static BudgetWriteExecution<TConfirmed> OutcomeUnknown(string message, string? diagnostic)
            => new(BudgetOperationResult.OutcomeUnknown(message), null, diagnostic);
    }

    /// <summary>
    /// Decisión de la fase de aplicación de un comando: confirmar con una instantánea o
    /// detenerse con un resultado ya clasificado (conflicto o fallo sin escritura).
    /// </summary>
    internal sealed class BudgetWriteStep<TConfirmed> where TConfirmed : class
    {
        private BudgetWriteStep(
            bool shouldCommit,
            BudgetOperationResult? stopResult,
            TConfirmed? confirmed,
            IReadOnlyList<int> confirmedConceptIds,
            string? diagnostic,
            Func<TConfirmed>? confirmedFactory = null,
            Func<IReadOnlyList<int>>? confirmedConceptIdsFactory = null)
        {
            ShouldCommit = shouldCommit;
            StopResult = stopResult;
            Confirmed = confirmed;
            ConfirmedConceptIds = confirmedConceptIds;
            Diagnostic = diagnostic;
            ConfirmedFactory = confirmedFactory;
            ConfirmedConceptIdsFactory = confirmedConceptIdsFactory;
        }

        public bool ShouldCommit { get; }

        public BudgetOperationResult? StopResult { get; }

        public TConfirmed? Confirmed { get; }

        public IReadOnlyList<int> ConfirmedConceptIds { get; }

        public string? Diagnostic { get; }

        /// <summary>
        /// Materializa la instantánea después del SaveChanges; necesaria cuando el
        /// Id de una entidad nueva solo existe una vez que la base lo asignó.
        /// </summary>
        public Func<TConfirmed>? ConfirmedFactory { get; }

        /// <summary>Ids confirmados que dependen de Ids generados por la base.</summary>
        public Func<IReadOnlyList<int>>? ConfirmedConceptIdsFactory { get; }

        internal static BudgetWriteStep<TConfirmed> Commit(
            TConfirmed confirmed,
            IReadOnlyList<int> confirmedConceptIds)
            => new(true, null, confirmed, confirmedConceptIds ?? Array.Empty<int>(), null);

        internal static BudgetWriteStep<TConfirmed> CommitAfterSave(
            Func<TConfirmed> confirmedFactory,
            Func<IReadOnlyList<int>> confirmedConceptIdsFactory)
            => new(true, null, null, Array.Empty<int>(), null,
                confirmedFactory ?? throw new ArgumentNullException(nameof(confirmedFactory)),
                confirmedConceptIdsFactory ?? throw new ArgumentNullException(nameof(confirmedConceptIdsFactory)));

        internal static BudgetWriteStep<TConfirmed> Abort(BudgetOperationResult result, string? diagnostic = null)
            => new(false, result ?? throw new ArgumentNullException(nameof(result)), null,
                Array.Empty<int>(), diagnostic);
    }

    /// <summary>
    /// Ejecutor ÚNICO de comandos de escritura del presupuesto (ADR-0001): contexto nuevo
    /// por operación creado por <see cref="IProjectDbContextFactory"/>, transacción corta
    /// opcional, aplicación delegada en el handler, exactamente un SaveChanges y
    /// clasificación de conflicto, fallo revertido y resultado incierto. El diagnóstico
    /// técnico viaja separado del mensaje seguro para el usuario.
    /// </summary>
    internal static class BudgetWriteExecutor
    {
        internal static BudgetWriteExecution<TConfirmed> Execute<TConfirmed>(
            IProjectDbContextFactory factory,
            string databasePath,
            bool useTransaction,
            Func<SOPROContext, BudgetWriteStep<TConfirmed>> stage)
            where TConfirmed : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            SOPROContext? context = null;
            IDbContextTransaction? transaction = null;

            try
            {
                context = factory.Create(databasePath);
                if (useTransaction)
                    transaction = context.Database.BeginTransaction();
            }
            catch (Exception ex)
            {
                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, Describe(ex));
            }

            BudgetWriteStep<TConfirmed> step;
            try
            {
                step = stage(context);
            }
            catch (Exception ex)
            {
                // Fallo al leer o aplicar valores: nada de este comando llegó a SaveChanges.
                RollbackQuietly(transaction);
                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, Describe(ex));
            }

            if (!step.ShouldCommit)
            {
                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Aborted(
                    step.StopResult!, step.Diagnostic);
            }

            try
            {
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                string? diagnostic = Describe(ex);
                try
                {
                    // Sin transacción explícita, SaveChanges ya revirtió la suya propia.
                    transaction?.Rollback();
                }
                catch (Exception rollbackFailure)
                {
                    DisposeQuietly(transaction);
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeReversionIncierta,
                        diagnostic + " | rollback: " + Describe(rollbackFailure));
                }

                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, diagnostic);
            }

            TConfirmed confirmed;
            IReadOnlyList<int> confirmedConceptIds;
            try
            {
                confirmed = step.ConfirmedFactory is not null
                    ? step.ConfirmedFactory()
                    : step.Confirmed!;
                confirmedConceptIds = step.ConfirmedConceptIdsFactory is not null
                    ? step.ConfirmedConceptIdsFactory()
                    : step.ConfirmedConceptIds;
            }
            catch (Exception ex)
            {
                // La escritura ya ocurrió dentro de la transacción: si la
                // instantánea no se puede materializar, se revierte para no
                // confirmar una operación que el llamador no puede reconciliar.
                string? diagnostic = Describe(ex);
                if (transaction == null)
                {
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeCommitIncierto, diagnostic);
                }

                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackFailure)
                {
                    DisposeQuietly(transaction);
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeReversionIncierta,
                        diagnostic + " | rollback: " + Describe(rollbackFailure));
                }

                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, diagnostic);
            }

            if (transaction != null)
            {
                try
                {
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    DisposeQuietly(transaction);
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeCommitIncierto, Describe(ex));
                }
            }

            try
            {
                transaction?.Dispose();
            }
            catch (Exception ex)
            {
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                    MensajeCommitIncierto, Describe(ex));
            }

            try
            {
                context.Dispose();
            }
            catch (Exception ex)
            {
                // El commit ya ocurrió pero la operación no se cerró de forma segura:
                // no se afirma ni éxito ni fallo (ADR-0001: reconciliar antes de reintentar).
                return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                    MensajeCommitIncierto, Describe(ex));
            }

            return BudgetWriteExecution<TConfirmed>.Committed(
                confirmed, confirmedConceptIds, diagnostic: null);
        }

        private static void RollbackQuietly(IDbContextTransaction? transaction)
        {
            if (transaction == null) return;

            try { transaction.Rollback(); } catch { }
        }

        private static void DisposeQuietly(IDbContextTransaction? transaction)
        {
            if (transaction == null) return;

            try { transaction.Dispose(); } catch { }
        }

        private static void DisposeQuietly(SOPROContext? context)
        {
            if (context == null) return;

            try { context.Dispose(); } catch { }
        }

        private static string Describe(Exception exception) => exception.ToString();

        private const string MensajeFalloRevertido =
            "No se pudo guardar el presupuesto; no se confirmó ningún cambio. Puede reintentar.";

        private const string MensajeReversionIncierta =
            "No se pudo guardar el presupuesto y no se pudo confirmar que la base haya quedado sin cambios. " +
            "Reconcilie con la base antes de reintentar.";

        private const string MensajeCommitIncierto =
            "No se pudo confirmar si el guardado del presupuesto quedó en la base. " +
            "Reconcilie con la base antes de reintentar.";
    }
}
